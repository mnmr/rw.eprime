using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    public enum ScopeKind
    {
        All,             // every colony pawn, including caravans
        CurrentLocation, // whatever location the player is looking at
        Location,        // one specific named location (ship or settlement)
    }

    /// A place colony pawns can be: a player-home map (ship or settlement).
    public sealed class LocationInfo
    {
        public string Id;     // stable per-session id (map unique id)
        public string Label;  // display name (settlement name / ship name)
        public bool IsShip;
    }

    public sealed class ScopeOption
    {
        public ScopeKind Kind;
        public string LocationId; // set for Kind == Location
        public string Label;      // set for Kind == Location; All/Current translate game-side
        public bool IsShip;       // set for Kind == Location
    }

    /// Scope selection over colony pawns by location. Also reused outside the
    /// colonist table (e.g. location-scoped role rules), so everything except
    /// pawn/map enumeration lives here.
    public static class ScopeEngine
    {
        /// The scope menu: All, Current Location, then ships A-Z, then
        /// settlements A-Z. Named entries only exist when the location does.
        public static List<ScopeOption> BuildOptions(IReadOnlyList<LocationInfo> locations)
        {
            var options = new List<ScopeOption>
            {
                new ScopeOption { Kind = ScopeKind.All },
                new ScopeOption { Kind = ScopeKind.CurrentLocation },
            };
            options.AddRange(locations
                .OrderByDescending(l => l.IsShip)
                .ThenBy(l => l.Label, System.StringComparer.OrdinalIgnoreCase)
                .Select(l => new ScopeOption
                {
                    Kind = ScopeKind.Location,
                    LocationId = l.Id,
                    Label = l.Label,
                    IsShip = l.IsShip,
                }));
            return options;
        }

        /// Whether a pawn falls inside the scope. Caravan pawns (no location)
        /// only appear under All.
        public static bool Matches(ScopeOption scope, string pawnLocationId, string currentLocationId)
        {
            switch (scope.Kind)
            {
                case ScopeKind.All: return true;
                case ScopeKind.CurrentLocation: return pawnLocationId != null && pawnLocationId == currentLocationId;
                default: return pawnLocationId != null && pawnLocationId == scope.LocationId;
            }
        }

        /// True when the listed pawns come from more than one place (several
        /// maps, or maps plus caravans) — colony planning needs a single
        /// location. Caravan pawns pass null and count as one shared bucket.
        public static bool SpansMultipleLocations(IEnumerable<string> pawnLocationIds)
        {
            string seen = null;
            bool any = false;
            foreach (var id in pawnLocationIds)
            {
                var bucket = id ?? "";
                if (!any) { seen = bucket; any = true; }
                else if (bucket != seen) return true;
            }
            return false;
        }

        /// Re-resolves a scope against current options: named locations that
        /// disappeared (abandoned settlement) fall back to Current Location.
        public static ScopeOption Revalidate(ScopeOption scope, IReadOnlyList<ScopeOption> options)
        {
            if (scope == null) return options.First(o => o.Kind == ScopeKind.CurrentLocation);
            if (scope.Kind != ScopeKind.Location) return scope;
            return options.FirstOrDefault(o => o.Kind == ScopeKind.Location && o.LocationId == scope.LocationId)
                ?? options.First(o => o.Kind == ScopeKind.CurrentLocation);
        }
    }
}
