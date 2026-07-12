using Verse;

namespace WorkRoles
{
    public enum ChipDisplay { Normal, Compact, Minimal }
    public enum ColonistOrder { ColonistBar, Alphabetical }
    public enum PaletteMode { Skills, Groups, Hidden }

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
        /// Palette arrangement: skill clusters, role groups in player order, or
        /// collapsed entirely (only the mode button remains).
        public PaletteMode paletteMode = PaletteMode.Skills;
        /// Player-chosen window size (0 = automatic, content-driven). Content
        /// minimums still apply: the stored size only ever enlarges the window.
        public float windowWidth;
        public float windowHeight;
        /// Mods already warned about swallowed SetPriority calls, one entry per
        /// "<worldKey>|<packageId>" (per savegame, but player-side: world state
        /// writes from client-local calls would desync MP).
        public System.Collections.Generic.List<string> warnedPriorityMods = new System.Collections.Generic.List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref chipDisplay, "chipDisplay", ChipDisplay.Normal);
            Scribe_Values.Look(ref colonistOrder, "colonistOrder", ColonistOrder.ColonistBar);
            Scribe_Collections.Look(ref skillColumns, "skillColumns", LookMode.Value);
            Scribe_Values.Look(ref groupBy, "groupBy", "none");
            Scribe_Collections.Look(ref collapsedGroups, "collapsedGroups", LookMode.Value);
            Scribe_Values.Look(ref sortColumn, "sortColumn", "");
            Scribe_Collections.Look(ref collapsedRoleGroups, "collapsedRoleGroups", LookMode.Value);
            Scribe_Values.Look(ref nestedRoleTree, "nestedRoleTree", true);
            Scribe_Values.Look(ref paletteMode, "paletteMode", PaletteMode.Skills);
            Scribe_Values.Look(ref windowWidth, "windowWidth", 0f);
            Scribe_Values.Look(ref windowHeight, "windowHeight", 0f);
            Scribe_Collections.Look(ref warnedPriorityMods, "warnedPriorityMods", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
                groupBy ??= "none";
                sortColumn ??= "";
            }
            else
            {
                skillColumns ??= new System.Collections.Generic.List<string>();
                collapsedGroups ??= new System.Collections.Generic.List<string>();
                collapsedRoleGroups ??= new System.Collections.Generic.List<string>();
                warnedPriorityMods ??= new System.Collections.Generic.List<string>();
            }
        }
    }
}
