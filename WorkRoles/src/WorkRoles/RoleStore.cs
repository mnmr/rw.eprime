using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class RoleStore : WorldComponent
    {
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public List<Role> roles = new List<Role>();
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public Dictionary<Pawn, PawnRoleSet> pawnSets = new Dictionary<Pawn, PawnRoleSet>();
        public bool seeded;
        /// Default training paths seeded (only ever set alongside role seeding;
        /// pre-existing saves adopt them via Restore Defaults instead).
        public bool pathsSeeded;
        /// Player deleted Odd Jobs (knows no mod adds invisible jobs): coverage
        /// won't recreate it until Restore Defaults is asked to bring it back.
        public bool oddJobsDeleted;
        /// GetPriority reports the vanilla 0-4 projection instead of raw ranks.
        /// World state, not a mod setting: other mods consume the values in
        /// sim-relevant code, so MP clients must agree.
        public bool reportVanillaPriorities = true;
        /// The user's recommendation order template (role ids); empty = the
        /// vanilla-grid-derived default. A pure override: unlisted roles are
        /// not merged in — they place dynamically (RecommendationOrder).
        public List<int> recommendationOrder = new List<int>();
        /// Legacy scribe slot: pre-Odd-Jobs saves carry the hidden All role here;
        /// PostLoadInit migrates it into the catalog as the managed role.
        public Role allRole;
        public List<string> knownWorkTypes = new List<string>();
        /// Custom swatch slot count: the editor's two rows of 19.
        public const int MaxCustomSwatches = 38;
        /// Player-defined swatch slots for the role editor (alpha 0 = empty slot).
        public List<UnityEngine.Color> customSwatches = new List<UnityEngine.Color>();
        /// Slot names (index-aligned with customSwatches): auto-named "custom-N",
        /// renamed only by editing an export file — imported names stick. Used by
        /// export/import to merge palettes by name.
        public List<string> customSwatchNames = new List<string>();
        /// Per-bill role restrictions (see BillRoles). Mutate via RoleCommands.
        public Dictionary<Bill, int> billRoles = new Dictionary<Bill, int>();
        /// Role-list groups in display order. Mutate via RoleCommands.
        public List<RoleGroup> groups = new List<RoleGroup>();
        /// Named training paths (Options tab). Mutate via RoleCommands.
        public List<TrainingPath> trainingPaths = new List<TrainingPath>();
        private int nextRoleId = 1;
        private int nextGroupId = 1; // 0 reserved for the Default group
        private int nextPathId = 1;

        private List<Pawn> pawnKeysWorkingList;
        private List<PawnRoleSet> setValuesWorkingList;
        private List<Bill> billKeysWorkingList;
        private List<int> billValuesWorkingList;

        private static RoleStore cached;

        public RoleStore(World world) : base(world)
        {
            cached = this;
        }

        public static RoleStore Current
        {
            get
            {
                var world = Find.World;
                if (world == null) return null;
                if (cached == null || cached.world != world)
                    cached = world.GetComponent<RoleStore>();
                return cached;
            }
        }

        public int NextId() => nextRoleId++;

        public int NextGroupId() => nextGroupId++;

        public int NextPathId() => nextPathId++;

        public RoleGroup GroupById(int id) => groups.FirstOrDefault(g => g.id == id);

        public RoleGroup GroupByName(string name) => groups.FirstOrDefault(g =>
            string.Equals(g.label, name?.Trim(), System.StringComparison.OrdinalIgnoreCase));

        public TrainingPath PathById(int id) =>
            trainingPaths.FirstOrDefault(p => p.id == id);

        /// The Default group (id 0), materialized on demand: pinned first,
        /// swept like any user group when it empties. The stored label is
        /// INVARIANT (never the translated name — this is scribed, synced
        /// state); the UI renders id 0 from the keyed string.
        public RoleGroup EnsureDefaultGroup()
        {
            var group = GroupById(RoleGroup.DefaultId);
            if (group == null)
            {
                group = new RoleGroup { id = RoleGroup.DefaultId, label = "Default" };
                groups.Insert(0, group);
            }
            return group;
        }

        /// Keeps slot names index-aligned with the swatch list (auto-name gaps).
        public void SyncSwatchNames()
        {
            while (customSwatchNames.Count < customSwatches.Count)
                customSwatchNames.Add($"custom-{customSwatchNames.Count + 1}");
            foreach (var i in Enumerable.Range(0, customSwatchNames.Count))
                if (customSwatchNames[i].NullOrEmpty())
                    customSwatchNames[i] = $"custom-{i + 1}";
        }

        public Role RoleById(int id) => roles.FirstOrDefault(r => r.id == id);

        /// The engine-managed Odd Jobs role (invisible modded work types), or null.
        public Role ManagedRole => roles.FirstOrDefault(r => r.managed);

        public Role RoleByTemplate(string templateDefName) =>
            roles.FirstOrDefault(r => r.templateDefName == templateDefName);

        public bool IsManaged(Pawn pawn) =>
            pawn != null && pawnSets.TryGetValue(pawn, out var set) && set.assignments.Count > 0;

        public PawnRoleSet SetFor(Pawn pawn)
        {
            if (!pawnSets.TryGetValue(pawn, out var set))
            {
                set = new PawnRoleSet();
                pawnSets[pawn] = set;
            }
            return set;
        }

        public IEnumerable<Pawn> PawnsWithRole(int roleId) =>
            pawnSets.Where(kv => kv.Value.assignments.Any(a => a.roleId == roleId)).Select(kv => kv.Key);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // The saved vanilla priority maps double as the mod-removal fallback —
                // make sure every managed pawn's projection is current before writing.
                // Empty sets are skipped: an unmanaged pawn's projection is empty and
                // syncing it would zero their real vanilla priorities.
                foreach (var kv in pawnSets)
                    if (kv.Key != null && !kv.Key.Destroyed && kv.Value.assignments.Count > 0)
                        CompiledJobOrders.EnsureFresh(kv.Key);
            }
            Scribe_Values.Look(ref seeded, "seeded");
            Scribe_Values.Look(ref pathsSeeded, "pathsSeeded");
            Scribe_Values.Look(ref oddJobsDeleted, "oddJobsDeleted");
            Scribe_Values.Look(ref reportVanillaPriorities, "reportVanillaPriorities", true);
            Scribe_Collections.Look(ref recommendationOrder, "recommendationOrder", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && recommendationOrder == null)
                recommendationOrder = new List<int>();
            Scribe_Deep.Look(ref allRole, "allRole");
            Scribe_Values.Look(ref nextRoleId, "nextRoleId", 1);
            Scribe_Collections.Look(ref roles, "roles", LookMode.Deep);
            Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref knownWorkTypes, "knownWorkTypes", LookMode.Value);
            Scribe_Collections.Look(ref customSwatches, "customSwatches", LookMode.Value);
            Scribe_Collections.Look(ref customSwatchNames, "customSwatchNames", LookMode.Value);
            Scribe_Collections.Look(ref pawnSets, "pawnSets", LookMode.Reference, LookMode.Deep,
                ref pawnKeysWorkingList, ref setValuesWorkingList);
            Scribe_Collections.Look(ref billRoles, "billRoles", LookMode.Reference, LookMode.Value,
                ref billKeysWorkingList, ref billValuesWorkingList);
            Scribe_Values.Look(ref nextPathId, "nextPathId", 1);
            Scribe_Collections.Look(ref trainingPaths, "trainingPaths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                roles ??= new List<Role>();
                groups ??= new List<RoleGroup>();
                knownWorkTypes ??= new List<string>();
                customSwatches ??= new List<UnityEngine.Color>();
                customSwatchNames ??= new List<string>();
                SyncSwatchNames();
                pawnSets ??= new Dictionary<Pawn, PawnRoleSet>();
                pawnSets.RemoveAll(kv => kv.Key == null || kv.Value == null);
                billRoles ??= new Dictionary<Bill, int>();
                billRoles.RemoveAll(kv => kv.Key == null || kv.Key.DeletedOrDereferenced);
                trainingPaths ??= new List<TrainingPath>();
                // Empty paths survive (named containers); only non-empty corrupt geometry drops.
                trainingPaths.RemoveAll(p =>
                    p == null
                    || (p.roleIds.Count > 0 && !SkillProgressionMath.Validate(p.roleIds.Count, p.bandMins, p.bandMaxes)));
                foreach (var path in trainingPaths)
                    if (path.roleIds.Count == 0 && (path.bandMins.Count > 0 || path.bandMaxes.Count > 0))
                    {
                        path.bandMins.Clear();
                        path.bandMaxes.Clear();
                    }
                // Migration: the once-hidden All role becomes the visible,
                // engine-managed Odd Jobs catalog role, assigned to every managed
                // pawn at the last position (its old implicit spot).
                if (allRole != null)
                {
                    allRole.managed = true;
                    allRole.autoAssign = true;
                    allRole.label = "WR_OddJobsRole".Translate();
                    roles.Add(allRole);
                    foreach (var set in pawnSets.Values)
                        if (set.assignments.Count > 0 && set.assignments.All(a => a.roleId != allRole.id))
                            set.assignments.Add(new RoleAssignment { roleId = allRole.id });
                    allRole = null;
                }
                // Corrupt-save hygiene (after the allRole migration, so its id
                // resolves): assignments referencing deleted roles are inert but
                // count as managed; drop them and any set they empty.
                foreach (var set in pawnSets.Values)
                    set.assignments?.RemoveAll(a => RoleById(a.roleId) == null);
                pawnSets.RemoveAll(kv => kv.Value.assignments == null || kv.Value.assignments.Count == 0);
                CompiledJobOrders.InvalidateAll();
            }
        }
    }
}
