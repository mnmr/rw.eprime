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
        // All bindings are set by the factory object initializer (= null!).
        /// Pawns the table lists for a given scope.
        public Func<ScopeOption, List<Pawn>> PawnsIn = null!;

        // Persisted display settings (null-safe: getters default, setters
        // no-op without storage and persist immediately).
        public Func<string> GetGroupBy = null!;
        public Action<string> SetGroupBy = null!;
        public Func<string> GetSortColumn = null!;
        public Action<string> SetSortColumn = null!;
        public Func<ColonistOrder> GetColonistOrder = null!;
        public Action<ColonistOrder> SetColonistOrder = null!;
        public Func<List<string>?> GetCollapsedGroups = null!;
        public Action<List<string>> SetCollapsedGroups = null!;
        public Func<List<string>?> GetSkillColumns = null!;
        public Action<List<string>> SetSkillColumns = null!;
        public Func<ChipDisplay> GetTableChips = null!;
        public Action<ChipDisplay> SetTableChips = null!;

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
            WorkRolesGameComponent.RequestSettingsWrite();
        }
    }
}
