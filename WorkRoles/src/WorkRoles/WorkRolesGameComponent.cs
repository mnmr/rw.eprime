using RimShared.Common;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class WorkRolesGameComponent : GameComponent
    {
        // World-pawn local hours advance when absolute ticks cross a 2500
        // boundary; map pawns have independent gates in WorkRolesMapComponent
        // so Multiplayer Async Time runs them in the owning map context. A
        // caravan crossing a timezone mid-hour is still caught immediately by
        // Patch_WorldObject_SetTile.
        private readonly FixedTickBoundaryGate worldHourBoundary =
            new FixedTickBoundaryGate(2500);

        public WorkRolesGameComponent(Game game) { }

        // Committing state from a dialog callback mid-OnGUI can change control
        // layout between a frame's Layout and event passes (IMGUI errors).
        // Update runs before the frame's GUI events, pause included.
        private static readonly System.Collections.Generic.Queue<System.Action> deferredUi
            = new System.Collections.Generic.Queue<System.Action>();
        private static readonly System.Action writeSettingsAction = WriteSettings;
        private static bool settingsWritePending;

        public static void RunOutsideOnGUI(System.Action action) => deferredUi.Enqueue(action);

        /// Client preferences become authoritative in memory immediately, but
        /// their disk write is coalesced and performed outside the IMGUI pass.
        public static void RequestSettingsWrite()
        {
            if (settingsWritePending) return;
            settingsWritePending = true;
            deferredUi.Enqueue(writeSettingsAction);
        }

        private static void WriteSettings()
        {
            settingsWritePending = false;
            WorkRolesMod.Settings?.Write();
        }

        /// Returning to the menu must not carry a deferred command into the
        /// next game, even when the queued closure contains only scalar IDs.
        internal static void ReleaseForTeardown()
        {
            deferredUi.Clear();
            settingsWritePending = false;
        }

        public override void GameComponentUpdate()
        {
            while (deferredUi.Count > 0)
                deferredUi.Dequeue()();
        }

        // Runs after both "new game started" and "save loaded".
        public override void FinalizeInit()
        {
            Seeding.SweepEmptyRoleSets();
            Seeding.SeedIfNeeded();
            Seeding.RefreshWorkTypeSnapshots();
            var generated = Seeding.EnsureWorkTypeCoverage();
            if (generated.Count > 0)
                UI.WrToast.Show("WR_NewWorkDetected".Translate(generated.ToCommaList()),
                    MessageTypeDefOf.NeutralEvent);

            RoleCommands.MigrateLocationTokensOnce();

            // Dead entries are visible (dimmed) while editing but scrubbed at
            // rest; older saves carry subset-marker givers that coverage-based
            // nesting no longer needs.
            var store = RoleStore.Current;
            if (store != null)
                foreach (var role in store.roles)
                    if (RoleCommands.ScrubDeadEntriesDirect(role))
                        CompiledJobOrders.InvalidateRole(role.id);

            CompiledJobOrders.WarmProjectionMetadata();
            JobSkillProfiles.WarmDefinitionFacts();
            // Deferred to load end: the remaining first-open costs (localized
            // facade, signal builders, recs engine) warm while the loading
            // screen still owns the frame instead of on the first click.
            FirstOpenWarmup.Queue();

            // Jobs resumed from the save predate the in-memory rank baselines.
            // A job that survived until the save was never demoted below its
            // issue rank, so the loaded rank is a valid stand-in: re-stamp it,
            // and queue a first-tick reconcile to sweep revocations left by
            // saves from versions without interruption.
            if (store != null)
                foreach (var pawn in store.pawnSets.Keys)
                {
                    JobRankBaseline.NotifyJobStarted(pawn);
                    CompiledJobOrders.EnqueueReconcile(pawn);
                }
        }

        public override void GameComponentTick()
        {
            PawnLocationTracker.ProcessPendingDepartures();
            CompiledJobOrders.DrainWorldPendingReconciles();
            int now = Find.TickManager.TicksGame;
            if (PrioritySetWatcher.HasPendingWarning)
                PrioritySetWatcher.ShowPendingWarning(now);
            if (worldHourBoundary.Observe(GenTicks.TicksAbs))
                CompiledJobOrders.InvalidateWorldTimeRuled();
        }
    }
}
