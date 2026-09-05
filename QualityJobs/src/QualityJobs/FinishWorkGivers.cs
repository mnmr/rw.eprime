using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// The generated finish givers (Patch_FinishWorkGivers) as one array, for
    /// the high-priority path that issues their jobs without walking a pawn's
    /// work-giver list, plus the care work types that path still yields to.
    ///
    /// Cache — Owner: process (def-derived only). Key: none. Value: immutable
    /// WorkGiverDef[] sorted by defName so every MP client visits the givers in
    /// the same order, and the resolved Doctor, Patient and PatientBedRest
    /// work types (only Doctor has a DefOf). Dependencies: def database
    /// contents; ManagedRecipes.Invalidate clears both after a definition
    /// reload. Refresh: lazy on first read after startup. Equality: n/a.
    /// Teardown: Invalidate drops the arrays; nothing else is owned.
    public static class FinishWorkGivers
    {
        private static WorkGiverDef[]? all;
        private static WorkTypeDef?[]? careTypes;
        private static readonly Comparison<WorkGiverDef> ByDefName =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        public static WorkGiverDef[] All => all ??= Build();

        /// Doctor, Patient, and Patient bed rest: the work the finish job
        /// stays behind even in high-priority mode.
        public static bool IsCareWorkType(WorkTypeDef? workType)
        {
            if (workType == null) return false;
            WorkTypeDef?[] care = careTypes ??= BuildCareTypes();
            for (int i = 0; i < care.Length; i++)
                if (care[i] == workType) return true;
            return false;
        }

        public static void Invalidate()
        {
            all = null;
            careTypes = null;
        }

        private static WorkTypeDef?[] BuildCareTypes()
            => new[]
            {
                WorkTypeDefOf.Doctor,
                DefDatabase<WorkTypeDef>.GetNamedSilentFail("Patient"),
                DefDatabase<WorkTypeDef>.GetNamedSilentFail("PatientBedRest"),
            };

        private static WorkGiverDef[] Build()
        {
            var found = new List<WorkGiverDef>(8);
            List<WorkGiverDef> defs = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].giverClass == typeof(WorkGiver_FinishQualityWork))
                    found.Add(defs[i]);
            found.Sort(ByDefName);
            return found.ToArray();
        }
    }
}
