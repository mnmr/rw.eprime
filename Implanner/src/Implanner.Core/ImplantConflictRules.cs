using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Everything the conflict rules need about one planned implant slot,
    /// extracted from game data by the caller (reference-body record indices,
    /// hediff tags, recipe incompatibility tags). Deliberately raw: no
    /// conflict decisions are made during extraction.
    public sealed class PlannedSlotFacts
    {
        public PlannedSlotFacts(string defName, bool isReplacement,
            int slotRecord, IReadOnlyList<int> slotAncestors,
            IReadOnlyList<string> tags, IReadOnlyList<string> incompatibleTags)
        {
            DefName = defName;
            IsReplacement = isReplacement;
            SlotRecord = slotRecord;
            SlotAncestors = slotAncestors;
            Tags = tags;
            IncompatibleTags = incompatibleTags;
        }

        public string DefName { get; }

        /// Recipe_InstallArtificialBodyPart replaces the part; anything else
        /// mounts on it.
        public bool IsReplacement { get; }

        /// The targeted anatomy instance (reference-body record index).
        public int SlotRecord { get; }

        /// Record indices from the slot's parent up to the body root.
        public IReadOnlyList<int> SlotAncestors { get; }

        /// HediffDef.tags of the implant.
        public IReadOnlyList<string> Tags { get; }

        /// Union of the surgery recipes' incompatibleWithHediffTags.
        public IReadOnlyList<string> IncompatibleTags { get; }
    }

    /// Deterministic implant-combination rules, mirroring what the game's
    /// surgery workers actually allow (verified in RimWorld source):
    ///
    /// - Recipe_InstallImplant refuses a part that is, or sits under, an
    ///   artificial part (PartOrAnyAncestorHasDirectlyAddedParts), and
    ///   refuses a part carrying a hediff whose tags match the recipe's
    ///   incompatibleWithHediffTags (the skin-gland mechanism).
    /// - Recipe_InstallArtificialBodyPart restores the part first, destroying
    ///   every hediff mounted on it or on its children.
    ///
    /// Two planned slots therefore conflict when only one of them can ever be
    /// present, whatever the surgery order.
    public static class ImplantConflictRules
    {
        public static bool Conflicts(PlannedSlotFacts a, PlannedSlotFacts b)
        {
            if (a.SlotRecord == b.SlotRecord)
            {
                // One part per slot: a replacement occupies the slot, and an
                // implant cannot mount on an added part (nor survive the
                // replacement being installed after it).
                if (a.IsReplacement || b.IsReplacement) return true;
                // Same-part implants coexist (multiple brain implants) unless
                // either recipe declares the other's hediff tags incompatible.
                return TagsClash(a.IncompatibleTags, b.Tags)
                    || TagsClash(b.IncompatibleTags, a.Tags);
            }
            // A replacement clears its whole subtree: anything planned on a
            // descendant slot can never coexist with it.
            if (a.IsReplacement && IsAncestorOf(a.SlotRecord, b)) return true;
            if (b.IsReplacement && IsAncestorOf(b.SlotRecord, a)) return true;
            return false;
        }

        static bool IsAncestorOf(int record, PlannedSlotFacts descendant)
        {
            IReadOnlyList<int> ancestors = descendant.SlotAncestors;
            for (int i = 0; i < ancestors.Count; i++)
                if (ancestors[i] == record)
                    return true;
            return false;
        }

        /// The game compares tags case-insensitively
        /// (RecipeDef.CompatibleWithHediff). Public: the game-side
        /// installed-vs-planned exclusivity gate applies the same rule.
        public static bool TagsClash(
            IReadOnlyList<string> incompatible, IReadOnlyList<string> tags)
        {
            for (int i = 0; i < incompatible.Count; i++)
                for (int j = 0; j < tags.Count; j++)
                    if (string.Equals(incompatible[i], tags[j],
                            StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }
    }
}
