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

        /// Help tab: topic slugs this player has opened. Player knowledge,
        /// so it lives here and never in the savegame.
        public System.Collections.Generic.List<string> helpTopicsRead =
            new System.Collections.Generic.List<string>();

        /// Saves (world persistent random values) whose one-time welcome
        /// dialog this player has already seen.
        public System.Collections.Generic.List<string> welcomeShownSaves =
            new System.Collections.Generic.List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showToolbarButton, "showToolbarButton", true);
            Scribe_Values.Look(ref helpPlanTiersFolded, "helpPlanTiersFolded", false);
            Scribe_Collections.Look(ref helpTopicsRead, "helpTopicsRead", LookMode.Value);
            Scribe_Collections.Look(ref welcomeShownSaves, "welcomeShownSaves", LookMode.Value);
            helpTopicsRead ??= new System.Collections.Generic.List<string>();
            welcomeShownSaves ??= new System.Collections.Generic.List<string>();
        }
    }
}
