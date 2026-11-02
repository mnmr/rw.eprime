using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// How a plannable body relates to the human body by the catalog slots
    /// it carries (owner, 2026-11-01): a race whose implant slots are the
    /// human ones is the human body for planning purposes, a race carrying
    /// every human slot plus more may join human plans and still be offered
    /// as its own body (its extra slots can be planned), and a race missing
    /// human slots is a body of its own.
    public enum BodyKinship : byte
    {
        /// Lacks at least one human slot (Android Tiers' androids).
        Distinct = 0,

        /// The human body itself.
        Human = 1,

        /// Exactly the human slots under another body name (a race mod's
        /// copy of the human body): folded under the human body.
        HumanEquivalent = 2,

        /// Every human slot and more (Milira: wings): accepted by plans for
        /// the human body, listed as a body of its own.
        HumanSuperset = 3,
    }

    public static class BodyKinships
    {
        /// Classifies a body by its catalog slot keys against the human
        /// body's. The human body passes its own keys and is never
        /// classified here.
        public static BodyKinship Classify(
            IReadOnlyCollection<string> humanSlots, IReadOnlyCollection<string> bodySlots)
        {
            var body = new HashSet<string>(bodySlots, StringComparer.Ordinal);
            foreach (string slot in humanSlots)
                if (!body.Contains(slot)) return BodyKinship.Distinct;
            return body.Count == humanSlots.Count
                ? BodyKinship.HumanEquivalent
                : BodyKinship.HumanSuperset;
        }

        /// Whether a colonist with pawnBody may be enlisted in (and is
        /// evaluated against) a plan of planBody ("" = any body). A plan for
        /// the human body, or for a body folded under it, takes every body
        /// of the human family; any other pinned plan takes its own body.
        public static bool Accepts(string planBody, BodyKinship planKinship,
            string pawnBody, BodyKinship pawnKinship)
        {
            if (planBody.Length == 0) return true;
            if (string.Equals(planBody, pawnBody, StringComparison.Ordinal)) return true;
            return (planKinship == BodyKinship.Human || planKinship == BodyKinship.HumanEquivalent)
                && pawnKinship != BodyKinship.Distinct;
        }

        /// Whether the body is listed as a body of its own (body filter,
        /// new-plan dialog): everything but a human equivalent.
        public static bool Offered(BodyKinship kinship) => kinship != BodyKinship.HumanEquivalent;
    }
}
