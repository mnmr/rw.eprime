using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace Implanner
{
    /// Shared acceptance at allocation/reconciliation and the irreversible
    /// surgery boundary. Execution reads the actual placed implant, not the
    /// reservation (a doctor may still be carrying an earlier selection).
    internal static class ImplantSurgerySafety
    {
        internal static bool Accepts(Pawn pawn, HediffDef def, BodyPartRecord part,
            Thing item, int planMinimum, float retainedFloor = 0f)
        {
            if (item.Destroyed || item.def != def.spawnThingOnRemoved) return false;
            return AcceptsQuality(pawn, def, part, ImplantQualities.QualityOf(item),
                planMinimum, retainedFloor);
        }

        private static bool AcceptsQuality(Pawn pawn, HediffDef def, BodyPartRecord part,
            int quality, int planMinimum, float retainedFloor)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(def.defName);
            bool replacement = entry?.IsReplacement
                ?? typeof(Hediff_AddedPart).IsAssignableFrom(def.hediffClass);
            float floor = replacement || entry?.WipesPart == true
                ? RecoverablePartEfficiency.Of(pawn, part) : 1f;
            int minimum = ImplantQualities.HasQuality(def.spawnThingOnRemoved) ? planMinimum : 0;
            int accepted = ImplantQuality.AcceptableQualities(
                ImplantQualities.AfterInstall(def, replacement), Math.Max(floor, retainedFloor), minimum);
            return ImplantQuality.Accepts(accepted, quality);
        }

        internal static bool CanOperate(Pawn doctor, Job job)
        {
            if (!(job.bill is Bill_Medical bill) || !SurgeryBindings.Any) return true;
            PlannerModel? model = ImplannerStore.Current?.Model;
            Pawn pawn = bill.GiverPawn;
            if (model == null) return true;
            string? key = OwnedKey(model, pawn, bill);
            if (key == null) return true; // Player operations remain the player's choice.
            BodyPartRecord part = bill.Part;
            if (part == null) return false;
            if (GoalKeys.IsUpgrade(key))
            {
                var records = model.ReinstallsFor(pawn.thingIDNumber);
                for (int i = 0; records != null && i < records.Count; i++)
                    if (!records[i].Active && records[i].WiperKey == key)
                        return CanRemoveForUpgrade(model, pawn, records[i]);
                return false;
            }
            HediffDef? added = bill.recipe.addsHediff;
            if (added == null) return true;
            float retainedFloor = 0f;
            int minimum = model.AssignedPlan(pawn.thingIDNumber)?.MinQuality ?? 0;
            var reinstalls = model.ReinstallsFor(pawn.thingIDNumber);
            for (int i = 0; reinstalls != null && i < reinstalls.Count; i++)
                if (reinstalls[i].Key == key)
                {
                    retainedFloor = reinstalls[i].MinimumEfficiency;
                    minimum = 0; // Return displaced implants whether planned or not.
                }

            // The actual recipe may be the base-install step of a planned
            // in-place upgrade. Always judge what this operation installs.
            var placed = job.placedThings;
            for (int i = 0; placed != null && i < placed.Count; i++)
                if (placed[i].Count > 0 && placed[i].thing.def == added.spawnThingOnRemoved)
                    return Accepts(pawn, added, part, placed[i].thing, minimum, retainedFloor);

            // An in-place upgrade creates a fresh hediff. Its worker does
            // not necessarily transfer the old implant's quality.
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(added.defName);
            if (entry?.UpgradesFrom == null && added.spawnThingOnRemoved != null) return false;
            return AcceptsWithoutItem(pawn, added, part, retainedFloor);
        }

        internal static bool AcceptsWithoutItem(Pawn pawn, HediffDef added, BodyPartRecord part,
            float retainedFloor = 0f)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(added.defName);
            bool replacement = entry?.IsReplacement ?? typeof(Hediff_AddedPart).IsAssignableFrom(added.hediffClass);
            float floor = replacement || entry?.WipesPart == true ? RecoverablePartEfficiency.Of(pawn, part) : 1f;
            return ImplantQuality.AtLeast(ImplantQualities.AfterInstallWithoutItem(added, replacement),
                Math.Max(floor, retainedFloor));
        }

        internal static bool CanRemoveForUpgrade(PlannerModel model, Pawn pawn, ReinstallRecord record)
        {
            if (record.PartIndex < 0 || record.PartIndex >= pawn.RaceProps.body.AllParts.Count) return false;
            BodyPartRecord part = pawn.RaceProps.body.AllParts[record.PartIndex];
            HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(record.ImplantDefName);
            if (def?.spawnThingOnRemoved == null || pawn.MapHeld == null) return false;
            if (!pawn.health.hediffSet.HasHediff(def, part)) return false;
            if (typeof(Hediff_AddedPart).IsAssignableFrom(def.hediffClass)
                && !PlannerUpgrades.SafeToRemovePart(pawn, part)) return false;
            // Reconciliation must record a raised floor before removal.
            // The patch cannot mutate the authoritative model to capture it.
            if (!ImplantQuality.AtLeast(record.MinimumEfficiency, RecoverablePartEfficiency.Of(pawn, part)))
                return false;
            int id = -1;
            foreach (var reservation in model.Reservations)
                if (reservation.Value.PawnId == pawn.thingIDNumber && reservation.Value.GoalKey == record.Key)
                { id = reservation.Key; break; }
            if (id < 0) return false;
            Thing? item = FindItem(pawn.MapHeld, def.spawnThingOnRemoved, id);
            return item != null && !item.IsForbidden(Faction.OfPlayer)
                && Accepts(pawn, def, part, item, 0, record.MinimumEfficiency);
        }

        private static Thing? FindItem(Map map, ThingDef def, int id)
        {
            var items = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < items.Count; i++) if (items[i].thingIDNumber == id) return items[i];
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Thing? carried = pawns[i].carryTracker?.CarriedThing;
                if (carried?.thingIDNumber == id) return carried;
            }
            return null;
        }

        private static string? OwnedKey(PlannerModel model, Pawn pawn, Bill bill)
        {
            var owned = model.OwnedBillsFor(pawn.thingIDNumber);
            if (owned == null) return null;
            string id = bill.GetUniqueLoadID();
            foreach (var pair in owned) if (pair.Value == id) return pair.Key;
            return null;
        }
    }
}
