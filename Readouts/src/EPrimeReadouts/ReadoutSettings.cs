using System.Collections.Generic;
using Verse;

namespace EPrimeReadouts
{
    /// Per-player view state: panel geometry and per-group tier depths. Group
    /// definitions live in ReadoutStore (per-save, synced); nothing here is
    /// ever synced or written to saves.
    public class ReadoutSettings : ModSettings
    {
        public bool useVanillaReadout;
        public float offsetX = 7f;
        public float offsetY = 7f;
        public float panelWidth = 140f;
        public float bottomMargin = 200f;
        /// Key: "{world.persistentRandomValue}:{groupId}" — unique across saves.
        public Dictionary<string, int> tierDepths = new Dictionary<string, int>();
        /// Key like tierDepths; absent = group disabled. Seeds start disabled.
        public Dictionary<string, bool> enabledGroups = new Dictionary<string, bool>();
        public float dialogW;
        public float dialogH;
        public bool helpGroupsFolded;
        public bool helpResourcesFolded;
        public bool helpEditorFolded;
        public bool helpPoolsFolded;
        public bool showSearchFilter = true;
        public bool showModNameWhenNoSearch = true;
        /// Count-basis filters applied to every displayed count (group slots,
        /// pools, thresholds, search results); hide-zero is search-only.
        public bool searchHideZero = true;
        public bool searchStorageOnly = true;
        public bool searchHideForbidden = true;
        /// Clicking a readout slot also pans the camera to the nearest
        /// selected stack.
        public bool selectJumpCamera = true;
        /// Master hover toggle: hovering the panel changes tier depth. Alone,
        /// hover expands configured tiers to all tiers.
        public bool expandOnHover;
        /// Sub-option of expandOnHover: idle shows 0 tiers (bands only) and
        /// hover shows the configured tiers, never more.
        public bool collapseWhenIdle;
        /// Draw the panel from cached surfaces (the buffered renderer). Off
        /// draws every frame directly: the compatibility escape hatch for a
        /// GPU or driver whose surfaces come back blank.
        public bool bufferedRendering = true;
        /// Keep the game's categorized-readout toggle in the toolbar. While
        /// kept, that toggle (Prefs.ResourceReadoutCategorized) shows or
        /// hides the bands; off leaves only the gear and the search field.
        public bool keepReadoutToggle;
        /// Tier layout of an expanded group, whether cycled or hovered:
        /// false puts every visible tier on the band's single row, true
        /// gives each tier its own row and grows the band downward.
        public bool verticalTiers;
        /// Planned-work reservations. All default off: a fresh install shows
        /// the same numbers it always did until the player opts in.
        /// Subtract ingredients outstanding bill iterations will consume.
        public bool reserveForBills;
        /// Subtract materials undelivered blueprints and frames still need.
        public bool reserveForBuildables;
        /// Show an overrun as a negative number instead of capping at zero.
        /// Pure presentation: never invalidates a count snapshot.
        public bool showNegativeCounts;
        /// Scale reservations by the rework a Quality Jobs quality target
        /// implies. Inert while that mod is absent.
        public bool qualityJobsRework;
        /// Help tab: topic slugs this player has opened. Player knowledge,
        /// so it lives here and never in the savegame.
        public List<string> helpTopicsRead = new List<string>();
        /// Saves (world persistent random values) whose one-time welcome
        /// dialog this player has already seen.
        public List<string> welcomeShownSaves = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref helpTopicsRead, "helpTopicsRead", LookMode.Value);
            if (helpTopicsRead == null) helpTopicsRead = new List<string>();
            Scribe_Collections.Look(ref welcomeShownSaves, "welcomeShownSaves", LookMode.Value);
            if (welcomeShownSaves == null) welcomeShownSaves = new List<string>();
            Scribe_Values.Look(ref useVanillaReadout, "useVanillaReadout", false);
            Scribe_Values.Look(ref offsetX, "offsetX", 7f);
            Scribe_Values.Look(ref offsetY, "offsetY", 7f);
            Scribe_Values.Look(ref panelWidth, "panelWidth", 140f);
            Scribe_Values.Look(ref bottomMargin, "bottomMargin", 200f);
            Scribe_Values.Look(ref dialogW, "dialogW", 0f);
            Scribe_Values.Look(ref dialogH, "dialogH", 0f);
            Scribe_Values.Look(ref helpGroupsFolded, "helpGroupsFolded", false);
            Scribe_Values.Look(ref helpResourcesFolded, "helpResourcesFolded", false);
            Scribe_Values.Look(ref helpEditorFolded, "helpEditorFolded", false);
            Scribe_Values.Look(ref helpPoolsFolded, "helpPoolsFolded", false);
            Scribe_Values.Look(ref showSearchFilter, "showSearchFilter", true);
            Scribe_Values.Look(ref showModNameWhenNoSearch, "showModNameWhenNoSearch", true);
            Scribe_Values.Look(ref searchHideZero, "searchHideZero", true);
            Scribe_Values.Look(ref searchStorageOnly, "searchStorageOnly", true);
            Scribe_Values.Look(ref searchHideForbidden, "searchHideForbidden", true);
            Scribe_Values.Look(ref selectJumpCamera, "selectJumpCamera", true);
            Scribe_Values.Look(ref expandOnHover, "expandOnHover", false);
            Scribe_Values.Look(ref collapseWhenIdle, "collapseWhenIdle", false);
            Scribe_Values.Look(ref verticalTiers, "verticalTiers", false);
            Scribe_Values.Look(ref bufferedRendering, "bufferedRendering", true);
            Scribe_Values.Look(ref keepReadoutToggle, "keepReadoutToggle", false);
            Scribe_Values.Look(ref reserveForBills, "reserveForBills", false);
            Scribe_Values.Look(ref reserveForBuildables, "reserveForBuildables", false);
            Scribe_Values.Look(ref showNegativeCounts, "showNegativeCounts", false);
            Scribe_Values.Look(ref qualityJobsRework, "qualityJobsRework", false);
            Scribe_Collections.Look(ref tierDepths, "tierDepths", LookMode.Value, LookMode.Value);
            if (tierDepths == null) tierDepths = new Dictionary<string, int>();
            Scribe_Collections.Look(ref enabledGroups, "enabledGroups", LookMode.Value, LookMode.Value);
            if (enabledGroups == null) enabledGroups = new Dictionary<string, bool>();
        }
    }
}
