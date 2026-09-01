using System.Collections.Generic;

namespace Implanner.Core
{
    /// Aggregate pawn state for the overview table. Away is decided game-side
    /// and overrides evaluation. Active means at least one possible slot is
    /// still missing.
    public enum PawnPlanState
    {
        Active = 0,
        Complete = 1,
        Away = 2,
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

    /// Evaluation result for one goal, parallel to the evaluated goal list
    /// by index. Requested counts only the slots this body can take:
    /// impossible slots (missing anatomy or missing mod content) are
    /// excluded from the target entirely, so Satisfied + Missing ==
    /// Requested and a wholly impossible goal contributes nothing.
    public readonly struct GoalResult
    {
        public GoalResult(int requested, int satisfied, int missing)
        {
            Requested = requested;
            Satisfied = satisfied;
            Missing = missing;
        }

        public int Requested { get; }
        public int Satisfied { get; }
        public int Missing { get; }

        public bool IsComplete => Satisfied == Requested;
    }

    /// Complete deterministic evaluation of one pawn against one Plan.
    public sealed class PlanEvaluation
    {
        public PlanEvaluation(GoalResult[] implants, PawnPlanState state,
            int satisfiedUnits, int totalUnits)
        {
            Implants = implants;
            State = state;
            SatisfiedUnits = satisfiedUnits;
            TotalUnits = totalUnits;
        }

        public GoalResult[] Implants { get; }
        public PawnPlanState State { get; }
        public int SatisfiedUnits { get; }
        public int TotalUnits { get; }

        /// 0..1; an empty Plan counts as complete.
        public float Progress => TotalUnits == 0 ? 1f : (float)SatisfiedUnits / TotalUnits;
    }
}
