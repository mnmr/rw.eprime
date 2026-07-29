using RimWorld;
using Verse;

namespace WorkRoles
{
    public class WorkRolesGameComponent : GameComponent
    {
        // Game tick of the next local-hour boundary. Local hours advance when
        // absolute ticks cross a 2500 boundary — the longitude offset is a
        // whole number of hours (GenDate.LocalTicksOffsetFromLongitude), so one
        // global schedule covers every map and caravan, and the per-tick cost
        // is a single comparison. (A caravan crossing a timezone mid-hour is
        // caught on the crossing tick by Patch_WorldObject_SetTile.) Ticks are
        // never skipped, only batched: TickManager runs DoSingleTick
        // sequentially at every speed.
        private int nextHourCheckTick;

        public WorkRolesGameComponent(Game game) { }

        // Committing state from a dialog callback mid-OnGUI can change control
        // layout between a frame's Layout and event passes (IMGUI errors).
        // Update runs before the frame's GUI events, pause included.
        private static readonly System.Collections.Generic.Queue<System.Action> deferredUi
            = new System.Collections.Generic.Queue<System.Action>();

        public static void RunOutsideOnGUI(System.Action action) => deferredUi.Enqueue(action);

        /// Returning to the menu must not carry a deferred command into the
        /// next game, even when the queued closure contains only scalar IDs.
        internal static void ReleaseForTeardown() => deferredUi.Clear();

        public override void GameComponentUpdate()
        {
            while (deferredUi.Count > 0)
                deferredUi.Dequeue()();
        }

        // Runs after both "new game started" and "save loaded".
        public override void FinalizeInit()
        {
            Seeding.SweepEmptyRoleSets();
            // Scales first: role seeding copies def scale references by name.
            Seeding.EnsurePresetScales();
            Seeding.SeedIfNeeded();
            Seeding.RefreshWorkTypeSnapshots();
            var generated = Seeding.EnsureWorkTypeCoverage();
            if (generated.Count > 0)
                UI.WrToast.Show("WR_NewWorkDetected".Translate(generated.ToCommaList()),
                    MessageTypeDefOf.NeutralEvent);

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
            CompiledJobOrders.DrainPendingReconciles();
            int now = Find.TickManager.TicksGame;
            if (PrioritySetWatcher.HasPendingWarning)
                PrioritySetWatcher.ShowPendingWarning(now);
            if (now < nextHourCheckTick) return;
            nextHourCheckTick = now + 2500 - (int)GenMath.PositiveMod(GenTicks.TicksAbs, 2500);
            CompiledJobOrders.InvalidateAllTimeRuled();
        }
    }
}
