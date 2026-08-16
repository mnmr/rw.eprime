using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace EPrimeReadouts
{
    /// Scans one map for work the colony has planned but not yet paid for, and
    /// folds the material it owes into the shared count accumulator.
    ///
    /// Generic work is read inside the tick-throttled count builder. QJA work is
    /// projected only behind QJA's source-reference invalidation gate and then
    /// reused by that count builder. Reads game state; mutates nothing.
    internal static class PlannedWorkCounts
    {
        private sealed class QualityMapBuilder
        {
            internal QualityMapBuilder(Map map)
            {
                Map = map;
            }

            internal readonly Map Map;
            internal readonly List<Bill_Production> Bills =
                new List<Bill_Production>();
            internal readonly List<Thing> Targets = new List<Thing>();
            internal readonly Dictionary<ThingDef, List<QualityJobsWorkEntry>>
                WorkByResource = new Dictionary<ThingDef,
                    List<QualityJobsWorkEntry>>(IdentityComparer<ThingDef>.Instance);
            internal readonly List<ThingDef> Resources = new List<ThingDef>();
            internal Dictionary<CarriedKey, int>? Carried;
            internal bool CarriedBuilt;

            internal void Add(ThingDef resource, QualityJobsWorkEntry work)
            {
                if (!WorkByResource.TryGetValue(
                        resource, out List<QualityJobsWorkEntry> entries))
                {
                    entries = new List<QualityJobsWorkEntry>();
                    WorkByResource.Add(resource, entries);
                    Resources.Add(resource);
                }
                entries.Add(work);
            }

            internal QualityJobsMapWorkSnapshot Build()
            {
                ThingDef[] resources = Resources.ToArray();
                System.Array.Sort(resources, compareResourceDefs);
                var projected = new QualityJobsResourceWork[resources.Length];
                for (int i = 0; i < resources.Length; i++)
                {
                    ThingDef resource = resources[i];
                    QualityJobsWorkEntry[] entries =
                        WorkByResource[resource].ToArray();
                    System.Array.Sort(entries, compareQualityWork);
                    projected[i] = new QualityJobsResourceWork(
                        resource, entries);
                }
                Bill_Production[] bills = Bills.ToArray();
                System.Array.Sort(bills, compareBills);
                Thing[] targets = Targets.ToArray();
                System.Array.Sort(targets, compareTargets);
                return new QualityJobsMapWorkSnapshot(
                    Map, bills, targets, projected);
            }
        }

        private static readonly System.Comparison<ThingDef> compareResourceDefs =
            CompareResourceDefs;
        private static readonly System.Comparison<QualityMapBuilder>
            compareQualityMaps = CompareQualityMaps;
        private static readonly System.Comparison<Bill_Production> compareBills =
            CompareBills;
        private static readonly System.Comparison<Thing> compareTargets =
            CompareTargets;
        private static readonly System.Comparison<QualityJobsWorkEntry>
            compareQualityWork = CompareQualityWork;

        private readonly struct CarriedKey : System.IEquatable<CarriedKey>
        {
            internal CarriedKey(Thing destination, ThingDef resource)
            {
                Destination = destination;
                Resource = resource;
            }

            internal readonly Thing Destination;
            internal readonly ThingDef Resource;

            public bool Equals(CarriedKey other) =>
                ReferenceEquals(Destination, other.Destination)
                && ReferenceEquals(Resource, other.Resource);

            public override bool Equals(object obj) =>
                obj is CarriedKey other && Equals(other);

            public override int GetHashCode() =>
                ((Destination != null ? Destination.GetHashCode() : 0) * 397)
                ^ (Resource != null ? Resource.GetHashCode() : 0);
        }

        internal static void Accumulate(
            Map map,
            CountAccumulator accumulator,
            PlannedWorkOptions options,
            QualityJobsPlannedWorkSnapshot qualityJobs)
        {
            if (map == null) return;
            QualityJobsMapWorkSnapshot? managed = options.QualityRework
                ? qualityJobs?.For(map)
                : null;
            if (options.ReserveBills)
                AccumulateBills(map, accumulator, managed);
            if (options.ReserveBuildables)
                AccumulateBuildables(map, accumulator, managed);
            managed?.Accumulate(
                accumulator, options.ReserveBills, options.ReserveBuildables);
        }

        internal static QualityJobsPlannedWorkSnapshot BuildQualityJobsSnapshot(
            QualityJobsBridge.ManagedJobsSnapshot source)
        {
            if (source.Bills.Length == 0 && source.Construction.Length == 0)
                return QualityJobsPlannedWorkSnapshot.Empty;

            var byMap = new Dictionary<Map, QualityMapBuilder>(
                IdentityComparer<Map>.Instance);
            var maps = new List<QualityMapBuilder>();

            for (int i = 0; i < source.Bills.Length; i++)
            {
                QualityJobsBridge.ManagedBillJob job = source.Bills[i];
                QualityMapBuilder map = BuilderFor(job.Map, byMap, maps);
                map.Bills.Add(job.Bill);
                ProjectManagedBill(job, map);
            }

            for (int i = 0; i < source.Construction.Length; i++)
            {
                QualityJobsBridge.ManagedConstructionJob job =
                    source.Construction[i];
                QualityMapBuilder map = BuilderFor(job.Map, byMap, maps);
                for (int target = 0; target < job.Targets.Length; target++)
                    map.Targets.Add(job.Targets[target]);
                ProjectManagedConstruction(job, map);
            }

            maps.Sort(compareQualityMaps);
            var projected = new QualityJobsMapWorkSnapshot[maps.Count];
            for (int i = 0; i < maps.Count; i++)
                projected[i] = maps[i].Build();
            return new QualityJobsPlannedWorkSnapshot(projected);
        }

        private static QualityMapBuilder BuilderFor(
            Map map,
            Dictionary<Map, QualityMapBuilder> byMap,
            List<QualityMapBuilder> ordered)
        {
            if (byMap.TryGetValue(map, out QualityMapBuilder builder))
                return builder;
            builder = new QualityMapBuilder(map);
            byMap.Add(map, builder);
            ordered.Add(builder);
            return builder;
        }

        private static void ProjectManagedBill(
            QualityJobsBridge.ManagedBillJob job,
            QualityMapBuilder map)
        {
            int queued = job.RemainingAcceptedIterations;
            List<IngredientCount> ingredients = job.Recipe.ingredients;
            if (queued <= 0 || ingredients == null || ingredients.Count == 0)
                return;

            float attempts = PlannedWorkMath.ExpectedAttempts(job.Probability);
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                ThingDef? resource = SoleAllowedDef(
                    ingredient, job.Recipe, job.Bill);
                if (resource == null) continue;
                int unitCost = ingredient.CountRequiredOfFor(
                    resource, job.Recipe, job.Bill);
                int drain = PlannedWorkMath.BillDebt(
                    unitCost, queued, attempts);
                if (drain <= 0) continue;
                map.Add(resource, new QualityJobsWorkEntry(
                    PlannedWorkKind.Bill,
                    job.Product.defName,
                    stuffDefName: null,
                    queued,
                    unitCost,
                    drain));
            }
        }

        private static void ProjectManagedConstruction(
            QualityJobsBridge.ManagedConstructionJob job,
            QualityMapBuilder map)
        {
            if (job.Targets.Length == 0) return;
            List<ThingDefCountClass> cost = job.BuildableDef.CostListAdjusted(
                job.Stuff, errorOnNullStuff: false);
            if (cost == null || cost.Count == 0) return;

            if (!map.CarriedBuilt)
            {
                map.Carried = CarriedToBuildables(map.Map);
                map.CarriedBuilt = true;
            }

            float attempts = PlannedWorkMath.ExpectedAttempts(job.Probability);
            float baseReturned = job.BuildableDef.resourcesFractionWhenDeconstructed;
            BuildingProperties? leavings = job.BuildableDef.building;
            for (int costIndex = 0; costIndex < cost.Count; costIndex++)
            {
                ThingDefCountClass item = cost[costIndex];
                ThingDef resource = item.thingDef;
                if (resource == null || !Counted(resource)) continue;
                float returned = PlannedWorkMath.ReturnedFraction(
                    baseReturned,
                    forced: Lists(leavings?.forcedCostLeavings, resource),
                    blacklisted: Lists(leavings?.leavingsBlacklist, resource));

                int drain = 0;
                for (int targetIndex = 0;
                     targetIndex < job.Targets.Length;
                     targetIndex++)
                {
                    Thing target = job.Targets[targetIndex];
                    int targetDebt;
                    if (target is IConstructible constructible)
                    {
                        int outstanding = constructible.ThingCountNeeded(resource);
                        int inTransit = TakeCarried(
                            map.Carried, target, resource, outstanding);
                        targetDebt = PlannedWorkMath.BuildableDebt(
                            outstanding - inTransit,
                            item.count,
                            attempts,
                            returned);
                    }
                    else if (target is Building)
                    {
                        targetDebt = PlannedWorkMath.FailedBuildableDebt(
                            item.count, attempts, returned);
                    }
                    else
                    {
                        continue;
                    }
                    drain = SaturatingAdd(drain, targetDebt);
                }

                if (drain <= 0) continue;
                map.Add(resource, new QualityJobsWorkEntry(
                    PlannedWorkKind.Buildable,
                    job.BuildableDef.defName,
                    job.Stuff?.defName,
                    queued: job.Targets.Length,
                    unitCost: item.count,
                    drain));
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (right <= 0) return left;
            return left >= int.MaxValue - right
                ? int.MaxValue : left + right;
        }

        private static int CompareResourceDefs(ThingDef left, ThingDef right)
            => string.Compare(left?.defName, right?.defName,
                System.StringComparison.Ordinal);

        private static int CompareQualityMaps(
            QualityMapBuilder left, QualityMapBuilder right)
            => left.Map.uniqueID.CompareTo(right.Map.uniqueID);

        private static int CompareBills(Bill_Production left, Bill_Production right)
            => string.Compare(left.GetUniqueLoadID(), right.GetUniqueLoadID(),
                System.StringComparison.Ordinal);

        private static int CompareTargets(Thing left, Thing right)
            => left.thingIDNumber.CompareTo(right.thingIDNumber);

        private static int CompareQualityWork(
            QualityJobsWorkEntry left, QualityJobsWorkEntry right)
        {
            int compare = left.Kind.CompareTo(right.Kind);
            if (compare != 0) return compare;
            compare = string.Compare(left.WorkDefName, right.WorkDefName,
                System.StringComparison.Ordinal);
            if (compare != 0) return compare;
            compare = string.Compare(left.StuffDefName, right.StuffDefName,
                System.StringComparison.Ordinal);
            if (compare != 0) return compare;
            compare = left.UnitCost.CompareTo(right.UnitCost);
            if (compare != 0) return compare;
            compare = left.Queued.CompareTo(right.Queued);
            return compare != 0 ? compare : left.Drain.CompareTo(right.Drain);
        }

        // ---- bills -------------------------------------------------------

        private static void AccumulateBills(
            Map map,
            CountAccumulator accumulator,
            QualityJobsMapWorkSnapshot? managed)
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
                    if (bills[b] is Bill_Production bill
                        && (managed == null || !managed.Contains(bill)))
                        AccumulateBill(bill, accumulator);
                }
            }
        }

        private static void AccumulateBill(
            Bill_Production bill, CountAccumulator accumulator)
        {
            // A suspended or satisfied-and-paused bill is not going to draw
            // anything; neither is one whose giver has gone.
            if (bill.suspended || bill.paused || bill.DeletedOrDereferenced) return;

            RecipeDef? recipe = bill.recipe;
            List<IngredientCount>? ingredients = recipe?.ingredients;
            if (recipe == null || ingredients == null || ingredients.Count == 0) return;

            int iterations = IterationsOf(bill, recipe);
            if (iterations <= 0) return;

            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                ThingDef? def = SoleAllowedDef(ingredient, recipe, bill);
                if (def == null) continue;
                int unitCost = ingredient.CountRequiredOfFor(def, recipe, bill);
                int debt = PlannedWorkMath.BillDebt(
                    unitCost, iterations, expectedAttempts: 1f);
                if (debt > 0)
                    accumulator.AddBillWork(def.defName, def.shortHash,
                        recipe.ProducedThingDef?.defName ?? recipe.defName,
                        iterations, unitCost, debt);
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
        private static ThingDef? SoleAllowedDef(
            IngredientCount ingredient, RecipeDef recipe, Bill bill)
        {
            ThingFilter? filter = ingredient.filter;
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

            ThingDef? sole = null;
            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ThingDef def = allDefs[i];
                if (!filter.Allows(def)) continue;
                if (!Allowed(def, recipe, bill)) continue;
                if (sole != null) return null;
                sole = def;
            }
            return sole != null && Counted(sole) ? sole : null;
        }

        private static bool Usable(ThingDef? def, RecipeDef recipe, Bill bill)
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
            Map map,
            CountAccumulator accumulator,
            QualityJobsMapWorkSnapshot? managed)
        {
            Dictionary<CarriedKey, int>? carried = CarriedToBuildables(map);
            AccumulateConstructibles(map, ThingRequestGroup.Blueprint,
                accumulator, managed, carried);
            AccumulateConstructibles(map, ThingRequestGroup.BuildingFrame,
                accumulator, managed, carried);
        }

        private static void AccumulateConstructibles(
            Map map, ThingRequestGroup group,
            CountAccumulator accumulator,
            QualityJobsMapWorkSnapshot? managed,
            Dictionary<CarriedKey, int>? carried)
        {
            Faction player = Faction.OfPlayer;
            List<Thing> things = map.listerThings.ThingsInGroup(group);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing.Faction != player) continue;
                if (thing.IsForbidden(player)) continue;
                if (!(thing is Blueprint_Build) && !(thing is Frame)) continue;
                if (!(thing is IConstructible constructible)) continue;
                if (managed != null && managed.Contains(thing)) continue;

                List<ThingDefCountClass> cost = constructible.TotalMaterialCost();
                if (cost == null || cost.Count == 0) continue;

                // A rebuild tears the finished building down first, so what it
                // hands back is governed by the BUILT thing's leavings rules,
                // not the blueprint's.
                var built = thing.def.entityDefToBuild as ThingDef;
                string? workDefName = thing.def.entityDefToBuild?.defName;
                if (workDefName == null) continue;
                string? stuffDefName = thing.Stuff?.defName;
                float baseReturned =
                    thing.def.entityDefToBuild?.resourcesFractionWhenDeconstructed ?? 0f;
                BuildingProperties? leavings = built?.building;

                for (int c = 0; c < cost.Count; c++)
                {
                    ThingDefCountClass item = cost[c];
                    ThingDef def = item.thingDef;
                    if (def == null || !Counted(def)) continue;
                    float returned = PlannedWorkMath.ReturnedFraction(
                        baseReturned,
                        forced: Lists(leavings?.forcedCostLeavings, def),
                        blacklisted: Lists(leavings?.leavingsBlacklist, def));
                    // ThingCountNeeded nets off what has reached a frame, but
                    // not material a pawn is currently carrying there. That
                    // stack has already left ResourceCounter, so net it here
                    // as well or it is subtracted once from stock and again as
                    // buildable debt until the pawn deposits it.
                    int outstanding = constructible.ThingCountNeeded(def);
                    int inTransit = TakeCarried(
                        carried, thing, def, outstanding);
                    int debt = PlannedWorkMath.BuildableDebt(
                        outstanding - inTransit, item.count,
                        expectedAttempts: 1f, returned);
                    if (debt > 0)
                        accumulator.AddBuildableWork(
                            def.defName, def.shortHash,
                            workDefName, stuffDefName,
                            queued: 1, unitCost: item.count, drain: debt);
                }
            }
        }

        /// Material that has actually been picked up for a player buildable.
        /// EnrouteManager reservations are deliberately insufficient: they are
        /// registered before pickup while ResourceCounter still includes the
        /// source stack. A carried stack is the exact transition where the
        /// stock basis stops counting it and the outstanding debt must follow.
        private static Dictionary<CarriedKey, int>? CarriedToBuildables(Map map)
        {
            Dictionary<CarriedKey, int>? carried = null;
            Faction player = Faction.OfPlayer;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                Thing? material = pawn.carryTracker?.CarriedThing;
                Job job = pawn.CurJob;
                if (material == null || material.stackCount <= 0
                    || job?.def != JobDefOf.HaulToContainer)
                    continue;

                if (!Counted(material.def)) continue;

                // Vanilla reserves targetC as the primary construction site.
                // When targetB is another site it receives only material in
                // excess of targetC's remaining need; anything still left can
                // continue through targetQueueB.
                Thing primary = job.GetTarget(TargetIndex.C).Thing;
                Thing current = job.GetTarget(TargetIndex.B).Thing;
                int primaryCapacity = CarriedCapacity(
                    carried, primary, material.def, player);
                int currentCapacity = ReferenceEquals(primary, current)
                    ? 0
                    : CarriedCapacity(carried, current, material.def, player);
                CappedMaterialCredit credit =
                    PlannedWorkMath.AllocateConstructionHaul(
                        material.stackCount,
                        primaryCapacity,
                        currentCapacity,
                        out int primaryCredit,
                        out int currentCredit);
                AddCarried(ref carried, primary, material.def, primaryCredit);
                AddCarried(ref carried, current, material.def, currentCredit);

                List<LocalTargetInfo> destinations = job.targetQueueB;
                if (destinations == null) continue;
                CreditQueuedByRoute(map, pawn, destinations,
                    primary, current, primaryCredit, material.def, player,
                    ref carried, ref credit);
            }
            return carried;
        }

        /// Vanilla chooses each later target by global reachable proximity
        /// from the site it just visited. Keep targetC in the route even though
        /// its material was logically reserved first: visiting it changes the
        /// origin used to choose the following destination.
        private static void CreditQueuedByRoute(
            Map map,
            Pawn pawn,
            List<LocalTargetInfo> queued,
            Thing primary,
            Thing current,
            int primaryCredit,
            ThingDef resource,
            Faction player,
            ref Dictionary<CarriedKey, int>? carried,
            ref CappedMaterialCredit credit)
        {
            if (credit.Remaining <= 0 || queued.Count == 0) return;

            var candidates = new List<Thing>(queued.Count);
            for (int i = 0; i < queued.Count; i++)
            {
                Thing destination = queued[i].Thing;
                if (destination != null) candidates.Add(destination);
            }

            IntVec3 origin = current != null && current.Spawned
                ? current.Position : pawn.Position;
            TraverseParms traverse = TraverseParms.For(pawn);
            while (credit.Remaining > 0 && candidates.Count > 0)
            {
                var closest = new ClosestPlannedDestination();
                for (int i = 0; i < candidates.Count; i++)
                {
                    Thing destination = candidates[i];
                    bool isPrimary = ReferenceEquals(destination, primary);
                    int capacity = isPrimary
                        ? primaryCredit
                        : CarriedCapacity(
                            carried, destination, resource, player);
                    bool eligible = capacity > 0
                        && destination.Spawned
                        && destination is IHaulEnroute enroute
                        && enroute.GetSpaceRemainingWithEnroute(
                            resource, pawn) > 0
                        && map.reachability.CanReach(
                            origin, destination.SpawnedParentOrMe,
                            PathEndMode.Touch, traverse);
                    float distance = eligible
                        ? (origin - destination.PositionHeld)
                            .LengthHorizontalSquared
                        : 0f;
                    closest.Consider(i, distance, eligible);
                }

                if (closest.Index < 0) break;
                Thing selected = candidates[closest.Index];
                if (ReferenceEquals(selected, primary))
                    primaryCredit = 0;
                else
                    CreditCarried(ref carried, selected, resource, player,
                        ref credit);

                origin = selected.Position;
                for (int i = candidates.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(candidates[i], selected))
                        candidates.RemoveAt(i);
            }
        }

        private static void CreditCarried(
            ref Dictionary<CarriedKey, int>? carried,
            Thing destination,
            ThingDef resource,
            Faction player,
            ref CappedMaterialCredit credit)
        {
            int taken = credit.Take(CarriedCapacity(
                carried, destination, resource, player));
            AddCarried(ref carried, destination, resource, taken);
        }

        private static int CarriedCapacity(
            Dictionary<CarriedKey, int>? carried,
            Thing? destination,
            ThingDef resource,
            Faction player)
        {
            if (!EligibleConstructible(destination, player)
                || !(destination is IConstructible constructible))
                return 0;

            var key = new CarriedKey(destination, resource);
            int credited = 0;
            if (carried != null) carried.TryGetValue(key, out credited);
            int capacity = constructible.ThingCountNeeded(resource) - credited;
            return capacity > 0 ? capacity : 0;
        }

        private static void AddCarried(
            ref Dictionary<CarriedKey, int>? carried,
            Thing? destination,
            ThingDef resource,
            int amount)
        {
            if (amount <= 0 || destination == null) return;

            if (carried == null)
                carried = new Dictionary<CarriedKey, int>();
            var key = new CarriedKey(destination, resource);
            carried.TryGetValue(key, out int credited);
            carried[key] = credited + amount;
        }

        /// Only player-owned, active construction draws on the colony's stock.
        /// The concrete-type check excludes Blueprint_Install: it also implements
        /// IConstructible, but its TotalMaterialCost logs an error by design.
        private static bool EligibleConstructible(Thing? thing, Faction player)
            => thing != null
               && thing.Faction == player
               && !thing.IsForbidden(player)
               && (thing is Blueprint_Build || thing is Frame)
               && thing is IConstructible;

        /// Consumes only material carried to this exact constructible. The
        /// destination matters now that the snapshot retains per-work rows:
        /// credit for one chair must not reduce another buildable's drain.
        private static int TakeCarried(
            Dictionary<CarriedKey, int>? carried,
            Thing destination,
            ThingDef def,
            int outstanding)
        {
            var key = new CarriedKey(destination, def);
            if (carried == null || outstanding <= 0
                || !carried.TryGetValue(key, out int available)
                || available <= 0)
                return 0;

            int taken = available < outstanding ? available : outstanding;
            if (taken == available) carried.Remove(key);
            else carried[key] = available - taken;
            return taken;
        }

        /// Debt is only worth publishing for defs the readout can display, which
        /// is exactly the set the count pass itself gathers.
        private static bool Counted(ThingDef def)
            => def.CountAsResource || GameResourceCatalog.IsExtraCountedDef(def);

        /// Membership in one of a building's short leavings lists; absent lists
        /// are the common case and cost nothing.
        private static bool Lists(List<ThingDef>? defs, ThingDef def)
        {
            if (defs == null) return false;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i] == def) return true;
            return false;
        }
    }
}
