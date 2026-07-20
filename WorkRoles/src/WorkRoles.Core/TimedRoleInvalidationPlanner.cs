using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WorkRoles.Core
{
    public readonly struct TimedRoleInvalidationSource
    {
        public TimedRoleInvalidationSource(int roleId, bool hasTimeRule,
            bool enabled = true, bool blocker = false, bool autoAssign = false)
        {
            RoleId = roleId;
            HasTimeRule = hasTimeRule;
            Enabled = enabled;
            Blocker = blocker;
            AutoAssign = autoAssign;
        }

        public int RoleId { get; }
        public bool HasTimeRule { get; }
        public bool Enabled { get; }
        public bool Blocker { get; }
        public bool AutoAssign { get; }
    }

    public readonly struct TimedRoleHolderAssignment<TPawn> where TPawn : class
    {
        public TimedRoleHolderAssignment(TPawn pawn, int stableOrder, int roleId,
            bool enabled = true, bool pinned = false)
        {
            Pawn = pawn;
            StableOrder = stableOrder;
            RoleId = roleId;
            Enabled = enabled;
            Pinned = pinned;
        }

        public TPawn Pawn { get; }
        public int StableOrder { get; }
        public int RoleId { get; }
        public bool Enabled { get; }
        public bool Pinned { get; }
    }

    public sealed class TimedRoleInvalidationPlan<TPawn> where TPawn : class
    {
        internal TimedRoleInvalidationPlan(IEnumerable<int> roleIds, IEnumerable<TPawn> pawns)
        {
            RoleIds = new ReadOnlyCollection<int>(roleIds.ToList());
            Pawns = new ReadOnlyCollection<TPawn>(pawns.ToList());
        }

        public IReadOnlyList<int> RoleIds { get; }
        public IReadOnlyList<TPawn> Pawns { get; }

        /// Applies the fully planned batch in publication order. Enabled,
        /// blocker, auto-assignment and pin state intentionally do not affect
        /// membership: toggling those states owns its own invalidation path.
        public void Apply(Action<int> invalidateRole,
            Action<TPawn> invalidatePawn, Action complete)
        {
            if (invalidateRole == null) throw new ArgumentNullException(nameof(invalidateRole));
            if (invalidatePawn == null) throw new ArgumentNullException(nameof(invalidatePawn));
            if (complete == null) throw new ArgumentNullException(nameof(complete));
            if (RoleIds.Count == 0) return;

            for (int i = 0; i < RoleIds.Count; i++) invalidateRole(RoleIds[i]);
            for (int i = 0; i < Pawns.Count; i++) invalidatePawn(Pawns[i]);
            complete();
        }
    }

    public static class TimedRoleInvalidationPlanner
    {
        private readonly struct PawnCandidate<TPawn> where TPawn : class
        {
            public PawnCandidate(TPawn pawn, int stableOrder, int firstSeenOrdinal)
            {
                Pawn = pawn;
                StableOrder = stableOrder;
                FirstSeenOrdinal = firstSeenOrdinal;
            }

            public TPawn Pawn { get; }
            public int StableOrder { get; }
            public int FirstSeenOrdinal { get; }
        }

        public static TimedRoleInvalidationPlan<TPawn> Plan<TPawn>(
            IEnumerable<TimedRoleInvalidationSource> roles,
            IEnumerable<TimedRoleHolderAssignment<TPawn>> assignments)
            where TPawn : class
        {
            if (roles == null) throw new ArgumentNullException(nameof(roles));
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));

            var timedRoleIds = new HashSet<int>();
            foreach (TimedRoleInvalidationSource role in roles)
                if (role.HasTimeRule)
                    timedRoleIds.Add(role.RoleId);

            if (timedRoleIds.Count == 0)
                return new TimedRoleInvalidationPlan<TPawn>(
                    Array.Empty<int>(), Array.Empty<TPawn>());

            var selected = new Dictionary<TPawn, PawnCandidate<TPawn>>(
                ReferenceIdentityComparer<TPawn>.Instance);
            int nextOrdinal = 0;
            foreach (TimedRoleHolderAssignment<TPawn> assignment in assignments)
            {
                if (assignment.Pawn == null || !timedRoleIds.Contains(assignment.RoleId))
                    continue;

                if (selected.TryGetValue(assignment.Pawn, out PawnCandidate<TPawn> existing))
                {
                    if (assignment.StableOrder < existing.StableOrder)
                        selected[assignment.Pawn] = new PawnCandidate<TPawn>(
                            assignment.Pawn, assignment.StableOrder,
                            existing.FirstSeenOrdinal);
                }
                else
                    selected.Add(assignment.Pawn,
                        new PawnCandidate<TPawn>(assignment.Pawn,
                            assignment.StableOrder, nextOrdinal++));
            }

            return new TimedRoleInvalidationPlan<TPawn>(
                timedRoleIds.OrderBy(id => id),
                selected.Values.OrderBy(candidate => candidate.StableOrder)
                    .ThenBy(candidate => candidate.FirstSeenOrdinal)
                    .Select(candidate => candidate.Pawn));
        }
    }
}
