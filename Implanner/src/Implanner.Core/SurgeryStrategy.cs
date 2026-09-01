using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Global automation iteration strategy. Batching is implied: colonist
    /// iteration works toward the whole plan; tier iteration works one star
    /// tier at a time. Values are persisted and synced by number.
    public enum IterationStrategy
    {
        /// Satisfy one colonist's plan before moving to the next pawn.
        Colonist = 0,
        /// Satisfy one star tier across pawns at a time (the default).
        ImplantTier = 1,
    }

    /// Star-ranking rules. Ranked implants carry 1–5 stars; 0 means unranked.
    /// The star tiers ARE the implant families: one batch per tier, best
    /// tier first.
    public static class StarRanking
    {
        public const int Max = 5;

        /// Tier index for ordering and batching: 5 stars → 0 (first) …
        /// 1 star → 4; unranked → 5 (always last).
        public static int TierOf(int stars) =>
            stars >= 1 && stars <= Max ? Max - stars : Max;
    }

    /// One unit of pending implant work for ordering: a missing goal slot on
    /// a pawn, positioned by star tier and pawn priority.
    public readonly struct SurgeryWorkItem
    {
        public SurgeryWorkItem(int pawnId, int pawnPriority, int tier, string goalKey)
        {
            PawnId = pawnId;
            PawnPriority = pawnPriority;
            Tier = tier;
            GoalKey = goalKey;
        }

        public int PawnId { get; }
        public int PawnPriority { get; }
        public int Tier { get; }
        public string GoalKey { get; }
    }

    /// Deterministic batch computation and iteration ordering for implant
    /// automation. Pure: tier membership derives from the model's star
    /// rankings and the effective goal list.
    public static class SurgeryPlanner
    {
        /// The pawn's active batch. Colonist iteration: every missing key
        /// (the whole plan is one batch). Tier iteration: the keys of the
        /// active tier — the best tier (lowest index) with missing work.
        /// Tier membership derives from the goal key, the effective goal
        /// list, and the model's star rankings, with no captured delegate
        /// (reconcile tick path). Unresolvable keys sort into the worst
        /// tier (int.MaxValue), so they never join a ranked tier's batch.
        public static List<string> ComputeBatch(
            IReadOnlyList<string> missingKeys, PlannerModel model,
            IReadOnlyList<ImplantGoal> goals, IterationStrategy strategy)
        {
            var batch = new List<string>();
            if (missingKeys.Count == 0) return batch;
            if (strategy == IterationStrategy.Colonist)
            {
                for (int i = 0; i < missingKeys.Count; i++)
                    batch.Add(missingKeys[i]);
                return batch;
            }
            var tiers = new int[missingKeys.Count];
            int active = int.MaxValue;
            for (int i = 0; i < missingKeys.Count; i++)
            {
                tiers[i] = TierOfKey(model, goals, missingKeys[i]);
                if (tiers[i] < active) active = tiers[i];
            }
            for (int i = 0; i < missingKeys.Count; i++)
                if (tiers[i] == active)
                    batch.Add(missingKeys[i]);
            return batch;
        }

        /// The star tier a goal key's implant kind occupies.
        public static int TierOfKey(PlannerModel model,
            IReadOnlyList<ImplantGoal> goals, string key) =>
            GoalKeys.TryResolveImplantSlot(goals, key, out ImplantGoal goal, out _)
                ? StarRanking.TierOf(model.ImplantStarsOf(goal.ImplantDefName))
                : int.MaxValue;

        /// Orders pending implant work for dispatch and the next-work list.
        /// Colonist iteration: pawn (priority, id) outranks tier; tier
        /// iteration: tier outranks pawn. Ties break on stable identifiers.
        public static void Order(List<SurgeryWorkItem> items, IterationStrategy strategy)
        {
            items.Sort(strategy == IterationStrategy.ImplantTier
                ? ByTierThenPawn
                : ByPawnThenTier);
        }

        static readonly Comparison<SurgeryWorkItem> ByPawnThenTier = (a, b) =>
        {
            int c = a.PawnPriority.CompareTo(b.PawnPriority);
            if (c != 0) return c;
            c = a.PawnId.CompareTo(b.PawnId);
            if (c != 0) return c;
            c = a.Tier.CompareTo(b.Tier);
            if (c != 0) return c;
            return string.CompareOrdinal(a.GoalKey, b.GoalKey);
        };

        static readonly Comparison<SurgeryWorkItem> ByTierThenPawn = (a, b) =>
        {
            int c = a.Tier.CompareTo(b.Tier);
            if (c != 0) return c;
            c = a.PawnPriority.CompareTo(b.PawnPriority);
            if (c != 0) return c;
            c = a.PawnId.CompareTo(b.PawnId);
            if (c != 0) return c;
            return string.CompareOrdinal(a.GoalKey, b.GoalKey);
        };
    }
}
