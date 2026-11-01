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

    /// Pawn-specific context for one implant goal: the anatomy instance each
    /// selected slot denotes on this pawn, parallel to the goal's Slots
    /// (null where the body lacks that part — the slot is impossible here),
    /// why a slot is outside this pawn's target (SlotApplicability; absent
    /// means a null key is missing anatomy), and the requested implant's
    /// own efficiency (the substitution floor).
    public sealed class ImplantContext
    {
        public ImplantContext(IReadOnlyList<string?> slotKeys, float efficiency,
            IReadOnlyList<SlotBlock>? blocks = null, IReadOnlyList<string?>? blockers = null)
        {
            SlotKeys = slotKeys;
            Efficiency = efficiency;
            Blocks = blocks;
            Blockers = blockers;
        }

        public IReadOnlyList<string?> SlotKeys { get; }
        public float Efficiency { get; }
        public IReadOnlyList<SlotBlock>? Blocks { get; }
        public IReadOnlyList<string?>? Blockers { get; }

        /// The pawn's anatomy instance for goal slot j, or null when the
        /// slot is outside this pawn's target (no such part, or blocked).
        public string? SlotKey(int j) =>
            j < SlotKeys.Count && Block(j) == SlotBlock.None ? SlotKeys[j] : null;

        public SlotBlock Block(int j)
        {
            if (Blocks != null && j < Blocks.Count) return Blocks[j];
            return j < SlotKeys.Count && SlotKeys[j] != null
                ? SlotBlock.None
                : SlotBlock.NoAnatomy;
        }

        /// The implant kind blocking slot j (installed above it, or planned
        /// to replace a part above it), or null.
        public string? Blocker(int j) =>
            Blockers != null && j < Blockers.Count ? Blockers[j] : null;
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
            int satisfiedUnits, int totalUnits,
            IReadOnlyList<ImplantContext>? contexts = null)
        {
            Implants = implants;
            State = state;
            SatisfiedUnits = satisfiedUnits;
            TotalUnits = totalUnits;
            Contexts = contexts;
        }

        public GoalResult[] Implants { get; }
        public PawnPlanState State { get; }
        public int SatisfiedUnits { get; }
        public int TotalUnits { get; }

        /// The per-goal contexts the evaluation ran on (parallel to
        /// Implants), so presentation can name why a slot is outside this
        /// pawn's target without re-projecting the pawn.
        public IReadOnlyList<ImplantContext>? Contexts { get; }

        /// 0..1; an empty Plan counts as complete.
        public float Progress => TotalUnits == 0 ? 1f : (float)SatisfiedUnits / TotalUnits;
    }
}
