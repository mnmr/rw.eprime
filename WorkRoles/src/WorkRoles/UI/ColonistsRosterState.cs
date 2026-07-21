using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Owns the Colonists tab's pawn scope and display projection: filtering,
    /// ordering, grouping, collapse state, and persisted skill columns.
    internal sealed class ColonistsRosterState
    {
        internal const int MaxSkillColumns = 3;

        private static readonly IReadOnlyList<Pawn> NoPawns = Array.Empty<Pawn>();

        private readonly ColonistsViewProfile profile;
        private readonly Func<Pawn, SkillDef, float> skillSortValue;
        private readonly PawnListRevisionTracker pawnListRevisions =
            new PawnListRevisionTracker();

        private ScopeOption scope;
        private List<Pawn> pawns;
        private ScopeCacheStamp pawnsStamp = ScopeCacheStamp.Invalid;
        private int pawnsMapId = -1;
        private List<ScopeOption> scopeOptions;
        private bool spansMultipleLocations;

        private List<GroupSection<Pawn>> sections;
        private ScopeCacheStamp sectionsStamp = ScopeCacheStamp.Invalid;
        private int sectionsMapId = -1;
        private string sectionsSearch;
        private int sectionsRoleFilter;
        private string sectionsGroupBy;
        private string sectionsSort;
        private ColonistOrder sectionsOrder;
        private readonly Dictionary<string, string> sectionTitles =
            new Dictionary<string, string>();

        private readonly List<SkillDef> skillColumns = new List<SkillDef>();
        private bool skillColumnsLoaded;

        internal ColonistsRosterState(ColonistsViewProfile profile,
            Func<Pawn, SkillDef, float> skillSortValue)
        {
            this.profile = profile;
            this.skillSortValue = skillSortValue
                ?? throw new ArgumentNullException(nameof(skillSortValue));
        }

        internal string Search { get; set; } = "";
        internal int RoleFilterId { get; set; } = -1;
        internal bool FiltersActive => !Search.NullOrEmpty() || RoleFilterId != -1;
        internal ScopeCacheStamp PawnListStamp
        {
            get
            {
                return pawnListRevisions.Stamp(
                    UiVersion.Current, Find.CurrentMap?.uniqueID ?? -1);
            }
        }
        internal int PawnListRevision => PawnListStamp.PawnListRevision;
        internal ScopeOption Scope => scope;
        internal IReadOnlyList<ScopeOption> ScopeOptions
        {
            get
            {
                ListedPawns();
                return scopeOptions != null
                    ? (IReadOnlyList<ScopeOption>)scopeOptions
                    : Array.Empty<ScopeOption>();
            }
        }
        internal bool SpansMultipleLocations
        {
            get { ListedPawns(); return spansMultipleLocations; }
        }
        internal IReadOnlyList<SkillDef> SkillColumns
        {
            get { EnsureSkillColumnsLoaded(); return skillColumns; }
        }
        internal GroupSourceDef CurrentGroupSource
        {
            get
            {
                List<GroupSourceDef> sources = GroupSources.All();
                string key = profile.GetGroupBy();
                for (int i = 0; i < sources.Count; i++)
                    if (sources[i].Key == key) return sources[i];
                return sources[0];
            }
        }
        internal bool Grouped => CurrentGroupSource.Partition != null;
        internal SkillDef SortSkill
        {
            get
            {
                string sort = profile.GetSortColumn();
                return sort.NullOrEmpty()
                    ? null
                    : DefDatabase<SkillDef>.GetNamedSilentFail(sort);
            }
        }

        internal void Reset()
        {
            Search = "";
            RoleFilterId = -1;
            InvalidatePawnSnapshot();
            InvalidateSections();
        }

        internal void InvalidateLanguageCaches()
        {
            scopeOptions = null;
            InvalidatePawnSnapshot();
            InvalidateSections();
        }

        internal void ReleaseSnapshots()
        {
            pawns = null;
            pawnsStamp = ScopeCacheStamp.Invalid;
            InvalidateSections();
            pawnListRevisions.Invalidate();
        }

        internal void InvalidatePawnSnapshot()
        {
            pawnListRevisions.Invalidate();
            pawns = null;
            pawnsStamp = ScopeCacheStamp.Invalid;
            InvalidateSections();
        }

        /// Skill ordering consumes the window's external pawn generation. A
        /// post-UiVersion Layout refresh must discard any sections that were
        /// built during the input event before that generation was recaptured.
        internal void InvalidateSnapshotConsumers() => InvalidateSections();

        internal IReadOnlyList<Pawn> ListedPawns()
        {
            ScopeCacheStamp stamp = PawnListStamp;
            if (Find.CurrentMap == null) return NoPawns;
            if (pawns == null || pawnsStamp != stamp)
            {
                pawnsMapId = Find.CurrentMap.uniqueID;
                scopeOptions = ScopeEngine.BuildOptions(ColonyScope.Locations());
                ScopeOption revalidated = ScopeEngine.Revalidate(scope, scopeOptions);
                if (scope != null && !SameScope(scope, revalidated))
                    pawnListRevisions.Invalidate();
                scope = revalidated;
                pawnsStamp = PawnListStamp;
                pawns = profile.PawnsIn(scope);
                spansMultipleLocations = ScopeEngine.SpansMultipleLocations(
                    pawns.Select(ColonyScope.LocationIdOf));
            }
            return pawns;
        }

        /// Every pawn reachable through any scope in this table. Explicit
        /// external snapshot generations capture this cohort eagerly so later
        /// scope changes never read a different moment of live game state.
        internal IReadOnlyList<Pawn> SnapshotPawns()
        {
            if (Find.CurrentMap == null) return NoPawns;
            return profile.PawnsIn(new ScopeOption { Kind = ScopeKind.All });
        }

        internal void SelectScope(ScopeOption value)
        {
            if (scope != null && value != null && SameScope(scope, value)) return;
            scope = value;
            InvalidatePawnSnapshot();
        }

        private static bool SameScope(ScopeOption left, ScopeOption right)
            => left.Kind == right.Kind && left.LocationId == right.LocationId;

        internal void ValidateRoleFilter(RoleStore store)
        {
            if (RoleFilterId != -1 && store.RoleById(RoleFilterId) == null)
            {
                RoleFilterId = -1;
                InvalidateSections();
            }
        }

        internal IReadOnlyList<GroupSection<Pawn>> Sections(RoleStore store)
        {
            IReadOnlyList<Pawn> listed = ListedPawns();
            ColonistOrder order = profile.GetColonistOrder();
            ScopeCacheStamp stamp = PawnListStamp;
            if (sections == null || sectionsStamp != stamp
                || sectionsMapId != pawnsMapId || sectionsSearch != Search
                || sectionsRoleFilter != RoleFilterId
                || sectionsGroupBy != profile.GetGroupBy()
                || sectionsSort != profile.GetSortColumn() || sectionsOrder != order)
            {
                sectionsStamp = stamp;
                sectionsMapId = pawnsMapId;
                sectionsSearch = Search;
                sectionsRoleFilter = RoleFilterId;
                sectionsGroupBy = profile.GetGroupBy();
                sectionsSort = profile.GetSortColumn();
                sectionsOrder = order;
                sections = GroupedSections(OrderedForDisplay(FilteredPawns(listed, store)));
                sectionTitles.Clear();
                foreach (GroupSection<Pawn> section in sections)
                    sectionTitles[section.Key] = section.Title
                        + " (" + section.Members.Count + ")";
            }
            return sections;
        }

        internal string SectionTitle(string key)
            => sectionTitles.TryGetValue(key, out string title) ? title : key;

        private void InvalidateSections()
        {
            sections = null;
            sectionsStamp = ScopeCacheStamp.Invalid;
            sectionTitles.Clear();
        }

        private List<GroupSection<Pawn>> GroupedSections(List<Pawn> listed)
        {
            GroupSourceDef source = CurrentGroupSource;
            if (source.Partition == null)
                return new List<GroupSection<Pawn>>
                {
                    new GroupSection<Pawn> { Key = "", Title = "", Members = listed },
                };
            return source.Partition(listed);
        }

        private List<Pawn> FilteredPawns(IReadOnlyList<Pawn> listed, RoleStore store)
        {
            if (!FiltersActive) return listed as List<Pawn> ?? listed.ToList();

            HashSet<int> matchingRoles = null;
            if (RoleFilterId != -1)
            {
                matchingRoles = new HashSet<int> { RoleFilterId };
                Role selected = store.RoleById(RoleFilterId);
                if (selected != null)
                    foreach (Role role in store.roles)
                        if (!role.blocker && role.CoversOrMatches(selected))
                            matchingRoles.Add(role.id);
            }

            var result = new List<Pawn>();
            for (int i = 0; i < listed.Count; i++)
            {
                Pawn pawn = listed[i];
                if (!Search.NullOrEmpty() && pawn.LabelShortCap.IndexOf(
                        Search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (matchingRoles != null)
                {
                    store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
                    if (set == null
                        || !set.assignments.Any(a => matchingRoles.Contains(a.roleId)))
                        continue;
                }
                result.Add(pawn);
            }
            return result;
        }

        private List<Pawn> OrderedForDisplay(List<Pawn> listed)
        {
            List<Pawn> ordered;
            if (profile.GetColonistOrder() == ColonistOrder.Alphabetical)
                ordered = listed.OrderBy(
                    pawn => pawn.LabelShortCap, StringComparer.OrdinalIgnoreCase).ToList();
            else
            {
                List<Pawn> bar = Find.ColonistBar?.GetColonistsInOrder();
                if (bar == null) ordered = listed;
                else
                {
                    var pool = new HashSet<Pawn>(listed);
                    ordered = bar.Where(pool.Contains).ToList();
                    if (ordered.Count < listed.Count)
                        foreach (Pawn pawn in listed)
                            if (!ordered.Contains(pawn)) ordered.Add(pawn);
                }
            }

            SkillDef sortSkill = SortSkill;
            if (sortSkill != null)
                ordered = ordered.OrderByDescending(
                    pawn => skillSortValue(pawn, sortSkill)).ToList();
            return ordered;
        }

        internal void SetSort(string column)
        {
            if (profile.GetSortColumn() == column) return;
            profile.SetSortColumn(column);
            InvalidateSections();
        }

        internal bool IsCollapsed(string groupKey)
            => profile.GetCollapsedGroups()?.Contains(groupKey) == true;

        internal void ToggleCollapsed(string groupKey)
        {
            List<string> collapsed = profile.GetCollapsedGroups();
            if (collapsed == null) return;
            if (!collapsed.Remove(groupKey)) collapsed.Add(groupKey);
            profile.SetCollapsedGroups(collapsed);
        }

        internal void ToggleSkillColumn(SkillDef skill)
        {
            EnsureSkillColumnsLoaded();
            if (skillColumns.Contains(skill))
            {
                if (profile.GetSortColumn() == skill.defName) SetSort("");
                skillColumns.Remove(skill);
            }
            else
            {
                if (skillColumns.Count >= MaxSkillColumns)
                {
                    if (profile.GetSortColumn() == skillColumns[0].defName) SetSort("");
                    skillColumns.RemoveAt(0);
                }
                skillColumns.Add(skill);
                SetSort(skill.defName);
            }
            SaveSkillColumns();
        }

        internal void RemoveSkillColumn(int index)
        {
            EnsureSkillColumnsLoaded();
            if (index < 0 || index >= skillColumns.Count) return;
            SkillDef removed = skillColumns[index];
            skillColumns.RemoveAt(index);
            if (profile.GetSortColumn() == removed.defName) SetSort("");
            SaveSkillColumns();
        }

        private void EnsureSkillColumnsLoaded()
        {
            if (skillColumnsLoaded) return;
            skillColumnsLoaded = true;
            List<string> saved = profile.GetSkillColumns();
            if (saved != null)
                foreach (string defName in saved)
                {
                    SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (def != null && !skillColumns.Contains(def)
                        && skillColumns.Count < MaxSkillColumns)
                        skillColumns.Add(def);
                }
            string sort = profile.GetSortColumn();
            if (!sort.NullOrEmpty()
                && !skillColumns.Any(skill => skill.defName == sort))
                profile.SetSortColumn("");
        }

        private void SaveSkillColumns()
            => profile.SetSkillColumns(skillColumns.Select(skill => skill.defName).ToList());
    }
}
