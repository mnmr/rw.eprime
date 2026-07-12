using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Where a pawn currently is, for location-rule matching.
    public sealed class PawnPlace
    {
        public string LocationId;  // map id; null when off-map (caravan, world)
        public bool IsSettlement;  // a player settlement map
        public bool IsShip;        // a gravship map not parked at a settlement
        // Neither flag set: caravan or any non-home map — the Caravans bucket.
    }

    /// Role location rules: a restricted role carries tokens and is active
    /// wherever any token matches; no tokens means anywhere.
    public static class LocationRules
    {
        public const string Settlements = "settlements";       // any player settlement
        public const string Caravans = "caravans";             // caravans + anywhere that is no home
        public const string SettlementPrefix = "settlement:";  // one settlement by id
        public const string ShipPrefix = "ship:";              // one ship by id

        public static bool Matches(IReadOnlyList<string> tokens, PawnPlace place)
        {
            if (tokens == null || tokens.Count == 0) return true;
            foreach (var token in tokens)
                if (MatchesOne(token, place))
                    return true;
            return false;
        }

        /// Unknown or stale tokens (a deleted settlement's id) never match.
        private static bool MatchesOne(string token, PawnPlace place)
        {
            switch (token)
            {
                case Settlements: return place.IsSettlement;
                case Caravans: return !place.IsSettlement && !place.IsShip;
            }
            if (token.StartsWith(SettlementPrefix))
                return place.IsSettlement && place.LocationId == token.Substring(SettlementPrefix.Length);
            if (token.StartsWith(ShipPrefix))
                return place.IsShip && place.LocationId == token.Substring(ShipPrefix.Length);
            return false;
        }
    }
}
