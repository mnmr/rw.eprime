using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public sealed class RecommendationOrderSnapshot
    {
        internal RecommendationOrderSnapshot(IReadOnlyList<RoleView> roles)
        {
            Roles = roles;
            Template = OrderTemplate.DeriveTemplate(roles);
            Positions = Ordering.BasePositions(roles, Template);
            AddCandidates = OrderTemplate.AddCandidates(roles, Template);
        }
        /// Ordering-only facts. Equal ordering projections retain this snapshot
        /// even when unrelated policy scalars changed in the current catalog.
        public IReadOnlyList<RoleView> Roles { get; }
        public IReadOnlyList<int> Template { get; }
        public IReadOnlyDictionary<int, long> Positions { get; }
        public IReadOnlyList<int> AddCandidates { get; }
    }

    // Owner: store-scoped adapter, at most two full/training input variants. Key:
    // store identity and detached role projection.
    // Value: immutable template/positions/add choices. Dependencies: role ids,
    // template identity, pinnability, coverage, hunting/natural priority and
    // unskilled placement flags. Legacy saved orders are not consumed.
    // Refresh: event-gated read.
    // Equality: equal consumed inputs preserve all derived identities. Teardown:
    // Reset/store replacement drops the prior owner and detached facts.
    public sealed class RecommendationOrderCache
    {
        private object? owner;
        private Entry? recent, other;
        public RecommendationOrderSnapshot For(object owner, IReadOnlyList<RoleView> roles,
            int revision, IReadOnlyList<int> stored)
        {
            // Retain the legacy call shape for saved-order consumers, but no
            // longer let old settings override the automatic template or
            // invalidate it. In particular, Hunter stays dynamically placed.
            if (!ReferenceEquals(this.owner, owner)) { Reset(); this.owner = owner; }
            if (recent != null && ReferenceEquals(recent.Roles, roles))
                return recent.Snapshot;
            if (other != null && ReferenceEquals(other.Roles, roles))
            {
                Entry matched = other;
                other = recent;
                recent = matched;
                return matched.Snapshot;
            }
            RecommendationOrderSnapshot snapshot = recent != null
                && InputsEqual(recent.Roles, roles) ? recent.Snapshot
                : other != null && InputsEqual(other.Roles, roles) ? other.Snapshot
                : new RecommendationOrderSnapshot(roles);
            other = recent;
            recent = new Entry(roles, snapshot);
            return snapshot;
        }
        public void Reset() { owner = null; recent = other = null; }

        private sealed class Entry
        {
            internal Entry(IReadOnlyList<RoleView> roles, RecommendationOrderSnapshot snapshot)
            { Roles = roles; Snapshot = snapshot; }
            internal readonly IReadOnlyList<RoleView> Roles;
            internal readonly RecommendationOrderSnapshot Snapshot;
        }

        private static bool InputsEqual(IReadOnlyList<RoleView> left, IReadOnlyList<RoleView> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                RoleView a = left[i], b = right[i];
                if (ReferenceEquals(a, b)) continue;
                if (a.Id != b.Id || a.TemplateDefName != b.TemplateDefName
                    || OrderTemplate.IsPinnable(a) != OrderTemplate.IsPinnable(b)
                    || a.Blocker != b.Blocker || a.HasRules != b.HasRules
                    || a.Hunting != b.Hunting || a.NaturalPriority != b.NaturalPriority
                    || a.UseUnskilledPlacementRules != b.UseUnskilledPlacementRules
                    || a.PreserveRecommendationOrder != b.PreserveRecommendationOrder
                    || !a.Coverage.SetEquals(b.Coverage)) return false;
            }
            return true;
        }
    }
}
