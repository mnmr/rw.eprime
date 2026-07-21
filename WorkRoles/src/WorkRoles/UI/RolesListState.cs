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

        private List<(RoleSection section, Role role, Role parent, bool virtualRow)> displayRows;
        private RoleListSnapshot snapshot;
        private int displayStamp = -1;
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
                || displayCollapseRevision != collapseRevision || displayNested != nested
                || displaySearch != RoleSearch || displayJobFilter != JobFilterDefName)
            {
                displayStamp = UiVersion.Current;
                displayCollapseRevision = collapseRevision;
                displayNested = nested;
                displaySearch = RoleSearch;
                displayJobFilter = JobFilterDefName;
                displayRows = new List<(RoleSection, Role, Role, bool)>();
                if (filtered)
                {
                    foreach (Role role in store.roles.Where(MatchesFilters))
                        displayRows.Add((null, role, null, false));
                }
                else
                {
                    foreach (RoleSection section in sections)
                    {
                        displayRows.Add((section, null, null, false));
                        if (!IsSectionCollapsed(section.key))
                            foreach (var (member, parent, virtualRow) in section.rows)
                                displayRows.Add((section, member, parent, virtualRow));
                    }
                }
                snapshot = new RoleListSnapshot(displayRows, filtered);
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
            IReadOnlyList<(Role role, Role parent, bool virtualRow)> rows)
            BuildRoleTree(RoleStore store)
        {
            var roots = new List<Role>();
            var rows = new List<(Role role, Role parent, bool virtualRow)>();
            foreach (RoleSection section in BuildSections(store, nested: true))
            {
                roots.AddRange(section.roots);
                rows.AddRange(section.rows);
            }
            return (roots, rows);
        }

        private static (List<Role> roots,
            List<(Role role, Role parent, bool virtualRow)> rows)
            BuildRoleTree(List<Role> members, List<Role> allRoles)
        {
            bool Eligible(Role role) => !role.blocker && !role.HasRules;

            var memberSet = new HashSet<Role>(members);
            var nested = new HashSet<Role>();
            foreach (Role role in members)
                if (Eligible(role) && members.Any(other =>
                        Eligible(other) && other.Covers(role)))
                    nested.Add(role);

            List<Role> roots = members.Where(role => !nested.Contains(role)).ToList();
            var rows = new List<(Role role, Role parent, bool virtualRow)>(members.Count);
            foreach (Role root in roots)
            {
                rows.Add((root, null, false));
                if (!Eligible(root)) continue;
                foreach (Role child in allRoles
                    .Where(role => Eligible(role) && root.Covers(role))
                    .OrderBy(role => RoleCommands.BlockStart(root.entries, role)))
                    rows.Add((child, root, !memberSet.Contains(child)));
            }
            return (roots, rows);
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
                        .Select(role => (role, (Role)null, false)).ToList();
                }
                section.displayTitle = section.title + " (" + section.members.Count + ")";
            }
            return sections;
        }
    }

    internal sealed class RoleListSnapshot
    {
        internal RoleListSnapshot(
            IReadOnlyList<(RoleSection section, Role role, Role parent, bool virtualRow)> rows,
            bool filtered)
        {
            Rows = rows;
            Filtered = filtered;
        }

        internal IReadOnlyList<(RoleSection section, Role role, Role parent, bool virtualRow)> Rows { get; }
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
        internal List<(Role role, Role parent, bool virtualRow)> rows;
        internal string displayTitle;
    }
}
