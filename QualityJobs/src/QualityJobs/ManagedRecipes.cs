using System.Collections.Generic;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Recipe scope (spec §2): unfinishedThingDef != null AND the produced def
    /// has CompQuality AND the crafter's skill decides that quality (recipes an
    /// ingredient decides are excluded, see IngredientQuality; the finisher
    /// work-giver generation applies the same rule).
    ///
    /// Cache — Owner: process (def-derived only). Key: UFT def set; managed
    /// recipes by defName. Value: immutable after startup. Dependencies: def database
    /// contents (recipe unfinishedThingDef and products, product CompQuality,
    /// ingredient filters' declared things/categories and the marked defs'
    /// modExtensions types);
    /// rebuilt on demand after definition reload via Invalidate().
    /// Refresh: eager at startup ([StaticConstructorOnStartup]); Invalidate()
    /// also clears Dispatcher.s_workTypeCache (same dependency set) so both
    /// caches stay coherent after a definition reload.
    /// Equality: n/a. Teardown/reset: Invalidate replaces all def-derived sets.
    [StaticConstructorOnStartup]
    public static class ManagedRecipes
    {
        // Initialized inline so fields are never null; rebuilt atomically in Build().
        private static HashSet<ThingDef> uftDefs = new HashSet<ThingDef>();
        private static ThingDef[] uftDefArray = System.Array.Empty<ThingDef>();
        // Keyed by defName, not RecipeDef: Craft with Color swaps bill.recipe
        // for an unregistered runtime clone (same defName, extra dye
        // ingredient) whose defNameHash is never resolved, so Def equality
        // misses it. The value is the registered def.
        private static Dictionary<string, RecipeDef> managed = new Dictionary<string, RecipeDef>();
        // Cached static delegate — HashSet order is process-nondeterministic; MP requires stable iteration order.
        private static readonly System.Comparison<ThingDef> DefNameComparison =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        static ManagedRecipes() => Build();

        public static void Invalidate()
        {
            Build();
            // The recipe→workType memo in Dispatcher shares the same def-database
            // dependency. Clear it so WorkTypeForRecipe re-resolves after a reload.
            Dispatcher.InvalidateWorkTypeCache();
            FinishWorkGivers.Invalidate();
            QualityBonusStats.Refresh();
            QualityJobsStore.Active?.NotifyDefinitionsChanged();
        }

        private static void Build()
        {
            var newUftDefs = new HashSet<ThingDef>();
            var newManaged = new Dictionary<string, RecipeDef>();
            List<ThingDef> ingredientQuality = IngredientQuality.MarkedDefs();
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.unfinishedThingDef == null) continue;
                newUftDefs.Add(recipe.unfinishedThingDef);
                ThingDef? product = recipe.ProducedThingDef;
                if (product != null && product.HasComp(typeof(CompQuality))
                    && !IngredientQuality.Decides(recipe, ingredientQuality))
                    newManaged[recipe.defName] = recipe;
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
            => Registered(recipe) != null;

        /// The registered managed def for recipe (itself, or the def a runtime
        /// clone was copied from), or null when unmanaged. Identity-keyed
        /// consumers must use this instead of bill.recipe / uft.Recipe.
        public static RecipeDef? Registered(RecipeDef? recipe)
            => recipe?.unfinishedThingDef != null
                && managed.TryGetValue(recipe.defName, out RecipeDef def) ? def : null;

        /// All UFT ThingDefs (managed or not) — sharing (§8) and cap counting
        /// (§9) enumerate spawned UFTs through these. Returns a plain array so
        /// callers in render/tick paths iterate without enumerator boxing.
        public static ThingDef[] AllUftDefs => uftDefArray;

        public static string? ProductDefName(RecipeDef? recipe)
            => recipe?.ProducedThingDef?.defName;
    }
}
