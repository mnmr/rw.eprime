using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Implanner
{
    /// One serviceable colony: the canonical ground map, every floor or
    /// pocket map that canonicalizes to it, the planable colonists held on
    /// any of those maps, and the colony's spawned haulable items. All lists
    /// are sorted by stable ids, so iteration is deterministic.
    internal sealed class Colony
    {
        internal Map CanonicalMap = null!;
        internal string LocationId = "";
        internal readonly List<Map> Maps = new List<Map>();
        internal readonly List<int> PawnIds = new List<int>();
        internal readonly List<int> ItemIds = new List<int>();
    }

    /// The reconciliation pass's single source of colony structure: which
    /// maps form a colony and which colonists and items are at it. All
    /// floor/pocket-map canonicalization and faction resolution happen here,
    /// once per pass — consumers never touch FloorMaps or per-client view
    /// state. Built exclusively from authoritative synchronized state with
    /// ColonyScope.AuthoritativeFaction, so every multiplayer client derives
    /// the identical index from the same tick. Pass-scoped: built at the top
    /// of a reconcile pass, discarded with it, never cached.
    internal sealed class ColonyIndex
    {
        /// Sorted by canonical map id.
        internal readonly List<Colony> Colonies = new List<Colony>();

        /// Every planable colonist, including pawns away from any colony
        /// (caravans, non-serviceable maps).
        internal readonly Dictionary<int, Pawn> PawnsById =
            new Dictionary<int, Pawn>();

        /// Every spawned haulable item on any colony map.
        internal readonly Dictionary<int, Thing> ItemsById =
            new Dictionary<int, Thing>();

        private readonly Dictionary<int, int> colonyOfPawn =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> colonyOfItem =
            new Dictionary<int, int>();
        private readonly Dictionary<Map, int> colonyOfCanonical =
            new Dictionary<Map, int>();

        /// The colony the pawn is currently at; null while away.
        internal Colony? ColonyOfPawn(int pawnId) =>
            colonyOfPawn.TryGetValue(pawnId, out int index)
                ? Colonies[index]
                : null;

        internal Colony? ColonyOfItem(int itemId) =>
            colonyOfItem.TryGetValue(itemId, out int index)
                ? Colonies[index]
                : null;

        internal Colony? ByCanonicalMap(Map? canonical) =>
            canonical != null
                && colonyOfCanonical.TryGetValue(canonical, out int index)
                ? Colonies[index]
                : null;

        /// Whether the pawn is present at the item's colony.
        internal bool SameColony(int pawnId, int itemId) =>
            colonyOfPawn.TryGetValue(pawnId, out int pawnColony)
            && colonyOfItem.TryGetValue(itemId, out int itemColony)
            && pawnColony == itemColony;

        /// Whether a reserved item is still collectable by its pawn: the
        /// pawn is either away (may return) or present at the item's colony.
        /// A pawn present at a DIFFERENT colony can never receive the item —
        /// medical ingredient searches never leave the patient's map stack.
        internal bool PawnMayCollect(int pawnId, int itemId)
        {
            if (!colonyOfPawn.TryGetValue(pawnId, out int pawnColony))
                return true;
            return colonyOfItem.TryGetValue(itemId, out int itemColony)
                && pawnColony == itemColony;
        }

        internal static ColonyIndex Build()
        {
            var index = new ColonyIndex();
            Faction faction = ColonyScope.AuthoritativeFaction;

            // Colonies: group maps by canonical map, serviceable stacks only.
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                Map? canonical = FloorMaps.Canonical(map);
                if (canonical == null) continue;
                if (!index.colonyOfCanonical.TryGetValue(canonical, out int at))
                {
                    string? locationId = ColonyScope.LocationId(canonical, faction);
                    if (locationId == null) continue;
                    at = index.Colonies.Count;
                    index.colonyOfCanonical.Add(canonical, at);
                    index.Colonies.Add(new Colony
                    {
                        CanonicalMap = canonical,
                        LocationId = locationId,
                    });
                }
                index.Colonies[at].Maps.Add(map);
            }
            index.Colonies.Sort(ByCanonicalId);
            index.colonyOfCanonical.Clear();
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                index.colonyOfCanonical[colony.CanonicalMap] = c;
                colony.Maps.Sort(ByMapId);
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    List<Thing> haulables = colony.Maps[m].listerThings
                        .ThingsInGroup(ThingRequestGroup.HaulableEver);
                    for (int t = 0; t < haulables.Count; t++)
                    {
                        Thing thing = haulables[t];
                        index.ItemsById[thing.thingIDNumber] = thing;
                        index.colonyOfItem[thing.thingIDNumber] = c;
                        colony.ItemIds.Add(thing.thingIDNumber);
                    }
                }
                colony.ItemIds.Sort();
            }

            List<Pawn> colonists =
                ColonyScope.AllPlanableColonists(faction);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                index.PawnsById[pawn.thingIDNumber] = pawn;
                Map? canonical = FloorMaps.Canonical(pawn.MapHeld);
                if (canonical == null
                    || !index.colonyOfCanonical.TryGetValue(canonical, out int at))
                    continue;
                index.colonyOfPawn[pawn.thingIDNumber] = at;
                index.Colonies[at].PawnIds.Add(pawn.thingIDNumber);
            }
            for (int c = 0; c < index.Colonies.Count; c++)
                index.Colonies[c].PawnIds.Sort();

            return index;
        }

        private static readonly Comparison<Colony> ByCanonicalId =
            static (a, b) => a.CanonicalMap.uniqueID.CompareTo(b.CanonicalMap.uniqueID);

        private static readonly Comparison<Map> ByMapId =
            static (a, b) => a.uniqueID.CompareTo(b.uniqueID);
    }
}
