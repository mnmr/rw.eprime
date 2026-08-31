using System.Collections.Generic;

namespace Implanner.Core
{
    /// Why an unsatisfied goal cannot currently be pursued. None means the
    /// goal is missing but eligible.
    public enum GoalBlocker
    {
        None = 0,
        /// Requested anatomy does not exist on this pawn (or not enough of it).
        Anatomy = 1,
    }

    /// Aggregate pawn state for the overview table. Away is decided game-side
    /// and overrides evaluation. Blocked means nothing unsatisfied is
    /// currently actionable; Active means at least one goal is; Regressed
    /// means only delivered-once goals that were later lost remain, awaiting
    /// an explicit re-enlist.
    public enum PawnPlanState
    {
        Active = 0,
        Complete = 1,
        Blocked = 2,
        Away = 3,
        Regressed = 4,
    }

    /// One installed implant as projected from the pawn's hediffs. SlotKey
    /// identifies the anatomy instance (body part record path) so symmetric
    /// parts stay distinct.
    public readonly struct InstalledImplant
    {
        public InstalledImplant(string implantDefName, string slotKey, float efficiency)
        {
            ImplantDefName = implantDefName;
            SlotKey = slotKey;
            Efficiency = efficiency;
        }

        public string ImplantDefName { get; }
        public string SlotKey { get; }
        public float Efficiency { get; }
    }

    /// Pawn-specific context for one implant goal: the anatomy instances the
    /// requested implant could occupy on this pawn, and the requested
    /// implant's own efficiency (the substitution floor).
    public sealed class ImplantContext
    {
        public ImplantContext(IReadOnlyList<string> applicableSlotKeys, float efficiency)
        {
            ApplicableSlotKeys = applicableSlotKeys;
            Efficiency = efficiency;
        }

        public IReadOnlyList<string> ApplicableSlotKeys { get; }
        public float Efficiency { get; }
    }

    /// Evaluation result for one goal. Counts partition the requested amount:
    /// Satisfied + Missing + Blocked + Regressed == Requested. Regressed
    /// units were delivered once (latched) and later lost; they are not
    /// actionable work until an explicit re-enlist.
    public readonly struct GoalResult
    {
        public GoalResult(int goalId, int requested, int satisfied, int missing,
            int blocked, GoalBlocker blocker, int regressed = 0)
        {
            GoalId = goalId;
            Requested = requested;
            Satisfied = satisfied;
            Missing = missing;
            Blocked = blocked;
            Blocker = blocker;
            Regressed = regressed;
        }

        public int GoalId { get; }
        public int Requested { get; }
        public int Satisfied { get; }
        public int Missing { get; }
        public int Blocked { get; }
        public GoalBlocker Blocker { get; }
        public int Regressed { get; }

        public bool IsComplete => Satisfied == Requested;
    }

    /// Complete deterministic evaluation of one pawn against one Plan.
    public sealed class PlanEvaluation
    {
        public PlanEvaluation(GoalResult[] implants, PawnPlanState state,
            int satisfiedUnits, int totalUnits, List<string> satisfiedGoalKeys)
        {
            Implants = implants;
            State = state;
            SatisfiedUnits = satisfiedUnits;
            TotalUnits = totalUnits;
            SatisfiedGoalKeys = satisfiedGoalKeys;
        }

        public GoalResult[] Implants { get; }
        public PawnPlanState State { get; }
        public int SatisfiedUnits { get; }
        public int TotalUnits { get; }

        /// Goal keys currently satisfied (sorted): the delivery observations
        /// the latch model consumes, and the complement re-enlist needs.
        public List<string> SatisfiedGoalKeys { get; }

        /// 0..1; an empty Plan counts as complete.
        public float Progress => TotalUnits == 0 ? 1f : (float)SatisfiedUnits / TotalUnits;
    }
}
