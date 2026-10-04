using Implanner.UI;
using Verse;

namespace Implanner
{
    /// Frame hook for work that must run outside OnGUI: draining the icon
    /// measurement queue in small batches.
    public class ImplannerGameComponent : GameComponent
    {
        public ImplannerGameComponent(Game game) { }

        public override void GameComponentUpdate()
        {
            GearIconMetrics.ProcessPending();
        }

        /// The synchronized tick path: reconciliation (reservations,
        /// implant allocation, surgery scheduling) runs here and never from
        /// OnGUI.
        public override void GameComponentTick()
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store != null)
                PlannerReconciler.Tick(store);
            if (Find.TickManager.TicksGame % PlannerReconciler.BoundaryTicks == 0)
                ImplantQualities.CheckSettings();
        }

        // Cached: queued from the load path, potentially every load.
        private static readonly System.Action queueWelcome = QueueWelcome;

        /// LoadedGame and StartedNewGame run on the long-event WORKER
        /// thread; adding a window (whose PreOpen measures text and loads a
        /// texture) there crashes the player natively. Everything defers to
        /// the main thread after the load event completes.
        public override void StartedNewGame() =>
            LongEventHandler.ExecuteWhenFinished(queueWelcome);

        public override void LoadedGame() =>
            LongEventHandler.ExecuteWhenFinished(queueWelcome);

        /// The welcome dialog appears once per player per save
        /// (`WelcomeDialog.ClaimSave`). Presentation only — never touches
        /// synced state.
        private static void QueueWelcome()
        {
            ImplannerSettings? settings = ImplannerMod.Settings;
            if (settings == null
                || !RimShared.UiLib.WelcomeDialog.ClaimSave(settings.welcomeShownSaves))
                return;
            ImplannerMod.Instance.WriteSettings();
            Find.WindowStack?.Add(new RimShared.UiLib.WelcomeDialog(
                "IMP_WelcomeTitle".Translate(),
                "IMP_WelcomeBody".Translate(),
                "IMP_WelcomeFind".Translate(),
                "IMP_WelcomeTakeMeThere".Translate(),
                System.IO.Path.Combine(ImplannerMod.ContentRootDir,
                    "About", "Preview.png"),
                // The illustration's ink band (assets/preview.svg y
                // 70.6..299.4 at half scale) padded by 16 logical units.
                new UnityEngine.Rect(0f, 109.2f, 1280f, 521.6f),
                OpenPlanner)
            {
                FindIcon = Patches.ImplannerTex.ToolbarButton,
                FindTextColor = PlannerStyle.TierStarColor,
            });
        }

        private static void OpenPlanner() =>
            Find.WindowStack.Add(new Dialog_Implanner());
    }
}
