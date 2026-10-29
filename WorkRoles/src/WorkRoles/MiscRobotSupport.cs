using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using HarmonyLib;
using Verse;
using RimWorld;

namespace WorkRoles
{
    /// Native AIRobot bindings and containment only. Role policy and lifecycle
    /// invalidation belong to MechanicalWorkers; location belongs to ColonyScope.
    internal static class MiscRobotSupport
    {
        // Owner/key: process and fixed optional AIRobot assembly identity.
        // Value: immutable bound accessors; dependencies: that assembly only.
        // Refresh: initialization once; equality: one binding. Teardown: exit;
        // these delegates retain no world/game objects or owned assets.
        private static readonly Type? RobotType = AccessTools.TypeByName("AIRobot.X2_AIRobot");
        private static readonly Type? StationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
        private static readonly Type? DefType = AccessTools.TypeByName("AIRobot.X2_ThingDef_AIRobot");
        private static readonly Type? WorkEntryType = AccessTools.TypeByName("AIRobot.X2_ThingDef_AIRobot+RobotWorkTypes");
        private static readonly Func<Pawn, bool>? isRobot = BindTypeTest();
        private static readonly Func<ThingDef, IList?>? workTypes = BindField<ThingDef, IList?>(DefType, "robotWorkTypes");
        private static readonly Func<object, WorkTypeDef?>? workType = BindField<object, WorkTypeDef?>(WorkEntryType, "workTypeDef");
        private static readonly Func<object, int>? priority = BindField<object, int>(WorkEntryType, "priority");
        private static readonly Func<Thing, IList?>? container = BindField<Thing, IList?>(StationType, "container");
        private static readonly Func<Pawn, Thing?>? stationFor = BindField<Pawn, Thing?>(RobotType, "rechargeStation");
        private static readonly Func<Thing, bool>? autoSpawn = BindField<Thing, bool>(StationType, "SpawnRobotAfterRecharge");

        // Owner: current Game. Key: station identity. Value: lifecycle registry,
        // not render data. Dependencies: spawn/load/destroy and map/world removal.
        // Refresh: those events only. Equality: duplicate registration no-op.
        // Teardown: Reset, ReleaseMap and removal release every retained object.
        private static Game? owner;
        private static readonly List<Thing> stations = new List<Thing>();
        // Same Game owner/lifecycle as stations. Keys: station/pawn identity;
        // values: last notified map and native serialized containment, not UI data.
        // Dependencies: station spawn/load/despawn/destroy and container mutations.
        // Refresh only at those boundaries; equal refreshes publish nothing.
        // Reset/removal releases both indexes, including unspawned native pawns.
        private static readonly Dictionary<Thing, Map?> stationMaps = new Dictionary<Thing, Map?>();
        private static readonly Dictionary<Pawn, Thing> containedStations = new Dictionary<Pawn, Thing>();

        internal static bool IsRobot(Pawn pawn) => isRobot != null && isRobot(pawn);
        internal static bool StationAutomaticWorkEnabled(Thing station) => autoSpawn?.Invoke(station) == true;
        internal static bool IsRegisteredStation(Thing station) => ReferenceEquals(owner, Current.Game)
            && stationMaps.ContainsKey(station);
        // AIRobot keeps docked pawns in a serialized plain list, not an
        // IThingHolder. Only that containment establishes their location.
        // rechargeStation is an assignment and can outlive a robot's departure.
        internal static Thing? ContainingStation(Pawn pawn) => ReferenceEquals(owner, Current.Game)
            && containedStations.TryGetValue(pawn, out Thing? station) && !station.Destroyed
                ? station : null;
        internal static bool HasPersistenceAnchor(Pawn pawn) => ContainingStation(pawn) is Thing station
            && (station.MapHeld != null || station.ParentHolder != null);
        internal static bool AutomaticWorkEnabled(Pawn pawn)
        {
            Thing? station = ContainingStation(pawn) ?? stationFor?.Invoke(pawn);
            return station != null && !station.Destroyed && station.Spawned
                && station.Faction == pawn.Faction
                && autoSpawn?.Invoke(station) == true;
        }

