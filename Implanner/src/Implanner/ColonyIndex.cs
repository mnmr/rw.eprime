using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Implanner
{
    /// One serviceable colony: the canonical ground map, every floor or
    /// pocket map that canonicalizes to it, the planable colonists held on
    /// any of those maps, and the colony's spawned implant items by kind.
    /// All lists are sorted by stable ids, so iteration is deterministic.
    internal sealed class Colony
    {
        internal Map CanonicalMap = null!;
        internal string LocationId = "";
        internal readonly List<Map> Maps = new List<Map>();
        internal readonly List<int> PawnIds = new List<int>();

        /// Spawned implant items on the colony's maps, keyed by item def,
        /// ids ascending. Only kinds the implant catalog can consume are
        /// indexed; other haulables never matter to automation.
        internal readonly Dictionary<ThingDef, List<int>> ItemIdsByDef =
            new Dictionary<ThingDef, List<int>>();

        internal List<int>? ItemIdsOf(ThingDef def) =>
            ItemIdsByDef.TryGetValue(def, out List<int> ids) ? ids : null;
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

        /// Every planable colonist alive anywhere, including pawns away
        /// from any colony (caravans, transporters in flight, a gravship in
        /// flight, non-serviceable maps). Presence here means the pawn is
        /// still ours: records are kept; only pawns at a colony get work.
        internal readonly Dictionary<int, Pawn> PawnsById =
            new Dictionary<int, Pawn>();

        /// Every spawned implant item on any colony map.
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

            // Only implant items matter: the kinds the catalog's surgeries
            // consume, looked up per def through the map's def lister
            // instead of walking every haulable thing.
            List<ThingDef> itemDefs = ImplantItemDefs();
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                index.colonyOfCanonical[colony.CanonicalMap] = c;
                colony.Maps.Sort(ByMapId);
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    ListerThings lister = colony.Maps[m].listerThings;
                    for (int d = 0; d < itemDefs.Count; d++)
                    {
                        List<Thing> things = lister.ThingsOfDef(itemDefs[d]);
                        if (things.Count == 0) continue;
                        if (!colony.ItemIdsByDef.TryGetValue(itemDefs[d], out List<int> ids))
                        {
                            ids = new List<int>(things.Count);
                            colony.ItemIdsByDef.Add(itemDefs[d], ids);
                        }
                        for (int t = 0; t < things.Count; t++)
                        {
                            Thing thing = things[t];
                            index.ItemsById[thing.thingIDNumber] = thing;
                            index.colonyOfItem[thing.thingIDNumber] = c;
                            ids.Add(thing.thingIDNumber);
                        }
                    }
                }
                foreach (KeyValuePair<ThingDef, List<int>> pair in colony.ItemIdsByDef)
                    pair.Value.Sort();
            }

            List<Pawn> colonists =
                ColonyScope.AllPlanableColonists(faction);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (index.PawnsById.ContainsKey(pawn.thingIDNumber)) continue;
                index.PawnsById.Add(pawn.thingIDNumber, pawn);
                // Only a pawn automation can reach (spawned, or carried by
                // a colonist) is placed at their map's colony; a pawn sealed
                // in a casket, pod or landed transporter, or off every map
                // (caravans, transporters and gravships in flight), is away:
                // indexed so records are kept, at no colony so no work is
                // scheduled and no doctor floor is derived from them.
                Map? canonical = ColonyScope.IsOperable(pawn)
                    ? FloorMaps.Canonical(pawn.MapHeld)
                    : null;
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

        /// The implant item kinds automation can consume, in defName
        /// order: the catalog's spawnThingOnRemoved defs, deduplicated.
        internal static List<ThingDef> ImplantItemDefs()
        {
            var defs = new List<ThingDef>();
            var seen = new HashSet<ThingDef>();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
            {
                ThingDef? item = catalog[i].Def.spawnThingOnRemoved;
                if (item != null && seen.Add(item))
                    defs.Add(item);
            }
            defs.Sort(ByDefName);
            return defs;
        }

        private static readonly Comparison<ThingDef> ByDefName =
            static (a, b) => string.CompareOrdinal(a.defName, b.defName);

        private static readonly Comparison<Colony> ByCanonicalId =
            static (a, b) => a.CanonicalMap.uniqueID.CompareTo(b.CanonicalMap.uniqueID);

        private static readonly Comparison<Map> ByMapId =
            static (a, b) => a.uniqueID.CompareTo(b.uniqueID);
    }
}
