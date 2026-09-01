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
        All,      // every enlisted pawn, including travellers
        Location, // one specific settlement, ship, or caravan
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
        public string? Label;      // set for Kind == Location; All translates game-side
    }

    /// Grouping selection over enlisted pawns by location.
    public static class LocationGrouping
    {
        /// Caravan location ids use this prefix; map/ship ids never do.
        public const string CaravanPrefix = "caravan:";

        /// The grouping menu: All, then ships A-Z, settlements A-Z,
        /// caravans A-Z. Every location appears under its own name; the
        /// viewed location is the default selection, not a separate entry.
        public static List<GroupingOption> BuildOptions(IReadOnlyList<GroupingInfo> locations)
        {
            var options = new List<GroupingOption>
            {
                new GroupingOption { Kind = GroupingKind.All },
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
        public static bool Matches(GroupingOption option, string? pawnLocationId)
        {
            if (option.Kind == GroupingKind.All) return true;
            return pawnLocationId != null && pawnLocationId == option.LocationId;
        }

        /// Re-resolves a selection against current options. No selection
        /// yet, and locations that disappeared (abandoned settlement,
        /// arrived caravan), resolve to the location the player is
        /// currently viewing, falling back to All when the viewed map is
        /// not a listed location.
        public static GroupingOption Revalidate(GroupingOption? option,
            IReadOnlyList<GroupingOption> options, string currentLocationId)
        {
            if (option != null && option.Kind == GroupingKind.All) return option;
            string? wanted = option?.LocationId ?? currentLocationId;
            return options.FirstOrDefault(o => o.Kind == GroupingKind.Location
                    && o.LocationId == wanted)
                ?? options.FirstOrDefault(o => o.Kind == GroupingKind.Location
                    && o.LocationId == currentLocationId)
                ?? options.First(o => o.Kind == GroupingKind.All);
        }
    }
}
