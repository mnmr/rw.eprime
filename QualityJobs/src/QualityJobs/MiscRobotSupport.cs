using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Optional AIRobot integration, shared by Misc. Robots and Misc. Robots++.
    /// Resolve members once; job searches use compiled delegates, never Invoke.
    internal static class MiscRobotSupport
    {
        // Owner: process. Key/dependencies: loaded AIRobot assembly identity
        // (fixed for the process). Value: immutable type-test/member delegates.
        // Refresh: static initialization. Equality: one binding per process.
        // Teardown: process exit; no world, pawn, map or owned assets retained.
        private static readonly Type? RobotType = AccessTools.TypeByName("AIRobot.X2_AIRobot");
        private static readonly Type? StationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
        private static readonly Type? RobotDefType = AccessTools.TypeByName("AIRobot.X2_ThingDef_AIRobot");
        private static readonly Type? WorkTypeEntryType = AccessTools.TypeByName("AIRobot.X2_ThingDef_AIRobot+RobotWorkTypes");
        private static readonly Func<Pawn, bool>? isRobot = BindTypeTest();
        private static readonly Func<Pawn, List<WorkGiver>>? workGivers = BindWorkGivers();
        private static readonly Func<Thing, IList>? containedRobots = BindContainer();
        private static readonly Func<Pawn, ThingDef?>? initializedDef = BindField<Pawn, ThingDef?>(RobotType, "def2");
        private static readonly Func<ThingDef, IList?>? declaredWorkTypes = BindField<ThingDef, IList?>(RobotDefType, "robotWorkTypes");
        private static readonly Func<object, WorkTypeDef?>? declaredWorkType = BindField<object, WorkTypeDef?>(WorkTypeEntryType, "workTypeDef");
        private static readonly Func<object, int>? declaredPriority = BindField<object, int>(WorkTypeEntryType, "priority");

        // Owner: current Game, including while QJ is disabled. Key: station
        // reference. Value: private lifecycle index, not a render snapshot.
        // Dependencies: station spawn/load/destruction and map/game removal.
        // Refresh: those events only; packing retains the same station identity.
        // Equality: duplicate registrations are no-ops. Pool builders read live
        // container/holder facts without retaining derived pawn lists. Teardown:
        // destruction/map removal drop entries; Game.Dispose/owner change clears
        // all references. No inventory scans, timer or per-pawn cache is added.
        private static Game? stationOwner;
        private static readonly List<Thing> stations = new List<Thing>();

        internal static bool IsRobot(Pawn pawn) => isRobot != null && isRobot(pawn);
        internal static List<WorkGiver>? WorkGivers(Pawn pawn) => workGivers?.Invoke(pawn);
        internal static IList? ContainedRobots(Thing station) => containedRobots?.Invoke(station);
        internal static IReadOnlyList<Thing> Stations
        {
            get { EnsureStationOwner(); return stations; }
        }

        internal static bool HasWorkGiver(Pawn pawn, WorkGiverDef def)
        {
            // A saved docked robot has never SpawnSetup in this game: AIRobot's
            // unsaved def2 is null and GetWorkGivers would repeatedly build an
            // empty list. Read the exact declarations its scheduler will use
            // after undocking, without initializing or mutating the robot.
            if (!pawn.Spawned && initializedDef?.Invoke(pawn) == null)
                return DeclaresWorkGiver(pawn.def, def);
            List<WorkGiver>? list = WorkGivers(pawn);
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].def == def) return true;
            return false;
        }

        private static bool DeclaresWorkGiver(ThingDef robotDef, WorkGiverDef giver)
        {
            IList? types = declaredWorkTypes?.Invoke(robotDef);
            if (types == null || declaredWorkType == null || declaredPriority == null) return false;
            for (int i = 0; i < types.Count; i++)
            {
                object entry = types[i]!;
                if (declaredWorkType(entry) == giver.workType)
                    return declaredPriority(entry) > 0
                        && giver.workType.workGiversByPriority.Contains(giver);
            }
            return false;
        }

        internal static bool RegisterStation(Thing station)
        {
            EnsureStationOwner();
            if (stationOwner == null || station.Destroyed || stations.Contains(station)) return false;
            stations.Add(station);
            return true;
        }

        internal static bool RemoveStation(Thing station)
        {
            EnsureStationOwner();
            return stations.Remove(station);
        }

        internal static bool ReleaseMap(Map map)
        {
            EnsureStationOwner();
            bool changed = false;
            for (int i = stations.Count - 1; i >= 0; i--)
                if (stations[i].MapHeld == map)
                {
                    stations.RemoveAt(i);
                    changed = true;
                }
            return changed;
        }

        internal static void ResetStations(Game game)
        {
            if (!ReferenceEquals(stationOwner, game)) return;
            stations.Clear();
            stationOwner = null;
        }

        private static void EnsureStationOwner()
        {
            if (ReferenceEquals(stationOwner, Current.Game)) return;
            stations.Clear();
            stationOwner = Current.Game;
        }

        private static Func<Pawn, bool>? BindTypeTest()
        {
            if (RobotType == null) return null;
            var pawn = Expression.Parameter(typeof(Pawn), "pawn");
            return Expression.Lambda<Func<Pawn, bool>>(
                Expression.TypeIs(pawn, RobotType), pawn).Compile();
        }

        private static Func<Pawn, List<WorkGiver>>? BindWorkGivers()
        {
            if (RobotType == null) return null;
            MethodInfo? method = AccessTools.Method(RobotType, "GetWorkGivers", new[] { typeof(bool) });
            if (method == null) throw new MissingMethodException(RobotType.FullName, "GetWorkGivers");
            var pawn = Expression.Parameter(typeof(Pawn), "pawn");
            return Expression.Lambda<Func<Pawn, List<WorkGiver>>>(
                Expression.Call(Expression.Convert(pawn, RobotType), method,
                    Expression.Constant(false)), pawn).Compile();
        }

        private static Func<Thing, IList>? BindContainer()
        {
            if (StationType == null) return null;
            FieldInfo? field = AccessTools.Field(StationType, "container");
            if (field == null) throw new MissingFieldException(StationType.FullName, "container");
            var station = Expression.Parameter(typeof(Thing), "station");
            return Expression.Lambda<Func<Thing, IList>>(Expression.Convert(
                Expression.Field(Expression.Convert(station, StationType), field),
                typeof(IList)), station).Compile();
        }

        private static Func<TSource, TValue>? BindField<TSource, TValue>(Type? owner, string name)
        {
            if (owner == null) return null;
            FieldInfo? field = AccessTools.Field(owner, name);
            if (field == null) throw new MissingFieldException(owner.FullName, name);
            var source = Expression.Parameter(typeof(TSource), "source");
            return Expression.Lambda<Func<TSource, TValue>>(Expression.Condition(
                Expression.TypeIs(source, owner),
                Expression.Convert(Expression.Field(Expression.Convert(source, owner), field), typeof(TValue)),
                Expression.Default(typeof(TValue))), source).Compile();
        }

    }
}
