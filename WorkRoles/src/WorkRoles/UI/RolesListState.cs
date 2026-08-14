using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Owns the Roles tab's group/tree projection, local list filters, collapse
    /// revision, and flattened row snapshot. Rendering and commands stay in the
    /// view; other views may consume the same read-only catalog projection.
    internal sealed class RolesListState
    {
        // Owner: process role catalog, with explicit window lifecycle release.
        // Key: nested/flat slot plus UiVersion. Value: private section-builder
        // projections; their mutable Lists and authoritative Role references are
        // never published to rendering. Dependencies: role/group structure,
        // ordering, collapse-independent nesting, and language through UiVersion.
        // Refresh: lazy on the first BuildSections read after revision change.
        // Equality: exact slot/revision hits preserve builder identity. Teardown:
        // ReleaseSectionsSnapshot clears both slots when window data is released.
        private static readonly List<RoleSection>[] sectionsCache = new List<RoleSection>[2];
        private static readonly int[] sectionsCacheStamp = { -1, -1 };
        private static int collapseRevision;

        // Owner: window. Key: this RolesListState instance. Value: immutable
        // role-list render rows; producer-owned buffers are transferred directly
        // and never exposed for mutation. Dependencies: UiVersion, location and
        // collapse revisions, nested/search/job-filter state, language.
        // Refresh: immediate at the next Snapshot call after a dependency moves.
        // Equality: unchanged dependencies preserve snapshot identity. Teardown:
        // Reset/ReleaseWindowData and language invalidation release the rows.
        private List<RoleListRowSnapshot> displayRows;
        private RoleListSnapshot snapshot;
        private int displayStamp = -1;
        private int displayLocationRevision = -1;
        private int displayCollapseRevision = -1;
        private bool displayNested;
        private string displaySearch;
        private string displayJobFilter;

        internal string RoleSearch { get; set; } = "";
        internal string JobFilterDefName { get; set; }
        internal bool FiltersActive => !RoleSearch.NullOrEmpty() || JobFilterDefName != null;

        internal void Reset()
        {
            RoleSearch = "";
            JobFilterDefName = null;
            displayRows = null;
            snapshot = null;
            displayStamp = -1;
            displayLocationRevision = -1;
            displayCollapseRevision = -1;
            displayNested = false;
            displaySearch = null;
            displayJobFilter = null;
        }

        internal void InvalidateLanguageCaches()
        {
            displayRows = null;
            snapshot = null;
            displayStamp = -1;
        }

        internal RoleListSnapshot Snapshot(RoleStore store, int selectedRoleId,
            bool revealSelected, System.Func<Role, StructuredTip> roleTooltip)
        {
            bool filtered = FiltersActive;
            bool nested = (WorkRolesMod.Settings?.nestedRoleTree ?? true) && !filtered;
            IReadOnlyList<RoleSection> sections = filtered ? null : BuildSections(store, nested);

            if (revealSelected && sections != null)
                foreach (RoleSection section in sections)
                    if (IsSectionCollapsed(section.key)
                        && section.rows.Any(row => row.role.id == selectedRoleId))
                        ToggleSectionCollapsed(section.key);

            if (displayRows == null || displayStamp != UiVersion.Current
                || displayLocationRevision != ColonyScope.LocationRevision
                || displayCollapseRevision != collapseRevision || displayNested != nested
                || displaySearch != RoleSearch || displayJobFilter != JobFilterDefName)
            {
                displayStamp = UiVersion.Current;
                displayLocationRevision = ColonyScope.LocationRevision;
                displayCollapseRevision = collapseRevision;
                displayNested = nested;
                displaySearch = RoleSearch;
                displayJobFilter = JobFilterDefName;
                displayRows = new List<RoleListRowSnapshot>();
                var liveLocationIds = new HashSet<string>(
                    ColonyScope.Locations().Select(location => location.Id));
                if (filtered)
                {
                    foreach (Role role in store.roles.Where(MatchesFilters))
                        displayRows.Add(PublishRoleRow(null, role, store, 0,
                            virtualRow: false, liveLocationIds, roleTooltip));
                }
                else
                {
                    var publishedSections = new Dictionary<RoleSection,
                        RoleListSectionSnapshot>(sections.Count);
                    foreach (RoleSection section in sections)
                        publishedSections.Add(section,
                            PublishSection(section, store));
                    foreach (RoleSection section in sections)
                    {
                        RoleListSectionSnapshot publishedSection =
                            publishedSections[section];
                        displayRows.Add(RoleListRowSnapshot.ForHeader(
                            publishedSection));
                        if (!IsSectionCollapsed(section.key))
                            foreach (var (member, parent, depth, virtualRow) in section.rows)
                                displayRows.Add(PublishRoleRow(publishedSection,
                                    member, store, depth, virtualRow,
                                    liveLocationIds, roleTooltip));
                    }
                }
                snapshot = new RoleListSnapshot(displayRows, filtered);
            }
            return snapshot;
        }

        private static RoleListRowSnapshot PublishRoleRow(
            RoleListSectionSnapshot section, Role role, RoleStore store,
            int depth, bool virtualRow, ISet<string> liveLocationIds,
            System.Func<Role, StructuredTip> roleTooltip)
        {
            string originGroupLabel = null;
            if (virtualRow)
                originGroupLabel = store.GroupById(role.groupId)?.label
                    ?? "WR_GroupDefault".Translate().ToString();
            return new RoleListRowSnapshot(section, role.id, depth, virtualRow,
                RoleLocationValidity.IsInvalid(
                    role.composite ? role.memberRoleIds.Count : role.entries.Count,
                    role.locationTokens, liveLocationIds),
                role.enabled
                    ? role.label
                    : "WR_RoleLabelOff".Translate(role.label).ToString(),
                roleTooltip?.Invoke(role), role.enabled, role.hasCustomColor,
                role.color, role.blocker, role.activeHours != Role.AllHours,
                role.locationTokens.Count > 0, role.composite, originGroupLabel);
        }

        private static RoleListSectionSnapshot PublishSection(RoleSection section,
            RoleStore store)
        {
            var nested = new List<int>();
            if (section.rows != null)
                for (int i = 0; i < section.rows.Count; i++)
                {
                    var row = section.rows[i];
                    if (row.parent != null && !row.virtualRow)
                        nested.Add(row.role.id);
                }
            return new RoleListSectionSnapshot(section.key, section.displayTitle,
                section.commandName, section.group?.id ?? -1,
                section.group == null ? -1 : store.groups.IndexOf(section.group),
                section.renamable, section.draggable, section.dropTarget,
                section.roots != null && section.roots.Count > 0
                    ? section.roots[0].id : -1,
                nested);
        }

        private bool MatchesFilters(Role role)
        {
            if (!RoleSearch.NullOrEmpty()
                && (role.label == null
                    || role.label.IndexOf(RoleSearch,
                        System.StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (JobFilterDefName == null) return true;

            WorkGiverDef giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(JobFilterDefName);
            // A composite spells nothing itself; its coverage is the members'
            // expanded giver union, which is what the filter asks about.
            if (role.composite) return role.Coverage().Contains(JobFilterDefName);
            string parentType = giver?.workType?.defName;
            return role.entries.Any(entry => entry.Kind == JobEntryKind.WorkGiver
                ? entry.DefName == JobFilterDefName
                : parentType != null && entry.DefName == parentType);
        }

        internal static bool IsSectionCollapsed(string key) =>
            WorkRolesMod.Settings?.collapsedRoleGroups.Contains(key) == true;

        internal static void ToggleSectionCollapsed(string key)
        {
            WorkRolesSettings settings = WorkRolesMod.Settings;
            if (settings == null) return;
            if (!settings.collapsedRoleGroups.Remove(key))
                settings.collapsedRoleGroups.Add(key);
            settings.Write();
            collapseRevision++;
        }

        internal static void InvalidateSectionsSnapshot()
            => sectionsCacheStamp[0] = sectionsCacheStamp[1] = -1;

        /// Invalidation alone leaves the old world's roles reachable until the
        /// next build. Close/teardown uses this stronger ownership release.
        internal static void ReleaseSectionsSnapshot()
        {
            sectionsCache[0] = sectionsCache[1] = null;
            InvalidateSectionsSnapshot();
        }

        internal static IReadOnlyList<RoleSection> BuildSections(RoleStore store, bool nested)
        {
            int slot = nested ? 1 : 0;
            if (sectionsCache[slot] == null || sectionsCacheStamp[slot] != UiVersion.Current)
            {
                sectionsCacheStamp[slot] = UiVersion.Current;
                sectionsCache[slot] = BuildSectionsUncached(store, nested);
            }
            return sectionsCache[slot];
        }

        internal static (IReadOnlyList<Role> roots,
            IReadOnlyList<(Role role, Role parent, int depth, bool virtualRow)> rows)
            BuildRoleTree(RoleStore store)
        {
            var roots = new List<Role>();
            var rows = new List<(Role role, Role parent, int depth, bool virtualRow)>();
            foreach (RoleSection section in BuildSections(store, nested: true))
            {
                roots.AddRange(section.roots);
                rows.AddRange(section.rows);
            }
            return (roots, rows);
        }

        /// Blockers nest under blockers, normal roles under normal roles;
        /// rule-carrying roles stay flat (they display under Conditional Roles) and
        /// composites never join coverage nesting (their coverage is the
        /// members' union, so it would show every covered role twice). A
        /// composite's member rows are a pure convenience display: always
        /// virtual, direct members only, and members keep their normal place
        /// in their own section.
        internal static bool CanNest(Role parent, Role child)
            => parent.blocker == child.blocker && !parent.HasRules && !child.HasRules
               && !parent.composite && !child.composite;

        private static (List<Role> roots,
            List<(Role role, Role parent, int depth, bool virtualRow)> rows)
            BuildRoleTree(List<Role> members, List<Role> allRoles)
        {
            var memberSet = new HashSet<Role>(members);
            var nested = new HashSet<Role>();
            foreach (Role role in members)
                if (members.Any(other => CanNest(other, role) && other.Covers(role)))
                    nested.Add(role);

            List<Role> roots = members.Where(role => !nested.Contains(role)).ToList();
            var rows = new List<(Role role, Role parent, int depth, bool virtualRow)>(members.Count);
            foreach (Role root in roots)
            {
                rows.Add((root, null, 0, false));
                AddChildren(root, 1);
            }
            return (roots, rows);

            void AddChildren(Role parent, int depth)
            {
                if (parent.composite)
                {
                    // Direct members in list order, no recursion into their
                    // own subtrees: the rows only spell out the bundle.
                    foreach (int memberId in parent.memberRoleIds)
                    {
                        Role child = allRoles.FirstOrDefault(
                            candidate => candidate.id == memberId);
                        if (child != null)
                            rows.Add((child, parent, depth, true));
                    }
                    return;
                }
                var covered = allRoles
                    .Where(role => CanNest(parent, role) && parent.Covers(role))
                    .ToList();
                if (covered.Count == 0) return;
                var coverages = covered.Select(role => role.Coverage()).ToList();
                var orderedCoverage = CoverageMath.OrderedCoverageOf(
                    parent.entries, GameJobCatalog.Instance);
                foreach (int index in CoverageMath.ImmediatelyCoveredIndexes(coverages)
                    .OrderBy(i => CoverageMath.FirstCoveredIndex(orderedCoverage, coverages[i])))
                {
                    Role child = covered[index];
                    rows.Add((child, parent, depth, !memberSet.Contains(child)));
                    AddChildren(child, depth + 1);
                }
            }
        }

        private static List<RoleSection> BuildSectionsUncached(RoleStore store, bool nested)
        {
            var sections = new List<RoleSection>();
            var byGroupId = new Dictionary<int, RoleSection>();
            RoleSection defaultSection = null;

            RoleSection Default() => defaultSection ??= new RoleSection
            {
                key = "g0",
                title = "WR_GroupDefault".Translate(),
                group = store.GroupById(RoleGroup.DefaultId),
                dropTarget = true,
            };

            RoleSection SectionOf(int groupId)
            {
                if (byGroupId.TryGetValue(groupId, out RoleSection section)) return section;
                RoleGroup group = store.GroupById(groupId);
                section = group == null || group.id == RoleGroup.DefaultId
                    ? Default()
                    : new RoleSection
                    {
                        key = "g" + group.id,
                        title = group.label,
                        commandName = group.label,
                        group = group,
                        renamable = true,
                        draggable = true,
                        dropTarget = true,
                    };
                byGroupId[groupId] = section;
                return section;
            }

            var auto = new RoleSection
            {
                key = "auto",
                title = "WR_GroupConditionalRoles".Translate(),
            };
            foreach (Role role in store.roles)
            {
                // Composites live in their stored group like any other role;
                // carrying rules moves any role (composite or not) to
                // Conditional Roles until the rules clear.
                if (role.HasRules) auto.members.Add(role);
                else SectionOf(role.groupId).members.Add(role);
            }

            // Conditional Roles leads the list; player groups follow.
            if (auto.members.Count > 0) sections.Add(auto);
            if (defaultSection != null && defaultSection.members.Count > 0)
                sections.Add(defaultSection);
            foreach (RoleGroup group in store.groups)
            {
                if (group.id == RoleGroup.DefaultId) continue;
                if (byGroupId.TryGetValue(group.id, out RoleSection section)
                    && section.members.Count > 0 && !sections.Contains(section))
                    sections.Add(section);
            }

            foreach (RoleSection section in sections)
            {
                if (section != auto)
                {
                    (section.roots, section.rows) = BuildRoleTree(section.members, store.roles);
                    if (!nested)
                    {
                        // Flat mode keeps the tree's depth-first visual order (a
                        // parent directly before its children) without
                        // indentation or virtual rows; a role covered by two
                        // parents keeps only its first occurrence.
                        var seen = new HashSet<Role>();
                        var flat = new List<(Role role, Role parent, int depth,
                            bool virtualRow)>(section.members.Count);
                        foreach (var row in section.rows)
                            if (!row.virtualRow && seen.Add(row.role))
                                flat.Add((row.role, null, 0, false));
                        section.rows = flat;
                        section.roots = flat.Select(row => row.role).ToList();
                    }
                }
                else
                {
                    section.roots = section.members;
                    section.rows = section.members
                        .Select(role => (role, (Role)null, 0, false)).ToList();
                }
                section.displayTitle = section.title + " (" + section.members.Count + ")";
            }
            return sections;
        }
    }

    internal sealed class RoleListSnapshot
    {
        internal RoleListSnapshot(
            List<RoleListRowSnapshot> rows,
            bool filtered)
        {
            this.rows = rows;
            Filtered = filtered;
        }

        private readonly List<RoleListRowSnapshot> rows;
        internal int Count => rows.Count;
        internal RoleListRowSnapshot RowAt(int index) => rows[index];
        internal bool Filtered { get; }

        internal int GroupIndexOf(int groupId)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                RoleListSectionSnapshot section = rows[i].Section;
                if (section != null && section.GroupId == groupId)
                    return section.GroupIndex;
            }
            return -1;
        }
    }

    internal sealed class RoleListRowSnapshot
    {
        internal RoleListRowSnapshot(RoleListSectionSnapshot section, int roleId,
            int depth, bool virtualRow, bool invalid, string label,
            StructuredTip tooltip, bool enabled, bool hasCustomColor, Color color,
            bool blocker, bool hasTimeRule, bool hasLocationRule,
            bool composite, string virtualOriginGroupLabel)
        {
            Section = section;
            RoleId = roleId;
            Depth = depth;
            VirtualRow = virtualRow;
            Invalid = invalid;
            Label = label;
            Tooltip = tooltip;
            Enabled = enabled;
            HasCustomColor = hasCustomColor;
            Color = color;
            Blocker = blocker;
            HasTimeRule = hasTimeRule;
            HasLocationRule = hasLocationRule;
            Composite = composite;
            VirtualOriginGroupLabel = virtualOriginGroupLabel;
        }

        internal static RoleListRowSnapshot ForHeader(
            RoleListSectionSnapshot section) =>
            new RoleListRowSnapshot(section, -1, 0, false, false, null,
                null, true, false, default, false, false, false, false, null);

        internal RoleListSectionSnapshot Section { get; }
        internal int RoleId { get; }
        internal int Depth { get; }
        internal bool VirtualRow { get; }
        internal bool Invalid { get; }
        internal string Label { get; }
        internal StructuredTip Tooltip { get; }
        internal bool Enabled { get; }
        internal bool HasCustomColor { get; }
        internal Color Color { get; }
        internal bool Blocker { get; }
        internal bool HasTimeRule { get; }
        internal bool HasLocationRule { get; }
        internal bool Composite { get; }
        internal string VirtualOriginGroupLabel { get; }
    }

    internal sealed class RoleListSectionSnapshot
    {
        private readonly List<int> nestedRoleIds;

        internal RoleListSectionSnapshot(string key, string displayTitle,
            string commandName, int groupId, int groupIndex, bool renamable,
            bool draggable, bool dropTarget, int firstRootRoleId,
            List<int> nestedRoleIds)
        {
            Key = key;
            DisplayTitle = displayTitle;
            CommandName = commandName;
            GroupId = groupId;
            GroupIndex = groupIndex;
            Renamable = renamable;
            Draggable = draggable;
            DropTarget = dropTarget;
            FirstRootRoleId = firstRootRoleId;
            this.nestedRoleIds = nestedRoleIds;
        }

        internal bool ContainsNestedRole(int roleId)
        {
            for (int i = 0; i < nestedRoleIds.Count; i++)
                if (nestedRoleIds[i] == roleId) return true;
            return false;
        }

        internal string Key { get; }
        internal string DisplayTitle { get; }
        internal string CommandName { get; }
        internal int GroupId { get; }
        internal int GroupIndex { get; }
        internal bool Renamable { get; }
        internal bool Draggable { get; }
        internal bool DropTarget { get; }
        internal int FirstRootRoleId { get; }
    }

    /// One display section of the role list: a user group or the derived
    /// Conditional Roles overlay. Instances belong to the shared section snapshot.
    internal sealed class RoleSection
    {
        internal string key;
        internal string title;
        internal string commandName = "";
        internal RoleGroup group;
        internal bool renamable;
        internal bool draggable;
        internal bool dropTarget;
        internal List<Role> members = new List<Role>();
        internal List<Role> roots;
        internal List<(Role role, Role parent, int depth, bool virtualRow)> rows;
        internal string displayTitle;
    }
}
