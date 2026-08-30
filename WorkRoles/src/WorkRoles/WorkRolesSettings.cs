using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public enum ChipDisplay
    {
        Normal = 0,
        Compact = 1,
        // Retains Minimal's legacy ordinal; the persistence codec also maps
        // the former textual name to this icon mode.
        Icons = 2,
        CompactGrid = 3,
        IconsGrid = 4,
    }
    public enum ColonistOrder { ColonistBar, Alphabetical }
    /// Hidden is retired (kept so older persisted settings still parse); it
    /// normalizes to Skills on load.
    public enum PaletteMode { Skills, Groups, Hidden }

    internal enum OptionsDisplayPreference
    {
        ColonistSkillCaptions,
        VerdictsOnColonistChips,
        VerdictsInPalette,
        VerdictsOnRecommendationChips,
    }

    /// Per-player display preferences: persisted across saves via ModSettings and
    /// deliberately NOT world state (each MP player keeps their own view).
    public class WorkRolesSettings : ModSettings
    {
        public ChipDisplay chipDisplay = ChipDisplay.Normal;
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
        /// table rows (each row's own pawn), the role palette (the selected
        /// colonist), and recommendation previews (the pawn recommended to).
        public bool verdictsOnColonistChips = true;
        public bool verdictsInPalette = true;
        public bool verdictsOnRecommendationChips = true;
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
        /// Help tab: topic slugs this player has opened (drives the guided
        /// tour checklist), and whether the completion chime already played.
        /// Player knowledge, so it lives here and never in the savegame.
        public System.Collections.Generic.List<string> helpTopicsRead = new System.Collections.Generic.List<string>();
        public bool helpTourCelebrated;

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
                case OptionsDisplayPreference.VerdictsOnRecommendationChips:
                    if (verdictsOnRecommendationChips == value) return false;
                    verdictsOnRecommendationChips = value;
                    return true;
                default:
                    return false;
            }
        }

        public override void ExposeData()
        {
            var persistedChipDisplay =
                ChipDisplayPreferenceCodec.Encode((int)chipDisplay);
            Scribe_Values.Look(ref persistedChipDisplay, "chipDisplay", "Normal");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                chipDisplay = (ChipDisplay)ChipDisplayPreferenceCodec.Decode(
                    persistedChipDisplay);
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
            Scribe_Values.Look(ref verdictsOnRecommendationChips, "verdictsOnRecommendationChips", true);
            Scribe_Values.Look(ref colonistSkillCaptions, "colonistSkillCaptions", true);
            Scribe_Values.Look(ref windowWidth, "windowWidth", 0f);
            Scribe_Values.Look(ref windowHeight, "windowHeight", 0f);
            Scribe_Collections.Look(ref warnedPriorityMods, "warnedPriorityMods", LookMode.Value);
            Scribe_Collections.Look(ref helpTopicsRead, "helpTopicsRead", LookMode.Value);
            Scribe_Values.Look(ref helpTourCelebrated, "helpTourCelebrated", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
                helpTopicsRead ??= new System.Collections.Generic.List<string>();
                groupBy ??= "none";
                sortColumn ??= "";
            }
            else
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
                helpTopicsRead ??= new System.Collections.Generic.List<string>();
            }
        }
    }
}
