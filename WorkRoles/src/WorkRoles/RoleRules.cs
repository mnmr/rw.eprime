using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WorkRoles
{
    /// Which rule (if any) suppresses a role for a pawn right now.
    public enum RuleFailReason
    {
        None,
        OutsideHours,
        AwayFromHome,   // HomeOnly role while the pawn is away
        AtHome          // AwayOnly role while the pawn is home
    }

    public static class RoleRules
    {
        /// Rules pass when the role is active in the pawn's local hour and at the
        /// pawn's current location. Suppression is absolute; gates chain with the
        /// global and per-pawn toggles.
        public static bool Pass(Role role, Pawn pawn) => FailReason(role, pawn) == RuleFailReason.None;

        /// Returns the first failing rule (hours before location), or None.
        public static RuleFailReason FailReason(Role role, Pawn pawn)
        {
            if (!role.HasRules) return RuleFailReason.None;
            if (role.activeHours != Role.AllHours)
            {
                int hour = LocalHour(pawn);
                if (hour >= 0 && (role.activeHours & (1 << hour)) == 0) return RuleFailReason.OutsideHours;
            }
            if (role.location != RoleLocation.Any)
            {
                bool home = IsAtHome(pawn);
                if (role.location == RoleLocation.HomeOnly && !home) return RuleFailReason.AwayFromHome;
                if (role.location == RoleLocation.AwayOnly && home) return RuleFailReason.AtHome;
            }
            return RuleFailReason.None;
        }

        public static int LocalHour(Pawn pawn)
        {
            var map = pawn.MapHeld;
            if (map != null) return GenLocalDate.HourInteger(map);
            var caravan = pawn.GetCaravan();
            if (caravan != null) return GenLocalDate.HourInteger(caravan.Tile);
            return -1; // unknown context: time rule does not suppress
        }

        public static bool IsAtHome(Pawn pawn)
        {
            var map = pawn.MapHeld;
            if (map != null) return map.IsPlayerHome;
            return false; // caravans and other off-map states count as away
        }
    }
}
