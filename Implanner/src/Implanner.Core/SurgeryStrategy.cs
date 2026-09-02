using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Global automation iteration strategy. Batching is implied: colonist
    /// iteration works toward the whole plan; tier iteration works one star
    /// tier at a time; ASAP has no batch gate at all. Values are persisted
    /// and synced by number.
    public enum IterationStrategy
    {
        /// Satisfy one colonist's plan before moving to the next pawn.
        Colonist = 0,
        /// Satisfy one star tier across pawns at a time (the default).
        ImplantTier = 1,
        /// Every implant in stock goes to the best candidate at once and is
        /// scheduled as soon as it is reserved on site; nothing waits for a
        /// batch to complete.
        Asap = 2,
    }

    /// The limb family an implant kind belongs to, for ASAP candidate
    /// ranking. Derived from the targeted part's body part tags.
    public enum LimbKind
    {
        None = 0,
        Leg = 1,
        Arm = 2,
    }

    /// The pawn facts the ASAP ranking consults when several colonists at
    /// the same priority miss the same implant kind: legs go to the slowest
    /// colonist, arms to a melee fighter and then to the better crafter or
    /// researcher (Intellectual plus Crafting).
    public readonly struct SurgeryCandidate
    {
        public SurgeryCandidate(float moveSpeed, bool hasMeleeWeapon, int armSkills)
        {
            MoveSpeed = moveSpeed;
            HasMeleeWeapon = hasMeleeWeapon;
            ArmSkills = armSkills;
        }

        public float MoveSpeed { get; }
        public bool HasMeleeWeapon { get; }
        public int ArmSkills { get; }
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
    /// a pawn, positioned by star tier and pawn priority. The implant kind,
    /// its limb family, and the pawn's candidate facts feed the ASAP
    /// ranking only; the batch strategies never read them.
    public readonly struct SurgeryWorkItem
    {
        public SurgeryWorkItem(int pawnId, int pawnPriority, int tier, string goalKey)
            : this(pawnId, pawnPriority, tier, goalKey, "", LimbKind.None, default)
        {
        }

        public SurgeryWorkItem(int pawnId, int pawnPriority, int tier, string goalKey,
            string implantDefName, LimbKind limb, SurgeryCandidate candidate)
        {
            PawnId = pawnId;
            PawnPriority = pawnPriority;
            Tier = tier;
            GoalKey = goalKey;
            ImplantDefName = implantDefName;
            Limb = limb;
            Candidate = candidate;
        }

        public int PawnId { get; }
        public int PawnPriority { get; }
        public int Tier { get; }
        public string GoalKey { get; }
        public string ImplantDefName { get; }
        public LimbKind Limb { get; }
        public SurgeryCandidate Candidate { get; }
    }

    /// Deterministic batch computation and iteration ordering for implant
    /// automation. Pure: tier membership derives from the model's star
    /// rankings and the effective goal list.
    public static class SurgeryPlanner
    {
        /// The pawn's active batch. Colonist and ASAP iteration: every
        /// missing key (the whole plan is one batch). Tier iteration: the
        /// keys of the active tier — the best tier (lowest index) with
        /// missing work. Tier membership derives from the goal key, the
        /// effective goal list, and the model's star rankings, with no
        /// captured delegate (reconcile tick path). Unresolvable keys sort
        /// into the worst tier (int.MaxValue), so they never join a ranked
        /// tier's batch.
        public static List<string> ComputeBatch(
            IReadOnlyList<string> missingKeys, PlannerModel model,
            IReadOnlyList<ImplantGoal> goals, IterationStrategy strategy)
        {
            var batch = new List<string>();
            if (missingKeys.Count == 0) return batch;
            if (strategy != IterationStrategy.ImplantTier)
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

        /// The batch keys whose operations may be released now. The batch
        /// strategies release the whole batch only once every key is ready
        /// (reserved on site), so several implants share one anesthetic
        /// sleep; ASAP releases every ready key at once. ready is parallel
        /// to batch.
        public static List<string> Releasable(List<string> batch, bool[] ready,
            IterationStrategy strategy)
        {
            var result = new List<string>();
            if (strategy == IterationStrategy.Asap)
            {
                for (int i = 0; i < batch.Count; i++)
                    if (ready[i]) result.Add(batch[i]);
                return result;
            }
            if (batch.Count == 0) return result;
            for (int i = 0; i < batch.Count; i++)
                if (!ready[i]) return result;
            for (int i = 0; i < batch.Count; i++)
                result.Add(batch[i]);
            return result;
        }

        /// Orders pending implant work for dispatch and the next-work list.
        /// Colonist iteration: pawn (priority, id) outranks tier; tier
        /// iteration: tier outranks pawn; ASAP: priority, then within one
        /// implant kind the candidate ranking. Ties break on stable
        /// identifiers.
        public static void Order(List<SurgeryWorkItem> items, IterationStrategy strategy)
        {
            items.Sort(strategy == IterationStrategy.ImplantTier ? ByTierThenPawn
                : strategy == IterationStrategy.Asap ? ByPriorityThenCandidate
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

        /// ASAP: stock of each kind is allocated independently, so the
        /// order only has to be total and deterministic across kinds
        /// (tier, then kind name) while ranking candidates within one kind.
        /// The ranking is a strict weak order only among items of one kind,
        /// which the kind grouping guarantees.
        static readonly Comparison<SurgeryWorkItem> ByPriorityThenCandidate = (a, b) =>
        {
            int c = a.PawnPriority.CompareTo(b.PawnPriority);
            if (c != 0) return c;
            c = a.Tier.CompareTo(b.Tier);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.ImplantDefName, b.ImplantDefName);
            if (c != 0) return c;
            c = CompareCandidates(a.Limb, a.Candidate, b.Candidate);
            if (c != 0) return c;
            c = a.PawnId.CompareTo(b.PawnId);
            if (c != 0) return c;
            return string.CompareOrdinal(a.GoalKey, b.GoalKey);
        };

        /// Legs: slowest first. Arms: melee fighters first, then the higher
        /// Intellectual plus Crafting sum. Other kinds: no preference.
        static int CompareCandidates(LimbKind limb, SurgeryCandidate a, SurgeryCandidate b)
        {
            switch (limb)
            {
                case LimbKind.Leg:
                    return a.MoveSpeed.CompareTo(b.MoveSpeed);
                case LimbKind.Arm:
                {
                    int c = b.HasMeleeWeapon.CompareTo(a.HasMeleeWeapon);
                    return c != 0 ? c : b.ArmSkills.CompareTo(a.ArmSkills);
                }
                default:
                    return 0;
            }
        }
    }
}
