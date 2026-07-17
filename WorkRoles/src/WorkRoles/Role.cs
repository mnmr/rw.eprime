using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class Role : IExposable
    {
        public const int AllHours = 0xFFFFFF;

        public int id;
        public string label;
        public bool enabled = true;
        public bool hasCustomColor;
        public Color color = Color.white;
        public string iconPath;
        /// defName of the RoleDef this role was seeded from; null for player-created roles.
        public string templateDefName;
        /// Mod version and def fingerprint (RoleDef.StableHash) captured when the
        /// role was created from its template — lets loads detect def drift.
        public string templateVersion;
        public uint templateHash;
        public bool autoAssign;
        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;
        /// Colonist count: -2 = never recommended, -1 = auto (resolves to the
        /// RoleDef default; player roles 0), 0 = interest-only, N = needed.
        public int minHolders = -1;
        /// Of the resolved minHolders, how many slots a colonist still in
        /// training (lower-band path partner) may fill.
        public int inTrainingAllowance;
        /// Role-list group (RoleGroup id; 0 = Default). Stored membership only —
        /// rule-carrying roles DISPLAY under Auto-Roles.
        public int groupId = RoleGroup.DefaultId;
        public int activeHours = AllHours;   // bit h set = active during local hour h
        /// LocationRules tokens; empty = active anywhere.
        public List<string> locationTokens = new List<string>();
        public List<JobEntry> entries = new List<JobEntry>();
        /// Engine-maintained, per work-type entry: every giver defName ever seen
        /// under that type (union-only, refreshed each load). Lets the role keep
        /// jobs that mods later move to another work type — see
        /// JobOrderCompiler.WithMovedSnapshotGivers. Invisible to the editor.
        public Dictionary<string, List<string>> workTypeSnapshots = new Dictionary<string, List<string>>();

        private List<string> scribeEntries;
        private Dictionary<string, string> scribeSnapshots;
        private string scribeLocations;
        private HashSet<string> coverageCache;

        public bool HasRules => activeHours != AllHours || locationTokens.Count > 0;

        /// Expanded job coverage — the nesting/redundancy identity, independent of
        /// how the entries spell it. Cached; entry edits invalidate through
        /// CompiledJobOrders.InvalidateRole/InvalidateAll.
        public HashSet<string> Coverage()
            => coverageCache ?? (coverageCache = CoverageMath.CoverageOf(entries, GameJobCatalog.Instance));

        public void InvalidateCoverage() => coverageCache = null;

        /// True when this role's coverage strictly includes other's (equal
        /// coverage does not cover — equals are siblings).
        public bool Covers(Role other)
        {
            if (other == null || other == this) return false;
            return CoverageMath.Covers(Coverage(), other.Coverage());
        }

        /// True when this role's coverage includes or matches other's
        /// (capability queries: an equal role provides the same jobs).
        public bool CoversOrMatches(Role other)
        {
            if (other == null || other == this) return false;
            return CoverageMath.CoversOrMatches(Coverage(), other.Coverage());
        }

        /// The effective colonist count: Auto (-1) falls back to the seeding
        /// def's default; player-created roles default to 0 (interest-only).
        public int ResolvedMinHolders()
        {
            if (minHolders != -1) return minHolders;
            var def = templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(templateDefName);
            return def?.minHolders ?? 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref hasCustomColor, "hasCustomColor");
            Scribe_Values.Look(ref color, "color", Color.white);
            Scribe_Values.Look(ref iconPath, "iconPath");
            Scribe_Values.Look(ref templateDefName, "templateDefName");
            Scribe_Values.Look(ref templateVersion, "templateVersion");
            Scribe_Values.Look(ref templateHash, "templateHash");
            Scribe_Values.Look(ref autoAssign, "autoAssign");
            Scribe_Values.Look(ref blocker, "blocker");
            // Retired engine-managed flag (Odd Jobs): consumed so old saves load
            // the role as an ordinary player role, keeping entries and holders.
            bool legacyManaged = false;
            Scribe_Values.Look(ref legacyManaged, "managed");
            // Retired train-band fields (trainSkill/trainMin/trainMax/
            // trainTargets/allowTrainingSubs) are simply not read from old saves.
            Scribe_Values.Look(ref minHolders, "minHolders", -1);
            // Pre-1.2 saves: never-dealt lived in maxHolders 0.
            int legacyMaxHolders = -1;
            Scribe_Values.Look(ref legacyMaxHolders, "maxHolders", -1);
            if (Scribe.mode == LoadSaveMode.LoadingVars && legacyMaxHolders == 0)
                minHolders = 0;
            Scribe_Values.Look(ref inTrainingAllowance, "inTrainingAllowance");
            Scribe_Values.Look(ref groupId, "groupId", RoleGroup.DefaultId);
            Scribe_Values.Look(ref activeHours, "activeHours", AllHours);
            // Location tokens scribe comma-joined (ids are numeric, category
            // words fixed — no commas possible).
            if (Scribe.mode == LoadSaveMode.Saving && locationTokens.Count > 0)
                scribeLocations = string.Join(",", locationTokens);
            Scribe_Values.Look(ref scribeLocations, "locations");
            // Pre-1.1 saves carried a Home/Away enum instead.
            RoleLocation legacyLocation = RoleLocation.Any;
            Scribe_Values.Look(ref legacyLocation, "location", RoleLocation.Any);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                locationTokens = scribeLocations.NullOrEmpty()
                    ? new List<string>()
                    : scribeLocations.Split(',').ToList();
                if (locationTokens.Count == 0 && legacyLocation != RoleLocation.Any)
                    locationTokens.Add(legacyLocation == RoleLocation.HomeOnly
                        ? LocationRules.Settlements : LocationRules.Caravans);
            }
            if (Scribe.mode == LoadSaveMode.Saving)
                scribeEntries = entries.Select(e => e.Encode()).ToList();
            Scribe_Collections.Look(ref scribeEntries, "entries", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                entries = new List<JobEntry>();
                if (scribeEntries != null)
                    foreach (var raw in scribeEntries)
                        if (JobEntry.TryDecode(raw, out var entry))
                            entries.Add(entry);
            }

            // Snapshots scribe as workType -> comma-joined giver defNames (defNames
            // cannot contain commas). Absent in old saves: reseeded on load.
            if (Scribe.mode == LoadSaveMode.Saving && workTypeSnapshots.Count > 0)
                scribeSnapshots = workTypeSnapshots.ToDictionary(
                    kv => kv.Key, kv => string.Join(",", kv.Value));
            Scribe_Collections.Look(ref scribeSnapshots, "workTypeSnapshots", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                workTypeSnapshots = new Dictionary<string, List<string>>();
                if (scribeSnapshots != null)
                    foreach (var kv in scribeSnapshots)
                        workTypeSnapshots[kv.Key] = kv.Value.Split(',').ToList();
            }

            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                scribeEntries = null;
                scribeSnapshots = null;
                scribeLocations = null;
            }
        }
    }
}
