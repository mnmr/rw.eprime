using Verse;

namespace WorkRoles
{
    public enum ChipDisplay { Normal, Compact, Minimal }
    public enum ColonistOrder { ColonistBar, Alphabetical }

    /// Per-player display preferences: persisted across saves via ModSettings and
    /// deliberately NOT world state (each MP player keeps their own view).
    public class WorkRolesSettings : ModSettings
    {
        public ChipDisplay chipDisplay = ChipDisplay.Normal;
        public ColonistOrder colonistOrder = ColonistOrder.ColonistBar;
        /// Skill columns (defNames) and the sorted column, so the table reopens
        /// exactly as it was closed.
        public System.Collections.Generic.List<string> skillColumns = new System.Collections.Generic.List<string>();
        public string sortSkill;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref chipDisplay, "chipDisplay", ChipDisplay.Normal);
            Scribe_Values.Look(ref colonistOrder, "colonistOrder", ColonistOrder.ColonistBar);
            Scribe_Collections.Look(ref skillColumns, "skillColumns", LookMode.Value);
            Scribe_Values.Look(ref sortSkill, "sortSkill");
            if (Scribe.mode == LoadSaveMode.PostLoadInit || skillColumns == null)
                skillColumns ??= new System.Collections.Generic.List<string>();
        }
    }
}
