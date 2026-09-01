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

        /// The welcome dialog appears once per player per save, keyed by the
        /// world's persistent random value (the save's stable identity) in
        /// the per-player settings. Marked seen the moment it is queued, so
        /// however it is dismissed it never returns for this save.
        /// Presentation only — never touches synced state.
        private static void QueueWelcome()
        {
            RimWorld.Planet.World? world = Find.World;
            ImplannerSettings? settings = ImplannerMod.Settings;
            if (world == null || settings == null) return;
            string id = world.info.persistentRandomValue.ToString();
            if (settings.welcomeShownSaves.Contains(id)) return;
            settings.welcomeShownSaves.Add(id);
            ImplannerMod.Instance.WriteSettings();
            Find.WindowStack?.Add(new Dialog_ImplannerWelcome());
        }
    }
}
