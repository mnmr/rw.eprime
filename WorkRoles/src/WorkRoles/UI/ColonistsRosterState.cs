using System;
using System.Collections.Generic;
using System.Linq;
using RimShared.Common;
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

        // Owner: Colonists window. Key: current-map identity plus the selected
        // ScopeOption. Value: the current scope options and producer-owned pawn
        // cohort; live Pawn references are stable external identities and the
        // collection itself is not exposed for mutation. Dependencies: UiVersion,
        // observed-map/pawn-list revision, map set, selected scope, and language.
        // Refresh: immediate on the next ListedPawns read after invalidation.
        // Equality: matching ScopeCacheStamp and map identity reuse the cohort.
        // Teardown: ReleaseSnapshots clears scope, options, pawns, and stamps.
        private ScopeOption? scope;
        private List<Pawn>? pawns;
        private ScopeCacheStamp pawnsStamp = ScopeCacheStamp.Invalid;
        private int pawnsMapId = -1;
        private List<ScopeOption>? scopeOptions;
        private bool spansMultipleLocations;

        // Owner: Colonists window. Key: RoleStore identity, UiVersion, language,
        // and definition revisions. Value: an immutable detached role/definition
        // catalog shared by palette, filters, grouping controls, and chip labels.
        // Dependencies: ordered role catalog/coverage/tree placement, job and skill
        // definitions, group sources, and translated labels. Refresh: immediate on
        // the next catalog read after a key change. Equality: an equal rebuild keeps
        // snapshot identity; a different store always republishes. Teardown:
        // ReleaseSnapshots drops the snapshot and owner reference.
        private ColonistsRosterCatalogSnapshot? catalog;
        private RoleStore? catalogOwner;
        private int catalogUiVersion = -1;
        private int catalogLanguageRevision = -1;
        private int catalogDefinitionRevision = -1;

        // Owner: Colonists window. Key: pawn-scope stamp, map identity, filters,
        // grouping/sort preferences, colonist order, and detached catalog identity.
        // Value: immutable producer-owned grouped display sections. Dependencies:
        // the complete key plus projected assignments and pawn presentation facts.
        // Refresh: immediate on the next Sections read after a key change. Equality:
        // equal rebuilt contents preserve identity. Teardown: ReleaseSnapshots
        // releases the snapshot and all remembered keys.
        private ColonistSectionsSnapshot? sections;
        private RoleStore? sectionsOwner;
        private ColonistsRosterCatalogSnapshot? sectionsCatalog;
        private ScopeCacheStamp sectionsStamp = ScopeCacheStamp.Invalid;
        private int sectionsMapId = -1;
        private string? sectionsSearch;
        private int sectionsRoleFilter;
        private string? sectionsJobFilter;
        private string? sectionsGroupBy;
        private string? sectionsSort;
        private ColonistOrder sectionsOrder;

        // Owner: Colonists window profile. Key: the profile's persisted skill
        // column names and definition revision. Value: an immutable bounded
        // snapshot of stable game-owned SkillDef references. Dependencies:
        // profile edits, definition availability/reload, and initial profile load.
        // Refresh: immediate on an explicit column edit; otherwise lazy after a
        // definition revision. Equality: an equal rebuild preserves snapshot
        // identity and revision. Teardown: ReleaseSnapshots drops the snapshot.
        private ColonistSkillColumnsSnapshot? skillColumns;
        private bool skillColumnsLoaded;
        private int skillColumnsDefinitionRevision = -1;
        private int skillColumnsRevision;

        internal ColonistsRosterState(ColonistsViewProfile profile,
            Func<Pawn, SkillDef, float> skillSortValue)
        {
            this.profile = profile;
            this.skillSortValue = skillSortValue
                ?? throw new ArgumentNullException(nameof(skillSortValue));
        }

        internal string Search { get; set; } = "";
        internal int RoleFilterId { get; set; } = -1;
        /// Work-giver defName; pawns pass when an assigned non-blocker role's
        /// coverage contains it.
        internal string? JobFilterDefName { get; set; }
        internal bool FiltersActive =>
            !Search.NullOrEmpty() || RoleFilterId != -1 || JobFilterDefName != null;
        internal ScopeCacheStamp PawnListStamp
        {
            get
            {
                return pawnListRevisions.Stamp(
                    UiVersion.Current, Find.CurrentMap?.uniqueID ?? -1);
            }
        }
        internal int PawnListRevision => PawnListStamp.PawnListRevision;
        internal ScopeOption? Scope => scope;
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
        internal ColonistSkillColumnsSnapshot SkillColumns
        {
            get { EnsureSkillColumnsLoaded(); return skillColumns!; } // published by EnsureSkillColumnsLoaded
        }
        internal int SkillColumnsRevision
        {
            get { EnsureSkillColumnsLoaded(); return skillColumnsRevision; }
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

        internal void Reset()
        {
            Search = "";
            RoleFilterId = -1;
            JobFilterDefName = null;
            InvalidatePawnSnapshot();
            InvalidateSections();
        }

        internal void InvalidateLanguageCaches()
        {
            scopeOptions = null;
            catalogLanguageRevision = -1;
            InvalidatePawnSnapshot();
            InvalidateSections();
        }

        internal void ReleaseSnapshots()
        {
            Search = "";
            RoleFilterId = -1;
            JobFilterDefName = null;
            scope = null;
            pawns = null;
            pawnsStamp = ScopeCacheStamp.Invalid;
            pawnsMapId = -1;
            scopeOptions = null;
            spansMultipleLocations = false;
            sections = null;
            sectionsOwner = null;
            sectionsCatalog = null;
            InvalidateSections();
            sectionsMapId = -1;
            sectionsSearch = null;
            sectionsRoleFilter = -1;
            sectionsJobFilter = null;
            sectionsGroupBy = null;
            sectionsSort = null;
            skillColumns = null;
            skillColumnsLoaded = false;
            skillColumnsDefinitionRevision = -1;
            catalog = null;
            catalogOwner = null;
            catalogUiVersion = -1;
            catalogLanguageRevision = -1;
            catalogDefinitionRevision = -1;
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
        /// post-UiVersion Repaint refresh must discard any sections that were
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
                ScopeOption revalidated = ScopeEngine.Revalidate(scope!, scopeOptions); // Revalidate handles a null scope
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

        internal void SelectScope(ScopeOption? value)
        {
            if (scope != null && value != null && SameScope(scope, value)) return;
            scope = value;
            InvalidatePawnSnapshot();
        }

        private static bool SameScope(ScopeOption left, ScopeOption right)
            => left.Kind == right.Kind && left.LocationId == right.LocationId;

        internal ColonistsRosterCatalogSnapshot Catalog(RoleStore store)
        {
            int uiVersion = UiVersion.Current;
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (catalog != null && ReferenceEquals(catalogOwner, store)
                && catalogUiVersion == uiVersion
                && catalogLanguageRevision == languageRevision
                && catalogDefinitionRevision == definitionRevision)
                return catalog;

            bool rebuildDefinitions = catalog == null
                || catalogLanguageRevision != languageRevision
                || catalogDefinitionRevision != definitionRevision;
            bool rebuildGroups = catalog == null
                || catalogLanguageRevision != languageRevision;
            ColonistsRosterCatalogSnapshot rebuilt =
                ColonistsRosterCatalogSnapshot.Build(store, catalog,
                    rebuildDefinitions, rebuildGroups,
                    !ReferenceEquals(catalogOwner, store));
            if (!ReferenceEquals(catalogOwner, store) || catalog == null
                || !catalog.ContentEquals(rebuilt))
                catalog = rebuilt;
            catalogOwner = store;
            catalogUiVersion = uiVersion;
            catalogLanguageRevision = languageRevision;
            catalogDefinitionRevision = definitionRevision;
            return catalog!; // assigned above whenever it was null
        }

        internal void ValidateRoleFilter(ColonistsRosterCatalogSnapshot current)
        {
            if (RoleFilterId != -1 && !current.ContainsRole(RoleFilterId))
            {
                RoleFilterId = -1;
                InvalidateSections();
            }
            // A removed mod can take the filtered giver with it.
            if (JobFilterDefName != null
                && !current.ContainsJob(JobFilterDefName))
            {
                JobFilterDefName = null;
                InvalidateSections();
            }
        }

        internal ColonistSectionsSnapshot Sections(RoleStore store)
        {
            IReadOnlyList<Pawn> listed = ListedPawns();
            ColonistsRosterCatalogSnapshot currentCatalog = Catalog(store);
            ValidateRoleFilter(currentCatalog);
            ColonistOrder order = profile.GetColonistOrder();
            ScopeCacheStamp stamp = PawnListStamp;
            if (sections == null || sectionsStamp != stamp
                || sectionsMapId != pawnsMapId || sectionsSearch != Search
                || sectionsRoleFilter != RoleFilterId
                || sectionsJobFilter != JobFilterDefName
                || sectionsGroupBy != profile.GetGroupBy()
                || sectionsSort != profile.GetSortColumn() || sectionsOrder != order
                || !ReferenceEquals(sectionsOwner, store)
                || !ReferenceEquals(sectionsCatalog, currentCatalog))
            {
                List<GroupSection<Pawn>> grouped = GroupedSections(
                    OrderedForDisplay(FilteredPawns(listed, store,
                        currentCatalog), currentCatalog));
                ColonistSectionsSnapshot rebuilt =
                    ColonistSectionsSnapshot.Build(grouped,
                        CurrentGroupSource.Partition != null);
                if (!ReferenceEquals(sectionsOwner, store) || sections == null
                    || !sections.ContentEquals(rebuilt))
                    sections = rebuilt;
            }
            sectionsOwner = store;
            sectionsCatalog = currentCatalog;
            sectionsStamp = stamp;
            sectionsMapId = pawnsMapId;
            sectionsSearch = Search;
            sectionsRoleFilter = RoleFilterId;
            sectionsJobFilter = JobFilterDefName;
            sectionsGroupBy = profile.GetGroupBy();
            sectionsSort = profile.GetSortColumn();
            sectionsOrder = order;
            return sections!; // assigned above whenever it was null
        }

        private void InvalidateSections()
        {
            sectionsStamp = ScopeCacheStamp.Invalid;
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

        private List<Pawn> FilteredPawns(IReadOnlyList<Pawn> listed,
            RoleStore store, ColonistsRosterCatalogSnapshot currentCatalog)
        {
            if (!FiltersActive) return listed as List<Pawn> ?? listed.ToList();

            HashSet<int>? matchingRoles = null;
            if (RoleFilterId != -1)
            {
                matchingRoles = new HashSet<int> { RoleFilterId };
                currentCatalog.AddRolesCovering(RoleFilterId, matchingRoles);
            }

            // Search matches pawn names OR job names: the term expands once to
            // the giver set whose display name (or work-type gerund) contains it.
            HashSet<string>? searchGivers =
                currentCatalog.SearchMatchingGivers(Search);

            var result = new List<Pawn>();
            for (int i = 0; i < listed.Count; i++)
            {
                Pawn pawn = listed[i];
                if (!Search.NullOrEmpty()
                    && pawn.LabelShortCap.IndexOf(
                        Search, StringComparison.OrdinalIgnoreCase) < 0
                    && !PawnCoverageIntersects(store, pawn, searchGivers,
                        currentCatalog))
                    continue;
                if (matchingRoles != null)
                {
                    store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
                    if (set == null
                        || !set.assignments.Any(a => matchingRoles.Contains(a.roleId)))
                        continue;
                }
                if (JobFilterDefName != null
                    && !PawnCoverageContains(store, pawn, JobFilterDefName,
                        currentCatalog))
                    continue;
                result.Add(pawn);
            }
            return result;
        }

        /// Union coverage of the pawn's assigned non-blocker roles contains
        /// the giver (blockers veto the job, so they never count as having it).
        private static bool PawnCoverageContains(RoleStore store, Pawn pawn,
            string giverDefName, ColonistsRosterCatalogSnapshot currentCatalog)
        {
            if (!store.pawnSets.TryGetValue(pawn, out PawnRoleSet set)) return false;
            foreach (var assignment in set.assignments)
                if (currentCatalog.RoleCoversJob(assignment.roleId,
                        giverDefName))
                    return true;
            return false;
        }

        private static bool PawnCoverageIntersects(RoleStore store, Pawn pawn,
            HashSet<string>? givers,
            ColonistsRosterCatalogSnapshot currentCatalog)
        {
            if (givers == null || givers.Count == 0) return false;
            if (!store.pawnSets.TryGetValue(pawn, out PawnRoleSet set)) return false;
            foreach (var assignment in set.assignments)
            {
                foreach (string giver in givers)
                    if (currentCatalog.RoleCoversJob(assignment.roleId, giver))
                        return true;
            }
            return false;
        }

        private List<Pawn> OrderedForDisplay(List<Pawn> listed,
            ColonistsRosterCatalogSnapshot currentCatalog)
        {
            List<Pawn> ordered;
            if (profile.GetColonistOrder() == ColonistOrder.Alphabetical)
                ordered = listed.OrderBy(
                    pawn => pawn.LabelShortCap, StringComparer.OrdinalIgnoreCase).ToList();
            else
            {
                List<Pawn>? bar = Find.ColonistBar?.GetColonistsInOrder();
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

            SkillDef? sortSkill = currentCatalog.SkillOrNull(
                profile.GetSortColumn());
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
            List<string>? collapsed = profile.GetCollapsedGroups();
            if (collapsed == null) return;
            if (!collapsed.Remove(groupKey)) collapsed.Add(groupKey);
            profile.SetCollapsedGroups(collapsed);
        }

        internal void ToggleSkillColumn(SkillDef skill)
        {
            EnsureSkillColumnsLoaded();
            List<SkillDef> rebuilt = skillColumns!.Copy();
            int existingIndex = skillColumns.IndexOf(skill);
            if (existingIndex >= 0)
            {
                if (profile.GetSortColumn() == skill.defName) SetSort("");
                rebuilt.RemoveAt(existingIndex);
            }
            else
            {
                if (rebuilt.Count >= MaxSkillColumns)
                {
                    if (profile.GetSortColumn() == rebuilt[0].defName)
                        SetSort("");
                    rebuilt.RemoveAt(0);
                }
                rebuilt.Add(skill);
                SetSort(skill.defName);
            }
            PublishSkillColumns(rebuilt);
            SaveSkillColumns();
        }

        internal void RemoveSkillColumn(int index)
        {
            EnsureSkillColumnsLoaded();
            if (index < 0 || index >= skillColumns!.Count) return;
            SkillDef removed = skillColumns.At(index);
            List<SkillDef> rebuilt = skillColumns.Copy();
            rebuilt.RemoveAt(index);
            PublishSkillColumns(rebuilt);
            if (profile.GetSortColumn() == removed.defName) SetSort("");
            SaveSkillColumns();
        }

        private void EnsureSkillColumnsLoaded()
        {
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (skillColumnsLoaded
                && skillColumnsDefinitionRevision == definitionRevision) return;
            skillColumnsLoaded = true;
            skillColumnsDefinitionRevision = definitionRevision;
            var rebuilt = new List<SkillDef>();
            List<string>? saved = profile.GetSkillColumns();
            if (saved != null)
                foreach (string defName in saved)
                {
                    SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (def != null && !rebuilt.Contains(def)
                        && rebuilt.Count < MaxSkillColumns)
                        rebuilt.Add(def);
                }
            PublishSkillColumns(rebuilt);
            string sort = profile.GetSortColumn();
            if (!sort.NullOrEmpty()
                && skillColumns!.IndexOfDefName(sort) < 0)
                profile.SetSortColumn("");
        }

        private void PublishSkillColumns(List<SkillDef> rebuilt)
        {
            if (skillColumns != null && skillColumns.ContentEquals(rebuilt))
                return;
            skillColumns = new ColonistSkillColumnsSnapshot(rebuilt);
            skillColumnsRevision++;
        }

        private void SaveSkillColumns()
        {
            var saved = new List<string>(skillColumns!.Count);
            for (int i = 0; i < skillColumns.Count; i++)
                saved.Add(skillColumns.At(i).defName);
            profile.SetSkillColumns(saved);
        }
    }

    internal sealed class ColonistSkillColumnsSnapshot
    {
        private readonly List<SkillDef> skills;

        internal ColonistSkillColumnsSnapshot(List<SkillDef> skills)
        {
            this.skills = skills;
        }

        internal int Count => skills.Count;
        internal SkillDef At(int index) => skills[index];
        internal bool Contains(SkillDef skill) => skills.Contains(skill);
        internal int IndexOf(SkillDef skill) => skills.IndexOf(skill);
        internal List<SkillDef> Copy() => new List<SkillDef>(skills);

        internal int IndexOfDefName(string defName)
        {
            for (int i = 0; i < skills.Count; i++)
                if (string.Equals(skills[i].defName, defName,
                        StringComparison.Ordinal)) return i;
            return -1;
        }

        internal bool ContentEquals(List<SkillDef> other)
        {
            if (other == null || skills.Count != other.Count) return false;
            for (int i = 0; i < skills.Count; i++)
                if (!ReferenceEquals(skills[i], other[i])) return false;
            return true;
        }
    }

    internal sealed class ColonistSectionSnapshot
    {
        private readonly List<Pawn> pawns;

        internal ColonistSectionSnapshot(string key, string title,
            List<Pawn> pawns)
        {
            Key = key;
            Title = title;
            this.pawns = pawns;
        }

        internal string Key { get; }
        internal string Title { get; }
        internal int Count => pawns.Count;
        internal Pawn PawnAt(int index) => pawns[index];
        internal bool Contains(Pawn pawn) => pawns.Contains(pawn);
        internal List<Pawn> CopyPawns() => new List<Pawn>(pawns);

        internal bool ContentEquals(ColonistSectionSnapshot other)
        {
            if (other == null || !string.Equals(Key, other.Key,
                    StringComparison.Ordinal)
                || !string.Equals(Title, other.Title,
                    StringComparison.Ordinal) || pawns.Count != other.pawns.Count)
                return false;
            for (int i = 0; i < pawns.Count; i++)
                if (!ReferenceEquals(pawns[i], other.pawns[i])) return false;
            return true;
        }
    }

    internal sealed class ColonistSectionsSnapshot
    {
        private readonly List<ColonistSectionSnapshot> sections;

        private ColonistSectionsSnapshot(
            List<ColonistSectionSnapshot> sections, bool grouped)
        {
            this.sections = sections;
            Grouped = grouped;
        }

        internal bool Grouped { get; }
        internal int Count => sections.Count;
        internal ColonistSectionSnapshot SectionAt(int index) =>
            sections[index];

        internal static ColonistSectionsSnapshot Build(
            List<GroupSection<Pawn>> source, bool grouped)
        {
            var result = new List<ColonistSectionSnapshot>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                GroupSection<Pawn> section = source[i];
                result.Add(new ColonistSectionSnapshot(section.Key,
                    section.Title + " (" + section.Members.Count + ")",
                    section.Members));
            }
            return new ColonistSectionsSnapshot(result, grouped);
        }

        internal List<Pawn> CopyPawns()
        {
            int count = 0;
            for (int i = 0; i < sections.Count; i++)
                count += sections[i].Count;
            var result = new List<Pawn>(count);
            for (int i = 0; i < sections.Count; i++)
            {
                ColonistSectionSnapshot section = sections[i];
                for (int pawnIndex = 0; pawnIndex < section.Count; pawnIndex++)
                    result.Add(section.PawnAt(pawnIndex));
            }
            return result;
        }

        internal bool ContentEquals(ColonistSectionsSnapshot other)
        {
            if (other == null || Grouped != other.Grouped
                || sections.Count != other.sections.Count) return false;
            for (int i = 0; i < sections.Count; i++)
                if (!sections[i].ContentEquals(other.sections[i])) return false;
            return true;
        }
    }
}
