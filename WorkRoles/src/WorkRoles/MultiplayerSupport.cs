using Multiplayer.API;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Wires WorkRoles up to the RimWorld Multiplayer mod (when present) via the
    /// Multiplayer API: registers all [SyncMethod]s on RoleCommands and the
    /// SyncWorkers below. The API dll ships with the mod; without the Multiplayer
    /// mod MP.enabled is false and this is a no-op.
    [StaticConstructorOnStartup]
    public static class MultiplayerSupport
    {
        static MultiplayerSupport()
        {
            // Timed including the MP.enabled read: it's potentially the first
            // touch of the MP API bridge by any mod in load order, so lazy init
            // on the Multiplayer side would land on our clock.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (MP.enabled) MP.RegisterAll();
            StartupTiming.Record("multiplayer registration", sw.ElapsedMilliseconds);
        }

        [SyncWorker(shouldConstruct = false)]
        private static void SyncJobEntry(SyncWorker sync, ref JobEntry entry)
        {
            if (sync.isWriting)
            {
                sync.Write((int)entry.Kind);
                sync.Write(entry.DefName);
            }
            else
            {
                var kind = (JobEntryKind)sync.Read<int>();
                var defName = sync.Read<string>();
                entry = new JobEntry(kind, defName);
            }
        }

        [SyncWorker(shouldConstruct = true)]
        private static void SyncRoleAssignment(SyncWorker sync, ref RoleAssignment assignment)
        {
            sync.Bind(ref assignment.roleId);
            sync.Bind(ref assignment.enabled);
            sync.Bind(ref assignment.pinned);
        }
    }
}
