using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Recipes whose product quality an ingredient decides rather than the
    /// crafter: Vanilla Genetics Expanded's hybrid implants take the quality
    /// of their genoframe (GR_GenoframeAwful … GR_GenoframeLegendary, each
    /// carrying a GeneticRim.DefExtension_Quality). Matched by type name,
    /// no assembly reference. Called when a production bill is created
    /// (reconcile pass), never while drawing.
    internal static class IngredientQuality
    {
        private const string ExtensionType = "GeneticRim.DefExtension_Quality";

        /// Limits the bill to quality-deciding ingredients of at least
        /// minQuality, through the bill's own ingredient filter (these
        /// ingredients come from a category, so the filter applies). True
        /// when the recipe has such ingredients: the product then has the
        /// quality of the ingredient used, so the bill promises the minimum.
        internal static bool Restrict(Bill bill, int minQuality)
        {
            List<IngredientCount>? ingredients = bill.recipe.ingredients;
            if (ingredients == null) return false;
            bool decides = false;
            for (int i = 0; i < ingredients.Count; i++)
                foreach (ThingDef def in ingredients[i].filter.AllowedThingDefs)
                {
                    if (!TryQualityOf(def, out int quality)) continue;
                    decides = true;
                    if (quality < minQuality) bill.ingredientFilter.SetAllow(def, false);
                }
            return decides;
        }

        private static bool TryQualityOf(ThingDef def, out int quality)
        {
            quality = 0;
            List<DefModExtension>? extensions = def.modExtensions;
            if (extensions == null) return false;
            for (int i = 0; i < extensions.Count; i++)
            {
                DefModExtension extension = extensions[i];
                if (extension == null || extension.GetType().FullName != ExtensionType)
                    continue;
                FieldInfo? field = extension.GetType().GetField("quality");
                if (field == null) return false;
                quality = (int)(QualityCategory)field.GetValue(extension);
                return true;
            }
            return false;
        }
    }
}
