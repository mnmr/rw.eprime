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
        /// implantContexts is parallel to goals by index. latchedKeys
        /// carries the pawn's delivered-once goal keys (null = none).
        /// sameSlotExclusive answers whether an installed implant kind
        /// excludes installing the requested kind on the same anatomy
        /// instance (replacement occupancy or tag conflict); substitution is
        /// only valid for such occupants — a coexisting implant never
        /// satisfies another kind's goal. Null allows any same-slot
        /// substitute (conflict-blind callers and tests).
        public static PlanEvaluation Evaluate(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installedImplants,
            IReadOnlyList<ImplantContext> implantContexts,
            bool away,
            HashSet<string>? latchedKeys = null,
            Func<string, string, bool>? sameSlotExclusive = null)
        {
            if (implantContexts.Count != goals.Count)
                throw new ArgumentException("implant context count mismatch", nameof(implantContexts));

            var slotSatisfied = MatchSlots(goals, installedImplants,
                implantContexts, sameSlotExclusive);
            var implants = AccountGoals(goals, implantContexts,
                slotSatisfied, latchedKeys);
            ComputeUnits(implants, out int satisfiedUnits, out int totalUnits);
            var state = DeriveState(implants, away);
            var satisfiedKeys = CollectSatisfiedKeys(goals, implantContexts,
                slotSatisfied);
            return new PlanEvaluation(implants, state,
                satisfiedUnits, totalUnits, satisfiedKeys);
        }

        /// The delivery observations: every goal key currently satisfied by
        /// the one-to-one matching passes. Consumption matters here exactly
        /// as it does for missing keys — one installed part latches one goal
        /// slot, never two.
        static List<string> CollectSatisfiedKeys(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<ImplantContext> implantContexts,
            bool[][] slotSatisfied)
        {
            var keys = new List<string>();
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                    if (slotSatisfied[i][j])
                        keys.Add(GoalKeys.ImplantSlot(goal.Id, goal.SlotOrdinals[j]));
            }
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// The implant slots surgery automation still has to deliver: selected
        /// slots that exist on this body, are not latched, and are not
        /// satisfied by the same one-to-one matching Evaluate uses. Goal
        /// order, then slot-ordinal order — deterministic; traversal ordering
        /// is applied by the caller.
        public static List<string> MissingImplantSlotKeys(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installed,
            IReadOnlyList<ImplantContext> implantContexts,
            HashSet<string>? latchedKeys,
            Func<string, string, bool>? sameSlotExclusive = null)
        {
            if (implantContexts.Count != goals.Count)
                throw new ArgumentException("implant context count mismatch", nameof(implantContexts));
            var slotSatisfied = MatchSlots(goals, installed, implantContexts,
                sameSlotExclusive);
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
                    string key = GoalKeys.ImplantSlot(goal.Id, ordinal);
                    if (latchedKeys != null && latchedKeys.Contains(key)) continue;
                    keys.Add(key);
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

        /// The one-to-one matching shared by evaluation, delivery latching,
        /// and the missing-slot derivation: which selected slots are
        /// satisfied by which installed implant, each implant consumed at
        /// most once. Returns per-goal, per-selected-slot satisfaction so
        /// regression can be attributed to the exact latched slot.
        static bool[][] MatchSlots(
            IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<InstalledImplant> installed,
            IReadOnlyList<ImplantContext> contexts,
            Func<string, string, bool>? sameSlotExclusive)
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
            // never satisfies a different brain implant's goal).
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
                            && !sameSlotExclusive(candidate.ImplantDefName, goal.ImplantDefName))
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
            bool[][] slotSatisfied,
            HashSet<string>? latchedKeys)
        {
            var results = new GoalResult[goals.Count];
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var keys = contexts[i].ApplicableSlotKeys;

                // Accounting: blocked (no anatomy) > satisfied > regressed
                // (latched, delivered once) > missing.
                int satisfied = 0, missing = 0, blocked = 0, regressed = 0;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    int ordinal = goal.SlotOrdinals[j];
                    if (ordinal >= keys.Count) { blocked++; continue; }
                    if (slotSatisfied[i][j]) { satisfied++; continue; }
                    if (latchedKeys != null
                        && latchedKeys.Contains(GoalKeys.ImplantSlot(goal.Id, ordinal)))
                        regressed++;
                    else
                        missing++;
                }
                results[i] = new GoalResult(
                    goal.Id, goal.SlotOrdinals.Count, satisfied, missing, blocked,
                    blocked > 0 ? GoalBlocker.Anatomy : GoalBlocker.None, regressed);
            }

            return results;
        }

        static PawnPlanState DeriveState(GoalResult[] implants, bool away)
        {
            if (away)
                return PawnPlanState.Away;

            bool anyMissing = false, anyBlocked = false, anyRegressed = false;
            for (int i = 0; i < implants.Length; i++)
            {
                anyMissing |= implants[i].Missing > 0;
                anyBlocked |= implants[i].Blocked > 0;
                anyRegressed |= implants[i].Regressed > 0;
            }

            if (!anyMissing && !anyBlocked && !anyRegressed)
                return PawnPlanState.Complete;
            if (anyMissing)
                return PawnPlanState.Active;
            if (anyRegressed)
                return PawnPlanState.Regressed;
            return PawnPlanState.Blocked;
        }
    }
}
