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
        public static void RemoveImplant(int planId, int goalId)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.RemoveImplant(planId, goalId));
        }

        [SyncMethod]
        public static void SetImplantSlot(int planId, string implantDefName, int slotOrdinal, bool wanted)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            store.Bump(store.Model.SetImplantSlot(
                planId, implantDefName, slotOrdinal, wanted, store.TakeGoalId));
        }

        /// Re-enlist: returns exactly the pawn's no-longer-satisfied
        /// delivered-once goals to the pipeline. Satisfaction is derived
        /// inside the synced command from authoritative state and definition
        /// data, so every client clears the identical keys.
        [SyncMethod]
        public static void ReEnlist(int pawnId)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            Plan? plan = store.Model.AssignedPlan(pawnId);
            HashSet<string>? latched = store.Model.LatchesFor(pawnId);
            if (plan == null || latched == null || latched.Count == 0) return;
            Pawn? pawn = null;
            // Synced command: resolve pawns with the authoritative faction,
            // never the local client's view faction.
            List<Pawn> colonists = ColonyScope.AllPlanableColonists(
                ColonyScope.AuthoritativeFaction);
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i].thingIDNumber == pawnId)
                {
                    pawn = colonists[i];
                    break;
                }
            if (pawn == null) return;
            List<ImplantGoal> goals = store.Model.EffectiveImplants(plan);
            PlanEvaluation evaluation = PawnProjection.Evaluate(
                pawn, goals, away: false, latched);
            var unsatisfied = new List<string>();
            foreach (string key in latched)
                if (!evaluation.SatisfiedGoalKeys.Contains(key))
                    unsatisfied.Add(key);
            unsatisfied.Sort(System.StringComparer.Ordinal);
            store.Bump(store.Model.ReEnlist(pawnId, unsatisfied));
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
    }
}
