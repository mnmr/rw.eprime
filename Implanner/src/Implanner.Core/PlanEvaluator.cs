using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Deterministic evaluation of one pawn's projected implants against a
    /// plan's effective goal list (own goals plus inherited base goals —
    /// PlannerModel.EffectiveImplants). Pure: no game types, no
    /// allocation-order dependence, stable results for identical inputs. All
    /// def resolution and anatomy projection happen game-side before this
    /// runs.
    public static class PlanEvaluator
    {
        /// implantContexts is parallel to goals by index.
        /// sameSlotExclusive answers whether an installed implant kind
        /// excludes installing the requested kind on the same anatomy
        /// instance (replacement occupancy or tag conflict); substitution is
        /// only valid for such occupants — a coexisting implant never
        /// satisfies another kind's goal. Null allows any same-slot
        /// substitute (conflict-blind callers and tests).
        /// kindsExclusive answers whether two implant kinds are one slot by
        /// player option (PlannerModel.KindsExclusive): such an installed
        /// kind substitutes for the goal even though game data lets them
        /// coexist, so automation never adds the second kind. Null means no
        /// option-driven exclusivity.
        public static PlanEvaluation Evaluate(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installedImplants,
            IReadOnlyList<ImplantContext> implantContexts,
            bool away,
            Func<string, string, bool>? sameSlotExclusive = null,
            Func<string, string, bool>? kindsExclusive = null)
        {
            if (implantContexts.Count != goals.Count)
                throw new ArgumentException("implant context count mismatch", nameof(implantContexts));

            var slotSatisfied = MatchSlots(goals, installedImplants,
                implantContexts, sameSlotExclusive, kindsExclusive);
            var implants = AccountGoals(goals, implantContexts, slotSatisfied);
            ComputeUnits(implants, out int satisfiedUnits, out int totalUnits);
            var state = DeriveState(implants, away);
            return new PlanEvaluation(implants, state,
                satisfiedUnits, totalUnits);
        }

        /// The implant slots surgery automation still has to deliver: selected
        /// slots that exist on this body and are not satisfied by the same
        /// one-to-one matching Evaluate uses. Goal order, then slot-ordinal
        /// order — deterministic; traversal ordering is applied by the caller.
        public static List<string> MissingImplantSlotKeys(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installed,
            IReadOnlyList<ImplantContext> implantContexts,
            Func<string, string, bool>? sameSlotExclusive = null,
            Func<string, string, bool>? kindsExclusive = null)
        {
            if (implantContexts.Count != goals.Count)
                throw new ArgumentException("implant context count mismatch", nameof(implantContexts));
            var slotSatisfied = MatchSlots(goals, installed, implantContexts,
                sameSlotExclusive, kindsExclusive);
            var keys = new List<string>();
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var slotKeys = implantContexts[i].ApplicableSlotKeys;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    int ordinal = goal.SlotOrdinals[j];
                    if (ordinal >= slotKeys.Count) continue; // blocked anatomy
                    if (slotSatisfied[i][j]) continue;
                    keys.Add(GoalKeys.ImplantSlot(goal, ordinal));
                }
            }
            return keys;
        }

        static void ComputeUnits(GoalResult[] implants,
            out int satisfiedUnits, out int totalUnits)
        {
            int satisfied = 0, total = 0;
            for (int i = 0; i < implants.Length; i++)
            {
                satisfied += implants[i].Satisfied;
                total += implants[i].Requested;
            }
            satisfiedUnits = satisfied;
            totalUnits = total;
        }

        /// The one-to-one matching shared by evaluation and the missing-slot
        /// derivation: which selected slots are satisfied by which installed
        /// implant, each implant consumed at most once.
        static bool[][] MatchSlots(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installed,
            IReadOnlyList<ImplantContext> contexts,
            Func<string, string, bool>? sameSlotExclusive,
            Func<string, string, bool>? kindsExclusive)
        {
            var consumed = new bool[installed.Count];
            var slotSatisfied = new bool[goals.Count][];

            // Goals select specific slots. Pass 1 — exact matches, goal order,
            // slot-ordinal order. Exact-first prevents one superior implant
            // from satisfying two goals and keeps superior parts free for
            // goals that request them.
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var keys = contexts[i].ApplicableSlotKeys;
                slotSatisfied[i] = new bool[goal.SlotOrdinals.Count];
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    int ordinal = goal.SlotOrdinals[j];
                    if (ordinal >= keys.Count)
                        continue; // blocked; resolved in the final accounting
                    string slotKey = keys[ordinal];
                    for (int s = 0; s < installed.Count; s++)
                    {
                        if (consumed[s])
                            continue;
                        if (!string.Equals(installed[s].SlotKey, slotKey, StringComparison.Ordinal))
                            continue;
                        if (!string.Equals(installed[s].ImplantDefName, goal.ImplantDefName, StringComparison.Ordinal))
                            continue;
                        consumed[s] = true;
                        slotSatisfied[i][j] = true;
                        break;
                    }
                }
            }

            // Pass 2 — superior substitutes at the same slot, one-to-one. A
            // substitute counts when it occupies the selected slot, its
            // efficiency is at least the requested implant's, AND it actually
            // excludes installing the requested implant there (a manually
            // installed archotech leg satisfies a bionic-leg goal because the
            // goal cannot be installed over it; a coexisting brain implant
            // never satisfies a different brain implant's goal). Kinds the
            // player's options declare one slot substitute the same way.
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var context = contexts[i];
                var keys = context.ApplicableSlotKeys;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    if (slotSatisfied[i][j])
                        continue;
                    int ordinal = goal.SlotOrdinals[j];
                    if (ordinal >= keys.Count)
                        continue;
                    string slotKey = keys[ordinal];
                    int pick = -1;
                    for (int s = 0; s < installed.Count; s++)
                    {
                        if (consumed[s])
                            continue;
                        var candidate = installed[s];
                        if (!string.Equals(candidate.SlotKey, slotKey, StringComparison.Ordinal))
                            continue;
                        if (string.Equals(candidate.ImplantDefName, goal.ImplantDefName, StringComparison.Ordinal))
                            continue;
                        if (candidate.Efficiency < context.Efficiency)
                            continue;
                        if (sameSlotExclusive != null
                            && !sameSlotExclusive(candidate.ImplantDefName, goal.ImplantDefName)
                            && !(kindsExclusive != null
                                && kindsExclusive(candidate.ImplantDefName, goal.ImplantDefName)))
                            continue;
                        if (pick < 0 || candidate.Efficiency < installed[pick].Efficiency)
                            pick = s;
                    }
                    if (pick >= 0)
                    {
                        consumed[pick] = true;
                        slotSatisfied[i][j] = true;
                    }
                }
            }

            return slotSatisfied;
        }

        static GoalResult[] AccountGoals(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<ImplantContext> contexts,
            bool[][] slotSatisfied)
        {
            var results = new GoalResult[goals.Count];
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var keys = contexts[i].ApplicableSlotKeys;

                // A slot the body cannot take is excluded from the target:
                // it neither completes nor counts as missing.
                int satisfied = 0, missing = 0;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    int ordinal = goal.SlotOrdinals[j];
                    if (ordinal >= keys.Count) continue;
                    if (slotSatisfied[i][j]) { satisfied++; continue; }
                    missing++;
                }
                results[i] = new GoalResult(
                    satisfied + missing, satisfied, missing);
            }

            return results;
        }

        static PawnPlanState DeriveState(GoalResult[] implants, bool away)
        {
            if (away)
                return PawnPlanState.Away;
            for (int i = 0; i < implants.Length; i++)
                if (implants[i].Missing > 0)
                    return PawnPlanState.Active;
            return PawnPlanState.Complete;
        }
    }
}
