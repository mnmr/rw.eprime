using System.Collections.Generic;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Recipe scope (spec §2): unfinishedThingDef != null AND the produced def
    /// has CompQuality.
    ///
    /// Cache — Owner: process (def-derived only). Key: none (two sets built
    /// once). Value: immutable after startup. Dependencies: def database
    /// contents; rebuilt on demand after definition reload via Invalidate().
    /// Refresh: eager at startup ([StaticConstructorOnStartup]); Invalidate()
    /// exists for definition-reload scenarios but is not wired to any event
    /// in v1; def reloads mid-session are dev-mode only.
    /// Equality: n/a. Teardown: none needed (no world data).
    [StaticConstructorOnStartup]
    public static class ManagedRecipes
    {
        // Initialized inline so fields are never null; rebuilt atomically in Build().
        private static HashSet<ThingDef> uftDefs = new HashSet<ThingDef>();
        private static ThingDef[] uftDefArray = System.Array.Empty<ThingDef>();
        private static HashSet<RecipeDef> managed = new HashSet<RecipeDef>();
        // Cached static delegate — HashSet order is process-nondeterministic; MP requires stable iteration order.
        private static readonly System.Comparison<ThingDef> DefNameComparison =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        static ManagedRecipes() => Build();

        public static void Invalidate() => Build();

        private static void Build()
        {
            var newUftDefs = new HashSet<ThingDef>();
            var newManaged = new HashSet<RecipeDef>();
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.unfinishedThingDef == null) continue;
                newUftDefs.Add(recipe.unfinishedThingDef);
                ThingDef? product = recipe.ProducedThingDef;
                if (product != null && product.HasComp(typeof(CompQuality)))
                    newManaged.Add(recipe);
            }
            uftDefs = newUftDefs;
            var array = new ThingDef[newUftDefs.Count];
            newUftDefs.CopyTo(array);
            // HashSet order is process-nondeterministic; MP requires stable iteration order.
            System.Array.Sort(array, DefNameComparison);
            uftDefArray = array;
            managed = newManaged;
        }

        public static bool IsManagedRecipe(RecipeDef? recipe)
            => recipe != null && managed.Contains(recipe);

        /// All UFT ThingDefs (managed or not) — sharing (§8) and cap counting
        /// (§9) enumerate spawned UFTs through these. Returns a plain array so
        /// callers in render/tick paths iterate without enumerator boxing.
        public static ThingDef[] AllUftDefs => uftDefArray;

        public static string? ProductDefName(RecipeDef? recipe)
            => recipe?.ProducedThingDef?.defName;
    }
}
