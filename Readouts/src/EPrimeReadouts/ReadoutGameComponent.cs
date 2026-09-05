using EPrimeReadouts.UI;
using Verse;

namespace EPrimeReadouts
{
    /// Queues the one-time welcome dialog after a save loads or a new game
    /// starts. Holds no state of its own; the "already seen" record is the
    /// per-player settings list keyed by the save's persistent random value.
    public sealed class ReadoutGameComponent : GameComponent
    {
        // Cached: queued from the load path, potentially every load.
        private static readonly System.Action queueWelcome = QueueWelcome;

        public ReadoutGameComponent(Game game)
        {
        }

        /// LoadedGame and StartedNewGame run on the long-event WORKER
        /// thread; adding a window (whose PreOpen measures text and loads a
        /// texture) there crashes the player natively. Everything defers to
        /// the main thread after the load event completes.
        public override void StartedNewGame() =>
            LongEventHandler.ExecuteWhenFinished(queueWelcome);

        public override void LoadedGame() =>
            LongEventHandler.ExecuteWhenFinished(queueWelcome);

        private static void QueueWelcome()
        {
            RimWorld.Planet.World? world = Find.World;
            ReadoutSettings? settings = EPrimeReadoutsMod.Settings;
            if (world == null || settings == null) return;
            string id = world.info.persistentRandomValue.ToString();
            if (settings.welcomeShownSaves.Contains(id)) return;
            EPrimeReadoutsMod.Persist(s => s.welcomeShownSaves.Add(id));
            Find.WindowStack?.Add(new Dialog_ReadoutsWelcome());
        }
    }
}
