using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Game-side adapters for the Core scope engine: enumerates the player's
    /// locations (ships and settlements) and places pawns in them. Also the
    /// scope-aware pawn enumerator for the colonist table.
    internal static class ColonyScope
    {
        internal static List<LocationInfo> Locations()
        {
            var result = new List<LocationInfo>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                bool ship = IsShipMap(map);
                // Unnamed ships fall back to a short label — the map parent's
                // ("Gravship landing site") overflows every dropdown.
                string label = ship
                    ? (GravshipUtility.TryGetNameOfGravshipOnMap(map, out var shipName)
                        ? shipName : "WR_ShipFallback".Translate().ToString())
                    : map.Parent?.LabelCap.ToString() ?? "?";
                result.Add(new LocationInfo { Id = LocationId(map), Label = label, IsShip = ship });
            }
            return result;
        }

        /// A gravship map that isn't parked at a settlement — a ship landed at
        /// one of the player's settlements counts as that settlement.
        internal static bool IsShipMap(Map map) =>
            GravshipUtility.PlayerHasGravEngine(map)
            && !(map.Parent is RimWorld.Planet.Settlement);

        internal static bool IsSettlementMap(Map map) => map.IsPlayerHome && !IsShipMap(map);

        /// The pawn's place, for Core location-rule matching.
        internal static PawnPlace PlaceOf(Pawn pawn)
        {
            var map = pawn.MapHeld;
            return new PawnPlace
            {
                LocationId = LocationId(map),
                IsSettlement = map != null && IsSettlementMap(map),
                IsShip = map != null && IsShipMap(map),
            };
        }

        internal static string LocationId(Map map) => map?.uniqueID.ToString();

        internal static string LocationIdOf(Pawn pawn) => LocationId(pawn.MapHeld);

        internal static string CurrentLocationId() => LocationId(Find.CurrentMap);

        /// Colonists and slaves within the scope (no babies): spawned map pawns,
        /// plus pawns travelling in player caravans under All.
        internal static List<Pawn> PawnsIn(ScopeOption scope)
        {
            var result = new List<Pawn>();
            string currentId = CurrentLocationId();
            foreach (var map in Find.Maps)
            {
                if (!ScopeEngine.Matches(scope, LocationId(map), currentId)) continue;
                result.AddRange(map.mapPawns.FreeColonistsSpawned);
                result.AddRange(map.mapPawns.SlavesOfColonySpawned);
            }
            if (scope.Kind == ScopeKind.All)
                foreach (var caravan in Find.WorldObjects.Caravans)
                {
                    if (!caravan.IsPlayerControlled) continue;
                    foreach (var pawn in caravan.PawnsListForReading)
                        if (pawn.IsFreeColonist || pawn.IsSlaveOfColony)
                            result.Add(pawn);
                }
            return result
                .Where(p => !p.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }

        internal static string LabelOf(ScopeOption option) =>
            option.Kind == ScopeKind.All ? "WR_ScopeAll".Translate().ToString()
            : option.Kind == ScopeKind.CurrentLocation ? "WR_ScopeCurrent".Translate().ToString()
            : option.Label;
    }

    /// One grouping the colonist table offers. Partition == null means the
    /// flat list; classify-backed sources section A-Z, membership-backed
    /// sources (Colony Groups) keep their own group order.
    internal sealed class GroupSourceDef
    {
        internal string Key;
        internal string Label;
        internal System.Func<List<Pawn>, List<GroupSection<Pawn>>> Partition;
    }

    internal static class GroupSources
    {
        private static List<GroupSourceDef> all;

        internal static List<GroupSourceDef> All() => all ??= Build();

        private static GroupSourceDef Classified(string key, string label,
            System.Func<Pawn, (string key, string title)> classify) =>
            new GroupSourceDef
            {
                Key = key,
                Label = label,
                Partition = pawns => GroupEngine.Partition(pawns, classify),
            };

        private static List<GroupSourceDef> Build()
        {
            var list = new List<GroupSourceDef>
            {
                new GroupSourceDef { Key = "none", Label = "WR_GroupNone".Translate() },
                Classified("faction", "WR_GroupFaction".Translate(), pawn =>
                {
                    var faction = pawn.HomeFaction ?? pawn.Faction;
                    string name = faction?.Name ?? pawn.kindDef.race.LabelCap.ToString();
                    return ("faction|" + name, name);
                }),
                Classified("race", "WR_GroupRace".Translate(), pawn =>
                {
                    string name = pawn.kindDef.race.LabelCap.ToString();
                    return ("race|" + name, name);
                }),
                Classified("gender", "WR_GroupGender".Translate(), pawn =>
                {
                    string name = pawn.gender.GetLabel().CapitalizeFirst();
                    return ("gender|" + pawn.gender, name);
                }),
            };
            if (ModsConfig.BiotechActive)
                list.Add(Classified("xenotype", "WR_GroupXenotype".Translate(), pawn =>
                {
                    string name = pawn.genes?.XenotypeLabelCap.ToString();
                    if (name.NullOrEmpty()) name = "?";
                    return ("xenotype|" + name, name);
                }));
            if (ModsConfig.IdeologyActive)
            {
                list.Add(Classified("ideo", "WR_GroupIdeo".Translate(), pawn =>
                {
                    string name = pawn.Ideo?.name ?? "?";
                    return ("ideo|" + name, name);
                }));
                list.Add(Classified("slaves", "WR_GroupSlaves".Translate(), pawn => pawn.IsSlave
                    ? ("slaves|1", "WR_GroupTitleSlaves".Translate().ToString())
                    : ("slaves|0", "WR_GroupTitleColonists".Translate().ToString())));
            }
            if (ColonyGroupsDataSource.Available)
                list.Add(new GroupSourceDef
                {
                    Key = "colonygroups",
                    Label = "WR_GroupColonyGroups".Translate(),
                    Partition = pawns => GroupEngine.PartitionByMembership(
                        pawns, ColonyGroupsDataSource.Groups(), "WR_GroupTitleUngrouped".Translate()),
                });
            return list;
        }
    }
}
