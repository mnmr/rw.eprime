using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    internal static class SharedWorkCompatibility
    {
        // Structural compatibility, independent of temporary hauling/reservations.
        // A blocked source must still be able to consume its pending shared item.
        internal static bool Matches(WorkItemEntry entry, Bill bill)
        {
            UnfinishedThing? item = entry.uft;
            if (item == null || item.Destroyed || item.Recipe != bill.recipe) return false;
            if (!ShareMatch.StyleCompatible(entry.snapshot?.ToStyleKey() ?? StyleKey.Unknown,
                    StyleSnapshot.KeyOf((Bill_Production)bill))) return false;
            return IngredientsAllowed(item, bill);
        }

        internal static bool IngredientsAllowed(UnfinishedThing item, Bill bill)
        {
            for (int i = 0; i < item.ingredients.Count; i++)
                if (!bill.IsFixedOrAllowedIngredient(item.ingredients[i].def)) return false;
            return true;
        }
    }
}
