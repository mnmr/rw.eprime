using Verse;

namespace Implanner
{
    /// Where Implanner opens from (owner, 2026-11-01): the toolbar button
    /// beside the bottom-right view toggles (the floating Dialog_Implanner),
    /// the bottom-bar main tab (MainTabWindow_Implanner), or both entry
    /// points. Persisted as its integer value.
    public enum EntryStyle
    {
        Both = 0,
        Toolbar = 1,
        Tab = 2,
    }

    /// Per-player global settings (new-save defaults and presentation
    /// preferences). Authoritative per-save state belongs in the store,
    /// not here.
    public class ImplannerSettings : ModSettings
    {
        // Presentation preferences (per player, never synced or scribed per
        // save).
        public EntryStyle entryStyle = EntryStyle.Both;

        /// Legacy toggle (before entry styles): kept in the file so an older
        /// build still reads it, and read when a file carries no style.
        public bool showToolbarButton = true;

        /// Whether the toolbar button draws (Both or Toolbar).
        public bool ShowToolbarButton => entryStyle != EntryStyle.Tab;

        /// Player-sized bottom-bar tab (0 = default size per axis).
        public float tabWindowWidth;
        public float tabWindowHeight;

        /// Presentation only: upper bound of the Medical/Crafting sliders.
        /// Does not clamp stored thresholds or participate in automation.
        public int skillSliderMaximum = 20;

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

        // Per-player acknowledgement of each active conflict in a particular
        // save. A newly encountered level mod shows the welcome again once.
        public System.Collections.Generic.List<string> compatibilityWarningsSeen =
            new System.Collections.Generic.List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                showToolbarButton = ShowToolbarButton;
            Scribe_Values.Look(ref showToolbarButton, "showToolbarButton", true);
            // -1 marks a file from before entry styles: the legacy toggle
            // decides (hidden toolbar button = tab only).
            int style = (int)entryStyle;
            Scribe_Values.Look(ref style, "entryStyle", -1);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                entryStyle = style >= (int)EntryStyle.Both && style <= (int)EntryStyle.Tab
                    ? (EntryStyle)style
                    : showToolbarButton ? EntryStyle.Both : EntryStyle.Tab;
            Scribe_Values.Look(ref tabWindowWidth, "tabWindowWidth", 0f);
            Scribe_Values.Look(ref tabWindowHeight, "tabWindowHeight", 0f);
            Scribe_Values.Look(ref skillSliderMaximum, "skillSliderMaximum", 20);
            skillSliderMaximum = System.Math.Max(20,
                System.Math.Min(100, skillSliderMaximum / 10 * 10));
            Scribe_Values.Look(ref helpPlanTiersFolded, "helpPlanTiersFolded", false);
            Scribe_Collections.Look(ref helpTopicsRead, "helpTopicsRead", LookMode.Value);
            Scribe_Collections.Look(ref welcomeShownSaves, "welcomeShownSaves", LookMode.Value);
            Scribe_Collections.Look(ref compatibilityWarningsSeen, "compatibilityWarningsSeen", LookMode.Value);
            helpTopicsRead ??= new System.Collections.Generic.List<string>();
            welcomeShownSaves ??= new System.Collections.Generic.List<string>();
            compatibilityWarningsSeen ??= new System.Collections.Generic.List<string>();
        }
    }
}
