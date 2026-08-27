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
        public string label = null!; // assigned at creation or load
        public bool enabled = true;
        public bool hasCustomColor;
        public Color color = Color.white;
        public string? iconPath;
        /// defName of the RoleDef this role was seeded from; null for player-created roles.
        public string? templateDefName;
        /// Mod version and def fingerprint (RoleDef.StableHash) captured when the
        /// role was created from its template — lets loads detect def drift.
        public string? templateVersion;
        public uint templateHash;
        public bool autoAssign;
        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;
        /// User-authored hard skill gates. Empty means no explicit skill gate;
        /// Used/Trained skill facts are always derived from the role's jobs.
        public List<string> requiredSkills = new List<string>();
        /// False only while loading a save from before requiredSkills became
        /// opt-in gates. Migration drops the old classification instead of
        /// silently turning it into assignment rejection rules.
        public bool skillGatesSeeded;
        public RoleCategory category;
        public RoleTime time;
        /// False = repeat championships use the occasional-work penalty.
        public bool championPenalty = true;
        /// Minimum biological age (years) for holding the role; 0 = no gate.
        /// -1 = not yet derived (pre-minAge saves and role files); load
        /// migration derives it from the covered work types' unlock ages.
        public int minAge = -1;
        /// Maximum biological age (years, inclusive) for holding the role;
        /// 0 = no gate. Nothing to derive, so pre-maxAge saves load as 0.
        public int maxAge;
        /// Assignment scaling inputs: the minimum assignment count (0-30) and
        /// the ideal percentage of colonists holding the role (0-100). The
        /// engine's holder requirement derives from these (RoleDemand).
        public int colonyMin;
        public int coverage;
        /// The role's own training path: this role plus its training roles
        /// with [min, max) skill bands. Empty = the implicit self-only path
        /// (full axis), which the engine treats as no path at all.
        public List<int> trainingRoleIds = new List<int>();
        public List<int> trainingMins = new List<int>();
        public List<int> trainingMaxes = new List<int>();
        /// False only on roles from saves that predate tuning; RoleStore
        /// migrates those at load and sets this.
        public bool tuningSeeded;
        /// Role-list group (RoleGroup id; 0 = Default). Stored membership only —
        /// rule-carrying roles DISPLAY under Conditional Roles.
        public int groupId = RoleGroup.DefaultId;
        public int activeHours = AllHours;   // bit h set = active during local hour h
        /// LocationRules tokens; empty = active anywhere.
        public List<string> locationTokens = new List<string>();
        public List<JobEntry> entries = new List<JobEntry>();
        /// Composite role: holds an ordered list of member roles instead of job
        /// entries (entries stays empty). Members are existing, non-composite,
        /// rule-free roles; CompositeRoles owns the policy.
        public bool composite;
        public List<int> memberRoleIds = new List<int>();
        /// Engine-maintained, per work-type entry: every giver defName ever seen
        /// under that type (union-only, refreshed each load). Lets the role keep
        /// jobs that mods later move to another work type — see
        /// JobOrderCompiler.WithMovedSnapshotGivers. Invisible to the editor.
        public Dictionary<string, List<string>> workTypeSnapshots = new Dictionary<string, List<string>>();

        private List<string>? scribeEntries;
        private Dictionary<string, string>? scribeSnapshots;
        private string? scribeLocations;
        private string? scribeRequiredSkills;
        // Retired role-skill classification, consumed and dropped on load.
        private string? scribeOptionalSkills;
        private HashSet<string>? coverageCache;
        // Cached XP-frequency primary skill (null is a valid value, hence the
        // flag); derived from coverage, so it invalidates with it.
        private string? primarySkillCache;
        private bool primarySkillCached;
        // Cached age (years) at which every covered work type is unlocked;
        // derived from coverage, so it invalidates with it. -1 = not computed.
        private int fullyUnlocksAtAgeCache = -1;

        internal bool TryGetPrimarySkillCache(out string? skill)
        {
            skill = primarySkillCache;
            return primarySkillCached;
        }

        internal void SetPrimarySkillCache(string? skill)
        {
            primarySkillCache = skill;
            primarySkillCached = true;
        }

        internal bool TryGetFullyUnlocksAtAgeCache(out int age)
        {
            age = fullyUnlocksAtAgeCache;
            return fullyUnlocksAtAgeCache >= 0;
        }

        internal void SetFullyUnlocksAtAgeCache(int age)
        {
            fullyUnlocksAtAgeCache = age;
        }

        public bool HasRules => activeHours != AllHours || locationTokens.Count > 0;

        /// Expanded job coverage — the nesting/redundancy identity, independent of
        /// how the entries spell it. Cached; entry edits invalidate through
        /// CompiledJobOrders.InvalidateRole/InvalidateAll (member edits reach a
        /// composite via that command's composite reverse scan). A composite's
        /// coverage is the union of its members' coverage; a blocker member
        /// contributes nothing (its jobs are vetoes) unless the composite itself
        /// is a blocker, in which case every member job is part of the veto.
        public HashSet<string> Coverage()
        {
            if (coverageCache != null) return coverageCache;
            if (!composite)
                return coverageCache = CoverageMath.CoverageOf(entries, GameJobCatalog.Instance);
            var union = new HashSet<string>();
            var store = RoleStore.Current;
            if (store == null) return union; // no world: nothing to cache
            for (int i = 0; i < memberRoleIds.Count; i++)
            {
                Role? member = store.RoleById(memberRoleIds[i]);
                if (member == null || member.composite) continue;
                if (member.blocker && !blocker) continue;
                union.UnionWith(member.Coverage());
            }
            return coverageCache = union;
        }

        public void InvalidateCoverage()
        {
            coverageCache = null;
            primarySkillCache = null;
            primarySkillCached = false;
            fullyUnlocksAtAgeCache = -1;
            // Coverage-affecting edits (entries, snapshots, composite member
            // edits via the reverse scan) are exactly the work-spec inputs.
            RoleWorkRevision.Bump();
        }

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

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label!, "label");
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
            Scribe_Values.Look(ref category, "category", RoleCategory.None);
            Scribe_Values.Look(ref time, "time", RoleTime.None);
            Scribe_Values.Look(ref championPenalty, "championPenalty", true);
            Scribe_Values.Look(ref minAge, "minAge", -1);
            Scribe_Values.Look(ref maxAge, "maxAge");
            Scribe_Values.Look(ref colonyMin, "colonyMin");
            Scribe_Values.Look(ref coverage, "coverage");
            Scribe_Values.Look(ref tuningSeeded, "tuningSeeded");
            Scribe_Values.Look(ref skillGatesSeeded, "skillGatesSeeded");
            Scribe_Collections.Look(ref trainingRoleIds, "trainingRoleIds", LookMode.Value);
            Scribe_Collections.Look(ref trainingMins, "trainingMins", LookMode.Value);
            Scribe_Collections.Look(ref trainingMaxes, "trainingMaxes", LookMode.Value);
            Scribe_Values.Look(ref composite, "composite");
            Scribe_Collections.Look(ref memberRoleIds, "memberRoleIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                trainingRoleIds ??= new List<int>();
                trainingMins ??= new List<int>();
                trainingMaxes ??= new List<int>();
                memberRoleIds ??= new List<int>();
            }
            // Skill lists scribe comma-joined (skill defNames cannot contain commas).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (requiredSkills.Count > 0)
                    scribeRequiredSkills = string.Join(",", requiredSkills);
            }
            Scribe_Values.Look(ref scribeRequiredSkills, "requiredSkills");
            Scribe_Values.Look(ref scribeOptionalSkills, "optionalSkills");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                requiredSkills = !skillGatesSeeded
                    || scribeRequiredSkills.NullOrEmpty()
                    ? new List<string>() : scribeRequiredSkills!.Split(',').ToList();
            }
            Scribe_Values.Look(ref groupId, "groupId", RoleGroup.DefaultId);
            Scribe_Values.Look(ref activeHours, "activeHours", AllHours);
            // Location tokens scribe comma-joined (category words and game
            // map/Thing identifiers cannot contain commas).
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
                    : scribeLocations!.Split(',').ToList();
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
                scribeRequiredSkills = null;
                scribeOptionalSkills = null;
            }
        }
    }
}
