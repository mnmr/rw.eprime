using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public static class CompiledJobOrders
    {
        private sealed class Entry
        {
            public List<WorkGiver> Normal;
            public List<WorkGiver> Emergency;
            public Dictionary<WorkTypeDef, int> Priorities;
            /// Rank order projected onto vanilla's 1-4 scale (order-faithful).
            public Dictionary<WorkTypeDef, int> VanillaBuckets;
        }

        private static readonly Dictionary<Pawn, Entry> cache = new Dictionary<Pawn, Entry>();

        public static void Invalidate(Pawn pawn)
        {
            cache.Remove(pawn);
        }

        public static void InvalidateRole(int roleId)
        {
            var store = RoleStore.Current;
            if (store == null) { cache.Clear(); return; }
            store.RoleById(roleId)?.InvalidateCoverage();
            foreach (var pawn in store.PawnsWithRole(roleId).ToList())
                cache.Remove(pawn);
        }

        public static void InvalidateAll()
        {
            cache.Clear();
            var store = RoleStore.Current;
            if (store != null)
                foreach (var role in store.roles)
                    role.InvalidateCoverage();
        }

        /// Recompile every pawn holding a role with a time rule (hour boundary crossed).
        public static void InvalidateAllTimeRuled()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            foreach (var role in store.roles)
                if (role.activeHours != Role.AllHours)
                    InvalidateRole(role.id);
        }

        /// Returned lists are owned by the cache — callers must never mutate them.
        public static List<WorkGiver> NormalFor(Pawn pawn) => For(pawn).Normal;
        public static List<WorkGiver> EmergencyFor(Pawn pawn) => For(pawn).Emergency;

        public static int PriorityFor(Pawn pawn, WorkTypeDef workType) =>
            For(pawn).Priorities.TryGetValue(workType, out var bucket) ? bucket : 0;

        /// The rank projected onto vanilla's 0-4 scale, such that vanilla's
        /// replay of the numbers reproduces the internal order where four
        /// numbers suffice (same values as the dormant fallback map).
        public static int VanillaPriorityFor(Pawn pawn, WorkTypeDef workType)
        {
            return For(pawn).VanillaBuckets.TryGetValue(workType, out var bucket) ? bucket : 0;
        }

        private static Entry For(Pawn pawn)
        {
            if (!cache.TryGetValue(pawn, out var entry))
            {
                entry = Build(pawn);
                cache[pawn] = entry;
                SyncVanillaFallback(pawn, entry);
            }
            return entry;
        }

        /// Ensures the pawn's compiled order (and its vanilla fallback map) is current.
        public static void EnsureFresh(Pawn pawn) => For(pawn);

        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, DefMap<WorkTypeDef, int>> VanillaPriorities =
            AccessTools.FieldRefAccess<Pawn_WorkSettings, DefMap<WorkTypeDef, int>>("priorities");

        /// Mirrors the compiled order into the dormant vanilla priorities map as 0-4
        /// values, so removing the mod leaves the vanilla Work tab in a sane state.
        /// Writes the private DefMap directly: SetPriority is swallowed for managed
        /// pawns and its side effects (job interruption on 0) must not fire here.
        private static void SyncVanillaFallback(Pawn pawn, Entry entry)
        {
            var workSettings = pawn.workSettings;
            if (workSettings == null) return;
            var map = VanillaPriorities(workSettings);
            if (map == null) return;
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                map[workType] = entry.VanillaBuckets.TryGetValue(workType, out var bucket) ? bucket : 0;
        }

        private static VanillaProjectionCategories BuildProjectionCategories(RoleStore store)
        {
            var categories = new VanillaProjectionCategories();
            var basics = store?.roles.FirstOrDefault(r => r.templateDefName == "WS_Basics");
            if (basics != null)
                foreach (var entry in basics.entries)
                {
                    var type = entry.Kind == JobEntryKind.WorkType
                        ? entry.DefName
                        : GameJobCatalog.Instance.WorkTypeOf(entry.DefName);
                    if (type != null) categories.Basics.Add(type);
                }
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                bool skilled = !workType.relevantSkills.NullOrEmpty();
                if (skilled) categories.Skilled.Add(workType.defName);
                if (skilled && workType.relevantSkills.Contains(SkillDefOf.Intellectual))
                    categories.Research.Add(workType.defName);
                if (!skilled && !categories.Basics.Contains(workType.defName))
                    categories.Grunt.Add(workType.defName);
            }
            return categories;
        }

        private static Entry Build(Pawn pawn)
        {
            var store = RoleStore.Current;
            var roleEntries = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
            if (store != null && store.pawnSets.TryGetValue(pawn, out var set))
            {
                foreach (var assignment in set.assignments)
                {
                    if (!assignment.enabled) continue;
                    var role = store.RoleById(assignment.roleId);
                    if (role != null && role.enabled && RoleRules.Pass(role, pawn))
                        roleEntries.Add((JobOrderCompiler.WithMovedSnapshotGivers(
                            role.entries, role.workTypeSnapshots, GameJobCatalog.Instance), role.blocker));
                }
            }

            Func<string, bool> pawnCanDo = giverDefName =>
            {
                var def = GameJobCatalog.Instance.GiverDef(giverDefName);
                return def != null
                    && !pawn.WorkTypeIsDisabled(def.workType)
                    && !pawn.WorkTagIsDisabled(def.workTags);
            };

            var compiled = JobOrderCompiler.Compile(roleEntries, GameJobCatalog.Instance, pawnCanDo);

            // Work-tab column order — how vanilla replays equal priority numbers.
            var columns = new Dictionary<string, int>();
            foreach (var workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
                columns[workType.defName] = columns.Count;
            var buckets = JobOrderCompiler.ToVanillaPriorities(compiled.WorkTypePriorities,
                name => columns.TryGetValue(name, out var column) ? column : int.MaxValue,
                BuildProjectionCategories(store));

            return new Entry
            {
                Normal = compiled.Normal.Select(n => GameJobCatalog.Instance.GiverDef(n).Worker).ToList(),
                Emergency = compiled.Emergency.Select(n => GameJobCatalog.Instance.GiverDef(n).Worker).ToList(),
                Priorities = compiled.WorkTypePriorities.ToDictionary(
                    kv => DefDatabase<WorkTypeDef>.GetNamed(kv.Key),
                    kv => kv.Value),
                VanillaBuckets = buckets.ToDictionary(
                    kv => DefDatabase<WorkTypeDef>.GetNamed(kv.Key),
                    kv => kv.Value)
            };
        }
    }
}
