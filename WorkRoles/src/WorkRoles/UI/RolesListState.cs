using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Owns the Roles tab's group/tree projection, local list filters, collapse
    /// revision, and flattened row snapshot. Rendering and commands stay in the
    /// view; other views may consume the same read-only catalog projection.
    internal sealed class RolesListState
    {
        private static readonly List<RoleSection>[] sectionsCache = new List<RoleSection>[2];
        private static readonly int[] sectionsCacheStamp = { -1, -1 };
        private static int collapseRevision;

        private List<(RoleSection section, Role role, Role parent, int depth,
            bool virtualRow, bool invalid, string label)> displayRows;
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
            bool revealSelected)
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
                displayRows = new List<(RoleSection, Role, Role, int, bool, bool, string)>();
                var liveLocationIds = new HashSet<string>(
                    ColonyScope.Locations().Select(location => location.Id));
                if (filtered)
                {
                    foreach (Role role in store.roles.Where(MatchesFilters))
                        displayRows.Add(Row(null, role, null, 0, false));
                }
                else
                {
                    foreach (RoleSection section in sections)
                    {
                        displayRows.Add(Row(section, null, null, 0, false));
                        if (!IsSectionCollapsed(section.key))
                            foreach (var (member, parent, depth, virtualRow) in section.rows)
                                displayRows.Add(Row(section, member, parent, depth, virtualRow));
                    }
                }
                snapshot = new RoleListSnapshot(displayRows, filtered);

                (RoleSection section, Role role, Role parent, int depth,
                    bool virtualRow, bool invalid, string label) Row(
                    RoleSection section, Role role, Role parent, int depth,
                    bool virtualRow)
                {
                    if (role == null)
                        return (section, null, parent, depth, virtualRow, false, null);
                    return (section, role, parent, depth, virtualRow,
                        RoleLocationValidity.IsInvalid(role.entries.Count,
                            role.locationTokens, liveLocationIds),
                        role.enabled
                            ? role.label
                            : "WR_RoleLabelOff".Translate(role.label).ToString());
                }
            }
            return snapshot;
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
        /// rule-carrying roles stay flat (they display under Auto-Roles).
        internal static bool CanNest(Role parent, Role child)
            => parent.blocker == child.blocker && !parent.HasRules && !child.HasRules;

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
                title = "WR_GroupAutoRules".Translate(),
            };
            foreach (Role role in store.roles)
            {
                if (role.HasRules) auto.members.Add(role);
                else SectionOf(role.groupId).members.Add(role);
            }

            if (defaultSection != null && defaultSection.members.Count > 0)
                sections.Add(defaultSection);
            foreach (RoleGroup group in store.groups)
            {
                if (group.id == RoleGroup.DefaultId) continue;
                if (byGroupId.TryGetValue(group.id, out RoleSection section)
                    && section.members.Count > 0 && !sections.Contains(section))
                    sections.Add(section);
            }
            if (auto.members.Count > 0) sections.Add(auto);

            foreach (RoleSection section in sections)
            {
                if (nested && section != auto)
                    (section.roots, section.rows) = BuildRoleTree(section.members, store.roles);
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
            IReadOnlyList<(RoleSection section, Role role, Role parent, int depth,
                bool virtualRow, bool invalid, string label)> rows,
            bool filtered)
        {
            Rows = rows;
            Filtered = filtered;
        }

        internal IReadOnlyList<(RoleSection section, Role role, Role parent, int depth,
            bool virtualRow, bool invalid, string label)> Rows { get; }
        internal bool Filtered { get; }
    }

    /// One display section of the role list: a user group or the derived
    /// Auto-Roles overlay. Instances belong to the shared section snapshot.
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
