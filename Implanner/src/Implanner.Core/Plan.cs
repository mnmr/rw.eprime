using System.Collections.Generic;

namespace Implanner.Core
{
    /// One implant goal: an implant kind plus the selected anatomy slots.
    /// Slot ordinals index the implant's canonical applicable-slot
    /// enumeration (catalog part order, then body record order), so "left
    /// leg" is ordinal 0 and "right leg" ordinal 1 on any body that has both.
    public sealed class ImplantGoal
    {
        public ImplantGoal(int id, string implantDefName, IReadOnlyList<int> slotOrdinals)
        {
            Id = id;
            ImplantDefName = implantDefName;
            SlotOrdinals = slotOrdinals;
        }

        /// Globally stable per save (allocated from the store counter);
        /// never reused after deletion, so goal keys survive plan extension.
        public int Id { get; }
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
    /// the chain can never form a cycle.
    public sealed class Plan
    {
        public Plan(int id, string name)
        {
            Id = id;
            Name = name;
            Implants = new List<ImplantGoal>();
        }

        public int Id { get; }
        public string Name { get; set; }

        /// The plan this plan extends; 0 = none.
        public int BasePlanId { get; set; }

        public List<ImplantGoal> Implants { get; }
    }
}
