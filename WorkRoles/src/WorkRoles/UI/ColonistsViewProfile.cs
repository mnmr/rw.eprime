using System;
using System.Collections.Generic;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Per-instance bindings for a ColonistsTabView: the pawn source, where its
    /// display settings persist, and which optional panels it shows — so a
    /// second table (future Mechs tab) can exist beside the colonists one.
    public sealed class ColonistsViewProfile
    {
        /// Pawns the table lists for a given scope.
        public Func<ScopeOption, List<Pawn>> PawnsIn;

        // Persisted display settings (null-safe: getters default, setters
        // no-op without storage and persist immediately).
        public Func<string> GetGroupBy;
        public Action<string> SetGroupBy;
        public Func<string> GetSortColumn;
        public Action<string> SetSortColumn;
        public Func<ColonistOrder> GetColonistOrder;
        public Action<ColonistOrder> SetColonistOrder;
        public Func<List<string>> GetCollapsedGroups;
        public Action<List<string>> SetCollapsedGroups;
        public Func<List<string>> GetSkillColumns;
        public Action<List<string>> SetSkillColumns;
        public Func<ChipDisplay> GetTableChips;
        public Action<ChipDisplay> SetTableChips;

        /// Optional panels: the skill-columns UI and the stats-panel
        /// recommendation section.
        public bool ShowSkills;
        public bool ShowRecommendations;

        /// The standard profile: colony pawns, ModSettings-backed display
        /// prefs, all panels on.
        public static ColonistsViewProfile Colonists() => new ColonistsViewProfile
        {
            PawnsIn = ColonyScope.PawnsIn,
            GetGroupBy = () => WorkRolesMod.Settings?.groupBy ?? "none",
            SetGroupBy = v => Persist(s => s.groupBy = v),
            GetSortColumn = () => WorkRolesMod.Settings?.sortColumn ?? "",
            SetSortColumn = v => Persist(s => s.sortColumn = v),
            GetColonistOrder = () => WorkRolesMod.Settings?.colonistOrder ?? ColonistOrder.ColonistBar,
            SetColonistOrder = v => Persist(s => s.colonistOrder = v),
            GetCollapsedGroups = () => WorkRolesMod.Settings?.collapsedGroups,
            SetCollapsedGroups = v => Persist(s => s.collapsedGroups = v),
            GetSkillColumns = () => WorkRolesMod.Settings?.skillColumns,
            SetSkillColumns = v => Persist(s => s.skillColumns = v),
            GetTableChips = () => WorkRolesMod.Settings?.chipDisplay ?? ChipDisplay.Normal,
            SetTableChips = v => Persist(s => s.chipDisplay = v),
            ShowSkills = true,
            ShowRecommendations = true,
        };

        private static void Persist(Action<WorkRolesSettings> apply)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            apply(settings);
            settings.Write();
        }
    }
}
