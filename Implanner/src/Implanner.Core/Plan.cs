using System.Collections.Generic;

namespace Implanner.Core
{
    /// One implant goal: an implant kind plus the selected anatomy slots.
    /// Slot ordinals index the implant's canonical applicable-slot
    /// enumeration (catalog part order, then body record order), so "left
    /// leg" is ordinal 0 and "right leg" ordinal 1 on any body that has both.
    public sealed class ImplantGoal
    {
        public ImplantGoal(int planId, string implantDefName, IReadOnlyList<int> slotOrdinals)
        {
            PlanId = planId;
            ImplantDefName = implantDefName;
            SlotOrdinals = slotOrdinals;
        }

        /// The owning plan. A goal's identity is natural — (PlanId,
        /// ImplantDefName) — because a plan holds at most one goal per
        /// implant kind (SetImplantSlot merges ordinals into it). No id is
        /// ever allocated, so goal keys cannot collide and re-adding the
        /// same pick reproduces the same identity. In an effective goal
        /// list an inherited goal keeps its base plan's id.
        public int PlanId { get; }
        public string ImplantDefName { get; }

        /// Sorted, deduplicated, never empty (a goal with no slots is removed).
        public IReadOnlyList<int> SlotOrdinals { get; }

        public int Count => SlotOrdinals.Count;
    }

    /// A named end-state implant configuration assignable to colonists. A
    /// plan may extend another plan (BasePlanId): its effective goals are its
    /// own plus the base chain's, with its own selections overriding
    /// overlapping slots (PlannerModel.EffectiveImplants). The base link is
    /// chosen at creation and only cleared when the base plan disappears, so
    /// the chain can never form a cycle. Only the model (same assembly)
    /// mutates a plan; everything outside Core sees read-only state.
    public sealed class Plan
    {
        readonly List<ImplantGoal> implants;

        public Plan(int id, string name)
            : this(id, name, 0, new List<ImplantGoal>())
        {
        }

        /// Hydration path (load, import parsing): builds a fully populated
        /// plan in one step. Takes ownership of <paramref name="goals"/>:
        /// the caller must not retain or mutate the list afterwards.
        public Plan(int id, string name, int basePlanId, List<ImplantGoal> goals)
        {
            Id = id;
            Name = name;
            BasePlanId = basePlanId;
            implants = goals;
        }

        public int Id { get; }
        public string Name { get; internal set; }

        /// The plan this plan extends; 0 = none.
        public int BasePlanId { get; internal set; }

        /// The plan's own goals (inherited goals live only in the effective
        /// list). Read-only outside Core; the model edits MutableImplants.
        public IReadOnlyList<ImplantGoal> Implants => implants;

        internal List<ImplantGoal> MutableImplants => implants;
    }
}
