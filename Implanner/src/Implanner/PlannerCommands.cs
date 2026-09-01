using System.Collections.Generic;
using Multiplayer.API;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// The only mutation path for shared Implanner state. Every method is a
    /// synced command: resolve store, normalize, let the Core model decide
    /// the exact change, bump only the reported domains. No-op mutations
    /// advance no revision.
    public static class PlannerCommands
    {
        /// basePlanId 0 creates a standalone plan; otherwise the new plan
        /// extends the given plan (its goals are inherited until overridden).
        [SyncMethod]
        public static void CreatePlan(string name, int basePlanId)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            if (store.Model.CreatePlan(name, store.TakePlanId, basePlanId) != null)
                store.Bump(PlannerChange.Plans);
        }

        [SyncMethod]
        public static void RenamePlan(int planId, string name)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.RenamePlan(planId, name));
        }

        [SyncMethod]
        public static void DeletePlan(int planId)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.DeletePlan(planId));
        }

        [SyncMethod]
        public static void RemoveImplant(int planId, string implantDefName)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.RemoveImplant(planId, implantDefName));
        }

        [SyncMethod]
        public static void SetImplantSlot(int planId, string implantDefName, int slotOrdinal, bool wanted)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetImplantSlot(
                planId, implantDefName, slotOrdinal, wanted));
        }

        /// Ranks an implant kind (stars 1–5). Rankings are the player's
        /// manual preference order; three stars is the default tier.
        [SyncMethod]
        public static void SetImplantStars(string implantDefName, int stars)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetImplantStars(implantDefName, stars));
        }

        /// Drops an implant kind into a star tier at an exact position:
        /// before the anchor kind, or at the tier's end when the anchor is
        /// empty. The target tier's complete sequence is materialized here
        /// from the catalog (membership by stars, ordered by existing
        /// position then defName — language-independent), so every client
        /// applies the identical order.
        [SyncMethod]
        public static void MoveImplantRank(
            string implantDefName, int stars, string beforeDefName)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null || implantDefName.NullOrEmpty()) return;
            Core.PlannerModel model = store.Model;

            var tier = new List<string>();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
            {
                string defName = catalog[i].Def.defName;
                if (string.Equals(defName, implantDefName, System.StringComparison.Ordinal))
                    continue;
                if (model.ImplantStarsOf(defName) == stars)
                    tier.Add(defName);
            }
            tier.Sort((a, b) =>
            {
                int order = model.ImplantOrderOf(a)
                    .CompareTo(model.ImplantOrderOf(b));
                return order != 0 ? order : string.CompareOrdinal(a, b);
            });
            int index = beforeDefName.NullOrEmpty()
                ? tier.Count
                : tier.IndexOf(beforeDefName);
            if (index < 0) index = tier.Count;
            tier.Insert(index, implantDefName);
            store.Bump(model.ApplyTierOrder(stars, tier));
        }

        [SyncMethod]
        public static void SetIteration(int iteration)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetIteration((IterationStrategy)iteration));
        }

        [SyncMethod]
        public static void SetManualDoctorFloor(int level)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetManualDoctorFloor(level));
        }

        /// Switching the automatic floor off seeds the manual minimum from
        /// the best currently eligible doctor, so the visible value starts at
        /// what automation was enforcing. Derived inside the synced command
        /// from authoritative state, so every client seeds identically.
        /// Sets how many of an implant's items automation must leave in
        /// stock for manual use; 0 removes the entry.
        [SyncMethod]
        public static void SetImplantReserve(string implantDefName, int count)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetImplantReserve(implantDefName, count));
        }

        [SyncMethod]
        public static void SetAutoDoctorFloor(bool enabled)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            PlannerChange change = PlannerChange.None;
            if (!enabled && store.Model.AutoDoctorFloor)
                change |= store.Model.SetManualDoctorFloor(BestDoctorSkill());
            change |= store.Model.SetAutoDoctorFloor(enabled);
            store.Bump(change);
        }

        /// The best Medical skill among doctors at any serviceable location.
        /// Synced-command path: authoritative faction, shared eligibility
        /// rule.
        private static int BestDoctorSkill()
        {
            int best = 0;
            List<Pawn> colonists = ColonyScope.AllPlanableColonists(
                ColonyScope.AuthoritativeFaction);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (!ColonyScope.PlaceOf(pawn).IsServiceable) continue;
                int skill = PlannerSurgery.EligibleDoctorSkill(pawn);
                if (skill > best) best = skill;
            }
            return best;
        }

        [SyncMethod]
        public static void SetAutomationPaused(bool paused)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetAutomationPaused(paused));
        }

        /// Hands queued automation work back to the player when the master
        /// switch turns off (issued by the cleanup dialog's OK, right after
        /// the pause command): deletes the
        /// listed Implanner-owned bills from the game, drops their records,
        /// and releases every item reservation. Unlisted bills keep their
        /// bill objects AND records, so re-enabling automation resumes
        /// managing them without duplicating operations. billIds is a
        /// newline-joined list of bill load ids — a plain string keeps the
        /// sync payload trivially serialization-safe.
        [SyncMethod]
        public static void CleanupAutomation(string billIds)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            PlannerModel model = store.Model;
            var remove = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string id in billIds.Split('\n'))
                if (id.Length > 0)
                    remove.Add(id);
            var change = PlannerChange.None;

            // Reservations release unconditionally: with automation off
            // nothing consumes them, and they silently lock stock.
            var itemIds = new List<int>(model.Reservations.Keys);
            itemIds.Sort();
            for (int i = 0; i < itemIds.Count; i++)
                change |= model.ReleaseReservation(itemIds[i]);

            // Listed records drop even when the bill object is already gone
            // (completed or player-deleted while the dialog was open).
            var pawnIds = new List<int>(model.OwnedBills.Keys);
            pawnIds.Sort();
            for (int p = 0; p < pawnIds.Count; p++)
            {
                Dictionary<string, string>? owned =
                    model.OwnedBillsFor(pawnIds[p]);
                if (owned == null) continue;
                var goalKeys = new List<string>(owned.Keys);
                goalKeys.Sort(System.StringComparer.Ordinal);
                for (int k = 0; k < goalKeys.Count; k++)
                    if (remove.Contains(owned[goalKeys[k]]))
                        change |= model.RemoveOwnedBill(pawnIds[p], goalKeys[k]);
            }
            var productionIds = new List<string>(model.OwnedProductionBills.Keys);
            productionIds.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < productionIds.Count; i++)
                if (remove.Contains(productionIds[i]))
                    change |= model.RemoveOwnedProductionBill(productionIds[i]);

            DeleteBillObjects(remove);
            store.Bump(change);
        }

        /// Deletes bill objects by load id wherever automation places them:
        /// colonist worktables and planable colonists' operation lists.
        /// Idempotent — missing bills are simply not found.
        private static void DeleteBillObjects(HashSet<string> ids)
        {
            if (ids.Count == 0) return;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings =
                    maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                    if (buildings[b] is Building_WorkTable bench)
                        DeleteFromStack(bench.BillStack, ids);
            }
            List<Pawn> pawns = ColonyScope.AllPlanableColonists(
                ColonyScope.AuthoritativeFaction);
            for (int i = 0; i < pawns.Count; i++)
                DeleteFromStack(pawns[i].BillStack, ids);
        }

        private static void DeleteFromStack(BillStack? stack, HashSet<string> ids)
        {
            if (stack == null) return;
            for (int i = stack.Count - 1; i >= 0; i--)
                if (ids.Contains(stack[i].GetUniqueLoadID()))
                    stack.Delete(stack[i]);
        }

        /// colonists 1–20: how many colonists per colony may have surgeries
        /// planned at once.
        [SyncMethod]
        public static void SetSurgeryConcurrency(int colonists)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetSurgeryConcurrency(colonists));
        }

        [SyncMethod]
        public static void SetCountHospitalized(bool enabled)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetCountHospitalized(enabled));
        }

        [SyncMethod]
        public static void SetAutoProduction(bool enabled)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetAutoProduction(enabled));
        }

        [SyncMethod]
        public static void SetProductionConcurrency(int benches)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetProductionConcurrency(benches));
        }

        [SyncMethod]
        public static void SetOnlyIdleBenches(bool enabled)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetOnlyIdleBenches(enabled));
        }

        [SyncMethod]
        public static void SetProductionSkill(int level)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetProductionSkill(level));
        }

        [SyncMethod]
        public static void SetAllowIntermediaries(bool enabled)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetAllowIntermediaries(enabled));
        }

        [SyncMethod]
        public static void SetResourceReserve(string resourceDefName, int amount)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetResourceReserve(resourceDefName, amount));
        }

        /// level 0 (first) … 4 (last); 2 is the stored-free default.
        [SyncMethod]
        public static void SetPawnPriority(int pawnId, int level)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetPawnPriority(pawnId, level));
        }

        /// planId 0 clears the assignment.
        [SyncMethod]
        public static void AssignPlan(int pawnId, int planId)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.AssignPlan(pawnId, planId));
        }

        /// Additive plan import. The raw XML is the sync payload: every
        /// client re-parses and re-applies it deterministically (identical
        /// input plus identical id-allocator state), and a payload that fails
        /// validation applies nothing anywhere. Names are uniquified against
        /// existing plans; nothing is overwritten.
        [SyncMethod]
        public static void ImportPlans(string xml)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            if (!Core.PlansXml.TryImport(xml, out var parsed, out _,
                    ModRequirements.IsModActive))
                return;
            store.Bump(store.Model.ImportPlans(
                parsed, store.TakePlanId));
        }
    }
}
