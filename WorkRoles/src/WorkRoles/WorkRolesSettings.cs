using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// What a colonist-table chip shows. The grid/stack layout is the
    /// separate chipGrid preference; the retired combined values
    /// (CompactGrid, IconsGrid) load through the codec as display + grid.
    public enum ChipDisplay
    {
        Normal = 0,
        Compact = 1,
        // Retains Minimal's legacy ordinal; the persistence codec also maps
        // the former textual name to this icon mode.
        Icons = 2,
    }
    /// How a role chip shows its markers: the four-light stripe, one icon
    /// per marker (the original display, with strike-throughs), or none.
    public enum ChipMarkers { Dots = 0, Icons = 1, Off = 2 }
    public enum ColonistOrder { ColonistBar, Alphabetical }
    /// Hidden is retired (kept so older persisted settings still parse); it
    /// normalizes to Skills on load.
    public enum PaletteMode { Skills, Groups, Hidden }

    internal enum OptionsDisplayPreference
    {
        ColonistSkillCaptions,
        VerdictsOnColonistChips,
        VerdictsInPalette,
        RemoveRoleIcon,
        RecommendationsEnabled,
    }

    /// Per-player display preferences: persisted across saves via ModSettings and
    /// deliberately NOT world state (each MP player keeps their own view).
    public class WorkRolesSettings : ModSettings
    {
        public bool autoUpdateDefault;
        public ChipDisplay chipDisplay = ChipDisplay.Normal;
        public ChipMarkers chipMarkers = ChipMarkers.Dots;
        /// Colonist table chips in equal-width grid columns (false = stacked
        /// at their natural widths). New installs start in the grid with
        /// Medium columns; the field initializers are the new-install
        /// defaults, while ExposeData keeps the older stacked/Automatic
        /// fallbacks so an existing settings file that omitted those values
        /// (the game skips values equal to the fallback) loads unchanged.
        public bool chipGrid = true;
        /// Full-name grid chips: how many letters the column is sized for
        /// (names longer than that fade out).
        public GridNamePreference chipGridNames = GridNamePreference.Medium;
        public ColonistOrder colonistOrder = ColonistOrder.ColonistBar;
        /// Skill columns (defNames), so the table reopens exactly as it was closed.
        public System.Collections.Generic.List<string> skillColumns = new System.Collections.Generic.List<string>();
        /// Active grouping (GroupSources key; "none" = flat list).
        public string groupBy = "none";
        /// Collapsed group sections, keyed "<grouping>|<group>".
        public System.Collections.Generic.List<string> collapsedGroups = new System.Collections.Generic.List<string>();
        /// Skill column the table sorts by (defName; "" = default colonist order).
        public string sortColumn = "";
        /// Role list: collapsed group sections ("g<id>", "auto", "locked").
        public System.Collections.Generic.List<string> collapsedRoleGroups = new System.Collections.Generic.List<string>();
        /// Role list: auto-nest covered roles under their coverer (false = flat).
        public bool nestedRoleTree = true;
        /// Palette arrangement: skill clusters or role groups in player order.
        public PaletteMode paletteMode = PaletteMode.Skills;
        /// Suitability verdict badges on role chips, per surface: colonist
        /// table rows (each row's own pawn) and the role palette (the
        /// selected colonist). Recommendation previews always show them.
        public bool verdictsOnColonistChips = true;
        public bool verdictsInPalette = true;
        /// Trash can on assigned role chips (false = no icon; the chip's
        /// right-click menu removes the role instead).
        public bool removeRoleIcon = true;
        /// Recommendation surfaces: the Recommendations tab, Fix My Colony
        /// and the selected colonist's recommended roles. Verdicts and the
        /// per-save auto-optimize schedule are independent of it.
        public bool recommendationsEnabled = true;
        /// Colonist table: best-skills caption under each name (false = name only).
        public bool colonistSkillCaptions = true;
        /// Player-chosen window size (0 = automatic, content-driven). Content
        /// minimums still apply: the stored size only ever enlarges the window.
        public float windowWidth;
        public float windowHeight;
        /// Mods already warned about swallowed SetPriority calls, one entry per
        /// "<worldKey>|<packageId>" (per savegame, but player-side: world state
        /// writes from client-local calls would desync MP).
        public System.Collections.Generic.List<string> warnedPriorityMods = new System.Collections.Generic.List<string>();
        /// Worlds already shown the excluded-giver job notice, one entry per
        /// "<worldKey>|jobBypass" (per savegame, player-side for the same
        /// multiplayer reason as warnedPriorityMods).
        public System.Collections.Generic.List<string> warnedGiverBypass = new System.Collections.Generic.List<string>();
        /// Help tab: topic slugs this player has opened (drives the guided
        /// tour checklist), and whether the completion chime already played.
        /// Player knowledge, so it lives here and never in the savegame.
        public System.Collections.Generic.List<string> helpTopicsRead = new System.Collections.Generic.List<string>();
        public bool helpTourCelebrated;
        /// Saves (world persistent random values) whose one-time welcome
        /// dialog this player has already seen.
        public System.Collections.Generic.List<string> welcomeShownSaves = new System.Collections.Generic.List<string>();
        // Per-player acknowledgement of each active conflict in a particular
        // save. Adding another conflicting mod shows the welcome again once.
        public System.Collections.Generic.List<string> compatibilityWarningsSeen = new System.Collections.Generic.List<string>();

        /// Per-player presentation command used by the Options UI. Returning
        /// false for a no-op keeps persistence and snapshot refresh exact.
        internal bool SetDisplayPreference(
            OptionsDisplayPreference preference, bool value)
        {
            switch (preference)
            {
                case OptionsDisplayPreference.ColonistSkillCaptions:
                    if (colonistSkillCaptions == value) return false;
                    colonistSkillCaptions = value;
                    return true;
                case OptionsDisplayPreference.VerdictsOnColonistChips:
                    if (verdictsOnColonistChips == value) return false;
                    verdictsOnColonistChips = value;
                    return true;
                case OptionsDisplayPreference.VerdictsInPalette:
                    if (verdictsInPalette == value) return false;
                    verdictsInPalette = value;
                    return true;
                case OptionsDisplayPreference.RemoveRoleIcon:
                    if (removeRoleIcon == value) return false;
                    removeRoleIcon = value;
                    return true;
                case OptionsDisplayPreference.RecommendationsEnabled:
                    if (recommendationsEnabled == value) return false;
                    recommendationsEnabled = value;
                    return true;
                default:
                    return false;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref autoUpdateDefault, "autoUpdateDefault");
            var persistedChipDisplay =
                ChipDisplayPreferenceCodec.Encode((int)chipDisplay);
            Scribe_Values.Look(ref persistedChipDisplay, "chipDisplay", "Normal");
            Scribe_Values.Look(ref chipGrid, "chipGrid", false);
            Scribe_Values.Look(ref chipGridNames, "chipGridNames",
                GridNamePreference.Automatic);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                ChipDisplayPreference loaded = ChipDisplayPreferenceCodec.Decode(
                    persistedChipDisplay, chipGrid);
                chipDisplay = (ChipDisplay)loaded.Mode;
                chipGrid = loaded.Grid;
                if (!System.Enum.IsDefined(typeof(GridNamePreference), chipGridNames))
                    chipGridNames = GridNamePreference.Automatic;
            }
            Scribe_Values.Look(ref chipMarkers, "chipMarkers", ChipMarkers.Dots);
            if (Scribe.mode == LoadSaveMode.LoadingVars
                && !System.Enum.IsDefined(typeof(ChipMarkers), chipMarkers))
                chipMarkers = ChipMarkers.Dots;
            Scribe_Values.Look(ref colonistOrder, "colonistOrder", ColonistOrder.ColonistBar);
            Scribe_Collections.Look(ref skillColumns, "skillColumns", LookMode.Value);
            Scribe_Values.Look(ref groupBy, "groupBy", "none");
            Scribe_Collections.Look(ref collapsedGroups, "collapsedGroups", LookMode.Value);
            Scribe_Values.Look(ref sortColumn, "sortColumn", "");
            Scribe_Collections.Look(ref collapsedRoleGroups, "collapsedRoleGroups", LookMode.Value);
            Scribe_Values.Look(ref nestedRoleTree, "nestedRoleTree", true);
            Scribe_Values.Look(ref paletteMode, "paletteMode", PaletteMode.Skills);
            // The retired Hidden mode reads as Skills so the palette always
            // renders (the in-panel cycle button that restored it is gone).
            if (Scribe.mode != LoadSaveMode.Saving
                && paletteMode == PaletteMode.Hidden)
                paletteMode = PaletteMode.Skills;
            Scribe_Values.Look(ref verdictsOnColonistChips, "verdictsOnColonistChips", true);
            Scribe_Values.Look(ref verdictsInPalette, "verdictsInPalette", true);
            Scribe_Values.Look(ref colonistSkillCaptions, "colonistSkillCaptions", true);
            Scribe_Values.Look(ref removeRoleIcon, "removeRoleIcon", true);
            Scribe_Values.Look(ref recommendationsEnabled, "recommendationsEnabled", true);
            Scribe_Values.Look(ref windowWidth, "windowWidth", 0f);
            Scribe_Values.Look(ref windowHeight, "windowHeight", 0f);
            Scribe_Collections.Look(ref warnedPriorityMods, "warnedPriorityMods", LookMode.Value);
            Scribe_Collections.Look(ref warnedGiverBypass, "warnedGiverBypass", LookMode.Value);
            Scribe_Collections.Look(ref helpTopicsRead, "helpTopicsRead", LookMode.Value);
            Scribe_Values.Look(ref helpTourCelebrated, "helpTourCelebrated", false);
            Scribe_Collections.Look(ref welcomeShownSaves, "welcomeShownSaves", LookMode.Value);
            Scribe_Collections.Look(ref compatibilityWarningsSeen, "compatibilityWarningsSeen", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
                warnedGiverBypass ??= new System.Collections.Generic.List<string>();
                helpTopicsRead ??= new System.Collections.Generic.List<string>();
                welcomeShownSaves ??= new System.Collections.Generic.List<string>();
                compatibilityWarningsSeen ??= new System.Collections.Generic.List<string>();
                groupBy ??= "none";
                sortColumn ??= "";
            }
            else
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
                warnedGiverBypass ??= new System.Collections.Generic.List<string>();
                helpTopicsRead ??= new System.Collections.Generic.List<string>();
                welcomeShownSaves ??= new System.Collections.Generic.List<string>();
                compatibilityWarningsSeen ??= new System.Collections.Generic.List<string>();
            }
        }
    }
}
