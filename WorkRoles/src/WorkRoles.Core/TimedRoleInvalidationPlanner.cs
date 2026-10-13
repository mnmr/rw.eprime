using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RimShared.Common;

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
            ActiveHours = hasTimeRule ? 0 : 0xFFFFFF;
        }

        public TimedRoleInvalidationSource(int roleId, int activeHours, bool enabled = true)
            : this(roleId, (activeHours & 0xFFFFFF) != 0xFFFFFF, enabled)
        {
            ActiveHours = activeHours & 0xFFFFFF;
        }

        public int RoleId { get; }
        public bool HasTimeRule { get; }
        public bool Enabled { get; }
        public bool Blocker { get; }
        public bool AutoAssign { get; }
        public int ActiveHours { get; }
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
            State = enabled ? AssignmentState.Enabled : AssignmentState.Disabled;
            PreviousHour = int.MinValue;
            CurrentHour = -1;
            LocationMatches = true;
        }

        public TimedRoleHolderAssignment(TPawn pawn, int stableOrder, int roleId,
            AssignmentState state, int previousHour, int currentHour, bool locationMatches = true)
            : this(pawn, stableOrder, roleId, state != AssignmentState.Disabled)
        {
            State = state;
            PreviousHour = previousHour;
            CurrentHour = currentHour;
            LocationMatches = locationMatches;
        }

        public TPawn Pawn { get; }
        public int StableOrder { get; }
        public int RoleId { get; }
        public bool Enabled { get; }
        public bool Pinned { get; }
        public AssignmentState State { get; }
        // int.MinValue means there is no prior compiled observation; -1 is a
        // known context without a local clock, where time rules do not suppress.
        public int PreviousHour { get; }
        public int CurrentHour { get; }
        public bool LocationMatches { get; }
    }

    public sealed class TimedRoleInvalidationPlan<TPawn> where TPawn : class
    {
        internal TimedRoleInvalidationPlan(IEnumerable<int> roleIds, IEnumerable<TPawn> pawns,
            IEnumerable<TPawn>? presentationPawns = null)
        {
            RoleIds = new ReadOnlyCollection<int>(roleIds.ToList());
            Pawns = new ReadOnlyCollection<TPawn>(pawns.ToList());
            PresentationPawns = new ReadOnlyCollection<TPawn>((presentationPawns ?? pawns).ToList());
        }

        public IReadOnlyList<int> RoleIds { get; }
        public IReadOnlyList<TPawn> Pawns { get; }
        public IReadOnlyList<TPawn> PresentationPawns { get; }

        /// Applies the fully planned batch in publication order. A changed rule
        /// result can update presentation while a disabled/suppressed assignment
        /// leaves compiled work untouched; those owners are PresentationPawns.
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

        /// Applies only runtime effects of an hour transition. Timed rules can
        /// change a pawn's compiled order, but they cannot change role data
        /// derived solely from job entries and definitions.
        public void ApplyRuntime(Action<TPawn> publishRuntime, Action<TPawn> invalidateOrders)
        {
            if (publishRuntime == null) throw new ArgumentNullException(nameof(publishRuntime));
            if (invalidateOrders == null) throw new ArgumentNullException(nameof(invalidateOrders));

            for (int i = 0; i < PresentationPawns.Count; i++) publishRuntime(PresentationPawns[i]);
            for (int i = 0; i < Pawns.Count; i++) invalidateOrders(Pawns[i]);
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

            var timedRoles = new Dictionary<int, TimedRoleInvalidationSource>();
            foreach (TimedRoleInvalidationSource role in roles)
                if (role.HasTimeRule)
                    timedRoles[role.RoleId] = role;

            if (timedRoles.Count == 0)
                return new TimedRoleInvalidationPlan<TPawn>(
                    Array.Empty<int>(), Array.Empty<TPawn>());

            var selected = new Dictionary<TPawn, PawnCandidate<TPawn>>(
                ReferenceIdentityComparer<TPawn>.Instance);
            var presentation = new Dictionary<TPawn, PawnCandidate<TPawn>>(
                ReferenceIdentityComparer<TPawn>.Instance);
            int nextOrdinal = 0;
            foreach (TimedRoleHolderAssignment<TPawn> assignment in assignments)
            {
                if (assignment.Pawn == null || !timedRoles.TryGetValue(assignment.RoleId, out var role))
                    continue;
                if (assignment.PreviousHour != int.MinValue
                    && HoursPass(role.ActiveHours, assignment.PreviousHour)
                        == HoursPass(role.ActiveHours, assignment.CurrentHour)) continue;
                int ordinal = nextOrdinal++;
                Add(presentation, assignment, ordinal);
                if (RoleActivation.IsActive(role.Enabled, assignment.State) && assignment.LocationMatches)
                    Add(selected, assignment, ordinal);
            }

            return new TimedRoleInvalidationPlan<TPawn>(
                timedRoles.Keys.OrderBy(id => id), Ordered(selected), Ordered(presentation));
        }

        public static bool HoursPass(int activeHours, int hour) =>
            hour < 0 || (activeHours & (1 << hour)) != 0;

        private static IEnumerable<TPawn> Ordered<TPawn>(Dictionary<TPawn, PawnCandidate<TPawn>> values)
            where TPawn : class => values.Values.OrderBy(candidate => candidate.StableOrder)
                .ThenBy(candidate => candidate.FirstSeenOrdinal).Select(candidate => candidate.Pawn);

        private static void Add<TPawn>(Dictionary<TPawn, PawnCandidate<TPawn>> selected,
            TimedRoleHolderAssignment<TPawn> assignment, int ordinal) where TPawn : class
        {
            if (selected.TryGetValue(assignment.Pawn, out PawnCandidate<TPawn> existing))
            {
                if (assignment.StableOrder < existing.StableOrder)
                    selected[assignment.Pawn] = new PawnCandidate<TPawn>(assignment.Pawn,
                        assignment.StableOrder, existing.FirstSeenOrdinal);
            }
            else selected.Add(assignment.Pawn, new PawnCandidate<TPawn>(assignment.Pawn,
                assignment.StableOrder, ordinal));
        }
    }
}
