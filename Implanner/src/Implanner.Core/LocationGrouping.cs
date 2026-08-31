using System.Collections.Generic;
using System.Linq;

namespace Implanner.Core
{
    // Adapted from WorkRoles.Core.ScopeEngine (an intentional independent
    // copy). Implanner's location selector is a presentation grouping only:
    // it never affects what is planned or automated. Caravans and other
    // travelling groups appear as their own groupings.

    public enum GroupingKind
    {
        All,             // every enlisted pawn, including travellers
        CurrentLocation, // whatever location the player is looking at
        Location,        // one specific settlement, ship, or caravan
    }

    /// A named settlement, ship, or caravan in the grouping catalog.
    public sealed class GroupingInfo
    {
        public GroupingInfo(string id, string label, bool isShip, bool isCaravan)
        {
            Id = id;
            Label = label;
            IsShip = isShip;
            IsCaravan = isCaravan;
        }

        public string Id { get; }     // stable map, ship, or caravan identity
        public string Label { get; }  // display name
        public bool IsShip { get; }
        public bool IsCaravan { get; }
    }

    public sealed class GroupingOption
    {
        public GroupingKind Kind;
        public string? LocationId; // set for Kind == Location
        public string? Label;      // set for Kind == Location; All/Current translate game-side
    }

    /// Grouping selection over enlisted pawns by location.
    public static class LocationGrouping
    {
        /// Caravan location ids use this prefix; map/ship ids never do.
        public const string CaravanPrefix = "caravan:";

        /// The grouping menu: All, Current Location, then ships A-Z,
        /// settlements A-Z, caravans A-Z.
        public static List<GroupingOption> BuildOptions(IReadOnlyList<GroupingInfo> locations)
        {
            var options = new List<GroupingOption>
            {
                new GroupingOption { Kind = GroupingKind.All },
                new GroupingOption { Kind = GroupingKind.CurrentLocation },
            };
            options.AddRange(locations
                .OrderBy(l => l.IsCaravan)
                .ThenByDescending(l => l.IsShip)
                .ThenBy(l => l.Label, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l.Id, System.StringComparer.Ordinal)
                .Select(l => new GroupingOption
                {
                    Kind = GroupingKind.Location,
                    LocationId = l.Id,
                    Label = l.Label,
                }));
            return options;
        }

        /// Whether a pawn falls inside the grouping. Pawns nowhere (no map,
        /// no caravan) only appear under All.
        public static bool Matches(GroupingOption option, string? pawnLocationId, string currentLocationId)
        {
            switch (option.Kind)
            {
                case GroupingKind.All: return true;
                case GroupingKind.CurrentLocation: return pawnLocationId != null && pawnLocationId == currentLocationId;
                default: return pawnLocationId != null && pawnLocationId == option.LocationId;
            }
        }

        /// Re-resolves a selection against current options: locations that
        /// disappeared (abandoned settlement, arrived caravan) fall back to
        /// Current Location.
        public static GroupingOption Revalidate(GroupingOption? option, IReadOnlyList<GroupingOption> options)
        {
            if (option == null) return options.First(o => o.Kind == GroupingKind.CurrentLocation);
            if (option.Kind != GroupingKind.Location) return option;
            return options.FirstOrDefault(o => o.Kind == GroupingKind.Location && o.LocationId == option.LocationId)
                ?? options.First(o => o.Kind == GroupingKind.CurrentLocation);
        }
    }
}
