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

        /// The synchronized tick path: reconciliation (latches, reservations,
        /// implant allocation, surgery scheduling) runs here and never from
        /// OnGUI.
        public override void GameComponentTick()
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store != null)
                PlannerReconciler.Tick(store);
        }
    }
}
