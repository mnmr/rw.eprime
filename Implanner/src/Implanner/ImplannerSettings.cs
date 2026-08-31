using Verse;

namespace Implanner
{
    /// Per-player global settings (new-save defaults and presentation
    /// preferences). Authoritative per-save state belongs in the store,
    /// not here.
    public class ImplannerSettings : ModSettings
    {
        // Presentation preferences (per player, never synced or scribed per
        // save).
        public bool showToolbarButton = true;

        /// Fold state of the plan editor's tier-panel help section.
        public bool helpPlanTiersFolded;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showToolbarButton, "showToolbarButton", true);
            Scribe_Values.Look(ref helpPlanTiersFolded, "helpPlanTiersFolded", false);
        }
    }
}
