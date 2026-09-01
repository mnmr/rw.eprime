using RimWorld;
using Verse;
using Verse.AI;

namespace WorkRoles
{
    /// Detects a managed pawn starting a work job whose giver no active role
    /// claims — evidence that another mod issues work from its own giver
    /// lists instead of the patched Pawn_WorkSettings getters (as MultiFloors'
    /// prioritized scanner did before its compat patch). Detection only: the
    /// job is never blocked or altered. One dialog per savegame, persisted in
    /// per-player ModSettings like the SetPriority notice (client-local; a
    /// scribed world-state write would desync multiplayer). Player-forced
    /// jobs are exempt, matching vanilla's forced-work semantics. The steady
    /// path — silenced or nothing to report — is a few field reads and
    /// dictionary probes with no allocations.
    internal static class GiverBypassWatcher
    {
        private sealed class PendingWarning
        {
            public string key = null!;      // always set by the object initializer
            public int worldId;
            public string pawnName = null!; // always set by the object initializer
            public string jobLabel = null!; // always set by the object initializer
        }

        private const string KeySuffix = "|jobBypass";

        // Scalar session identity only (never the World reference): the world
        // whose notice is pending, already shown, or found persisted.
        private static int? silencedWorldId;
        private static PendingWarning? pending;

        internal static bool HasPendingWarning => pending != null;

        /// Runs from the StartJob postfix, inside the synced simulation.
        internal static void OnJobStarted(Pawn pawn, Job job)
        {
            var giver = job?.workGiverDef;
            if (giver == null || job!.playerForced || pending != null) return;
            var world = Find.World;
            if (world == null) return;
            int worldId = world.info.persistentRandomValue;
            if (silencedWorldId == worldId) return;
            if (RoleStore.Current?.IsManaged(pawn) != true) return;
            if (CompiledJobOrders.TryGetClaimingRole(pawn, giver.defName, out _))
                return;

            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            // Allocation and the persisted-key scan happen only on this rare
            // path: an excluded-giver job on a world not yet silenced this
            // session. The silence latch makes every later call one compare.
            silencedWorldId = worldId;
            string key = worldId + KeySuffix;
            if (settings.warnedGiverBypass.Contains(key)) return;
            pending = new PendingWarning
            {
                key = key,
                worldId = worldId,
                pawnName = pawn.LabelShortCap,
                jobLabel = giver.label.NullOrEmpty() ? giver.defName : giver.label,
            };
        }

        /// Runs from the game-component tick only while a report is pending.
        internal static void ShowPendingWarning()
        {
            var warning = pending;
            if (warning == null) return;
            pending = null;
            var world = Find.World;
            var settings = WorkRolesMod.Settings;
            if (world == null || settings == null
                || world.info.persistentRandomValue != warning.worldId
                || settings.warnedGiverBypass.Contains(warning.key))
                return;
            // Persist before showing: the notice can never reappear, even if
            // the dialog is dismissed by a crash or the session ends abruptly.
            settings.warnedGiverBypass.Add(warning.key);
            settings.Write();
            Find.WindowStack.Add(new Dialog_MessageBox(
                "WR_GiverBypassBody".Translate(warning.pawnName, warning.jobLabel),
                title: "WR_GiverBypassTitle".Translate()));
        }

        internal static void ReleaseForTeardown()
        {
            silencedWorldId = null;
            pending = null;
        }
    }
}
