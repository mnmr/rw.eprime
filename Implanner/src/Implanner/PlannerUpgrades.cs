using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// "Better implants to high-priority colonists" (owner, 2026-09-27;
    /// PlannerModel.UpgradeByPriority). Per colony and implant kind with a
    /// quality, QualityRebalance hands the better items to higher-priority
    /// colonists: reservations not yet scheduled swap items, and an
    /// installed implant is replaced. A replacement is a removal operation
    /// under GoalKeys.Upgrade plus an install record for the better item
    /// (PlannerReinstall), which is reserved at once so nothing else takes
    /// it; the removed implant drops and goes to the next colonist in line
    /// through ordinary allocation. An upgrade starts only for a healthy
    /// colonist at the colony with no other Implanner surgery, inside the
    /// concurrent-surgery cap, and never when the removal would take
    /// anything else with it. Runs inside the reconcile pass only, after
    /// allocation and before operations are scheduled.
    internal static class PlannerUpgrades
    {
        /// An installed implant that could be replaced now.
        private sealed class Target
        {
            internal Target(Pawn pawn, BodyPartRecord part, HediffDef def, int quality)
            {
                Pawn = pawn;
                Part = part;
                Def = def;
                Quality = quality;
            }

            internal Pawn Pawn { get; }
            internal BodyPartRecord Part { get; }
            internal HediffDef Def { get; }
            internal int Quality { get; }
        }

        private sealed class Kind
        {
            internal readonly List<QualityHolder> Reservations = new List<QualityHolder>();
            internal readonly List<QualityHolder> Installed = new List<QualityHolder>();
            internal readonly List<Target> Targets = new List<Target>();
            internal readonly List<SpareItem> Spare = new List<SpareItem>();
            internal int BestOffer = ImplantQuality.None;
        }

        internal static PlannerChange Reconcile(PlannerModel model, ReconcilePass pass)
        {
            if (!model.UpgradeByPriority) return PlannerChange.None;
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;
            Dictionary<string, int> planned = PlannerSurgery.PlannedByColony(model, index);
            Dictionary<ThingDef, int> playerReserves = PlannerSurgery.ImplantItemReserves(model);
            var reservationIds = new List<int>(model.Reservations.Keys);
            reservationIds.Sort();

            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                var kinds = new Dictionary<ThingDef, Kind>();
                var order = new List<ThingDef>();

                // Reservations for plan slots at this colony, oldest item
                // first; reinstall keys are no plan slots and stay out.
                for (int i = 0; i < reservationIds.Count; i++)
                {
                    int itemId = reservationIds[i];
                    model.TryGetReservation(itemId, out ItemReservation reservation);
                    int pawnId = reservation.PawnId;
                    if (index.ColonyOfPawn(pawnId) != colony
                        || !index.ItemsById.TryGetValue(itemId, out Thing thing)
                        || !index.SameColony(pawnId, itemId)
                        || !ImplantQualities.HasQuality(thing.def))
                        continue;
                    PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                    if (evaluation == null || !GoalKeys.TryResolveImplantSlot(evaluation.Goals,
                            reservation.GoalKey, out ImplantGoal goal, out int ordinal))
                        continue;
                    ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
                    Pawn pawn = index.PawnsById[pawnId];
                    BodyPartRecord? part = entry != null
                        ? PawnProjection.ResolveSlotPart(pawn, entry, ordinal)
                        : null;
                    if (part == null) continue;
                    int minimum = PawnProjection.MinimumAcceptableQuality(
                        pawn, entry!, part, evaluation.Plan.MinQuality);
                    if (minimum == ImplantQuality.None) continue;
                    Kind kind = KindOf(kinds, order, colony, index, thing.def,
                        playerReserves, model);
                    bool movable = model.OwnedBill(pawnId, reservation.GoalKey) == null;
                    int quality = ImplantQualities.QualityOf(thing);
                    kind.Reservations.Add(new QualityHolder(model.PriorityOf(pawnId), pawnId,
                        reservation.GoalKey, quality, itemId, minimum, movable));
                    if (movable && quality > kind.BestOffer) kind.BestOffer = quality;
                }

                // Installed implants of colonists who could be operated on
                // now, only where something better is on offer.
                planned.TryGetValue(colony.LocationId, out int busy);
                int slots = model.SurgeryConcurrency - busy;
                for (int p = 0; slots > 0 && p < colony.PawnIds.Count; p++)
                {
                    int pawnId = colony.PawnIds[p];
                    IReadOnlyDictionary<string, string>? owned = model.OwnedBillsFor(pawnId);
                    if ((owned != null && owned.Count > 0) || model.ReinstallsFor(pawnId) != null)
                        continue;
                    PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                    Pawn pawn = index.PawnsById[pawnId];
                    if (evaluation == null || !PlannerSurgery.Healthy(pawn)) continue;
                    IReadOnlyList<ImplantGoal> goals = evaluation.Goals;
                    for (int g = 0; g < goals.Count; g++)
                    {
                        ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goals[g].ImplantDefName);
                        ThingDef? item = entry?.Def.spawnThingOnRemoved;
                        if (entry == null || !ImplantQualities.HasQuality(item)) continue;
                        Kind kind = KindOf(kinds, order, colony, index, item!,
                            playerReserves, model);
                        IReadOnlyList<int> ordinals = goals[g].SlotOrdinals;
                        for (int o = 0; o < ordinals.Count; o++)
                        {
                            BodyPartRecord? part =
                                PawnProjection.ResolveSlotPart(pawn, entry, ordinals[o]);
                            Hediff? hediff = part != null ? HediffOn(pawn, entry.Def, part) : null;
                            if (hediff == null) continue;
                            int quality = ImplantQualities.InstalledQuality(hediff);
                            if (quality < 0 || quality >= kind.BestOffer
                                || RemovalFor(pawn, entry, hediff, part!) == null)
                                continue;
                            kind.Installed.Add(new QualityHolder(model.PriorityOf(pawnId),
                                pawnId, GoalKeys.ImplantSlot(goals[g], ordinals[o]),
                                quality, -1, 0, available: true));
                            kind.Targets.Add(new Target(pawn, part!, entry.Def, quality));
                        }
                    }
                }

                var upgraded = new HashSet<int>();
                for (int k = 0; k < order.Count; k++)
                    change |= Apply(model, pass, colony, kinds[order[k]], upgraded, ref slots);
            }
            return change;
        }

        /// Plans one kind and applies the result: freed reservations first,
        /// then the new ones and the upgrades.
        private static PlannerChange Apply(PlannerModel model, ReconcilePass pass,
            Colony colony, Kind kind, HashSet<int> upgraded, ref int slots)
        {
            var holders = new List<QualityHolder>(kind.Reservations);
            var targets = new List<Target?>(new Target?[kind.Reservations.Count]);
            for (int i = 0; i < kind.Installed.Count; i++)
            {
                if (upgraded.Contains(kind.Installed[i].PawnId)) continue;
                holders.Add(kind.Installed[i]);
                targets.Add(kind.Targets[i]);
            }
            if (holders.Count == 0) return PlannerChange.None;
            List<QualityMove> moves = QualityRebalance.Plan(holders, kind.Spare,
                slots > 0 ? slots : 0);
            var change = PlannerChange.None;
            for (int i = 0; i < moves.Count; i++)
            {
                QualityHolder holder = holders[moves[i].Holder];
                if (!holder.Installed) change |= model.ReleaseReservation(holder.ItemId);
            }
            for (int i = 0; i < moves.Count; i++)
            {
                QualityHolder holder = holders[moves[i].Holder];
                int itemId = moves[i].ItemId;
                if (!holder.Installed)
                {
                    if (itemId >= 0) change |= model.Reserve(itemId, holder.PawnId, holder.Key);
                    continue;
                }
                Target target = targets[moves[i].Holder]!;
                change |= StartUpgrade(model, pass, colony, holder.PawnId, target, itemId);
                upgraded.Add(holder.PawnId);
                slots--;
            }
            return change;
        }

        /// The removal operation, the install record for the better item,
        /// and its reservation.
        private static PlannerChange StartUpgrade(PlannerModel model, ReconcilePass pass,
            Colony colony, int pawnId, Target target, int itemId)
        {
            Pawn pawn = target.Pawn;
            int partIndex = pawn.RaceProps.body.GetIndexOfPart(target.Part);
            string removalKey = GoalKeys.Upgrade(target.Def.defName, partIndex);
            int quality = ImplantQualities.QualityOf(pass.Index.ItemsById[itemId]);
            var record = new ReinstallRecord(removalKey, target.Def.defName, partIndex,
                quality, active: false);
            Hediff hediff = HediffOn(pawn, target.Def, target.Part)!;
            RecipeDef recipe = RemovalFor(pawn, Catalogs.ImplantByDefName(target.Def.defName)!,
                hediff, target.Part)!;
            int floor = PlannerSurgery.FloorFor(model, pass.Index, colony, pawn);
            var change = model.SetOwnedBill(pawnId, removalKey, pass.BillId(
                PlannerSurgery.CreateOperation(pawn, recipe, target.Part, floor)));
            change |= model.SetPendingReinstalls(pawnId, removalKey, new[] { record });
            return change | model.Reserve(itemId, pawnId, record.Key);
        }

        private static Kind KindOf(Dictionary<ThingDef, Kind> kinds, List<ThingDef> order,
            Colony colony, ColonyIndex index, ThingDef def,
            Dictionary<ThingDef, int> playerReserves, PlannerModel model)
        {
            if (kinds.TryGetValue(def, out Kind kind)) return kind;
            kind = new Kind();
            kinds.Add(def, kind);
            order.Add(def);
            // Free items, less the worst ones the player holds back.
            List<int>? ids = colony.ItemIdsOf(def);
            if (ids != null)
                for (int i = 0; i < ids.Count; i++)
                {
                    if (model.Reservations.ContainsKey(ids[i])) continue;
                    Thing thing = index.ItemsById[ids[i]];
                    if (thing.IsForbidden(Faction.OfPlayer)) continue;
                    kind.Spare.Add(new SpareItem(ids[i], ImplantQualities.QualityOf(thing)));
                }
            if (playerReserves.TryGetValue(def, out int held))
                for (int n = 0; n < held && kind.Spare.Count > 0; n++)
                {
                    int worst = 0;
                    for (int i = 1; i < kind.Spare.Count; i++)
                        if (kind.Spare[i].Quality < kind.Spare[worst].Quality) worst = i;
                    kind.Spare.RemoveAt(worst);
                }
            for (int i = 0; i < kind.Spare.Count; i++)
                if (kind.Spare[i].Quality > kind.BestOffer) kind.BestOffer = kind.Spare[i].Quality;
            return kind;
        }

        /// The surgery taking the installed implant out with its item, or
        /// null when it cannot be done cleanly: an artificial part comes off
        /// with the game's own part removal (the part is then missing and the
        /// better one goes on), an implant with its removal surgery, which
        /// takes the pawn's first instance of the kind, so only a single one
        /// qualifies. Nothing else may sit on the part or below when the
        /// removal or the reinstall clears the part, and the better item must
        /// be installable there afterwards. In-place upgrades stay out.
        internal static RecipeDef? RemovalFor(Pawn pawn, ImplantCatalogEntry entry,
            Hediff hediff, BodyPartRecord part)
        {
            if (entry.UpgradesFrom != null
                || PlannerReinstall.RecipeFor(pawn, entry.Def, part, checkWorker: false) == null)
                return null;
            bool addedPart = hediff is Hediff_AddedPart;
            if ((addedPart || entry.WipesPart) && AnythingElseOn(pawn, hediff, part))
                return null;
            if (addedPart)
            {
                RecipeDef remove = RecipeDefOf.RemoveBodyPart;
                return remove.AvailableNow && remove.AvailableOnNow(pawn, part)
                    && PlannerSurgery.AppliesTo(remove, pawn, part)
                    ? remove
                    : null;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            int instances = 0;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i].def == entry.Def) instances++;
            if (instances != 1) return null;
            RecipeDef? best = null;
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe.removesHediff != entry.Def || !recipe.IsSurgery
                    || !recipe.AvailableNow || !recipe.AvailableOnNow(pawn, part)
                    || !PlannerSurgery.AppliesTo(recipe, pawn, part))
                    continue;
                if (best == null || string.CompareOrdinal(recipe.defName, best.defName) < 0)
                    best = recipe;
            }
            return best;
        }

        private static bool AnythingElseOn(Pawn pawn, Hediff hediff, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff other = hediffs[i];
                if (other != hediff && other.Part != null && !other.def.isBad
                    && PlannerSurgery.IsSameOrAncestor(part, other.Part))
                    return true;
            }
            return false;
        }

        private static Hediff? HediffOn(Pawn pawn, HediffDef def, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i].def == def && hediffs[i].Part == part)
                    return hediffs[i];
            return null;
        }
    }
}
