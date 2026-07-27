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
        private bool seeded;
        private List<GroupRecord> groupRecords;
        private Dictionary<string, string> thresholdRecords;

        /// Bumped on every mutation; the panel rebuilds when it changes.
        public int Version { get; private set; }

        public ReadoutStore(World world) : base(world) { }

        public static ReadoutStore Current => Find.World?.GetComponent<ReadoutStore>();

        public int TakeGroupId() => nextGroupId++;

        public void Bump() => Version++;

        public string DepthKey(int groupId) =>
            world.info.persistentRandomValue + ":" + groupId;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
            Scribe_Values.Look(ref seeded, "seeded", false);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                groupRecords = new List<GroupRecord>();
                foreach (var group in Model.Groups) groupRecords.Add(new GroupRecord(group));
                thresholdRecords = new Dictionary<string, string>();
                foreach (var pair in Model.Thresholds)
                    thresholdRecords[pair.Key] = pair.Value.Low + "," + pair.Value.Critical;
            }
            Scribe_Collections.Look(ref groupRecords, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref thresholdRecords, "thresholds", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Model = new ReadoutModel();
                if (groupRecords != null)
                    foreach (var record in groupRecords)
                        Model.Groups.Add(record.ToGroup());
                if (thresholdRecords != null)
                    foreach (var pair in thresholdRecords)
                    {
                        var parts = pair.Value.Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int low)
                            && int.TryParse(parts[1], out int critical))
                            Model.Thresholds[pair.Key] = new ThresholdSpec(low, critical);
                    }
                groupRecords = null;
                thresholdRecords = null;
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            Model.CleanupMissing(TokenValid);
            if (!seeded)
            {
                DefaultGroups.Seed(this);
                seeded = true;
            }
            PruneDepthKeys();
            Bump();
        }

        /// Tier tokens: "[~]defName" or "[~]@CategoryDefName". Valid while the
        /// underlying def or category still resolves; flags are preserved.
        private static bool TokenValid(string token)
        {
            string member = SlotToken.MemberName(token);
            return SlotToken.IsPool(token)
                ? DefDatabase<ThingCategoryDef>.GetNamedSilentFail(member) != null
                : GameResourceCatalog.Instance.Exists(member);
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
            if (changed) settings.Write();
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
