using RimWorld;
using Verse;

namespace WorkRoles
{
    /// Roles outlive death. Pawn.Kill ends in Thing.Kill → Destroy(KillFinalize),
    /// so a dying managed pawn reaches the destroy patch: its set moves out of
    /// pawnSets (every live path assumes a living pawn) into a dormant slot.
    /// Resurrection keeps the same pawn and restores it; a Misc. Robots robot is
    /// destroyed for good and its station's next robot (the repaired one)
    /// inherits it. Deterministic simulation code on every client.
    internal static class DeathRoles
    {
        /// Called before the destroy patch drops the pawn from the store.
        internal static void Park(RoleStore store, Pawn pawn)
        {
            if (!pawn.Dead || !store.pawnSets.TryGetValue(pawn, out PawnRoleSet? set)
                || set.assignments.Count == 0) return;
            if (MiscRobotSupport.IsRobot(pawn))
            {
                Thing? station = MiscRobotSupport.AssignedStation(pawn);
                if (station != null && !station.Destroyed) store.stationRoleSets[station] = set;
                return;
            }
            store.dormantPawnSets[pawn] = set;
        }

        /// A dead pawn can come back only from a corpse the game still keeps.
        internal static bool CanReturn(Pawn pawn) => pawn != null && pawn.Dead
            && pawn.Corpse is Corpse corpse && !corpse.Destroyed && corpse.ParentHolder != null;

        internal static void NotifyResurrected(Pawn pawn)
        {
            RoleStore? store = RoleStore.Current;
            if (store == null || pawn == null || pawn.Dead
                || !store.dormantPawnSets.TryGetValue(pawn, out PawnRoleSet? set)) return;
            store.dormantPawnSets.Remove(pawn);
            // Replaces whatever auto-assignment gave the pawn while it revived.
            if (pawn.Faction?.IsPlayer == true)
                RoleCommands.PasteRoleSetDirect(pawn, set.assignments);
        }

        internal static void NotifyRobotSpawned(Thing station)
        {
            RoleStore? store = RoleStore.Current;
            if (store?.stationRoleSets == null || !store.stationRoleSets.TryGetValue(station, out PawnRoleSet? set)) return;
            Pawn? robot = MiscRobotSupport.StationRobot(station);
            if (robot == null || robot.Dead || robot.Destroyed) return;
            store.stationRoleSets.Remove(station);
            RoleCommands.PasteRoleSetDirect(robot, set.assignments);
        }

        internal static void NotifyStationDestroyed(Thing station) =>
            RoleStore.Current?.stationRoleSets?.Remove(station);
    }
}
