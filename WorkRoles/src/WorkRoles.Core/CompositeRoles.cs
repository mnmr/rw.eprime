using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Facts the composite membership policy needs about a candidate member.
    public readonly struct CompositeMemberFacts
    {
        public CompositeMemberFacts(bool exists, bool composite, bool hasRules)
        {
            Exists = exists;
            Composite = composite;
            HasRules = hasRules;
        }

        public bool Exists { get; }
        public bool Composite { get; }
        public bool HasRules { get; }
    }

    /// Membership and expansion policy for composite roles. A composite holds an
    /// ordered list of member roles instead of job entries. Members must exist,
    /// must not be composites themselves (depth 1, so cycles cannot form), and
    /// must not carry hour/location rules: the composite's own rules gate the
    /// whole bundle. Blocker members are allowed and keep their veto semantics,
    /// so assigning a composite equals assigning its members independently.
    public static class CompositeRoles
    {
        public static bool IsEligibleMember(int compositeId, int memberId,
            CompositeMemberFacts member)
            => memberId != compositeId && member.Exists
               && !member.Composite && !member.HasRules;

        /// Drops ineligible and duplicate ids in place, preserving order. Load
        /// sanitize, import resolution, and synced commands share this rule.
        public static bool SanitizeMembers(List<int> memberIds, int compositeId,
            Func<int, CompositeMemberFacts> factsOf)
        {
            if (memberIds == null || memberIds.Count == 0) return false;
            bool changed = false;
            var seen = new HashSet<int>();
            for (int i = 0; i < memberIds.Count;)
            {
                int memberId = memberIds[i];
                if (!seen.Add(memberId)
                    || !IsEligibleMember(compositeId, memberId, factsOf(memberId)))
                {
                    memberIds.RemoveAt(i);
                    changed = true;
                    continue;
                }
                i++;
            }
            return changed;
        }

        /// Whether one member contributes a compile slice, and that slice's veto
        /// flag. Disabled members contribute nothing (the global role toggle
        /// applies inside composites too); a blocker composite turns every
        /// member slice into a veto, and a blocker member stays one regardless.
        public static bool TryGetMemberSlice(bool compositeBlocker,
            bool memberEnabled, bool memberBlocker, out bool sliceBlocker)
        {
            sliceBlocker = compositeBlocker || memberBlocker;
            return memberEnabled;
        }
    }
}
