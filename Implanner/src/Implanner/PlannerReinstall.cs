using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Implants a part-wiping surgery pushes out go straight back in (owner,
    /// 2026-09-26). Vanilla Genetics Expanded's install worker restores the
    /// whole part before adding its implant, so neuron reinforcement drops
    /// every brain implant already there next to the doctor. Automation
    /// installs a wiper before same-part implants where it can
    /// (PlannerSurgery.HeldFlags); when the pawn already carries implants
    /// there, the wiper's operation records them (PushedOut, via
    /// PlannerModel.SetPendingReinstalls), the records wake once the wiper
    /// is installed, and each dropped item is reserved back for the same
    /// pawn (the same quality first) and installed again, planned or not,
    /// outside the batch and concurrency gates. A wiper that would push out
    /// something no surgery can put back is never scheduled
    /// (PlannerSurgery.WouldDestroy). Runs inside the reconcile pass only,
    /// from authoritative synchronized state.
    internal static class PlannerReinstall
    {
        internal static PlannerChange Reconcile(PlannerModel model, ReconcilePass pass)
        {
            if (model.Reinstalls.Count == 0) return PlannerChange.None;
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            var reservedItems = new HashSet<int>(model.Reservations.Keys);
            var pawnIds = new List<int>(model.Reinstalls.Keys);
            pawnIds.Sort();
            for (int p = 0; p < pawnIds.Count; p++)
            {
                int pawnId = pawnIds[p];
                if (!index.PawnsById.TryGetValue(pawnId, out Pawn pawn)) continue;

                // Pending records: the wiper went in (wake them), or its
                // operation is gone without having run (drop them).
                IReadOnlyList<ReinstallRecord>? records = model.ReinstallsFor(pawnId);
                var wipers = new List<string>();
                for (int i = 0; records != null && i < records.Count; i++)
                    if (!records[i].Active && !wipers.Contains(records[i].WiperKey))
                        wipers.Add(records[i].WiperKey);
                wipers.Sort(StringComparer.Ordinal);
                for (int w = 0; w < wipers.Count; w++)
                {
                    if (GoalKeys.IsUpgrade(wipers[w]))
                        change |= ReconcileRemoval(model, pass, pawn, pawnId,
                            wipers[w], records!);
                    else if (WiperInstalled(pawn, wipers[w]))
                        change |= model.ActivateReinstalls(pawnId, wipers[w]);
                    else if (model.OwnedBill(pawnId, wipers[w]) == null)
                        change |= model.DropReinstalls(pawnId, wipers[w]);
                }

                records = model.ReinstallsFor(pawnId);
                if (records == null) continue;
                var active = new List<ReinstallRecord>();
                for (int i = 0; i < records.Count; i++)
                    if (records[i].Active) active.Add(records[i]);
                Colony? colony = index.ColonyOfPawn(pawnId);
                for (int i = 0; i < active.Count; i++)
                    change |= Pursue(model, pass, pawn, pawnId, colony,
                        active[i], reservedItems);
            }
            return change;
        }

        /// The removal half of a quality upgrade (PlannerUpgrades): once the
        /// old implant is off its part the install record wakes and the
        /// removal's bill record goes; a removal that vanished without
        /// running (failed, or cancelled by the player) drops the upgrade
        /// and frees its item.
        private static PlannerChange ReconcileRemoval(PlannerModel model,
            ReconcilePass pass, Pawn pawn, int pawnId, string removalKey,
            IReadOnlyList<ReinstallRecord> records)
        {
            ReinstallRecord record = default;
            for (int i = 0; i < records.Count; i++)
                if (!records[i].Active && records[i].WiperKey == removalKey)
                    record = records[i];
            HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(record.ImplantDefName);
            BodyPartRecord? part = PartAt(pawn, record.PartIndex);
            string? billId = model.OwnedBill(pawnId, removalKey);
            Bill? bill = billId != null ? pass.FindBill(pawn.BillStack, billId) : null;
            var change = PlannerChange.None;
            if (def != null && part != null && !HasHediffOn(pawn, def, part))
            {
                if (bill != null) pawn.BillStack.Delete(bill);
                change |= model.RemoveOwnedBill(pawnId, removalKey);
                return change | model.ActivateReinstalls(pawnId, removalKey);
            }
            if (bill != null && def != null && part != null) return change;
            if (bill != null) pawn.BillStack.Delete(bill);
            change |= model.RemoveOwnedBill(pawnId, removalKey);
            int itemId = ReservedItem(model, pawnId, record.Key);
            if (itemId >= 0) change |= model.ReleaseReservation(itemId);
            return change | model.DropReinstalls(pawnId, removalKey);
        }

        /// One active record: done once the implant is back, otherwise an
        /// item reserved and an operation scheduled while the pawn is here.
        private static PlannerChange Pursue(PlannerModel model, ReconcilePass pass,
            Pawn pawn, int pawnId, Colony? colony, ReinstallRecord record,
            HashSet<int> reservedItems)
        {
            string key = record.Key;
            HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(record.ImplantDefName);
            BodyPartRecord? part = PartAt(pawn, record.PartIndex);
            ThingDef? item = def?.spawnThingOnRemoved;
            if (def == null || part == null || item == null
                || HasHediffOn(pawn, def, part))
                return Finish(model, pass, pawn, pawnId, key);
            if (colony == null) return PlannerChange.None; // away: keep waiting
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            int itemId = ReservedItem(model, pawnId, key);
            if (itemId < 0)
            {
                Thing? pick = PickItem(colony, index, item, record.Quality, reservedItems);
                if (pick == null)
                {
                    // Nothing of the kind left at the colony at all (sold,
                    // destroyed): there is nothing to put back.
                    return colony.ItemIdsOf(item) == null && !AnyCarried(colony, item)
                        ? change | Finish(model, pass, pawn, pawnId, key)
                        : change;
                }
                itemId = pick.thingIDNumber;
                reservedItems.Add(itemId);
                change |= model.Reserve(itemId, pawnId, key);
            }

            if (!index.ItemsById.ContainsKey(itemId) || !index.SameColony(pawnId, itemId)
                || !PlannerSurgery.Healthy(pawn))
                return change;
            RecipeDef? recipe = RecipeFor(pawn, def, part, checkWorker: true);
            if (recipe == null) return change;
            int floor = PlannerSurgery.FloorFor(model, index, colony, pawn);

            string? recordedId = model.OwnedBill(pawnId, key);
            Bill? recorded = recordedId != null ? pass.FindBill(pawn.BillStack, recordedId) : null;
            if (recorded is Bill_Medical mine && mine.recipe == recipe && mine.Part == part)
            {
                if (mine.allowedSkillRange.min != floor) mine.allowedSkillRange.min = floor;
                return change;
            }
            if (recordedId != null)
            {
                if (recorded != null) pawn.BillStack.Delete(recorded);
                change |= model.RemoveOwnedBill(pawnId, key);
            }
            BillStack bills = pawn.BillStack;
            for (int b = 0; b < bills.Count; b++)
                if (bills[b] is Bill_Medical existing
                    && existing.recipe == recipe && existing.Part == part)
                    return change;
            return change | model.SetOwnedBill(pawnId, key,
                pass.BillId(PlannerSurgery.CreateOperation(pawn, recipe, part, floor)));
        }

        /// The record is done: its operation (if still queued) and record go.
        private static PlannerChange Finish(PlannerModel model, ReconcilePass pass,
            Pawn pawn, int pawnId, string key)
        {
            var change = PlannerChange.None;
            string? billId = model.OwnedBill(pawnId, key);
            if (billId != null)
            {
                Bill? bill = pass.FindBill(pawn.BillStack, billId);
                if (bill != null) pawn.BillStack.Delete(bill);
                change |= model.RemoveOwnedBill(pawnId, key);
            }
            return change | model.RemoveReinstall(pawnId, key);
        }

        /// What installing the wiper at the part will push out: every
        /// implant on the part or below it that drops an item (harmful
        /// conditions and restoration-proof hediffs stay out of it), in the
        /// pawn's hediff order. The wiper key is filled in by the model.
        internal static List<ReinstallRecord> PushedOut(Pawn pawn,
            ImplantCatalogEntry wiper, BodyPartRecord part)
        {
            var result = new List<ReinstallRecord>();
            BodyDef body = pawn.RaceProps.body;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                HediffDef def = hediff.def;
                if (hediff.Part == null || def == wiper.Def || def.isBad
                    || def.keepOnBodyPartRestoration || def.spawnThingOnRemoved == null
                    || !PlannerSurgery.IsSameOrAncestor(part, hediff.Part))
                    continue;
                result.Add(new ReinstallRecord("", def.defName,
                    body.GetIndexOfPart(hediff.Part),
                    ImplantQualities.InstalledQuality(hediff), active: false));
            }
            return result;
        }

        /// The deterministic surgery installing the hediff at the part:
        /// lowest defName among the surgeries adding it there that are
        /// researched, and (checkWorker) that the game offers on this pawn's
        /// part right now. Catalog kinds use their recipe list; others are
        /// looked up once per call, which only happens for the rare
        /// pushed-out implant.
        internal static RecipeDef? RecipeFor(Pawn pawn, HediffDef def,
            BodyPartRecord part, bool checkWorker)
        {
            List<RecipeDef> candidates = Catalogs.ImplantByDefName(def.defName)?.SurgeryRecipes
                ?? SurgeriesAdding(def);
            for (int i = 0; i < candidates.Count; i++)
            {
                RecipeDef recipe = candidates[i];
                if (!recipe.appliedOnFixedBodyParts.NullOrEmpty()
                    && !recipe.appliedOnFixedBodyParts.Contains(part.def))
                    continue;
                if (!recipe.AvailableNow) continue;
                if (checkWorker && (!recipe.AvailableOnNow(pawn, part)
                        || !PlannerSurgery.AppliesTo(recipe, pawn, part)))
                    continue;
                return recipe;
            }
            return null;
        }

        private static List<RecipeDef> SurgeriesAdding(HediffDef def)
        {
            var result = new List<RecipeDef>();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i].addsHediff == def && recipes[i].IsSurgery)
                    result.Add(recipes[i]);
            result.Sort(static (a, b) => string.CompareOrdinal(a.defName, b.defName));
            return result;
        }

        /// Whether the wiper named by the goal key sits on its part now.
        private static bool WiperInstalled(Pawn pawn, string wiperKey)
        {
            if (!GoalKeys.TryParseImplantSlot(wiperKey, out _, out string defName,
                    out int ordinal))
                return false;
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            if (entry == null) return false;
            BodyPartRecord? part = PawnProjection.ResolveSlotPart(pawn, entry, ordinal);
            return part != null && HasHediffOn(pawn, entry.Def, part);
        }

        private static bool HasHediffOn(Pawn pawn, HediffDef def, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i].def == def && hediffs[i].Part == part)
                    return true;
            return false;
        }

        private static BodyPartRecord? PartAt(Pawn pawn, int index)
        {
            List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
            return index >= 0 && index < parts.Count ? parts[index] : null;
        }

        private static int ReservedItem(PlannerModel model, int pawnId, string key)
        {
            foreach (KeyValuePair<int, ItemReservation> pair in model.Reservations)
                if (pair.Value.PawnId == pawnId
                    && string.Equals(pair.Value.GoalKey, key, StringComparison.Ordinal))
                    return pair.Key;
            return -1;
        }

        /// A free, unforbidden item of the kind at the colony: the pushed-out
        /// quality first, else the best one, oldest among equals.
        private static Thing? PickItem(Colony colony, ColonyIndex index, ThingDef def,
            int quality, HashSet<int> reservedItems)
        {
            List<int>? ids = colony.ItemIdsOf(def);
            if (ids == null) return null;
            Thing? best = null;
            int bestQuality = -1;
            for (int i = 0; i < ids.Count; i++)
            {
                if (reservedItems.Contains(ids[i])) continue;
                Thing thing = index.ItemsById[ids[i]];
                if (thing.IsForbidden(Faction.OfPlayer)) continue;
                int q = ImplantQualities.QualityOf(thing);
                if (q == quality) return thing;
                if (q > bestQuality)
                {
                    best = thing;
                    bestQuality = q;
                }
            }
            return best;
        }

        private static bool AnyCarried(Colony colony, ThingDef def)
        {
            for (int m = 0; m < colony.Maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = colony.Maps[m].mapPawns.AllPawnsSpawned;
                for (int p = 0; p < pawns.Count; p++)
                    if (pawns[p].carryTracker?.CarriedThing?.def == def)
                        return true;
            }
            return false;
        }
    }
}
