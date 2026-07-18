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

        // List binding hand-rolled as count+elements (like SyncJobEntry's
        // fields): the SyncWorker API guarantees only primitive Binds.
        private static void BindList(SyncWorker sync, ref System.Collections.Generic.List<int> list)
        {
            if (sync.isWriting)
            {
                sync.Write(list?.Count ?? 0);
                if (list != null)
                    foreach (int value in list)
                        sync.Write(value);
            }
            else
            {
                int count = sync.Read<int>();
                list = new System.Collections.Generic.List<int>(count);
                for (int i = 0; i < count; i++)
                    list.Add(sync.Read<int>());
            }
        }

        private static void BindList(SyncWorker sync, ref System.Collections.Generic.List<string> list)
        {
            if (sync.isWriting)
            {
                sync.Write(list?.Count ?? 0);
                if (list != null)
                    foreach (string value in list)
                        sync.Write(value);
            }
            else
            {
                int count = sync.Read<int>();
                list = new System.Collections.Generic.List<string>(count);
                for (int i = 0; i < count; i++)
                    list.Add(sync.Read<string>());
            }
        }

        [SyncWorker(shouldConstruct = true)]
        private static void SyncImportSelection(SyncWorker sync, ref ImportSelection selection)
        {
            sync.Bind(ref selection.xml);
            sync.Bind(ref selection.palette);
            sync.Bind(ref selection.paletteOverwrite);
            sync.Bind(ref selection.roles);
            sync.Bind(ref selection.rolesOverwrite);
            sync.Bind(ref selection.paths);
            sync.Bind(ref selection.pathsOverwrite);
            sync.Bind(ref selection.order);
            BindList(sync, ref selection.paletteRows);
            BindList(sync, ref selection.roleRows);
            BindList(sync, ref selection.pathRows);
        }

        [SyncWorker(shouldConstruct = true)]
        private static void SyncRestoreSelection(SyncWorker sync, ref RestoreSelection selection)
        {
            BindList(sync, ref selection.templateDefs);
            BindList(sync, ref selection.workTypes);
            BindList(sync, ref selection.backfillRoleIds);
            BindList(sync, ref selection.pathDefs);
            BindList(sync, ref selection.groupRoleIds);
            BindList(sync, ref selection.colorRoleIds);
            BindList(sync, ref selection.holderRoleIds);
            sync.Bind(ref selection.recommendationOrder);
        }
    }
}
