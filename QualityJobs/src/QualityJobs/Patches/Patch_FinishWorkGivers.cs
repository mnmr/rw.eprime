using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Generates one QJ_FinishQualityWork_<workType> WorkGiverDef per relevant
    /// work type (Construction + every bench work type hosting managed quality
    /// recipes) so the dispatched finisher scanner runs at a priority above all
    /// vanilla peers in the same work type.
    ///
    /// Load order (verified PlayDataLoader.cs DoPlayLoad):
    ///   1. XML cross-references resolved (RecipeDef.AllRecipeUsers usable)
    ///   2. GenerateImpliedDefs_PreResolve (this postfix runs here, Priority.Last)
    ///   3. Cross-references resolved again for implied defs
    ///   4. WorkTypeDef.ResolveReferences — builds workGiversByPriority (AFTER us)
    ///
    /// Priority value: max(priorityInType among existing givers of this work type)
    /// + 1, because WorkTypeDef.ResolveReferences orders by priorityInType
    /// DESCENDING, so higher values run first. If no existing givers exist for
    /// the work type, falls back to 1 (modest positive value in the winning
    /// direction). [Verified: WorkTypeDef.cs:104-110 "orderby d.priorityInType
    /// descending".]
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_FinishWorkGivers
    {
        // Comparison delegate for deterministic MP/report sort by defName.
        // Static readonly — never re-allocated on hot paths.
        private static readonly System.Comparison<WorkTypeDef> ByDefName =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        public static void Postfix(bool hotReload)
        {
            // Compute the set of relevant work types:
            //   - WorkTypeDefOf.Construction (always)
            //   - The work type of every recipe that has unfinishedThingDef != null
            //     AND whose produced def has CompQuality. (ManagedRecipes uses the
            //     same predicate but is built at StaticConstructorOnStartup — after
            //     def generation — so we duplicate the small predicate here.)
            //
            // The work-type resolution below intentionally duplicates the logic in
            // Dispatcher.WorkTypeForRecipe rather than calling it or using
            // ManagedRecipes. Memoizing via Dispatcher at PreResolve would freeze
            // the cache before other mods finish loading their defs; the runtime
            // memo in Dispatcher starts fresh at first post-startup use and sees the
            // final, fully-resolved def database. The duplication is small, one-time,
            // and isolated to this startup path.
            //
            // Use a List+manual dedup (not HashSet) to avoid enumerator boxing in
            // this one-time startup path; the set is tiny (~3-5 work types).
            var workTypes = new List<WorkTypeDef>(8);

            // Construction is always included.
            WorkTypeDef? construction = WorkTypeDefOf.Construction;
            if (construction != null)
                workTypes.Add(construction);

            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            List<WorkGiverDef> givers = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            for (int r = 0; r < recipes.Count; r++)
            {
                RecipeDef recipe = recipes[r];
                if (recipe.unfinishedThingDef == null) continue;
                ThingDef? product = recipe.ProducedThingDef;
                if (product == null || !product.HasComp(typeof(CompQuality))) continue;

                // Resolve work type the same way Dispatcher.WorkTypeForRecipe does.
                WorkTypeDef? wt = null;
                foreach (ThingDef benchDef in recipe.AllRecipeUsers)
                {
                    for (int g = 0; g < givers.Count; g++)
                    {
                        if (givers[g].fixedBillGiverDefs != null
                            && givers[g].fixedBillGiverDefs.Contains(benchDef))
                        {
                            wt = givers[g].workType;
                            goto foundWorkType;
                        }
                    }
                }
                foundWorkType:
                if (wt == null) continue;

                // Dedup without LINQ/HashSet allocation.
                bool already = false;
                for (int w = 0; w < workTypes.Count; w++)
                {
                    if (workTypes[w] == wt) { already = true; break; }
                }
                if (!already) workTypes.Add(wt);
            }

            // Sort by defName for MP determinism and reproducible report output.
            workTypes.Sort(ByDefName);

            // Translate once per def at generation time. Keyed language data is
            // injected before implied-def generation (PlayDataLoader.cs:158 vs :176),
            // so Translate() resolves correctly here (not hot path).
            string label  = "QJ_FinishGiverLabel".Translate();
            string verb   = "QJ_FinishGiverVerb".Translate();
            string gerund = "QJ_FinishGiverGerund".Translate();

            // Generate one WorkGiverDef per work type.
            for (int w = 0; w < workTypes.Count; w++)
            {
                WorkTypeDef wt = workTypes[w];
                int maxPriority = ComputeMaxPriority(wt, givers);
                // Higher priorityInType runs first (descending order in ResolveReferences).
                int ourPriority = maxPriority + 1;

                var def = new WorkGiverDef
                {
                    defName = "QJ_FinishQualityWork_" + wt.defName,
                    label   = label,
                    verb    = verb,
                    gerund  = gerund,
                    giverClass        = typeof(WorkGiver_FinishQualityWork),
                    workType          = wt,
                    priorityInType    = ourPriority,
                    requiredCapacities = new List<PawnCapacityDef> { PawnCapacityDefOf.Manipulation },
                    // scanThings = true (default), scanCells = false (default).
                    // Invisible in the Work tab: no verb in the work type's visible sense;
                    // WorkRoles may list gerund under the work type's jobs.
                    modContentPack    = QualityJobsMod.Instance.Content,
                };

                DefGenerator.AddImpliedDef(def, hotReload);
            }
        }

        /// Returns the maximum priorityInType among existing WorkGiverDefs for
        /// this work type. Falls back to 0 if none exist (result will be 0+1=1).
        private static int ComputeMaxPriority(WorkTypeDef wt, List<WorkGiverDef> givers)
        {
            int max = 0;
            bool any = false;
            for (int i = 0; i < givers.Count; i++)
            {
                if (givers[i].workType != wt) continue;
                if (!any || givers[i].priorityInType > max)
                {
                    max = givers[i].priorityInType;
                    any = true;
                }
            }
            return max;
        }
    }
}
