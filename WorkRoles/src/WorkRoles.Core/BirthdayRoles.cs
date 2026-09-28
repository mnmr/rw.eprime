using System.Collections.Generic;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core
{
    /// A role's age gates as the birthday rule sees them (years; 0 or less =
    /// no gate on that end, see AgeBands.Admits).
    public readonly struct AgeGatedRole
    {
        public AgeGatedRole(int id, bool autoAssign, int minAge, int maxAge)
        {
            Id = id;
            AutoAssign = autoAssign;
            MinAge = minAge;
            MaxAge = maxAge;
        }

        public int Id { get; }
        public bool AutoAssign { get; }
        public int MinAge { get; }
        public int MaxAge { get; }
    }

    public sealed class BirthdayRoleChanges
    {
        public static readonly BirthdayRoleChanges None =
            new BirthdayRoleChanges(new List<int>(), new List<int>());

        public BirthdayRoleChanges(List<int> gained, List<int> lost)
        {
            Gained = gained;
            Lost = lost;
        }

        /// Role ids to assign, in store order.
        public IReadOnlyList<int> Gained { get; }
        /// Held role ids to remove.
        public IReadOnlyList<int> Lost { get; }
        public bool IsEmpty => Gained.Count == 0 && Lost.Count == 0;
    }

    /// What a biological birthday changes in a pawn's role set. Only roles
    /// whose age gates flip at this birthday are touched, so earlier player
    /// choices stand: an auto-assign role removed while it already fit is not
    /// re-added, and a role deliberately held outside its gates is kept.
    /// Auto-assign roles the pawn now enters are gained; any held role the
    /// pawn has aged out of is lost. Pawns the game does not age-gate never
    /// change.
    public static class BirthdayRoles
    {
        /// Gates never exceed AgeBands.OldestGate, so the last birthday that
        /// can flip one is the year after it (leaving an inclusive cap).
        /// Callers check this before gathering any role data.
        public static bool CanChangeRoles(int birthdayAge) =>
            birthdayAge > 0 && birthdayAge <= AgeBands.OldestGate + 1;

        public static BirthdayRoleChanges Plan(IReadOnlyList<AgeGatedRole> roles,
            ICollection<int> heldRoleIds, int birthdayAge, bool ageLimitsApply)
        {
            if (!ageLimitsApply || !CanChangeRoles(birthdayAge)) return BirthdayRoleChanges.None;
            long nowTicks = birthdayAge * BiologicalAge.TicksPerYear;
            long beforeTicks = nowTicks - 1;
            List<int>? gained = null;
            List<int>? lost = null;
            for (int index = 0; index < roles.Count; index++)
            {
                AgeGatedRole role = roles[index];
                bool before = AgeBands.Admits(role.MinAge, role.MaxAge, beforeTicks, true);
                bool now = AgeBands.Admits(role.MinAge, role.MaxAge, nowTicks, true);
                if (before == now) continue;
                bool held = heldRoleIds.Contains(role.Id);
                if (now && !held && role.AutoAssign)
                    (gained ??= new List<int>()).Add(role.Id);
                else if (!now && held)
                    (lost ??= new List<int>()).Add(role.Id);
            }
            return gained == null && lost == null
                ? BirthdayRoleChanges.None
                : new BirthdayRoleChanges(gained ?? new List<int>(), lost ?? new List<int>());
        }
    }
}
