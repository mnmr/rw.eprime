using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Recipes whose product quality an ingredient decides rather than the
    /// crafter: Vanilla Genetics Expanded's GenRecipe.PostProcessProduct
    /// postfix overwrites the rolled quality with the genoframe ingredient's
    /// DefExtension_Quality, so a skill gate or a below-target retry would only
    /// burn genoframes. Matched by type name (no GeneticRim reference).
    ///
    /// Reads each ingredient filter's declared thing and category lists
    /// (categories include their parents) minus explicit disallows, never the
    /// resolved allowed set, so it gives the same answer before definition
    /// resolution (finisher work-giver generation) and after (ManagedRecipes).
    /// Stateless: callers pass the marked defs, computed once per scan.
    internal static class IngredientQuality
    {
        private const string ExtensionType = "GeneticRim.DefExtension_Quality";

        private static readonly AccessTools.FieldRef<ThingFilter, List<ThingDef>> ThingDefs =
            AccessTools.FieldRefAccess<ThingFilter, List<ThingDef>>("thingDefs");
        private static readonly AccessTools.FieldRef<ThingFilter, List<string>> Categories =
            AccessTools.FieldRefAccess<ThingFilter, List<string>>("categories");
        private static readonly AccessTools.FieldRef<ThingFilter, List<ThingDef>> DisallowedThingDefs =
            AccessTools.FieldRefAccess<ThingFilter, List<ThingDef>>("disallowedThingDefs");
        private static readonly AccessTools.FieldRef<ThingFilter, List<string>> DisallowedCategories =
            AccessTools.FieldRefAccess<ThingFilter, List<string>>("disallowedCategories");

        /// The ThingDefs carrying the ingredient-quality extension (empty
        /// without VGE). Startup/reload scans only.
        internal static List<ThingDef> MarkedDefs()
        {
            var marked = new List<ThingDef>();
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                List<DefModExtension>? extensions = defs[i].modExtensions;
                if (extensions == null) continue;
                for (int e = 0; e < extensions.Count; e++)
                    if (extensions[e].GetType().FullName == ExtensionType)
                    {
                        marked.Add(defs[i]);
                        break;
                    }
            }
            return marked;
        }

        /// True when any ingredient of the recipe accepts a marked def.
        internal static bool Decides(RecipeDef recipe, List<ThingDef> marked)
        {
            if (marked.Count == 0 || recipe.ingredients == null) return false;
            List<IngredientCount> ingredients = recipe.ingredients;
            for (int i = 0; i < ingredients.Count; i++)
            {
                ThingFilter? filter = ingredients[i].filter;
                if (filter == null) continue;
                for (int m = 0; m < marked.Count; m++)
                    if (Accepts(filter, marked[m])) return true;
            }
            return false;
        }

        private static bool Accepts(ThingFilter filter, ThingDef def)
        {
            List<ThingDef>? things = ThingDefs(filter);
            bool allowed = things != null && things.Contains(def)
                || InCategory(def, Categories(filter));
            if (!allowed) return false;
            List<ThingDef>? disallowed = DisallowedThingDefs(filter);
            return (disallowed == null || !disallowed.Contains(def))
                && !InCategory(def, DisallowedCategories(filter));
        }

        private static bool InCategory(ThingDef def, List<string>? names)
        {
            if (names == null || def.thingCategories == null) return false;
            for (int c = 0; c < def.thingCategories.Count; c++)
                for (ThingCategoryDef? category = def.thingCategories[c];
                     category != null; category = category.parent)
                    if (names.Contains(category.defName)) return true;
            return false;
        }
    }
}
