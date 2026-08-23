using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EPrimeReadouts
{
    /// Shared per-save state: the readout model plus id allocation. Mutated
    /// only by ReadoutCommands (plus deterministic load-time seeding and
    /// cleanup here), so MP clients cannot diverge.
    public class ReadoutStore : WorldComponent
    {
        public ReadoutModel Model = new ReadoutModel();
        private int nextGroupId = 1;
        private int nextPoolId = 1;
        private bool seeded;
        private List<GroupRecord>? groupRecords;
        private List<PoolRecord>? poolRecords;
        private Dictionary<string, string>? thresholdRecords;
        private Dictionary<string, string>? countRuleRecords;

        private readonly ReadoutRevisions revisions = new ReadoutRevisions();

        /// Global mutation stamp for consumers that depend on the entire model.
        public int Version => revisions.Version;
        public int GroupsVersion => revisions.Groups;
        public int PoolsVersion => revisions.Pools;
        public int ThresholdsVersion => revisions.Thresholds;
        public int CountRulesVersion => revisions.CountRules;

        public ReadoutStore(World world) : base(world) { }

        public static ReadoutStore? Current => Find.World?.GetComponent<ReadoutStore>();

        public int TakeGroupId() => nextGroupId++;

        public int TakePoolId() => nextPoolId++;

        public void Bump(ReadoutChange change) => revisions.Bump(change);

        public string DepthKey(int groupId) =>
            world.info.persistentRandomValue + ":" + groupId;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
            Scribe_Values.Look(ref nextPoolId, "nextPoolId", 1);
            Scribe_Values.Look(ref seeded, "seeded", false);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                groupRecords = new List<GroupRecord>();
                foreach (var group in Model.Groups) groupRecords.Add(new GroupRecord(group));
                poolRecords = new List<PoolRecord>();
                foreach (var pool in Model.Pools) poolRecords.Add(new PoolRecord(pool));
                thresholdRecords = new Dictionary<string, string>();
                foreach (var pair in Model.Thresholds)
                    thresholdRecords[pair.Key] = pair.Value.Low + "," + pair.Value.Critical;
                countRuleRecords = new Dictionary<string, string>();
                foreach (var pair in Model.CountRules)
                    countRuleRecords[pair.Key] = CountRuleCodec.Encode(pair.Value);
            }
            Scribe_Collections.Look(ref groupRecords, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref poolRecords, "pools", LookMode.Deep);
            Scribe_Collections.Look(ref thresholdRecords, "thresholds", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref countRuleRecords, "countRules", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Model = new ReadoutModel();
                if (groupRecords != null)
                    foreach (var record in groupRecords)
                        Model.Groups.Add(record.ToGroup());
                if (poolRecords != null)
                    foreach (var record in poolRecords)
                        Model.Pools.Add(record.ToPool());
                if (thresholdRecords != null)
                    foreach (var pair in thresholdRecords)
                    {
                        var parts = pair.Value.Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int low)
                            && int.TryParse(parts[1], out int critical))
                            Model.Thresholds[pair.Key] = new ThresholdSpec(low, critical);
                    }
                if (countRuleRecords != null)
                    foreach (var pair in countRuleRecords)
                        // Malformed or fully-inherit entries are skipped, never
                        // partially applied.
                        if (CountRuleCodec.TryDecode(pair.Value, out CountRule rule)
                            && !rule.IsInherit)
                            Model.CountRules[pair.Key] = rule;
                groupRecords = null;
                poolRecords = null;
                thresholdRecords = null;
                countRuleRecords = null;
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            Model.CleanupMissing(TokenValid, MemberValid);
            // Migration: convert legacy @Category tokens → #poolId. Run before seeding
            // so pre-A4 saves convert; after CleanupMissing so only valid tokens remain.
            Model.MigrateCategoryTokens(TakePoolId, CategoryLabelName);
            if (!seeded)
            {
                DefaultGroups.Seed(this);
                seeded = true;
            }
            PruneDepthKeys();
            Bump(ReadoutChange.All);
        }

        private static string CategoryLabelName(string catDefName)
        {
            var label = GameResourceCatalog.Instance.CategoryLabelOf(catDefName);
            return !string.IsNullOrEmpty(label) ? label.CapitalizeFirst() : catDefName;
        }

        /// Tier tokens: "[~]defName", "[~]@CategoryDefName", or "[~]#poolId".
        /// Valid while the underlying def, category, or pool still resolves.
        private static bool TokenValid(string token)
        {
            if (SlotToken.IsPoolRef(token))
            {
                int id = SlotToken.PoolId(token);
                return id >= 0 && Current?.Model.PoolById(id) != null;
            }
            string member = SlotToken.MemberName(token);
            return SlotToken.IsPool(token)
                ? DefDatabase<ThingCategoryDef>.GetNamedSilentFail(member) != null
                : GameResourceCatalog.Instance.Exists(member);
        }

        /// Pool members: plain defNames or "@CategoryDefName" refs.
        private static bool MemberValid(string member)
        {
            if (string.IsNullOrEmpty(member)) return false;
            if (member.StartsWith("@"))
            {
                string cat = member.Substring(1);
                return DefDatabase<ThingCategoryDef>.GetNamedSilentFail(cat) != null;
            }
            return GameResourceCatalog.Instance.Exists(member);
        }

        /// Per-player depth prefs are keyed by world+group; drop this world's
        /// entries whose group no longer exists so the settings dicts cannot
        /// grow unboundedly.
        private void PruneDepthKeys()
        {
            var settings = EPrimeReadoutsMod.Settings;
            string prefix = world.info.persistentRandomValue + ":";
            var stale = new List<string>();
            foreach (var key in settings.tierDepths.Keys)
                if (key.StartsWith(prefix))
                {
                    string idPart = key.Substring(prefix.Length);
                    if (!int.TryParse(idPart, out int id) || Model.GroupById(id) == null)
                        stale.Add(key);
                }
            bool changed = stale.Count > 0;
            foreach (var key in stale) settings.tierDepths.Remove(key);
            stale.Clear();
            foreach (var key in settings.enabledGroups.Keys)
                if (key.StartsWith(prefix))
                {
                    string idPart = key.Substring(prefix.Length);
                    if (!int.TryParse(idPart, out int id) || Model.GroupById(id) == null)
                        stale.Add(key);
                }
            changed = changed || stale.Count > 0;
            foreach (var key in stale) settings.enabledGroups.Remove(key);
            // FinalizeInit runs mid-load (Scribe.mode == LoadingVars);
            // ModSettings.Write() starts its own ScribeSaver, which vanilla
            // answers by force-stopping the ACTIVE load. Defer the disk write
            // until the load operation has finished.
            if (changed) LongEventHandler.ExecuteWhenFinished(settings.Write);
        }
    }

    public class PoolRecord : IExposable
    {
        private int id;
        private string name = "";
        private string membersBlob = "";
        private string iconDefName = "";

        public PoolRecord() { }

        public PoolRecord(ResourcePool pool)
        {
            id = pool.Id;
            name = pool.Name;
            membersBlob = PoolMembersCodec.Encode(pool.Members);
            iconDefName = pool.IconDefName ?? "";
        }

        public ResourcePool ToPool() => new ResourcePool
        {
            Id = id,
            Name = name,
            Members = PoolMembersCodec.Decode(membersBlob),
            IconDefName = string.IsNullOrEmpty(iconDefName) ? null : iconDefName,
        };

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref membersBlob, "members", "");
            Scribe_Values.Look(ref iconDefName, "iconDefName", "");
        }
    }

    public class GroupRecord : IExposable
    {
        private int id;
        private string name = "";
        private int orderIndex;
        private string tierBlob = "";
        private bool defaultEnabled = true;

        public GroupRecord() { }

        public GroupRecord(ReadoutGroup group)
        {
            id = group.Id;
            name = group.Name;
            orderIndex = group.OrderIndex;
            tierBlob = TierBlobCodec.Encode(group.Tiers);
            defaultEnabled = group.DefaultEnabled;
        }

        public ReadoutGroup ToGroup() => new ReadoutGroup
        {
            Id = id,
            Name = name,
            OrderIndex = orderIndex,
            Tiers = TierBlobCodec.Decode(tierBlob),
            DefaultEnabled = defaultEnabled,
        };

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref orderIndex, "orderIndex", 0);
            Scribe_Values.Look(ref tierBlob, "tiers", "");
            Scribe_Values.Look(ref defaultEnabled, "defaultEnabled", true);
        }
    }
}
