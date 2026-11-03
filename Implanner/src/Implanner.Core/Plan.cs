using System.Collections.Generic;

namespace Implanner.Core
{
    /// One implant goal: an implant kind plus the selected anatomy slots,
    /// each a PartAddress (part kind + occurrence), so a slot names the
    /// same part on every body that has it and nothing on one that does
    /// not ("Leg#1" is the second leg, "RightSightSensor#0" an android's
    /// right sight sensor).
    public sealed class ImplantGoal
    {
        public ImplantGoal(int planId, string implantDefName, IReadOnlyList<PartAddress> slots)
        {
            PlanId = planId;
            ImplantDefName = implantDefName;
            Slots = slots;
        }

        /// The owning plan. A goal's identity is natural — (PlanId,
        /// ImplantDefName) — because a plan holds at most one goal per
        /// implant kind (SetImplantSlot merges ordinals into it). No id is
        /// ever allocated, so goal keys cannot collide and re-adding the
        /// same pick reproduces the same identity. In an effective goal
        /// list an inherited goal keeps its base plan's id.
        public int PlanId { get; }
        public string ImplantDefName { get; }

        /// Sorted (PartAddress order), deduplicated, never empty (a goal
        /// with no slots is removed).
        public IReadOnlyList<PartAddress> Slots { get; }

        public int Count => Slots.Count;

        public bool HasSlot(PartAddress slot)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].Equals(slot)) return true;
            return false;
        }
    }

    /// A named end-state implant configuration assignable to colonists. A
    /// plan may extend another plan (BasePlanId): its effective goals are its
    /// own plus the base chain's, with its own selections overriding
    /// overlapping slots (PlannerModel.EffectiveImplants). The base link is
    /// chosen at creation and only cleared when the base plan disappears, so
    /// the chain can never form a cycle. A plan may be pinned to one body
    /// (Body): it then lists and validates that body's parts only and
    /// enlists only colonists with that body; an unpinned plan applies to
    /// every colonist. Only the model (same assembly) mutates a plan;
    /// everything outside Core sees read-only state.
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
        public Plan(int id, string name, int basePlanId, List<ImplantGoal> goals,
            int minQuality = 0, string? body = null, string? mutant = null)
        {
            Id = id;
            Name = name;
            BasePlanId = basePlanId;
            implants = goals;
            MinQuality = ImplantQuality.Clamp(minQuality);
            Body = NormalizeBody(body);
            Mutant = NormalizeBody(mutant);
        }

        public int Id { get; }
        public string Name { get; internal set; }

        /// The plan this plan extends; 0 = none.
        public int BasePlanId { get; internal set; }

        /// The body this plan is pinned to (a BodyDef name), or "" for any
        /// body. Fixed at creation; a child plan's body must be at least as
        /// specific as its base's (IsCompatibleBase).
        public string Body { get; internal set; } = "";

        public bool IsPinned => Body.Length > 0;

        /// The mutant kind this plan is made for (a MutantDef name, "Ghoul"),
        /// or "" for ordinary colonists. Fixed at creation like Body and
        /// orthogonal to it. A plan enlists a pawn only when the pawn's
        /// mutant kind is exactly this (AcceptsMutant): an ordinary plan
        /// never enlists a ghoul, a ghoul plan enlists nobody else.
        public string Mutant { get; internal set; } = "";

        public bool IsForMutant => Mutant.Length > 0;

        /// Whether a pawn of the given mutant kind ("" = none) may be
        /// enlisted in this plan.
        public bool AcceptsMutant(string pawnMutant) =>
            string.Equals(Mutant, pawnMutant, System.StringComparison.Ordinal);

        /// Whether a plan of <paramref name="body"/> and <paramref name="mutant"/>
        /// may extend a plan of <paramref name="baseBody"/> and
        /// <paramref name="baseMutant"/>: an unpinned base takes any child, a
        /// pinned base only children of the same body; the same rule for the
        /// mutant kind (a ghoul plan may build on an ordinary plan, since
        /// ghouls take ordinary bionics, but nothing else may build on it).
        public static bool IsCompatibleBase(string baseBody, string baseMutant,
            string body, string mutant) =>
            (baseBody.Length == 0 || string.Equals(baseBody, body, System.StringComparison.Ordinal))
            && (baseMutant.Length == 0 || string.Equals(baseMutant, mutant, System.StringComparison.Ordinal));

        public static bool IsCompatibleBase(Plan basePlan, string body, string mutant) =>
            IsCompatibleBase(basePlan.Body, basePlan.Mutant, body, mutant);

        internal static string NormalizeBody(string? body) => body?.Trim() ?? "";

        /// The lowest item quality automation installs for this plan's
        /// goals, inherited ones included (0 Awful = any). Applies only to
        /// implants whose items carry a quality.
        public int MinQuality { get; internal set; }

        /// The plan's own goals (inherited goals live only in the effective
        /// list). Read-only outside Core; the model edits MutableImplants.
        public IReadOnlyList<ImplantGoal> Implants => implants;

        internal List<ImplantGoal> MutableImplants => implants;
    }
}
