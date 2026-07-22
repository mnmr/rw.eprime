using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Where a pawn currently is, for location-rule matching.
    public struct PawnPlace
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
            for (int i = 0; i < tokens.Count; i++)
                if (MatchesOne(tokens[i], place))
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
            if (token.StartsWith(SettlementPrefix, System.StringComparison.Ordinal))
                return place.IsSettlement && MatchesId(token, SettlementPrefix.Length, place.LocationId);
            if (token.StartsWith(ShipPrefix, System.StringComparison.Ordinal))
                return place.IsShip && MatchesId(token, ShipPrefix.Length, place.LocationId);
            return false;
        }

        private static bool MatchesId(string token, int prefixLength, string locationId) =>
            locationId != null
            && token.Length == prefixLength + locationId.Length
            && string.CompareOrdinal(token, prefixLength,
                locationId, 0, locationId.Length) == 0;
    }
}
