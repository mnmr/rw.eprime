using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Scans one map for work the colony has planned but not yet paid for, and
    /// folds the material it owes into the shared count accumulator.
    ///
    /// Runs only inside the tick-throttled count-snapshot builder, and only for
    /// the reservation options the player enabled — an all-off configuration
    /// never reaches this class. Reads game state; mutates nothing.
    internal static class PlannedWorkCounts
    {
        internal static void Accumulate(
            Map map, CountAccumulator accumulator, PlannedWorkOptions options)
        {
            if (map == null) return;
            if (options.ReserveBills)
                AccumulateBills(map, accumulator, options.QualityRework);
            if (options.ReserveBuildables)
                AccumulateBuildables(map, accumulator, options.QualityRework);
        }

        // ---- bills -------------------------------------------------------

        private static void AccumulateBills(
            Map map, CountAccumulator accumulator, bool qualityRework)
        {
            var givers = map.listerThings.ThingsInGroup(
                ThingRequestGroup.PotentialBillGiver);
            for (int i = 0; i < givers.Count; i++)
            {
                if (!(givers[i] is IBillGiver giver)) continue;
                List<Bill> bills = giver.BillStack.Bills;
                for (int b = 0; b < bills.Count; b++)
                {
                    // Only production bills repeat and consume ingredients on a
                    // schedule; medical and other one-shot bills own no debt.
                    if (bills[b] is Bill_Production bill)
                        AccumulateBill(bill, accumulator, qualityRework);
                }
            }
        }

        private static void AccumulateBill(
            Bill_Production bill, CountAccumulator accumulator, bool qualityRework)
        {
            // A suspended or satisfied-and-paused bill is not going to draw
            // anything; neither is one whose giver has gone.
            if (bill.suspended || bill.paused || bill.DeletedOrDereferenced) return;

            RecipeDef recipe = bill.recipe;
            List<IngredientCount> ingredients = recipe?.ingredients;
            if (ingredients == null || ingredients.Count == 0) return;

            int iterations = IterationsOf(bill, recipe);
            if (iterations <= 0) return;

            float attempts = qualityRework
                ? QualityJobsBridge.ExpectedAttemptsForBill(bill)
                : QualityJobsBridge.NoRework;

            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                ThingDef def = SoleAllowedDef(ingredient, recipe, bill);
                if (def == null) continue;
                int debt = PlannedWorkMath.BillDebt(
                    ingredient.CountRequiredOfFor(def, recipe, bill),
                    iterations, attempts);
                if (debt > 0)
                    accumulator.AddBillDebt(def.defName, def.shortHash, debt);
            }
        }

        /// Iterations the bill still owes, per its repeat mode. Reads bill
        /// state without touching it: vanilla's own ShouldDoNow would re-evaluate
        /// and write the paused flag, which a render-data pass must never do.
        private static int IterationsOf(Bill_Production bill, RecipeDef recipe)
        {
            if (bill.repeatMode == BillRepeatModeDefOf.Forever)
                return PlannedWorkMath.BillIterations(
                    BillRepeat.Forever, 0, 0, 0, 1);

            if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                // CanCountProducts is vanilla's own guard that products[0] is
                // the single meaningful product: without it a multi-product or
                // special-product recipe yields a count for the wrong thing and
                // we would reserve against it. CountProducts also dereferences
                // the bill's map, which needs a spawned giver.
                if (recipe.WorkerCounter == null
                    || !recipe.WorkerCounter.CanCountProducts(bill))
                    return 0;
                if (!(bill.billStack?.billGiver is Thing giver) || !giver.Spawned)
                    return 0;
                return PlannedWorkMath.BillIterations(
                    BillRepeat.TargetCount, bill.repeatCount, bill.targetCount,
                    recipe.WorkerCounter.CountProducts(bill),
                    YieldPerIteration(recipe));
            }

            // Vanilla's default for anything else, including a modded mode.
            return PlannedWorkMath.BillIterations(
                BillRepeat.RepeatCount, bill.repeatCount, 0, 0, 1);
        }

        /// How many of the counted product one run yields, so a
        /// do-until-you-have bill does not reserve one run per missing unit.
        private static int YieldPerIteration(RecipeDef recipe)
        {
            ThingDef product = recipe.ProducedThingDef;
            if (product == null) return 1;
            List<ThingDefCountClass> products = recipe.products;
            for (int i = 0; i < products.Count; i++)
                if (products[i].thingDef == product)
                    return products[i].count;
            return 1;
        }

        /// The one def an ingredient can only be satisfied by, or null when the
        /// choice is open. An ingredient that accepts several defs (any leather,
        /// any meat, any stuff) gives no honest way to say which stack a hauler
        /// will pick, so it reserves nothing rather than overstating scarcity on
        /// every candidate. The bill's own ingredient filter counts: narrowing a
        /// stuffed recipe to a single material makes it unambiguous.
        private static ThingDef SoleAllowedDef(
            IngredientCount ingredient, RecipeDef recipe, Bill bill)
        {
            ThingFilter filter = ingredient.filter;
            if (filter == null) return null;

            // Most recipes name their ingredient outright, and vanilla answers
            // that from one field. Taking it skips walking an allowed set that
            // can run to every stuff def in the game — but the recipe and bill
            // filters still decide, because a bill that disallows its only
            // possible ingredient can never run and must reserve nothing.
            if (ingredient.IsFixedIngredient)
            {
                ThingDef fixedDef = ingredient.FixedIngredient;
                return Usable(fixedDef, recipe, bill) ? fixedDef : null;
            }

            ThingDef sole = null;
            foreach (ThingDef def in filter.AllowedThingDefs)
            {
                if (!Allowed(def, recipe, bill)) continue;
                if (sole != null) return null;
                sole = def;
            }
            return sole != null && Counted(sole) ? sole : null;
        }

        private static bool Usable(ThingDef def, RecipeDef recipe, Bill bill)
            => def != null && Allowed(def, recipe, bill) && Counted(def);

        /// Both narrowing filters that stand between an ingredient slot and a
        /// def actually being consumed.
        private static bool Allowed(ThingDef def, RecipeDef recipe, Bill bill)
        {
            if (recipe.fixedIngredientFilter != null
                && !recipe.fixedIngredientFilter.Allows(def)) return false;
            return bill.ingredientFilter == null
                || bill.ingredientFilter.Allows(def);
        }

        // ---- buildables --------------------------------------------------

        private static void AccumulateBuildables(
            Map map, CountAccumulator accumulator, bool qualityRework)
        {
            AccumulateConstructibles(map, ThingRequestGroup.Blueprint,
                accumulator, qualityRework);
            AccumulateConstructibles(map, ThingRequestGroup.BuildingFrame,
                accumulator, qualityRework);
        }

        private static void AccumulateConstructibles(
            Map map, ThingRequestGroup group,
            CountAccumulator accumulator, bool qualityRework)
        {
            Faction player = Faction.OfPlayer;
            List<Thing> things = map.listerThings.ThingsInGroup(group);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                // Another faction's construction never draws on our stock.
                if (thing.Faction != player) continue;
                // A forbidden blueprint or frame is not going to be worked on,
                // so it must not shrink the counter behind the player's back.
                if (thing.IsForbidden(player)) continue;
                // Blueprint_Build (and every vanilla subclass) and Frame are the
                // only constructibles with a material cost. The check is NOT
                // redundant with the IConstructible cast below: Blueprint_Install
                // is also IConstructible, and its TotalMaterialCost logs an error
                // by design — calling it here would spam the log every refresh.
                if (!(thing is Blueprint_Build) && !(thing is Frame)) continue;
                if (!(thing is IConstructible constructible)) continue;

                List<ThingDefCountClass> cost = constructible.TotalMaterialCost();
                if (cost == null || cost.Count == 0) continue;

                float attempts = qualityRework
                    ? QualityJobsBridge.ExpectedAttemptsForConstructible(thing)
                    : QualityJobsBridge.NoRework;
                // A rebuild tears the finished building down first, so what it
                // hands back is governed by the BUILT thing's leavings rules,
                // not the blueprint's.
                var built = thing.def.entityDefToBuild as ThingDef;
                float baseReturned =
                    thing.def.entityDefToBuild?.resourcesFractionWhenDeconstructed ?? 0f;
                BuildingProperties leavings = built?.building;

                for (int c = 0; c < cost.Count; c++)
                {
                    ThingDefCountClass item = cost[c];
                    ThingDef def = item.thingDef;
                    if (def == null || !Counted(def)) continue;
                    float returned = PlannedWorkMath.ReturnedFraction(
                        baseReturned,
                        forced: Lists(leavings?.forcedCostLeavings, def),
                        blacklisted: Lists(leavings?.leavingsBlacklist, def));
                    // ThingCountNeeded already nets off what has been hauled
                    // into a part-built frame; a blueprint owes the lot.
                    int debt = PlannedWorkMath.BuildableDebt(
                        constructible.ThingCountNeeded(def), item.count,
                        attempts, returned);
                    if (debt > 0)
                        accumulator.AddBuildableDebt(def.defName, def.shortHash, debt);
                }
            }
        }

        /// Debt is only worth publishing for defs the readout can display, which
        /// is exactly the set the count pass itself gathers.
        private static bool Counted(ThingDef def)
            => def.CountAsResource || GameResourceCatalog.IsExtraCountedDef(def);

        /// Membership in one of a building's short leavings lists; absent lists
        /// are the common case and cost nothing.
        private static bool Lists(List<ThingDef> defs, ThingDef def)
        {
            if (defs == null) return false;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i] == def) return true;
            return false;
        }
    }
}
