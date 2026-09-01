using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Stock coverage of one implant item kind for the colony strip's
    /// production breakdown. Items the player holds back never count;
    /// items automation has reserved for a colonist's surgery count toward
    /// coverage (they exist and need no production) but not toward the
    /// free figure, which is never capped to the need so surplus shows.
    public readonly struct StockCoverage
    {
        public StockCoverage(int needed, int covered, int free)
        {
            Needed = needed;
            Covered = covered;
            Free = free;
        }

        public int Needed { get; }
        public int Covered { get; }
        public int Free { get; }

        /// Items still to produce.
        public int Queued => Needed - Covered;

        public static StockCoverage Of(int needed, int stock, int heldBack, int reserved)
        {
            int usable = Math.Max(0, stock - heldBack);
            int covered = Math.Min(needed, usable);
            int free = Math.Max(0, usable - reserved);
            return new StockCoverage(needed, covered, free);
        }
    }

    /// One colonist's contribution to the surgery breakdown: the effective
    /// goal list, its evaluation results (parallel by index), and the goal
    /// keys automation currently holds a reservation or an operation bill
    /// for on this colonist.
    public sealed class SurgeryPawnInput
    {
        public SurgeryPawnInput(IReadOnlyList<ImplantGoal> goals,
            IReadOnlyList<GoalResult> results,
            IEnumerable<string> reservedKeys, IEnumerable<string> scheduledKeys)
        {
            Goals = goals;
            Results = results;
            ReservedKeys = reservedKeys;
            ScheduledKeys = scheduledKeys;
        }

        public IReadOnlyList<ImplantGoal> Goals { get; }
        public IReadOnlyList<GoalResult> Results { get; }
        public IEnumerable<string> ReservedKeys { get; }
        public IEnumerable<string> ScheduledKeys { get; }
    }

    /// Planned slots of one implant kind across the scoped colonists, split
    /// by pipeline stage: installed, or missing and waiting (nothing held),
    /// reserved (an item is held), or scheduled (an operation bill exists;
    /// wins over a reservation for the same slot).
    public sealed class SurgeryKindTotals
    {
        public SurgeryKindTotals(string kind, int tier, int order)
        {
            Kind = kind;
            Tier = tier;
            Order = order;
        }

        public string Kind { get; }
        public int Tier { get; }
        public int Order { get; }
        public int Planned { get; internal set; }
        public int Installed { get; internal set; }
        public int Waiting { get; internal set; }
        public int Reserved { get; internal set; }
        public int Scheduled { get; internal set; }
    }

    /// Per-kind tallies behind the colony strip's tooltips. Pure and
    /// deterministic: callers project pawn state into the inputs.
    public static class StripBreakdown
    {
        /// Kinds in dispatch order (star tier, then the player-arranged
        /// tier position, then defName). The partition is made per
        /// colonist and goal, so keys are counted once each, keys that do
        /// not resolve to an effective goal of their pawn are ignored, and
        /// a stale key on an already satisfied slot can neither push
        /// Waiting below zero nor shift another colonist's slot.
        public static List<SurgeryKindTotals> Surgery(
            IReadOnlyList<SurgeryPawnInput> pawns, PlannerModel model)
        {
            var byKind = new Dictionary<string, SurgeryKindTotals>(StringComparer.Ordinal);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var scheduledPerGoal = new List<int>();
            var reservedPerGoal = new List<int>();
            for (int p = 0; p < pawns.Count; p++)
            {
                SurgeryPawnInput pawn = pawns[p];
                IReadOnlyList<ImplantGoal> goals = pawn.Goals;
                scheduledPerGoal.Clear();
                reservedPerGoal.Clear();
                for (int g = 0; g < goals.Count; g++)
                {
                    scheduledPerGoal.Add(0);
                    reservedPerGoal.Add(0);
                }
                seenKeys.Clear();
                foreach (string key in pawn.ScheduledKeys)
                {
                    int g = GoalIndexOf(goals, key);
                    if (g >= 0 && seenKeys.Add(key)) scheduledPerGoal[g]++;
                }
                foreach (string key in pawn.ReservedKeys)
                {
                    int g = GoalIndexOf(goals, key);
                    if (g >= 0 && seenKeys.Add(key)) reservedPerGoal[g]++;
                }

                for (int g = 0; g < goals.Count; g++)
                {
                    GoalResult result = pawn.Results[g];
                    if (result.Requested == 0) continue;
                    int scheduled = Math.Min(scheduledPerGoal[g], result.Missing);
                    int reserved = Math.Min(reservedPerGoal[g], result.Missing - scheduled);
                    SurgeryKindTotals totals = TotalsFor(byKind, goals[g].ImplantDefName, model);
                    totals.Planned += result.Requested;
                    totals.Installed += result.Satisfied;
                    totals.Scheduled += scheduled;
                    totals.Reserved += reserved;
                    totals.Waiting += result.Missing - scheduled - reserved;
                }
            }

            var kinds = new List<SurgeryKindTotals>(byKind.Count);
            foreach (KeyValuePair<string, SurgeryKindTotals> pair in byKind)
                if (pair.Value.Planned > 0)
                    kinds.Add(pair.Value);
            kinds.Sort(ByDispatchOrder);
            return kinds;
        }

        /// Index of the effective goal a slot key belongs to, or -1.
        private static int GoalIndexOf(IReadOnlyList<ImplantGoal> goals, string key)
        {
            if (!GoalKeys.TryParseImplantSlot(key,
                    out int planId, out string defName, out _))
                return -1;
            for (int g = 0; g < goals.Count; g++)
                if (goals[g].PlanId == planId
                    && string.Equals(goals[g].ImplantDefName, defName,
                        StringComparison.Ordinal))
                    return g;
            return -1;
        }

        private static SurgeryKindTotals TotalsFor(
            Dictionary<string, SurgeryKindTotals> byKind, string kind, PlannerModel model)
        {
            if (!byKind.TryGetValue(kind, out SurgeryKindTotals totals))
            {
                totals = new SurgeryKindTotals(kind,
                    StarRanking.TierOf(model.ImplantStarsOf(kind)),
                    model.ImplantOrderOf(kind));
                byKind.Add(kind, totals);
            }
            return totals;
        }

        private static readonly Comparison<SurgeryKindTotals> ByDispatchOrder = (a, b) =>
        {
            int c = a.Tier.CompareTo(b.Tier);
            if (c != 0) return c;
            c = a.Order.CompareTo(b.Order);
            return c != 0 ? c : string.CompareOrdinal(a.Kind, b.Kind);
        };
    }
}