        internal static bool DeclaresWorkGiver(ThingDef robotDef, WorkGiverDef giver)
        {
            IList? types = workTypes?.Invoke(robotDef);
            if (types == null || workType == null || priority == null) return false;
            for (int i = 0; i < types.Count; i++)
                if (workType(types[i]!) == giver.workType)
                    return priority(types[i]!) > 0
                        && giver.workType.workGiversByPriority.Contains(giver);
            return false;
        }

        // Return affected native members; WorkRoles owns their model/cache
        // invalidation. The adapter has no role, scope or roster policy.
        internal static List<Pawn>? RegisterStation(Thing station)
        {
            if (!ReferenceEquals(owner, Current.Game)) Reset();
            owner = Current.Game;
            if (owner == null || station.Destroyed) return null;
            bool changed = !stationMaps.TryGetValue(station, out Map? oldMap) || oldMap != station.MapHeld;
            if (!stations.Contains(station)) stations.Add(station);
            stationMaps[station] = station.MapHeld;
            IList? robots = container?.Invoke(station);
            var previous = new List<Pawn>();
            foreach (var entry in containedStations)
                if (entry.Value == station) previous.Add(entry.Key);
            for (int i = 0; i < previous.Count; i++)
                if (robots == null || !robots.Contains(previous[i]))
                { containedStations.Remove(previous[i]); changed = true; }
            if (robots != null)
                for (int i = 0; i < robots.Count; i++)
                    if (robots[i] is Pawn pawn)
                    {
                        if (!containedStations.TryGetValue(pawn, out Thing? prior) || prior != station) changed = true;
                        containedStations[pawn] = station;
                        if (!previous.Contains(pawn)) previous.Add(pawn);
                    }
            return changed ? previous : null;
        }

        internal static List<Pawn>? RemoveStation(Thing station)
        {
            if (!stations.Remove(station)) return null;
            stationMaps.Remove(station);
            var removed = new List<Pawn>();
            foreach (var entry in containedStations)
                if (entry.Value == station) removed.Add(entry.Key);
            for (int i = 0; i < removed.Count; i++)
                containedStations.Remove(removed[i]);
            return removed;
        }

        internal static void NotifyAreaRemoved(Area area)
        {
            if (!ReferenceEquals(owner, Current.Game)) return;
            // Native plain-list containers are absent from PawnsFinder.
            // Keep native restrictions valid even while WR support is off.
            foreach (var entry in containedStations)
                entry.Key.playerSettings?.Notify_AreaRemoved(area);
        }

        internal static List<Pawn>? ReleaseMap(Map map)
        {
            List<Pawn>? removed = null;
            for (int i = stations.Count - 1; i >= 0; i--)
                if (stations[i].MapHeld == map)
                {
                    if (removed == null) removed = new List<Pawn>();
                    removed.AddRange(RemoveStation(stations[i])!);
                }
            return removed;
        }

        internal static void Reset() { stations.Clear(); stationMaps.Clear(); containedStations.Clear(); owner = null; }

        internal static void AppendDockedRobots(List<Pawn> result, Map? map = null)
        {
            if (container == null || !ReferenceEquals(owner, Current.Game)) return;
            for (int i = 0; i < stations.Count; i++)
            {
                Thing station = stations[i];
                if (station.Destroyed || map != null && station.MapHeld != map) continue;
                IList? robots = container(station);
                if (robots == null) continue;
                for (int r = 0; r < robots.Count; r++)
                    if (robots[r] is Pawn pawn && !result.Contains(pawn)) result.Add(pawn);
            }
        }

        private static Func<Pawn, bool>? BindTypeTest()
        {
            if (RobotType == null) return null;
            var pawn = Expression.Parameter(typeof(Pawn), "pawn");
            return Expression.Lambda<Func<Pawn, bool>>(Expression.TypeIs(pawn, RobotType), pawn).Compile();
        }

        private static Func<TSource, TValue>? BindField<TSource, TValue>(Type? type, string name)
        {
            if (type == null) return null;
            var field = AccessTools.Field(type, name);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            var source = Expression.Parameter(typeof(TSource), "source");
            return Expression.Lambda<Func<TSource, TValue>>(Expression.Condition(
                Expression.TypeIs(source, type),
                Expression.Convert(Expression.Field(Expression.Convert(source, type), field), typeof(TValue)),
                Expression.Default(typeof(TValue))), source).Compile();
        }
    }
}
