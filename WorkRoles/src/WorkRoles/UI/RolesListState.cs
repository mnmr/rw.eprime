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
        // Key: RoleStore identity, nested/flat slot, UiVersion, and definition
        // revision. Value:
        // private section-builder projections; their mutable Lists and
        // authoritative Role references are never published to rendering.
        // Dependencies: store ownership, role/group structure, ordering,
        // collapse-independent nesting, game job definitions, and language.
        // Refresh: lazy on the first BuildSections read after revision change.
        // Equality: exact slot/revision hits preserve builder identity. Teardown:
        // ReleaseSectionsSnapshot clears both slots when window data is released.
        private static readonly List<RoleSection>[] sectionsCache = new List<RoleSection>[2];
        private static readonly int[] sectionsCacheStamp = { -1, -1 };
        private static readonly int[] sectionsCacheDefinitionRevision = { -1, -1 };
        private static RoleStore sectionsCacheOwner;
        private static int collapseRevision;

        // Owner: window. Key: this RolesListState instance and RoleStore
        // identity. Value: immutable role-list render rows; producer-owned
        // buffers are transferred directly and never exposed for mutation.
        // Dependencies: store ownership, UiVersion, definition, location and
        // collapse revisions, nested/search/job-filter state, and language.
        // Refresh: immediate at the next Snapshot call after a dependency moves.
        // Equality: exact equal contents preserve snapshot identity within one
        // store; store changes always republish. Teardown: Reset/ReleaseWindowData
        // release the rows and store reference.
        private RoleListSnapshot snapshot;
        private RoleStore displayOwner;
        private int displayStamp = -1;
        private int displayDefinitionRevision = -1;
        private int displayLocationRevision = -1;
        private int displayCollapseRevision = -1;
        private bool displayNestedPreference;
        private string displaySearch;
        private string displayJobFilter;

        // Owner: this Roles-list state for one open Work Roles window.
        // Key: RoleStore identity and UiVersion.Current.
        // Value: immutable detached ordered role IDs and labels used only for
        // presentation selection and command payload lookup.
        // Dependencies: ordered role-catalog membership, role IDs, and labels.
        // Refresh: immediately on the next selection-snapshot read after the
        // store identity or UI revision changes.
        // Equality: exact equal contents preserve identity within one store;
        // a store-owner change always republishes for ownership partitioning.
        // Teardown: Reset releases the snapshot and its store reference.
        private RoleSelectionSnapshot selectionSnapshot;
        private RoleStore selectionOwner;
        private int selectionStamp = -1;

        internal string RoleSearch { get; set; } = "";
        internal string JobFilterDefName { get; set; }
        internal bool FiltersActive => !RoleSearch.NullOrEmpty() || JobFilterDefName != null;

        internal void Reset()
        {
            RoleSearch = "";
            JobFilterDefName = null;
            snapshot = null;
            displayOwner = null;
            displayStamp = -1;
            displayDefinitionRevision = -1;
            displayLocationRevision = -1;
            displayCollapseRevision = -1;
            displayNestedPreference = false;
            displaySearch = null;
            displayJobFilter = null;
            selectionSnapshot = null;
            selectionOwner = null;
            selectionStamp = -1;
        }

        internal void InvalidateLanguageCaches()
        {
            displayStamp = -1;
            displayDefinitionRevision = -1;
        }

        internal RoleListSnapshot Snapshot(RoleStore store, int selectedRoleId,
            bool revealSelected, System.Func<Role, StructuredTip> roleTooltip)
        {
            bool ownerChanged = !ReferenceEquals(displayOwner, store);
            bool filtered = FiltersActive;
            bool nestedPreference = WorkRolesMod.Settings?.nestedRoleTree ?? true;
            bool nested = nestedPreference && !filtered;
            IReadOnlyList<RoleSection> sections = filtered ? null : BuildSections(store, nested);

            if (revealSelected && sections != null)
                foreach (RoleSection section in sections)
                    if (IsSectionCollapsed(section.key)
                        && section.rows.Any(row => row.role.id == selectedRoleId))
                        ToggleSectionCollapsed(section.key);

            if (ownerChanged || snapshot == null
                || displayStamp != UiVersion.Current
                || displayDefinitionRevision != DefinitionReloadCoordinator.Revision
                || displayLocationRevision != ColonyScope.LocationRevision
                || displayCollapseRevision != collapseRevision
                || displayNestedPreference != nestedPreference
                || displaySearch != RoleSearch || displayJobFilter != JobFilterDefName)
            {
                displayStamp = UiVersion.Current;
                displayDefinitionRevision = DefinitionReloadCoordinator.Revision;
                displayLocationRevision = ColonyScope.LocationRevision;
                displayCollapseRevision = collapseRevision;
                displayNestedPreference = nestedPreference;
                displaySearch = RoleSearch;
                displayJobFilter = JobFilterDefName;
                var rebuiltRows = new List<RoleListRowSnapshot>();
                var liveLocationIds = new HashSet<string>(
                    ColonyScope.Locations().Select(location => location.Id));
                if (filtered)
                {
                    foreach (Role role in store.roles.Where(MatchesFilters))
                        rebuiltRows.Add(PublishRoleRow(null, role, store, 0,
                            virtualRow: false, liveLocationIds, roleTooltip));
                }
                else
                {
                    var publishedSections = new Dictionary<RoleSection,
                        RoleListSectionSnapshot>(sections.Count);
                    GameFont oldFont = Text.Font;
                    try
                    {
                        Text.Font = GameFont.Small;
                        foreach (RoleSection section in sections)
                        {
                            bool collapsed = IsSectionCollapsed(section.key);
                            publishedSections.Add(section,
                                PublishSection(section, store, collapsed));
                        }
                    }
                    finally
                    {
                        Text.Font = oldFont;
                    }
                    foreach (RoleSection section in sections)
                    {
                        RoleListSectionSnapshot publishedSection =
                            publishedSections[section];
                        rebuiltRows.Add(RoleListRowSnapshot.ForHeader(
                            publishedSection));
                        if (!publishedSection.Collapsed)
                            foreach (var (member, parent, depth, virtualRow) in section.rows)
                                rebuiltRows.Add(PublishRoleRow(publishedSection,
                                    member, store, depth, virtualRow,
                                    liveLocationIds, roleTooltip));
                    }
                }
                var rebuilt = new RoleListSnapshot(rebuiltRows, filtered,
                    nestedPreference);
                if (ownerChanged || snapshot == null
                    || !snapshot.ContentEquals(rebuilt))
                    snapshot = rebuilt;
            }
            displayOwner = store;
            return snapshot;
        }

        internal RoleSelectionSnapshot SelectionSnapshot(RoleStore store)
        {
            int stamp = UiVersion.Current;
            bool ownerChanged = !ReferenceEquals(selectionOwner, store);
            if (!ownerChanged && selectionSnapshot != null
                && selectionStamp == stamp)
                return selectionSnapshot;

            var entries = new RoleSelectionEntry[store.roles.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                Role role = store.roles[i];
                entries[i] = new RoleSelectionEntry(role.id, role.label);
            }
            var rebuilt = new RoleSelectionSnapshot(entries);
            if (ownerChanged || selectionSnapshot == null
                || !selectionSnapshot.ContentEquals(rebuilt))
                selectionSnapshot = rebuilt;
            selectionOwner = store;
            selectionStamp = stamp;
            return selectionSnapshot;
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
            return new RoleListRowSnapshot(section,
                RoleChipRenderData.From(role), depth, virtualRow,
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
            RoleStore store, bool collapsed)
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
                collapsed, section.renamable, section.draggable,
                section.dropTarget,
                section.roots != null && section.roots.Count > 0
                    ? section.roots[0].id : -1,
                section.draggable
                    ? WrText.FitWidth(section.commandName) + 4f : 0f,
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
            WorkRolesGameComponent.RequestSettingsWrite();
            collapseRevision++;
        }

        internal static void InvalidateSectionsSnapshot()
        {
            sectionsCacheStamp[0] = sectionsCacheStamp[1] = -1;
            sectionsCacheDefinitionRevision[0] =
                sectionsCacheDefinitionRevision[1] = -1;
        }

        /// Invalidation alone leaves the old world's roles reachable until the
        /// next build. Close/teardown uses this stronger ownership release.
        internal static void ReleaseSectionsSnapshot()
        {
            sectionsCache[0] = sectionsCache[1] = null;
            sectionsCacheOwner = null;
            InvalidateSectionsSnapshot();
        }

        internal static IReadOnlyList<RoleSection> BuildSections(RoleStore store, bool nested)
        {
            if (!ReferenceEquals(sectionsCacheOwner, store))
            {
                ReleaseSectionsSnapshot();
                sectionsCacheOwner = store;
            }
            int slot = nested ? 1 : 0;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (sectionsCache[slot] == null
                || sectionsCacheStamp[slot] != UiVersion.Current
                || sectionsCacheDefinitionRevision[slot] != definitionRevision)
            {
                sectionsCacheStamp[slot] = UiVersion.Current;
                sectionsCacheDefinitionRevision[slot] = definitionRevision;
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

    internal readonly struct RoleSelectionEntry
    {
        internal RoleSelectionEntry(int roleId, string label)
        {
            RoleId = roleId;
            Label = label;
        }

        internal int RoleId { get; }
        internal string Label { get; }
    }

    internal sealed class RoleSelectionSnapshot
    {
        private readonly RoleSelectionEntry[] entries;

        internal RoleSelectionSnapshot(RoleSelectionEntry[] entries)
        {
            this.entries = entries;
        }

        internal int Count => entries.Length;
        internal int FirstRoleId => entries.Length == 0 ? -1 : entries[0].RoleId;

        internal int NewestRoleIdWithLabel(string label)
        {
            for (int i = entries.Length - 1; i >= 0; i--)
                if (entries[i].Label == label)
                    return entries[i].RoleId;
            return -1;
        }

        internal bool TryGetRole(int roleId, out string label)
        {
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].RoleId == roleId)
                {
                    label = entries[i].Label;
                    return true;
                }
            label = null;
            return false;
        }

        internal bool ContentEquals(RoleSelectionSnapshot other)
        {
            if (other == null || entries.Length != other.entries.Length)
                return false;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].RoleId != other.entries[i].RoleId
                    || entries[i].Label != other.entries[i].Label)
                    return false;
            return true;
        }
    }

    internal sealed class RoleListSnapshot
    {
        internal RoleListSnapshot(
            List<RoleListRowSnapshot> rows,
            bool filtered,
            bool nestedPreference)
        {
            this.rows = rows;
            Filtered = filtered;
            NestedPreference = nestedPreference;
        }

        private readonly List<RoleListRowSnapshot> rows;
        internal int Count => rows.Count;
        internal RoleListRowSnapshot RowAt(int index) => rows[index];
        internal bool Filtered { get; }
        internal bool NestedPreference { get; }

        internal bool ContentEquals(RoleListSnapshot other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || Filtered != other.Filtered
                || NestedPreference != other.NestedPreference
                || rows.Count != other.rows.Count)
                return false;

            RoleListSectionSnapshot previousLeftSection = null;
            RoleListSectionSnapshot previousRightSection = null;
            for (int i = 0; i < rows.Count; i++)
            {
                RoleListRowSnapshot leftRow = rows[i];
                RoleListRowSnapshot rightRow = other.rows[i];
                if (!ReferenceEquals(leftRow.Section, previousLeftSection)
                    || !ReferenceEquals(rightRow.Section, previousRightSection))
                {
                    if (!ReferenceEquals(leftRow.Section, rightRow.Section)
                        && (leftRow.Section == null || rightRow.Section == null
                            || !leftRow.Section.ContentEquals(rightRow.Section)))
                        return false;
                    previousLeftSection = leftRow.Section;
                    previousRightSection = rightRow.Section;
                }
                if (!leftRow.ContentEqualsExcludingSection(rightRow))
                    return false;
            }
            return true;
        }

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
        internal RoleListRowSnapshot(RoleListSectionSnapshot section,
            RoleChipRenderData chip,
            int depth, bool virtualRow, bool invalid, string label,
            StructuredTip tooltip, bool enabled, bool hasCustomColor, Color color,
            bool blocker, bool hasTimeRule, bool hasLocationRule,
            bool composite, string virtualOriginGroupLabel)
        {
            Section = section;
            Chip = chip;
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
            new RoleListRowSnapshot(section,
                new RoleChipRenderData(-1, null, default(Color), false,
                    false, false, false),
                0, false, false, null,
                null, true, false, default, false, false, false, false, null);

        internal RoleListSectionSnapshot Section { get; }
        internal RoleChipRenderData Chip { get; }
        internal int RoleId => Chip.RoleId;
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

        internal bool ContentEqualsExcludingSection(
            RoleListRowSnapshot other)
        {
            if (other == null || !Chip.ContentEquals(other.Chip)
                || Depth != other.Depth || VirtualRow != other.VirtualRow
                || Invalid != other.Invalid
                || !string.Equals(Label, other.Label,
                    System.StringComparison.Ordinal)
                || Enabled != other.Enabled
                || HasCustomColor != other.HasCustomColor
                || !ColorEquals(Color, other.Color)
                || Blocker != other.Blocker
                || HasTimeRule != other.HasTimeRule
                || HasLocationRule != other.HasLocationRule
                || Composite != other.Composite
                || !string.Equals(VirtualOriginGroupLabel,
                    other.VirtualOriginGroupLabel,
                    System.StringComparison.Ordinal))
                return false;
            if (ReferenceEquals(Tooltip, other.Tooltip)) return true;
            return Tooltip != null && other.Tooltip != null
                && Tooltip.ContentEquals(other.Tooltip);
        }

        private static bool ColorEquals(Color left, Color right) =>
            left.r == right.r && left.g == right.g
            && left.b == right.b && left.a == right.a;
    }

    internal sealed class RoleListSectionSnapshot
    {
        private readonly List<int> nestedRoleIds;

        internal RoleListSectionSnapshot(string key, string displayTitle,
            string commandName, int groupId, int groupIndex, bool collapsed,
            bool renamable, bool draggable, bool dropTarget,
            int firstRootRoleId, float groupDragWidth,
            List<int> nestedRoleIds)
        {
            Key = key;
            DisplayTitle = displayTitle;
            CommandName = commandName;
            GroupId = groupId;
            GroupIndex = groupIndex;
            Collapsed = collapsed;
            Renamable = renamable;
            Draggable = draggable;
            DropTarget = dropTarget;
            FirstRootRoleId = firstRootRoleId;
            GroupDragWidth = groupDragWidth;
            this.nestedRoleIds = nestedRoleIds;
        }

        internal bool ContainsNestedRole(int roleId)
        {
            for (int i = 0; i < nestedRoleIds.Count; i++)
                if (nestedRoleIds[i] == roleId) return true;
            return false;
        }

        internal bool ContentEquals(RoleListSectionSnapshot other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null
                || !string.Equals(Key, other.Key,
                    System.StringComparison.Ordinal)
                || !string.Equals(DisplayTitle, other.DisplayTitle,
                    System.StringComparison.Ordinal)
                || !string.Equals(CommandName, other.CommandName,
                    System.StringComparison.Ordinal)
                || GroupId != other.GroupId || GroupIndex != other.GroupIndex
                || Collapsed != other.Collapsed || Renamable != other.Renamable
                || Draggable != other.Draggable || DropTarget != other.DropTarget
                || FirstRootRoleId != other.FirstRootRoleId
                || GroupDragWidth != other.GroupDragWidth
                || nestedRoleIds.Count != other.nestedRoleIds.Count)
                return false;
            for (int i = 0; i < nestedRoleIds.Count; i++)
                if (nestedRoleIds[i] != other.nestedRoleIds[i])
                    return false;
            return true;
        }

        internal string Key { get; }
        internal string DisplayTitle { get; }
        internal string CommandName { get; }
        internal int GroupId { get; }
        internal int GroupIndex { get; }
        internal bool Collapsed { get; }
        internal bool Renamable { get; }
        internal bool Draggable { get; }
        internal bool DropTarget { get; }
        internal int FirstRootRoleId { get; }
        internal float GroupDragWidth { get; }
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
