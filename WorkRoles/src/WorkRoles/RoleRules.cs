using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Which rule (if any) suppresses a role for a pawn right now.
    public enum RuleFailReason
    {
        None,
        OutsideHours,
        WrongLocation
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
            if (role.locationTokens.Count > 0
                && !LocationRules.Matches(role.locationTokens, ColonyScope.PlaceOf(pawn)))
                return RuleFailReason.WrongLocation;
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
    }
}
