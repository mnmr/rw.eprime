using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public class RolesTabView
    {
        private Vector2 listScroll;
        private Vector2 entriesScroll;
        private Vector2 treeScroll;
        private int selectedRoleId = -1;
        private string filter = "";
        private readonly HashSet<string> expanded = new HashSet<string>();
        private int entriesReorderableGroupId = -1;

        private const float ListWidth = 260f;
        private const float RowHeight = 28f;
        private const float IconButton = 20f;

        // Rules section (auto-role checkbox + active-hours grid + location dropdown).
        // Integer-pixel grid geometry: fixed cell width, even gaps.
        private const int HourCellW = 16;
        private const int HourCellH = 20;
        private const int HourCellGap = 2;
        private const int HourGridW = 24 * (HourCellW + HourCellGap) - HourCellGap;
        private const int HourLabelH = 18;
        private const float AutoRowH = 24f;
        // Legend row + hour-number row + cell row.
        private const float RulesSectionH = HourLabelH + 2f + HourLabelH + 2f + HourCellH;
        private const float TrainingRowH = 28f;
        private const float TrainingSectionH = TrainingRowH * 3f + 8f;
        // Vanilla-schedule look: paint a color over a grey base.
        private static readonly Color HourActiveColor = Hex("0E7490"); // Tailwind cyan-700
        private static readonly Color HourInactiveColor = new Color(0.35f, 0.35f, 0.35f);

        // Hour-grid paint state: accumulate locally while the button is held and
        // commit ONE SetRoleActiveHours on release (avoids SyncMethod spam in MP).
        private bool paintingHours;
        private bool hourPaintValue;
        private int pendingHoursMask;
        private int paintRoleId = -1;

        // A role is auto iff it has rules; this transient set only reveals the rule
        // inputs for roles that don't have any yet (never scribed, never synced).
        private readonly HashSet<int> rulesRevealed = new HashSet<int>();
        private readonly HashSet<int> trainingRevealed = new HashSet<int>();
        // Slider edits in flight: committed as ONE synced command on release
        // (dragging must not spam MP).
        private IntRange pendingBand;
        private int bandRoleId = -1;

        // Disambiguated display names per WorkGiverDef. Built once (defs don't
        // change mid-game), invalidated by Reset().
        private static Dictionary<WorkGiverDef, string> _giverDisplayCache;

        // Built-in swatches: Tailwind v3, shade-major rows (900 top, 600 bottom).
        internal static readonly Color[] Swatches;
        private static readonly string[] SwatchNames;

        /// Export-format name of a built-in swatch: "slate-600" style.
        internal static string ExportSwatchName(int index) =>
            SwatchNames[index].ToLowerInvariant().Replace(' ', '-');

        private static Color Hex(string h)
        {
            int r = System.Convert.ToInt32(h.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(h.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(h.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        static RolesTabView()
        {
            // 19 families (neutral dropped), each with (900, 800, 700, 600) shades.
            // family-major array; we draw as shade-major rows (row 0=all 900s, ... row 3=all 600s).
            var families = new string[,]
            {
                // family        900       800       700       600
                /* slate    */ { "0F172A", "1E293B", "334155", "475569" },
                /* stone    */ { "1C1917", "292524", "44403C", "57534E" },
                /* red      */ { "7F1D1D", "991B1B", "B91C1C", "DC2626" },
                /* orange   */ { "7C2D12", "9A3412", "C2410C", "EA580C" },
                /* amber    */ { "78350F", "92400E", "B45309", "D97706" },
                /* yellow   */ { "713F12", "854D0E", "A16207", "CA8A04" },
                /* lime     */ { "365314", "3F6212", "4D7C0F", "65A30D" },
                /* green    */ { "14532D", "166534", "15803D", "16A34A" },
                /* emerald  */ { "064E3B", "065F46", "047857", "059669" },
                /* teal     */ { "134E4A", "115E59", "0F766E", "0D9488" },
                /* cyan     */ { "164E63", "155E75", "0E7490", "0891B2" },
                /* sky      */ { "0C4A6E", "075985", "0369A1", "0284C7" },
                /* blue     */ { "1E3A8A", "1E40AF", "1D4ED8", "2563EB" },
                /* indigo   */ { "312E81", "3730A3", "4338CA", "4F46E5" },
                /* violet   */ { "4C1D95", "5B21B6", "6D28D9", "7C3AED" },
                /* purple   */ { "581C87", "6B21A8", "7E22CE", "9333EA" },
                /* fuchsia  */ { "701A75", "86198F", "A21CAF", "C026D3" },
                /* pink     */ { "831843", "9D174D", "BE185D", "DB2777" },
                /* rose     */ { "881337", "9F1239", "BE123C", "E11D48" },
            };

            var familyNames = new[]
            {
                "Slate", "Stone", "Red", "Orange", "Amber", "Yellow", "Lime", "Green", "Emerald",
                "Teal", "Cyan", "Sky", "Blue", "Indigo", "Violet", "Purple", "Fuchsia", "Pink", "Rose",
            };
            var shadeNames = new[] { "900", "800", "700", "600" };

            int numFamilies = families.GetLength(0); // 19
            int numShades   = families.GetLength(1); // 4

            // shade-major order: swatches[shade * numFamilies + family]
            Swatches = new Color[numShades * numFamilies];
            SwatchNames = new string[numShades * numFamilies];
            for (int shade = 0; shade < numShades; shade++)
                for (int family = 0; family < numFamilies; family++)
                {
                    Swatches[shade * numFamilies + family] = Hex(families[family, shade]);
                    SwatchNames[shade * numFamilies + family] = familyNames[family] + " " + shadeNames[shade];
                }
        }

        /// Content-driven height for window sizing: the role list on the left and
        /// the editor's collapsed job tree on the right are the tall pieces.
        public static float DesiredHeight()
        {
            var store = RoleStore.Current;
            if (store == null) return 684f;
            float chrome = 120f; // tabs, margins, editor gaps
            float list = store.roles.Count * RowHeight + 40f + ListFilterRowsH; // rows + buttons + filter rows
            // The job tree lists every work type, hidden ones included.
            int workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Count;
            float editor = 190f + 32f + workTypes * 26f; // top box + tree header + collapsed roots
            return chrome + Mathf.Max(list, editor);
        }

        /// Set on selection change; the next job-tree draw expands and scrolls
        /// to the selected role's first entry.
        private bool scrollJobTreeToSelection;

        private void SelectRole(int id)
        {
            if (id == selectedRoleId) return;
            CommitEdits();
            selectedRoleId = id;
            scrollJobTreeToSelection = true;
        }

        /// Editing a role ended (selection change, tab switch, window close):
        /// scrub its dead entries. Issued as a synced command only when there is
        /// something to scrub.
        public void CommitEdits()
        {
            var role = RoleStore.Current?.RoleById(selectedRoleId);
            if (role != null
                && JobOrderCompiler.DeadEntryIndexes(role.entries, GameJobCatalog.Instance).Count > 0)
                RoleCommands.ScrubDeadEntries(role.id);
        }

        public void Reset()
        {
            listScroll = entriesScroll = treeScroll = Vector2.zero;
            filter = "";
            roleSearch = "";
            jobFilterDefName = null;
            selectedRoleId = -1;
            paintingHours = false;
            paintRoleId = -1;
            rulesRevealed.Clear();
            _giverDisplayCache = null; // force rebuild on next use
            // Opening re-snapshots everything on this tab.
            InvalidateSectionsSnapshot();
            deadEntriesStamp = holdersStamp = -1;
            displayStamp = treeNodesStamp = -1;
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();
            if (selectedRoleId == -1 && store.roles.Count > 0)
                SelectRole(store.roles[0].id);

            var listRect = new Rect(rect.x, rect.y, ListWidth, rect.height);
            var editorRect = new Rect(rect.x + ListWidth + 12f, rect.y, rect.width - ListWidth - 12f, rect.height);
            DrawRoleList(listRect, store);

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            WrText.LineVertical(rect.x + ListWidth + 6f, rect.y, rect.height);
            GUI.color = Color.white;

            var selected = store.RoleById(selectedRoleId);
            if (selected != null) DrawEditor(editorRect, store, selected);
            else Widgets.Label(editorRect, "WR_SelectOrCreateRole".Translate());

            DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        // ----- Left: role list + management buttons -----

        /// The role list is a two-level tree: a role strictly covered by another
        /// eligible role displays under EVERY covering root, across groups
        /// (Rescuer under both Basics and Doctor, wherever they live). A row
        /// whose role belongs to another group is VIRTUAL: rendered dashed,
        /// never draggable, and it simply stops being generated when the
        /// coverage breaks — the role's real row lives in its own group (nested
        /// there, or as a root when nothing in its group covers it). Roots keep
        /// catalog order; each root's children sort by where their entry block
        /// starts inside the root's entry list (ties keep catalog order).
        internal static (List<Role> roots, List<(Role role, Role parent, bool virtualRow)> rows)
            BuildRoleTree(List<Role> members, List<Role> allRoles)
        {
            // Blockers and the managed role never nest and never parent: their
            // entry overlap is not a provides-relationship. Auto (rule-carrying)
            // roles stay in their overlay — they neither parent nor nest here.
            bool Eligible(Role r) => !r.blocker && !r.managed && !r.HasRules;

            var memberSet = new HashSet<Role>(members);
            var nested = new HashSet<Role>();
            foreach (var role in members)
                if (Eligible(role) && members.Any(o => Eligible(o) && o.Covers(role)))
                    nested.Add(role);

            var roots = members.Where(r => !nested.Contains(r)).ToList();
            var rows = new List<(Role role, Role parent, bool virtualRow)>(members.Count);
            foreach (var root in roots)
            {
                rows.Add((root, null, false));
                if (!Eligible(root)) continue;
                // Children with entries literally present in the root's list sort
                // by that position; coverage-only children (root expresses their
                // jobs via a work type) have no position and keep catalog order.
                foreach (var child in allRoles
                    .Where(r => Eligible(r) && root.Covers(r))
                    .OrderBy(r => RoleCommands.BlockStart(root.entries, r)))
                    rows.Add((child, root, !memberSet.Contains(child)));
            }
            return (roots, rows);
        }

        /// Group-aware whole-catalog tree (palette clustering): every display
        /// section's tree concatenated.
        internal static (List<Role> roots, List<(Role role, Role parent, bool virtualRow)> rows)
            BuildRoleTree(RoleStore store)
        {
            var roots = new List<Role>();
            var rows = new List<(Role role, Role parent, bool virtualRow)>();
            foreach (var section in BuildSections(store, nested: true))
            {
                roots.AddRange(section.roots);
                rows.AddRange(section.rows);
            }
            return (roots, rows);
        }

        /// One display section of the role list: a user group, or a derived
        /// overlay (Auto-Roles = rule-carrying roles, Locked = managed) — a
        /// member's stored group is remembered while it displays in an overlay.
        internal sealed class RoleSection
        {
            public string key;      // collapse-state key: "g<id>", "auto", "locked"
            public string title;
            /// Group name for commands: "" = Default (a language-independent
            /// sentinel — synced args must never carry translated names).
            public string commandName = "";
            public RoleGroup group; // null for overlays and the virtual Default
            public bool renamable;  // user groups except Default
            public bool draggable;  // reorderable user groups (not Default)
            public bool dropTarget; // accepts role drops
            public List<Role> members = new List<Role>();
            public List<Role> roots;
            public List<(Role role, Role parent, bool virtualRow)> rows;
            /// "Title (N)", composed once per snapshot — headers draw per pass.
            public string displayTitle;
        }

        // Open-window snapshot (see UiVersion) of the coverage-nested section
        // tree; one slot per nesting flag (list and palette disagree on it).
        private static readonly List<RoleSection>[] sectionsCache = new List<RoleSection>[2];
        private static readonly int[] sectionsCacheStamp = { -1, -1 };

        internal static void InvalidateSectionsSnapshot()
            => sectionsCacheStamp[0] = sectionsCacheStamp[1] = -1;

        internal static List<RoleSection> BuildSections(RoleStore store, bool nested)
        {
            int slot = nested ? 1 : 0;
            if (sectionsCache[slot] == null || sectionsCacheStamp[slot] != UiVersion.Current)
            {
                sectionsCacheStamp[slot] = UiVersion.Current;
                sectionsCache[slot] = BuildSectionsUncached(store, nested);
            }
            return sectionsCache[slot];
        }

        /// Sections in display order: Default (when it has a face), user groups
        /// (their order), Auto-Roles (hidden when empty), Locked (Odd Jobs exists
        /// from seeding, even with no jobs — the section only disappears while
        /// the player has deleted the role).
        private static List<RoleSection> BuildSectionsUncached(RoleStore store, bool nested)
        {
            var sections = new List<RoleSection>();
            var byGroupId = new Dictionary<int, RoleSection>();
            RoleSection defaultSection = null;

            RoleSection Default()
            {
                return defaultSection ??= new RoleSection
                {
                    key = "g0",
                    title = "WR_GroupDefault".Translate(),
                    group = store.GroupById(RoleGroup.DefaultId),
                    dropTarget = true,
                };
            }

            RoleSection SectionOf(int groupId)
            {
                if (byGroupId.TryGetValue(groupId, out var section)) return section;
                var group = store.GroupById(groupId);
                // Stale or Default group ids all land in the Default section.
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

            var auto = new RoleSection { key = "auto", title = "WR_GroupAutoRules".Translate() };
            var locked = new RoleSection { key = "locked", title = "WR_GroupLocked".Translate() };

            foreach (var role in store.roles)
            {
                if (role.managed) locked.members.Add(role);
                else if (role.HasRules) auto.members.Add(role);
                else SectionOf(role.groupId).members.Add(role);
            }

            // Overlay-displayed roles don't count as group members: a group
            // whose roles all sit in Auto-Roles renders nothing (the group
            // object survives in the store as the rules-cleared fallback).
            if (defaultSection != null && defaultSection.members.Count > 0)
                sections.Add(defaultSection);
            foreach (var group in store.groups)
            {
                if (group.id == RoleGroup.DefaultId) continue;
                if (byGroupId.TryGetValue(group.id, out var section)
                    && section.members.Count > 0 && !sections.Contains(section))
                    sections.Add(section);
            }
            if (auto.members.Count > 0) sections.Add(auto);
            if (locked.members.Count > 0) sections.Add(locked);

            foreach (var section in sections)
            {
                if (nested && section != auto && section != locked)
                    (section.roots, section.rows) = BuildRoleTree(section.members, store.roles);
                else
                {
                    section.roots = section.members;
                    section.rows = section.members.Select(r => (r, (Role)null, false)).ToList();
                }
                section.displayTitle = section.title + " (" + section.members.Count + ")";
            }
            return sections;
        }

        private static bool IsSectionCollapsed(string key) =>
            WorkRolesMod.Settings?.collapsedRoleGroups.Contains(key) == true;

        // Collapse toggles are UI-local (no UiVersion traffic): the display-row
        // snapshot keys on this revision instead.
        private static int collapseRev;

        private static void ToggleSectionCollapsed(string key)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            if (!settings.collapsedRoleGroups.Remove(key))
                settings.collapsedRoleGroups.Add(key);
            settings.Write();
            collapseRev++;
        }

        // Role list filters (view-local): label search + a single job whose
        // holders should be listed.
        private string roleSearch = "";
        private string jobFilterDefName;

        private bool ListFiltersActive => !roleSearch.NullOrEmpty() || jobFilterDefName != null;

        /// A role matches the job filter when its entries carry the job itself or
        /// the job's whole work type.
        private bool MatchesListFilters(Role role)
        {
            if (!roleSearch.NullOrEmpty()
                && (role.label == null
                    || role.label.IndexOf(roleSearch, System.StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (jobFilterDefName != null)
            {
                var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(jobFilterDefName);
                string parentType = giver?.workType?.defName;
                bool has = role.entries.Any(e =>
                    e.Kind == JobEntryKind.WorkGiver
                        ? e.DefName == jobFilterDefName
                        : parentType != null && e.DefName == parentType);
                if (!has) return false;
            }
            return true;
        }

        /// Two captioned rows: Search + Display Mode (left/right) on top, the
        /// Job Filter below with room for long job names, plus the clear X.
        internal const float ListFilterRowsH = 90f;

        private static void FilterCaption(Rect rect, string key)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.60f, 0.62f, 0.64f);
            Widgets.Label(rect, key.Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawListFilterRow(Rect rect)
        {
            const float LabelH = 16f; // room for Tiny descenders (Job Filter's y)
            const float InputH = 24f;
            const float ToggleW = 64f;
            const float JobBtnW = 220f;

            float y1 = rect.y + LabelH;
            float searchW = rect.width - ToggleW - 8f - 22f;
            FilterCaption(new Rect(rect.x, rect.y, searchW, LabelH), "WR_Search");
            roleSearch = Widgets.TextField(new Rect(rect.x, y1, searchW, InputH), roleSearch);
            if (!roleSearch.NullOrEmpty()
                && Widgets.ButtonImage(new Rect(rect.x + searchW + 4f, y1 + (InputH - 18f) / 2f, 18f, 18f),
                    TexButton.CloseXSmall))
            {
                roleSearch = "";
                GUIUtility.keyboardControl = 0; // release the field's edit buffer
            }

            // Nested/flat toggle: auto-nesting of covered roles on or off.
            var toggleRect = new Rect(rect.xMax - ToggleW, y1, ToggleW, InputH);
            FilterCaption(new Rect(toggleRect.x, rect.y, ToggleW, LabelH), "WR_DisplayModeLabel");
            var treeSettings = WorkRolesMod.Settings;
            bool nestedNow = treeSettings?.nestedRoleTree ?? true;
            TooltipHandler.TipRegion(toggleRect, "WR_TreeToggleTip".Translate());
            if (Widgets.ButtonText(toggleRect, (nestedNow ? "WR_TreeNested" : "WR_TreeFlat").Translate())
                && treeSettings != null)
            {
                treeSettings.nestedRoleTree = !nestedNow;
                treeSettings.Write();
            }

            float y2Label = y1 + InputH + 6f;
            float y2 = y2Label + LabelH;
            FilterCaption(new Rect(rect.x, y2Label, JobBtnW, LabelH), "WR_JobFilterLabel");
            var jobRect = new Rect(rect.x, y2, JobBtnW, InputH);
            var giverDef = jobFilterDefName == null ? null
                : DefDatabase<WorkGiverDef>.GetNamedSilentFail(jobFilterDefName);
            string jobLabel = giverDef != null
                ? GetGiverDisplayName(giverDef)
                : "WR_FilterAnyJob".Translate().ToString();
            // Long job names truncate to the button (the ButtonText inset eats
            // ~10px a side); the tooltip carries the full name.
            string jobShown = jobLabel.Truncate(jobRect.width - 20f);
            if (jobShown != jobLabel)
                TooltipHandler.TipRegion(jobRect, jobLabel);
            if (Widgets.ButtonText(jobRect, jobShown))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_FilterAnyJob".Translate(), () => jobFilterDefName = null),
                };
                foreach (var def in DefDatabase<WorkGiverDef>.AllDefsListForReading
                    .Where(d => d.workType != null)
                    .OrderBy(GetGiverDisplayName, System.StringComparer.OrdinalIgnoreCase))
                {
                    var captured = def.defName;
                    options.Add(new FloatMenuOption(GetGiverDisplayName(def), () => jobFilterDefName = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (jobFilterDefName != null)
            {
                var clearRect = new Rect(jobRect.xMax + 6f, y2 + (InputH - 18f) / 2f, 18f, 18f);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    jobFilterDefName = null;
            }
        }

        /// Create/Copy run through synced commands whose execution MP defers,
        /// so selection can't use a return value: the entered name is watched
        /// for instead, and the newest role carrying it gets selected, its
        /// section expanded and its row scrolled into view.
        private string pendingSelectLabel;
        private bool scrollToSelected;

        // Open-window snapshot of the flattened display rows (sections plus
        // expanded members, or the filtered flat list). Keyed on plain fields —
        // a composed key string would itself allocate per pass.
        private List<(RoleSection section, Role role, Role parent, bool virtualRow)> displayCache;
        private int displayStamp = -1;
        private int displayCollapseRev = -1;
        private bool displayNested;
        private string displaySearch;
        private string displayJobFilter;

        private void DrawRoleList(Rect rect, RoleStore store)
        {
            float buttonsHeight = 34f;
            if (pendingSelectLabel != null)
            {
                for (int i = store.roles.Count - 1; i >= 0; i--)
                    if (store.roles[i].label == pendingSelectLabel)
                    {
                        SelectRole(store.roles[i].id);
                        pendingSelectLabel = null;
                        scrollToSelected = true;
                        break;
                    }
            }
            DrawListFilterRow(new Rect(rect.x, rect.y, rect.width, ListFilterRowsH - 6f));
            var scrollRect = new Rect(rect.x, rect.y + ListFilterRowsH, rect.width,
                rect.height - buttonsHeight - 6f - ListFilterRowsH);

            bool filtered = ListFiltersActive;
            bool nested = (WorkRolesMod.Settings?.nestedRoleTree ?? true) && !filtered;
            // Display rows: group headers plus their (expanded) member rows; a
            // filtered list is flat and headerless. role == null marks a header.
            var sections = filtered ? null : BuildSections(store, nested);

            // A freshly created/copied role must land visibly: expand its
            // section if collapsed before the rows are built.
            if (scrollToSelected && sections != null)
                foreach (var section in sections)
                    if (IsSectionCollapsed(section.key)
                        && section.rows.Any(t => t.role.id == selectedRoleId))
                        ToggleSectionCollapsed(section.key);

            if (displayCache == null || displayStamp != UiVersion.Current
                || displayCollapseRev != collapseRev || displayNested != nested
                || displaySearch != roleSearch || displayJobFilter != jobFilterDefName)
            {
                displayStamp = UiVersion.Current;
                displayCollapseRev = collapseRev;
                displayNested = nested;
                displaySearch = roleSearch;
                displayJobFilter = jobFilterDefName;
                displayCache = new List<(RoleSection, Role, Role, bool)>();
                if (filtered)
                {
                    foreach (var match in store.roles.Where(MatchesListFilters))
                        displayCache.Add((null, match, null, false));
                }
                else
                {
                    foreach (var section in sections)
                    {
                        displayCache.Add((section, null, null, false));
                        if (!IsSectionCollapsed(section.key))
                            foreach (var (member, memberParent, virtualRow) in section.rows)
                                displayCache.Add((section, member, memberParent, virtualRow));
                    }
                }
            }
            var display = displayCache;
            float contentHeight = display.Count * RowHeight;

            if (scrollToSelected)
            {
                scrollToSelected = false;
                int row = display.FindIndex(t => t.role != null && t.role.id == selectedRoleId);
                if (row >= 0)
                {
                    float y = row * RowHeight;
                    if (y < listScroll.y) listScroll.y = y;
                    else if (y + RowHeight > listScroll.y + scrollRect.height)
                        listScroll.y = y + RowHeight - scrollRect.height;
                }
            }

            Role dragged = !filtered && RoleDrag.Active && RoleDrag.GroupId < 0
                ? store.RoleById(RoleDrag.RoleId) : null;
            bool groupDrag = !filtered && RoleDrag.Active && RoleDrag.GroupId >= 0;

            Widgets.BeginScrollView(scrollRect, ref listScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            for (int i = 0; i < display.Count; i++)
            {
                var (section, role, parentRole, virtualRow) = display[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (role == null)
                {
                    DrawGroupHeader(store, row, section, i, dragged, groupDrag);
                    continue;
                }
                bool isChild = parentRole != null;
                float indent = isChild ? 18f : 0f;

                if (role.id == selectedRoleId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row) && !RoleDrag.Active) Widgets.DrawHighlight(row);

                if (Mouse.IsOver(row))
                    TooltipHandler.TipRegion(row, RowTip(role));

                var swatch = new Rect(Mathf.Round(row.x) + 6f + indent, Mathf.Round(row.y) + 6f, 16f, 16f);
                Widgets.DrawBoxSolid(swatch, role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
                Widgets.DrawBox(swatch.ExpandedBy(1f));
                GUI.color = Color.white;
                if (virtualRow && Mouse.IsOver(row))
                    TooltipHandler.TipRegion(row, "WR_VirtualRoleTip".Translate(
                        store.GroupById(role.groupId)?.label ?? "WR_GroupDefault".Translate().ToString()));

                string rowLabel = role.enabled ? role.label : "WR_RoleLabelOff".Translate(role.label).ToString();
                var labelRect = new Rect(swatch.xMax + 6f, row.y, row.width - swatch.width - 8f - indent, RowHeight);
                // Invalid roles (no jobs, or every named location gone) render
                // subdued grey — they can never act until fixed.
                bool invalid = RoleInvalid(role);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (!role.enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                else if (invalid) GUI.color = new Color(0.55f, 0.55f, 0.55f);
                // Italics = virtual row: the role belongs to another group and
                // appears here only because this parent covers it.
                if (virtualRow) Text.CurFontStyle.fontStyle = FontStyle.Italic;
                Widgets.Label(labelRect, rowLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (invalid && Mouse.IsOver(row))
                    TooltipHandler.TipRegion(row, "WR_InvalidRoleTip".Translate());

                // Marker strip after the label: the same icons the chips carry
                // (pin excluded — it marks assignments, not role definitions).
                // Measured before the italic reset so markers clear the label.
                float markerX = labelRect.x + WrText.FitWidth(rowLabel) + 4f;
                if (virtualRow) Text.CurFontStyle.fontStyle = FontStyle.Normal;
                void ListMarker(Texture2D tex, bool tinted)
                {
                    var markerRect = new Rect(markerX, row.y + (RowHeight - 16f) / 2f, 16f, 16f);
                    if (markerRect.xMax > labelRect.xMax) return;
                    var markerColor = tinted ? RoleChipUI.RuleMarkerColor : Color.white;
                    if (!role.enabled) markerColor.a *= 0.5f;
                    GUI.color = markerColor;
                    GUI.DrawTexture(markerRect, tex);
                    GUI.color = Color.white;
                    markerX += 18f;
                }
                if (role.blocker) ListMarker(WorkRolesTex.BlockerMarker, tinted: false);
                if (role.activeHours != Role.AllHours) ListMarker(WorkRolesTex.TimeMarker, tinted: true);
                if (role.locationTokens.Count > 0) ListMarker(WorkRolesTex.LocationMarker, tinted: true);

                // Press registers a potential drag + click callback; a release inside
                // the 6px threshold selects (resolved centrally in ResolveMouseUp).
                // Virtual rows never drag — they select on press.
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    int capturedId = role.id;
                    if (virtualRow) SelectRole(capturedId);
                    else RoleDrag.OnPress(capturedId, null, () => SelectRole(capturedId));
                    e.Use();
                }

                if (e.type == EventType.MouseDown && e.button == 1 && row.Contains(e.mousePosition))
                {
                    ShowRoleContextMenu(store, role, parentRole);
                    e.Use();
                }

                if (dragged != null && Mouse.IsOver(row))
                    RegisterRoleDrop(display, i, row, dragged);
            }
            Widgets.EndScrollView();

            float bw = (rect.width - 8f) / 3f;
            float by = rect.yMax - buttonsHeight + 4f;
            if (Widgets.ButtonText(new Rect(rect.x, by, bw, 30f), "WR_New".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameRole("WR_NewRoleTitle".Translate(), null, enteredName =>
                {
                    RoleCommands.CreateRole(enteredName);
                    pendingSelectLabel = enteredName;
                }));
            }

            if (Widgets.ButtonText(new Rect(rect.x + bw + 4f, by, bw, 30f), "WR_Copy".Translate()))
            {
                var toCopy = RoleStore.Current.RoleById(selectedRoleId);
                if (toCopy != null)
                {
                    Find.WindowStack.Add(new Dialog_RenameRole("WR_CopyRoleTitle".Translate(), toCopy.label, enteredName =>
                    {
                        RoleCommands.DuplicateRole(selectedRoleId, enteredName);
                        pendingSelectLabel = enteredName;
                    }));
                }
            }

            var deleteRect = new Rect(rect.x + (bw + 4f) * 2f, by, bw, 30f);
            var selectedRole = RoleStore.Current.RoleById(selectedRoleId);
            if (Widgets.ButtonText(deleteRect, "WR_Delete".Translate(), active: selectedRole != null)
                && selectedRole != null)
            {
                // Odd Jobs gets its own warning: the role looks pointless when
                // empty, but deleting it opts out of invisible modded work.
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    selectedRole.managed
                        ? "WR_DeleteOddJobsWarning".Translate(selectedRole.label)
                        : "WR_DeleteConfirm".Translate(selectedRole.label),
                    () => RoleCommands.DeleteRole(selectedRole.id), destructive: true));
            }
        }

        // ----- Role-list drag & drop (organize only: membership + order) -----

        private static readonly Color BlockedTint = new Color(0.8f, 0.2f, 0.2f, 0.12f);

        /// Group header row: collapse arrow, title, displayed-member count and a
        /// rename pencil (user groups). Press = collapse toggle (click) or group
        /// reorder (drag, user groups); also a role-drop target (top of group).
        private void DrawGroupHeader(RoleStore store, Rect row, RoleSection section,
            int i, Role dragged, bool groupDrag)
        {
            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
            bool collapsed = IsSectionCollapsed(section.key);
            var arrowRect = new Rect(row.x + 4f, row.y + (row.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect, collapsed ? TexButton.Reveal : TexButton.Collapse);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(arrowRect.xMax + 6f, row.y, row.width - 60f, row.height),
                section.displayTitle);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var pencilRect = new Rect(row.xMax - 26f, row.y + (row.height - 18f) / 2f, 18f, 18f);
            if (section.renamable)
            {
                TooltipHandler.TipRegion(pencilRect, "WR_RenameGroup".Translate());
                if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
                {
                    int groupId = section.group.id;
                    Find.WindowStack.Add(new Dialog_RenameRole("WR_RenameGroupTitle".Translate(),
                        name => RoleCommands.RenameGroup(groupId, name), section.title));
                }
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition)
                && !(section.renamable && pencilRect.Contains(e.mousePosition)))
            {
                string key = section.key;
                if (section.draggable)
                    RoleDrag.OnPressGroup(section.group.id, () => ToggleSectionCollapsed(key));
                else
                    ToggleSectionCollapsed(key);
                e.Use();
            }

            // Role drop on the header: into this group, at the top. A nested
            // child dropped on its OWN group's header is a no-op — blocked.
            if (dragged != null && Mouse.IsOver(row))
            {
                bool nestedHere = section.rows != null
                    && section.rows.Any(t => t.role == dragged && t.parent != null && !t.virtualRow);
                if (!section.dropTarget || nestedHere)
                {
                    RoleDrag.HoverBlocked = true;
                    Widgets.DrawBoxSolid(row, BlockedTint);
                }
                else
                {
                    DrawInsertMarker(row, row.yMax);
                    int roleId = dragged.id;
                    int beforeId = section.roots.Count > 0 ? section.roots[0].id : -1;
                    string groupName = section.commandName;
                    RoleDrag.HoverDropAction = () =>
                        RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
                }
            }

            // Group reorder drop: above/below this header (user groups only;
            // Default, Auto-Roles and Locked are pinned).
            if (groupDrag && Mouse.IsOver(row) && section.group != null
                && section.group.id != RoleDrag.GroupId)
            {
                int from = store.groups.FindIndex(g => g.id == RoleDrag.GroupId);
                if (from < 0) return;
                bool below = e.mousePosition.y - row.y >= row.height / 2f;
                int target = store.groups.IndexOf(section.group) + (below ? 1 : 0);
                int to = target > from ? target - 1 : target;
                if (to == from) return;
                DrawInsertMarker(row, below ? row.yMax : row.y);
                RoleDrag.HoverDropAction = () => RoleCommands.MoveGroupInList(from, to);
            }
        }

        /// Organize-only drop while dragging a role: an insertion line at root
        /// granularity — above a root = before its block, below (or anywhere on
        /// its children) = after its block. Landing in another group moves the
        /// role (and its tree-children) there; overlay sections block.
        private static void RegisterRoleDrop(
            List<(RoleSection section, Role role, Role parent, bool virtualRow)> display, int i, Rect row, Role dragged)
        {
            var (section, role, parent, _) = display[i];
            // A nested child's within-own-group drop is a no-op — its display
            // position comes from the tree, not the catalog order. Its virtual
            // rows elsewhere don't block: dropping there moves it to that group.
            bool nestedHere = section.rows != null
                && section.rows.Any(t => t.role == dragged && t.parent != null && !t.virtualRow);
            if (!section.dropTarget || role == dragged || parent == dragged || nestedHere)
            {
                RoleDrag.HoverBlocked = true;
                Widgets.DrawBoxSolid(row, BlockedTint);
                return;
            }
            var root = parent ?? role;
            float my = Event.current.mousePosition.y - row.y;
            int roleId = dragged.id;
            string groupName = section.commandName;
            if (parent == null && my < row.height / 2f)
            {
                DrawInsertMarker(row, row.y);
                int beforeId = root.id;
                RoleDrag.HoverDropAction = () =>
                    RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
            }
            else
            {
                int end = i;
                while (end + 1 < display.Count && display[end + 1].parent == root) end++;
                DrawInsertMarker(row, (end + 1) * RowHeight);
                var next = end + 1 < display.Count ? display[end + 1].role : null;
                int beforeId = next?.id ?? -1;
                RoleDrag.HoverDropAction = () =>
                    RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
            }
        }

        // ----- Role-row context menu: composition without drag -----

        private static void ShowRoleContextMenu(RoleStore store, Role role, Role parent)
        {
            var options = new List<FloatMenuOption>();
            if (!role.managed)
                options.Add(new FloatMenuOption("WR_IncludeRole".Translate(),
                    () => ShowIncludeMenu(store, role)));
            if (parent != null)
            {
                int pid = parent.id, cid = role.id;
                string label = role.label;
                // A child nests because jobs overlap within the group — the
                // ways out are taking the jobs off the parent, or deleting it.
                options.Add(new FloatMenuOption("WR_RemoveFromParent".Translate(parent.label),
                    () => RoleCommands.ExcludeChild(pid, cid)));
                options.Add(new FloatMenuOption("WR_Delete".Translate(), () =>
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "WR_DeleteConfirm".Translate(label),
                        () => RoleCommands.DeleteRole(cid), destructive: true))));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        /// Candidates for inclusion: any non-blocker, non-managed role the target
        /// doesn't already cover. Cross-group entries render subdued with a
        /// "(will move from X)" suffix — inclusion pulls them into the group.
        private static void ShowIncludeMenu(RoleStore store, Role parent)
        {
            var options = new List<FloatMenuOption>();
            foreach (var candidate in store.roles
                .OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase))
            {
                if (candidate == parent || candidate.managed || candidate.blocker) continue;
                if (candidate.entries.Count > 0
                    && candidate.entries.All(e => parent.entries.Contains(e))) continue;
                string label = candidate.label;
                if (candidate.groupId != parent.groupId)
                {
                    string from = candidate.groupId == RoleGroup.DefaultId
                        ? "WR_GroupDefault".Translate().ToString()
                        : store.GroupById(candidate.groupId)?.label
                            ?? "WR_GroupDefault".Translate().ToString();
                    label = (candidate.label + " " + "WR_IncludeMove".Translate(from))
                        .Colorize(new Color(0.62f, 0.62f, 0.62f));
                }
                int pid = parent.id, cid = candidate.id;
                options.Add(new FloatMenuOption(label, () => RoleCommands.IncludeRole(pid, cid)));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("WR_NothingToInclude".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// 2px horizontal insertion marker across the row width at the given boundary.
        private static void DrawInsertMarker(Rect row, float y)
            => Widgets.DrawBoxSolid(
                LudeonTK.UIScaling.AdjustRectToUIScaling(new Rect(row.x, y - 1f, row.width, 2f)),
                new Color(1f, 1f, 1f, 0.9f));

        /// Floating drag ghost: the row's swatch square + label following the cursor,
        /// red-tinted while over a blocked target.
        private static void DrawDragGhost(RoleStore store)
        {
            if (!RoleDrag.Active) return;
            if (RoleDrag.GroupId >= 0)
            {
                // Group reorder ghost: just the group name.
                var group = store.GroupById(RoleDrag.GroupId);
                if (group == null) return;
                var m = Event.current.mousePosition;
                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(m.x + 12f, m.y + 2f, WrText.FitWidth(group.label) + 4f, 24f), group.label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }
            var role = store.RoleById(RoleDrag.RoleId);
            if (role == null) return;
            var mouse = Event.current.mousePosition;
            Color tint = RoleDrag.HoverBlocked
                ? new Color(1f, 0.3f, 0.3f, 0.7f)
                : new Color(1f, 1f, 1f, 0.7f);

            Text.Font = GameFont.Small;
            float labelW = WrText.FitWidth(role.label) + 4f;
            GUI.color = tint;
            Widgets.DrawBoxSolid(new Rect(mouse.x + 10f, mouse.y + 6f, 16f, 16f),
                role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(mouse.x + 32f, mouse.y + 2f, labelW, 24f), role.label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // ----- Right: editor for the selected role -----

        private void DrawEditor(Rect rect, RoleStore store, Role role)
        {
            const float SwatchSize = 18f;
            const float SwatchGap = 2f;
            const int SwatchCols = 19;
            const int SwatchRows = 4;

            // Split top box into LEFT (name + pencil, assigned-to, group,
            // checkbox column) and RIGHT (swatches). Height fits whichever half
            // is taller. The second custom swatch row only renders when it
            // holds a color or the first row is full (a "+" must stay reachable)
            // — at least 5 color rows either way.
            const float TopBoxPadding = 8f;
            const float TitleH = 30f;
            const float AssignedRowH = 22f;
            const float GroupRowH = 26f;
            const float CheckRowH = 24f;
            const float RulesRowGap = 6f;
            var customSlots = store.customSwatches;
            bool firstCustomRowFull = customSlots.Count >= SwatchCols
                && customSlots.Take(SwatchCols).All(c => c.a >= 0.5f);
            bool secondCustomRowUsed = customSlots.Skip(SwatchCols).Any(c => c.a >= 0.5f);
            int customRows = firstCustomRowFull || secondCustomRowUsed ? 2 : 1;
            float swatchGridH = (SwatchSize + SwatchGap) * (SwatchRows + customRows) - SwatchGap;
            float leftContentH = Mathf.Max(TitleH + AssignedRowH + 2f + GroupRowH, CheckRowH * 4f);
            bool rulesShown = role.HasRules || rulesRevealed.Contains(role.id);
            bool trainingShown = !role.managed
                && (HasTrainingConfig(role) || trainingRevealed.Contains(role.id));
            float TopBoxHeight = Mathf.Max(swatchGridH, leftContentH)
                + (rulesShown ? RulesRowGap + RulesSectionH : 0f)
                + (trainingShown ? RulesRowGap + TrainingSectionH : 0f)
                + TopBoxPadding * 2f;

            var topBox = new Rect(rect.x, rect.y, rect.width, TopBoxHeight);
            Widgets.DrawBoxSolidWithOutline(topBox, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Color(1f, 1f, 1f, 0.15f));

            float swatchGridW = SwatchCols * (SwatchSize + SwatchGap) - SwatchGap;

            // RIGHT half: swatch grid, right-aligned inside box
            float swatchStartX = topBox.xMax - TopBoxPadding - swatchGridW;
            float swatchStartY = topBox.y + TopBoxPadding;
            for (int i = 0; i < Swatches.Length; i++)
            {
                int col = i % SwatchCols;
                int row = i / SwatchCols;
                var swatchRect = new Rect(
                    swatchStartX + col * (SwatchSize + SwatchGap),
                    swatchStartY + row * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                Widgets.DrawBoxSolid(swatchRect, Swatches[i]);
                if (role.hasCustomColor && role.color.IndistinguishableFrom(Swatches[i]))
                    Widgets.DrawBox(swatchRect.ExpandedBy(2f));
                TooltipHandler.TipRegion(swatchRect, SwatchNames[i]);
                if (Widgets.ButtonInvisible(swatchRect))
                    RoleCommands.SetRoleColor(role.id, Swatches[i]);
            }

            // Custom rows: player-defined slots. Empty slot = pick a color (applies
            // it too); filled = click to apply, right-click to redefine.
            var custom = store.customSwatches;
            float customY = swatchStartY + SwatchRows * (SwatchSize + SwatchGap);
            for (int c = 0; c < SwatchCols * customRows; c++)
            {
                var slotRect = new Rect(
                    swatchStartX + c % SwatchCols * (SwatchSize + SwatchGap),
                    customY + c / SwatchCols * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                var slotColor = c < custom.Count ? custom[c] : UnityEngine.Color.clear;
                bool empty = slotColor.a < 0.5f;
                int capturedSlot = c;
                int capturedRoleId = role.id;

                void OpenPicker(bool applyToRole)
                {
                    Find.WindowStack.Add(new Dialog_RoleColorPicker(
                        role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor,
                        picked =>
                        {
                            // Deferred: filling the last slot of a custom row
                            // makes the next row (and a taller top box) appear —
                            // that must happen on a clean frame, not mid-event.
                            // Cancelling the dialog never gets here, so the row
                            // only materializes once a color is actually set.
                            WorkRolesGameComponent.RunOutsideOnGUI(() =>
                            {
                                RoleCommands.SetCustomSwatch(capturedSlot, picked);
                                if (applyToRole) RoleCommands.SetRoleColor(capturedRoleId, picked);
                            });
                        }));
                }

                if (empty)
                {
                    Widgets.DrawBoxSolid(slotRect, new Color(0.14f, 0.14f, 0.14f));
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    Widgets.DrawBox(slotRect);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(slotRect, "+");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(slotRect, "WR_CustomSwatchEmpty".Translate());
                    if (Widgets.ButtonInvisible(slotRect))
                        OpenPicker(applyToRole: true);
                }
                else
                {
                    Widgets.DrawBoxSolid(slotRect, slotColor);
                    if (role.hasCustomColor && role.color.IndistinguishableFrom(slotColor))
                        Widgets.DrawBox(slotRect.ExpandedBy(2f));
                    TooltipHandler.TipRegion(slotRect, "WR_CustomSwatchTip".Translate());
                    if (Widgets.ButtonInvisible(slotRect))
                        RoleCommands.SetRoleColor(role.id, slotColor);
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 1 && slotRect.Contains(e.mousePosition))
                    {
                        e.Use();
                        OpenPicker(applyToRole: false);
                    }
                }
            }

            // LEFT half: three rows — name+pencil, "Assigned to", colonist names
            // The name's container is 50% of the framed box's full width.
            float leftContainerW = topBox.width / 2f;
            float leftX = topBox.x + TopBoxPadding;
            // Usable width within the left container (inset from left padding, right edge = box centre)
            float leftW = leftContainerW - TopBoxPadding;

            // Row 1: role name top-aligned at box.y + padding, UpperLeft anchor
            float rowsStartY = topBox.y + TopBoxPadding;

            // Checkbox column: right-aligned in the left container, from the TOP —
            // Auto-assign, Blocker role, Auto role, Training role stacked. Measured
            // first so the title and pencil know their room.
            Text.Font = GameFont.Small;
            float checksW = Mathf.Max(
                Mathf.Max(WrText.FitWidth("WR_AutoAssign".Translate()),
                    WrText.FitWidth("WR_BlockerRole".Translate())),
                Mathf.Max(WrText.FitWidth("WR_AutoRole".Translate()),
                    WrText.FitWidth("WR_TrainingRole".Translate()))) + 30f;
            float checksX = leftContainerW + topBox.x - checksW;
            DrawEditorChecks(new Rect(checksX, rowsStartY, checksW, CheckRowH * 4f),
                role, store, rulesShown, trainingShown, CheckRowH);

            // Title with the rename pencil directly AFTER the name (the right
            // column now belongs to the four toggles).
            const float PencilSize = 26f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            float titleMaxW = checksX - 8f - leftX - PencilSize - 6f;
            float titleW = Mathf.Min(WrText.FitWidth(role.label), titleMaxW);
            Widgets.Label(new Rect(leftX, rowsStartY, titleW, TitleH), role.label.Truncate(titleW));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float pencilY = rowsStartY + (TitleH - PencilSize) / 2f;
            if (Widgets.ButtonImage(new Rect(leftX + titleW + 6f, pencilY, PencilSize, PencilSize), TexButton.Rename))
                Find.WindowStack.Add(new Dialog_RenameRole(role));

            float row2Y = rowsStartY + TitleH;

            // Row 2: small grey "Assigned to" label with the colonist names
            // inline after it (ordered by position in their assignment list).
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            string assignedLabel = "WR_AssignedTo".Translate();
            float assignedLabelW = WrText.FitWidth(assignedLabel);
            Widgets.Label(new Rect(leftX, row2Y, assignedLabelW, AssignedRowH), assignedLabel);
            GUI.color = Color.white;
            float namesX = leftX + assignedLabelW + 6f;
            DrawAssignedPawnNames(new Rect(namesX, row2Y, checksX - 8f - namesX, AssignedRowH), role, store);

            // Row 3: group picker button ("Group: <name>") + "New...".
            DrawGroupPickerRow(new Rect(leftX, row2Y + AssignedRowH + 2f,
                checksX - 8f - leftX, GroupRowH - 4f), role, store);

            // Expanding sections (full box width): rules while the auto-role
            // opt-in is on, then the training panel while its toggle is on.
            float sectionY = topBox.y + TopBoxPadding + Mathf.Max(swatchGridH, leftContentH) + RulesRowGap;
            if (rulesShown)
            {
                DrawRulesSection(new Rect(leftX, sectionY,
                    topBox.width - TopBoxPadding * 2f, RulesSectionH), role);
                sectionY += RulesSectionH + RulesRowGap;
            }
            if (trainingShown)
                DrawTrainingSection(new Rect(leftX, sectionY,
                    topBox.width - TopBoxPadding * 2f, TrainingSectionH), role, store);

            // BOTTOM: split vertically — left = job tree, right = entries table
            float bottomY = topBox.yMax + 6f;
            float bottomH = rect.yMax - bottomY;
            float halfW = (rect.width - 6f) / 2f;

            var treeRect    = new Rect(rect.x, bottomY, halfW, bottomH);
            var entriesRect = new Rect(rect.x + halfW + 6f, bottomY, halfW, bottomH);

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            WrText.LineVertical(rect.x + halfW + 3f, bottomY, bottomH);
            GUI.color = Color.white;

            if (role.managed)
            {
                // Engine-owned entries: no job tree; a note explains the container.
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(treeRect.ContractedBy(8f), "WR_ManagedEntriesNote".Translate());
                GUI.color = Color.white;
            }
            else
            {
                DrawJobTree(treeRect, role);
            }
            DrawEntries(entriesRect, role);
        }

        /// The role's group as a "Group: <name>" button: a dropdown of the
        /// existing groups plus "New..." (a name dialog; the role moves in, so
        /// no empty group ever exists). A parent moves WITH its nested roles —
        /// a combo role separated from its children would un-nest both. Overlay
        /// members (Auto-Roles/Locked) show a disabled "Group: Auto-Roles" /
        /// "Group: Locked" instead — the stored group resumes when rules clear.
        private void DrawGroupPickerRow(Rect rect, Role role, RoleStore store)
        {
            Text.Font = GameFont.Small;
            bool overlay = role.managed || role.HasRules;
            string current = overlay
                ? (role.managed ? "WR_GroupLocked" : "WR_GroupAutoRules").Translate().ToString()
                : role.groupId == RoleGroup.DefaultId
                    ? "WR_GroupDefault".Translate().ToString()
                    : store.GroupById(role.groupId)?.label
                        ?? "WR_GroupDefault".Translate().ToString();
            string full = "WR_GroupButton".Translate(current);
            var pickRect = new Rect(rect.x, rect.y, Mathf.Min(rect.width, 180f), rect.height);
            string shown = full.Truncate(pickRect.width - 16f);
            if (shown != full)
                TooltipHandler.TipRegion(pickRect, full);

            if (overlay)
            {
                Widgets.ButtonText(pickRect, shown,
                    drawBackground: true, doMouseoverSound: false, active: false);
                TooltipHandler.TipRegion(pickRect, "WR_GroupOverlayTip".Translate());
                return;
            }

            if (Widgets.ButtonText(pickRect, shown))
            {
                int roleId = role.id;
                var options = new List<FloatMenuOption>
                {
                    // "" = Default: the synced arg must stay language-independent.
                    new FloatMenuOption("WR_GroupDefault".Translate(), () =>
                        RoleCommands.SetRoleGroup(roleId, "", withChildren: true)),
                };
                foreach (var group in store.groups)
                {
                    if (group.id == RoleGroup.DefaultId) continue;
                    string name = group.label;
                    options.Add(new FloatMenuOption(name, () =>
                        RoleCommands.SetRoleGroup(roleId, name, withChildren: true)));
                }
                options.Add(new FloatMenuOption("WR_GroupNewOption".Translate(), () =>
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        "WR_NewGroupTitle".Translate().ToString(),
                        name => RoleCommands.SetRoleGroup(roleId, name, withChildren: true)))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        // ----- Rules section: auto-role opt-in, active-hours grid, location dropdown -----

        /// The editor's checkbox column: Auto-assign, Blocker role (not on the
        /// managed role) and the Auto role opt-in, whose checkbox derives from
        /// HasRules — unchecking clears the rules (confirmed), checking reveals
        /// the rules section. CheckboxLabeled pins the boxes to the column's
        /// right edge, so they align under the pencil.
        /// Whether the role carries any training/holder configuration — this
        /// keeps the panel visible. The "Training role" CHECKBOX is narrower:
        /// only roles that train TOWARD another (trainTargets) are training
        /// roles; a band or colonist count alone (Cook, Builder, Fabricator)
        /// marks a destination, not a trainer.
        private static bool HasTrainingConfig(Role role)
            => role.trainSkill != null || role.trainTargets.Count > 0
               || role.minHolders >= 0;

        private void DrawEditorChecks(Rect rect, Role role, RoleStore store,
            bool rulesShown, bool trainingShown, float rowH)
        {
            Text.Font = GameFont.Small;
            float y = rect.y;

            var assignRect = new Rect(rect.x, y, rect.width, rowH);
            TooltipHandler.TipRegion(assignRect, "WR_AutoAssignTip".Translate());
            bool autoAssign = role.autoAssign;
            Widgets.CheckboxLabeled(assignRect, "WR_AutoAssign".Translate(), ref autoAssign);
            if (autoAssign != role.autoAssign)
                RoleCommands.SetRoleAutoAssign(role.id, autoAssign);
            y += rowH;

            // Blocker: the role's jobs become vetoes (hidden on the managed role).
            if (!role.managed)
            {
                var blockRect = new Rect(rect.x, y, rect.width, rowH);
                TooltipHandler.TipRegion(blockRect, "WR_BlockerRoleTip".Translate());
                bool blocker = role.blocker;
                Widgets.CheckboxLabeled(blockRect, "WR_BlockerRole".Translate(), ref blocker);
                if (blocker != role.blocker)
                    RoleCommands.SetRoleBlocker(role.id, blocker);
                y += rowH;
            }

            var autoRect = new Rect(rect.x, y, rect.width, rowH);
            TooltipHandler.TipRegion(autoRect, "WR_AutoRoleTip".Translate());
            bool rulesWanted = rulesShown;
            Widgets.CheckboxLabeled(autoRect, "WR_AutoRole".Translate(), ref rulesWanted);
            y += rowH;
            if (rulesWanted != rulesShown)
            {
                if (rulesWanted && trainingShown)
                {
                    // Auto rules and training are mutually exclusive: a
                    // rule-carrying role is never recommended, so training
                    // config on it would be inert.
                    Find.WindowStack.Add(new Dialog_SmallConfirm(
                        "WR_AutoOverTrainingConfirm".Translate(role.label),
                        () =>
                        {
                            RoleCommands.ClearRoleTraining(role.id);
                            trainingRevealed.Remove(role.id);
                            rulesRevealed.Add(role.id);
                        }));
                }
                else if (rulesWanted)
                {
                    rulesRevealed.Add(role.id);
                }
                else if (role.HasRules)
                {
                    // The checkbox derives from HasRules, so unchecking means clearing the rules.
                    Find.WindowStack.Add(new Dialog_SmallConfirm(
                        "WR_ClearRulesConfirm".Translate(role.label),
                        () =>
                        {
                            RoleCommands.ClearRoleRules(role.id);
                            rulesRevealed.Remove(role.id);
                        }));
                }
                else
                {
                    rulesRevealed.Remove(role.id);
                }
            }

            if (role.managed) return;
            var trainRect = new Rect(rect.x, y, rect.width, rowH);
            TooltipHandler.TipRegion(trainRect, "WR_TrainingRoleTip".Translate());
            bool isTraining = role.trainTargets.Count > 0 || trainingRevealed.Contains(role.id);
            bool trainWanted = isTraining;
            Widgets.CheckboxLabeled(trainRect, "WR_TrainingRole".Translate(), ref trainWanted);
            if (trainWanted == isTraining) return;
            if (trainWanted && rulesShown)
            {
                Find.WindowStack.Add(new Dialog_SmallConfirm(
                    "WR_TrainingOverAutoConfirm".Translate(role.label),
                    () =>
                    {
                        if (role.HasRules) RoleCommands.ClearRoleRules(role.id);
                        rulesRevealed.Remove(role.id);
                        EnableTraining(role, store);
                    }));
            }
            else if (trainWanted)
            {
                EnableTraining(role, store);
            }
            else if (role.trainTargets.Count > 0)
            {
                Find.WindowStack.Add(new Dialog_SmallConfirm(
                    "WR_ClearTrainingConfirm".Translate(role.label),
                    () =>
                    {
                        RoleCommands.ClearRoleTraining(role.id);
                        trainingRevealed.Remove(role.id);
                    }));
            }
            else
            {
                trainingRevealed.Remove(role.id);
            }
        }

        /// Opens the training panel; a fresh role gets derived defaults — the
        /// skill its jobs train, its tightest covering roles as targets, and a
        /// band ceiling at the cheapest target's floor.
        private void EnableTraining(Role role, RoleStore store)
        {
            trainingRevealed.Add(role.id);
            if (role.trainTargets.Count > 0) return;
            var targets = DefaultTrainTargets(role, store);
            if (role.trainSkill == null)
            {
                string skill = DominantSkillOf(role);
                int bandMax = 0;
                foreach (int id in targets)
                {
                    var target = store.RoleById(id);
                    if (target != null && target.trainMin > 0
                        && (bandMax == 0 || target.trainMin < bandMax))
                        bandMax = target.trainMin;
                }
                if (skill != null) RoleCommands.SetRoleTraining(role.id, skill, 0, bandMax);
            }
            if (targets.Count > 0) RoleCommands.SetRoleTrainTargets(role.id, targets);
            // Training roles are never force-dealt: trainees come from interest.
            if (role.minHolders < 0) RoleCommands.SetRoleHolders(role.id, 0);
        }

        /// The role's tightest covering roles (the tree parents) — the natural
        /// "trains toward" prefill.
        private static List<int> DefaultTrainTargets(Role role, RoleStore store)
        {
            var candidates = store.roles
                .Where(o => o != role && !o.managed && !o.blocker && !o.HasRules && o.Covers(role))
                .ToList();
            if (candidates.Count == 0) return new List<int>();
            int tightest = candidates.Min(o => o.Coverage().Count);
            return candidates.Where(o => o.Coverage().Count == tightest)
                .Select(o => o.id).ToList();
        }

        /// The skill the role's jobs predominantly train — the prefill for a
        /// freshly enabled training panel.
        private static string DominantSkillOf(Role role)
        {
            var counts = new Dictionary<string, int>();
            foreach (var giverName in role.Coverage())
            {
                var skills = DefDatabase<WorkGiverDef>.GetNamedSilentFail(giverName)
                    ?.workType?.relevantSkills;
                if (skills == null) continue;
                foreach (var skill in skills)
                    counts[skill.defName] = counts.TryGetValue(skill.defName, out int c) ? c + 1 : 1;
            }
            return counts.Count == 0 ? null
                : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        }

        /// Training panel: skill + band (min..max, 20 = open-ended), the roles
        /// this one trains toward, and the colony holder bounds. Band drags
        /// commit as ONE synced command on release.
        private void DrawTrainingSection(Rect rect, Role role, RoleStore store)
        {
            Text.Font = GameFont.Small;
            float y = rect.y;

            // Row 1: skill picker + band slider.
            string skillLabel = role.trainSkill == null
                ? "WR_TrainSkillNone".Translate().ToString()
                : DefDatabase<SkillDef>.GetNamedSilentFail(role.trainSkill)?.skillLabel.CapitalizeFirst()
                  ?? role.trainSkill;
            var skillRect = new Rect(rect.x, y, 170f, TrainingRowH - 4f);
            if (Widgets.ButtonText(skillRect, "WR_TrainSkillButton".Translate(skillLabel)))
            {
                int roleId = role.id;
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_TrainSkillNone".Translate(),
                        () => RoleCommands.SetRoleTraining(roleId, null, 0, 0)),
                };
                foreach (var skill in DefDatabase<SkillDef>.AllDefsListForReading
                             .OrderBy(s => s.skillLabel.ToString()))
                {
                    var captured = skill.defName;
                    options.Add(new FloatMenuOption(skill.skillLabel.CapitalizeFirst(),
                        () => RoleCommands.SetRoleTraining(roleId, captured, 0, 0)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (role.trainSkill != null)
            {
                // Slider ends mid-panel, matching the auto-role section's reach.
                var bandRect = new Rect(skillRect.xMax + 12f, y - 4f,
                    rect.x + rect.width / 2f - skillRect.xMax - 12f, TrainingRowH);
                TooltipHandler.TipRegion(bandRect, "WR_TrainBandTip".Translate());
                var band = bandRoleId == role.id
                    ? pendingBand
                    : new IntRange(role.trainMin, role.trainMax == 0 ? 21 : role.trainMax);
                var before = band;
                Widgets.IntRange(bandRect, 87210 + role.id, ref band, 0, 21);
                if (band != before)
                {
                    pendingBand = band;
                    bandRoleId = role.id;
                }
                if (bandRoleId == role.id && !Input.GetMouseButton(0))
                {
                    bandRoleId = -1;
                    int max = pendingBand.max >= 21 ? 0 : pendingBand.max; // 21 = open-ended (skills cap at 20)
                    if (pendingBand.min != role.trainMin || max != role.trainMax)
                        RoleCommands.SetRoleTraining(role.id, role.trainSkill, pendingBand.min, max);
                }
            }
            y += TrainingRowH;

            // Row 2: train targets as removable chips + Add.
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            string targetsLabel = "WR_TrainTargetsLabel".Translate();
            float targetsLabelW = WrText.FitWidth(targetsLabel);
            Widgets.Label(new Rect(rect.x, y + 3f, targetsLabelW, TrainingRowH), targetsLabel);
            GUI.color = Color.white;
            float x = rect.x + targetsLabelW + 6f;
            foreach (int targetId in role.trainTargets
                         .OrderBy(id => store.RoleById(id)?.label, System.StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                var target = store.RoleById(targetId);
                if (target == null) continue;
                float w = RoleChipUI.WidthFor(target, showRemove: true, ChipDisplay.Normal, null, false);
                if (x + w > rect.xMax - 70f) break; // keep room for Add
                var chipRect = new Rect(x, y + (TrainingRowH - 4f - RoleChipUI.Height) / 2f,
                    w, RoleChipUI.Height);
                TooltipHandler.TipRegion(chipRect, "WR_TrainTargetTip".Translate(target.label));
                var click = RoleChipUI.Draw(chipRect, target, ChipStyle.Normal,
                    showRemove: true, dragSource: null, onClick: null);
                if (click == ChipClick.Remove)
                    RoleCommands.SetRoleTrainTargets(role.id,
                        role.trainTargets.Where(id => id != targetId).ToList());
                x += w + 4f;
            }
            if (Widgets.ButtonText(new Rect(x, y, 64f, TrainingRowH - 4f), "WR_TrainAddTarget".Translate()))
            {
                int roleId = role.id;
                var current = role.trainTargets;
                var options = new List<FloatMenuOption>();
                foreach (var candidate in store.roles)
                {
                    // Rule-carrying (auto) roles are never recommended, so a
                    // promotion could never surface them: not valid targets.
                    if (candidate.id == role.id || candidate.managed || candidate.blocker
                        || candidate.HasRules || current.Contains(candidate.id)) continue;
                    var captured = candidate.id;
                    options.Add(new FloatMenuOption(candidate.label, () =>
                        RoleCommands.SetRoleTrainTargets(roleId,
                            current.Concat(new[] { captured }).ToList())));
                }
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            y += TrainingRowH;

            // Row 3: the colonist count as a picker. Auto = dealt at colony
            // scale; 0 = never dealt (interest-driven only); N = N per
            // colony-scale unit, and Best drafts into the role.
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            string holdersLabel = "WR_HoldersLabel".Translate();
            float holdersLabelW = WrText.FitWidth(holdersLabel);
            Widgets.Label(new Rect(rect.x, y + 3f, holdersLabelW, TrainingRowH), holdersLabel);
            GUI.color = Color.white;
            var holdersRect = new Rect(rect.x + holdersLabelW + 12f, y, 72f, TrainingRowH - 4f);
            TooltipHandler.TipRegion(holdersRect, "WR_HoldersTip".Translate());
            string holdersText = role.minHolders < 0
                ? "WR_HoldersAuto".Translate().ToString()
                : role.minHolders.ToString();
            if (Widgets.ButtonText(holdersRect, holdersText))
            {
                int captured = role.id;
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_HoldersAuto".Translate(),
                        () => RoleCommands.SetRoleHolders(captured, -1)),
                };
                for (int n = 0; n <= 8; n++)
                {
                    int value = n;
                    options.Add(new FloatMenuOption(value.ToString(),
                        () => RoleCommands.SetRoleHolders(captured, value)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void DrawRulesSection(Rect rect, Role role)
        {
            // Selecting another role mid-paint abandons the pending edit.
            if (paintingHours && paintRoleId != role.id)
                paintingHours = false;

            int shownMask = paintingHours ? pendingHoursMask : role.activeHours;
            bool mouseHeld = Input.GetMouseButton(0);
            // The clock icon ties the grid to the chips' time marker.
            int x0 = Mathf.RoundToInt(rect.x) + 22;
            int legendY = Mathf.RoundToInt(rect.y);
            int labelsY = legendY + HourLabelH + 2;
            int cellsY = labelsY + HourLabelH + 2;

            // Legend top-left, above the grid it explains.
            const float LegendGap = 12f;
            Text.Font = GameFont.Small;
            float legendX = DrawLegendEntry(rect.x, legendY, HourActiveColor, "WR_HoursActive".Translate());
            DrawLegendEntry(legendX + LegendGap, legendY, HourInactiveColor, "WR_HoursInactive".Translate());

            GUI.color = RoleChipUI.RuleMarkerColor;
            GUI.DrawTexture(new Rect(rect.x, cellsY + (HourCellH - 16f) / 2f, 16f, 16f), WorkRolesTex.TimeMarker);
            GUI.color = Color.white;

            // Hour headers: one per cell, Tiny and bottom-anchored (vanilla schedule style).
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            for (int h = 0; h < 24; h++)
                Widgets.Label(new Rect(x0 + h * (HourCellW + HourCellGap), labelsY, HourCellW, HourLabelH), h.ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var gridRect = new Rect(x0, cellsY, HourGridW, HourCellH);
            if (Mouse.IsOver(gridRect))
                TooltipHandler.TipRegion(gridRect, "WR_ActiveHours".Translate());

            for (int h = 0; h < 24; h++)
            {
                var cell = new Rect(x0 + h * (HourCellW + HourCellGap), cellsY, HourCellW, HourCellH);
                bool active = (shownMask & (1 << h)) != 0;
                Widgets.DrawBoxSolid(cell, active ? HourActiveColor : HourInactiveColor);

                if (!Mouse.IsOver(cell)) continue;
                Widgets.DrawBox(cell, 2);

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    // Start painting: target value = inverse of the pressed cell
                    // (simplified vanilla timetable pattern).
                    paintingHours = true;
                    paintRoleId = role.id;
                    pendingHoursMask = role.activeHours;
                    hourPaintValue = !active;
                    ApplyHourPaint(h);
                    e.Use();
                }
                else if (paintingHours && mouseHeld)
                {
                    ApplyHourPaint(h);
                }
            }

            // Commit ONE synced command on release, and only when something changed.
            if (paintingHours && !mouseHeld)
            {
                paintingHours = false;
                if (pendingHoursMask != role.activeHours)
                    RoleCommands.SetRoleActiveHours(role.id, pendingHoursMask);
            }

            // Location multi-select right of the grid: Anywhere, or any set of
            // named settlements / ships / Caravans — active where any matches.
            // The location icon ties the picker to the chips' location marker.
            const float LocBtnW = 110f;
            float btnX = gridRect.xMax + 16f;
            GUI.color = RoleChipUI.RuleMarkerColor;
            GUI.DrawTexture(new Rect(btnX, cellsY + (HourCellH - 16f) / 2f, 16f, 16f), WorkRolesTex.LocationMarker);
            GUI.color = Color.white;
            btnX += 22f;
            if (Widgets.ButtonText(new Rect(btnX, cellsY + (HourCellH - 24f) / 2f, LocBtnW, 24f),
                    LocationSummary(role)))
            {
                int roleId = role.id;
                var tokens = role.locationTokens;
                string Check(bool on, string label) => (on ? "✓ " : "") + label;
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(Check(tokens.Count == 0, "WR_LocationAny".Translate()),
                        () => RoleCommands.ClearRoleLocations(roleId)),
                    new FloatMenuOption(Check(tokens.Contains(LocationRules.Settlements), "WR_LocationSettlements".Translate()),
                        () => RoleCommands.ToggleRoleLocation(roleId, LocationRules.Settlements)),
                };
                foreach (var loc in ColonyScope.Locations()
                    .OrderBy(l => l.IsShip)
                    .ThenBy(l => l.Label, System.StringComparer.OrdinalIgnoreCase))
                {
                    string token = (loc.IsShip ? LocationRules.ShipPrefix : LocationRules.SettlementPrefix) + loc.Id;
                    var item = new FloatMenuOption(Check(tokens.Contains(token), loc.Label),
                        () => RoleCommands.ToggleRoleLocation(roleId, token));
                    if (loc.IsShip) item.tooltip = "WR_ShipTip".Translate();
                    options.Add(item);
                }
                options.Add(new FloatMenuOption(Check(tokens.Contains(LocationRules.Caravans), "WR_LocationCaravans".Translate()),
                    () => RoleCommands.ToggleRoleLocation(roleId, LocationRules.Caravans)));
                Find.WindowStack.Add(new FloatMenu(options));
            }

        }

        private const float LegendSwatch = 12f;

        private static float DrawLegendEntry(float x, float y, Color color, string label)
        {
            Text.Font = GameFont.Small;
            float labelW = WrText.FitWidth(label);
            float labelH = Text.CalcSize(label).y;
            Widgets.DrawBoxSolid(new Rect(x, y + (HourLabelH - LegendSwatch) / 2f, LegendSwatch, LegendSwatch), color);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(new Rect(x + LegendSwatch + 4f, y + (HourLabelH - labelH) / 2f, labelW, labelH), label);
            GUI.color = Color.white;
            return x + LegendSwatch + 4f + labelW;
        }

        private void ApplyHourPaint(int hour)
        {
            if (hourPaintValue) pendingHoursMask |= 1 << hour;
            else pendingHoursMask &= ~(1 << hour);
        }

        // Only one row is hovered at a time: a single (role, stamp) slot stops
        // the entries summary from re-joining every hover pass.
        private static int rowTipStamp = -1;
        private static int rowTipRoleId = -1;
        private static string rowTipCache;

        private static string RowTip(Role role)
        {
            if (rowTipCache == null || rowTipStamp != UiVersion.Current || rowTipRoleId != role.id)
            {
                rowTipStamp = UiVersion.Current;
                rowTipRoleId = role.id;
                rowTipCache = $"{role.entries.Count} entries: "
                    + $"{string.Join(", ", role.entries.Take(4).Select(e => e.DefName))}"
                    + (role.entries.Count > 4 ? ", …" : "");
            }
            return rowTipCache;
        }

        /// A role that can never act: no jobs (managed roles legitimately sit
        /// empty), or every location it names is gone.
        internal static bool RoleInvalid(Role role) =>
            (role.entries.Count == 0 && !role.managed)
            || (role.locationTokens.Count > 0 && role.locationTokens.All(StaleLocationToken));

        private static bool StaleLocationToken(string token)
        {
            if (token == LocationRules.Settlements || token == LocationRules.Caravans) return false;
            string id = token.Substring(token.IndexOf(':') + 1);
            return ColonyScope.Locations().All(l => l.Id != id);
        }

        private static string LocationSummary(Role role)
        {
            var tokens = role.locationTokens;
            if (tokens.Count == 0) return "WR_LocationAny".Translate();
            if (tokens.Count > 1) return "WR_LocationCount".Translate(tokens.Count);
            return TokenLabel(tokens[0]);
        }

        private static string TokenLabel(string token)
        {
            if (token == LocationRules.Settlements) return "WR_LocationSettlements".Translate();
            if (token == LocationRules.Caravans) return "WR_LocationCaravans".Translate();
            string id = token.Substring(token.IndexOf(':') + 1);
            var loc = ColonyScope.Locations().FirstOrDefault(l => l.Id == id);
            return loc?.Label ?? "WR_LocationGone".Translate().ToString();
        }

        // ----- Assigned pawn names row -----

        // Open-window snapshot of the selected role's holders.
        private static List<(Pawn pawn, int position)> holdersCache;
        private static int holdersStamp = -1;
        private static int holdersRoleId = -1;

        private static void DrawAssignedPawnNames(Rect rect, Role role, RoleStore store)
        {
            if (holdersCache == null || holdersStamp != UiVersion.Current || holdersRoleId != role.id)
            {
                holdersStamp = UiVersion.Current;
                holdersRoleId = role.id;
                holdersCache = new List<(Pawn, int)>();
                foreach (var pawn in ColonistsTabView.ListedPawns())
                {
                    if (!store.pawnSets.TryGetValue(pawn, out var set)) continue;
                    int idx = set.assignments.FindIndex(a => a.roleId == role.id);
                    if (idx < 0) continue;
                    holdersCache.Add((pawn, idx + 1)); // 1-based position = priority
                }
                // Sort ascending by position so highest-priority pawns appear first
                holdersCache.Sort((a, b) => a.position.CompareTo(b.position));
            }
            var pawnPositions = holdersCache;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (pawnPositions.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(rect, "WR_Nobody".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Color SepColor = new Color(0.55f, 0.55f, 0.55f);
            const string Sep = ", ";
            float sepW = WrText.FitWidth(Sep);
            // Reserve enough width so "+99 others" always fits at the right edge.
            const float OverflowReserve = 70f;

            float x = rect.x;
            int remaining = 0;

            for (int i = 0; i < pawnPositions.Count; i++)
            {
                var (pawn, _) = pawnPositions[i];
                string name = pawn.LabelShortCap;
                float nameW = WrText.FitWidth(name);
                bool hasNext = i < pawnPositions.Count - 1;

                // Determine how much space remains after this name (sep + overflow reserve if more names follow).
                float needed = nameW + (hasNext ? sepW + OverflowReserve : 0f);
                if (x + needed > rect.xMax && i > 0)
                {
                    // No room — count remaining (including this one)
                    remaining = pawnPositions.Count - i;
                    break;
                }
                // Even the very first name doesn't fit: show overflow immediately.
                if (i == 0 && x + nameW + (hasNext ? OverflowReserve : 0f) > rect.xMax && hasNext)
                {
                    remaining = pawnPositions.Count;
                    break;
                }

                Widgets.Label(new Rect(x, rect.y, nameW, rect.height), name);
                x += nameW;

                if (hasNext && x + sepW <= rect.xMax)
                {
                    GUI.color = SepColor;
                    Widgets.Label(new Rect(x, rect.y, sepW, rect.height), Sep);
                    GUI.color = Color.white;
                    x += sepW;
                }
            }

            if (remaining > 0)
            {
                string moreText = "WR_PlusOthers".Translate(remaining);
                GUI.color = SepColor;
                Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), moreText);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ----- Selected Jobs: two-column table with drag reorder + up/down buttons -----

        private void DrawEntries(Rect rect, Role role)
        {
            // Same visible-gap correction as the Available Jobs header.
            WrText.HeaderLabel(new Rect(rect.x + 8f, rect.y + WrText.MediumTopBearing, rect.width - 8f, 28f),
                "WR_SelectedJobs".Translate());

            // Column headers — 24f height so descenders aren't clipped
            float headerY = rect.y + 28f + 4f;
            float removeW = (IconButton + 4f) * 3f; // room for up + down + [x]
            float typeW = (rect.width - 8f - removeW - 8f) * 0.45f;
            float jobW  = (rect.width - 8f - removeW - 8f) * 0.55f;

            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f + 4f, headerY, typeW, 24f), "WR_TypeColumn".Translate());
            Widgets.Label(new Rect(rect.x + 8f + 4f + typeW, headerY, jobW, 24f), "WR_JobColumn".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var scrollRect = new Rect(rect.x + 8f, headerY + 24f, rect.width - 8f, rect.height - 28f - 4f - 24f);
            float contentHeight = role.entries.Count * RowHeight;

            if (Event.current.type == EventType.Repaint)
            {
                int capturedRoleId = role.id;
                entriesReorderableGroupId = ReorderableWidget.NewGroup(
                    (from, to) => {
                        if (to > from) to--;
                        RoleCommands.MoveEntry(capturedRoleId, from, to);
                    },
                    ReorderableDirection.Vertical,
                    scrollRect);
            }

            Widgets.BeginScrollView(scrollRect, ref entriesScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));

            var deadEntries = DeadEntryIndexes(role);
            for (int i = 0; i < role.entries.Count; i++)
            {
                var entry = role.entries[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);

                bool dragging = ReorderableWidget.Reorderable(entriesReorderableGroupId, row, useRightButton: false, highlightDragged: true);

                if (Mouse.IsOver(row) && !dragging) Widgets.DrawHighlight(row);

                string typeLabel, jobLabel;
                bool missing = false;
                GetEntryLabels(entry, out typeLabel, out jobLabel, out missing);
                bool dead = !missing && deadEntries.Contains(i);

                Text.Anchor = TextAnchor.MiddleLeft;
                if (missing) GUI.color = new Color(1f, 0.4f, 0.4f, 0.8f);
                else if (dead) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                // Long names truncate to their column (never wrap into the next
                // row); the tooltip carries the full name — same treatment as
                // the job filter button.
                bool wrap = Text.WordWrap;
                Text.WordWrap = false;

                var typeRect = new Rect(row.x + 4f, row.y, typeW, RowHeight);
                string typeShown = typeLabel.Truncate(typeW - 4f);
                Widgets.Label(typeRect, typeShown);
                if (typeShown != typeLabel)
                    TooltipHandler.TipRegion(typeRect, typeLabel);

                string jobText = entry.Kind == JobEntryKind.WorkType
                    ? "WR_AllJobs".Translate().ToString() : jobLabel;
                var jobRect = new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight);
                string jobShown = jobText.Truncate(jobW - 4f);
                Widgets.Label(jobRect, jobShown);
                if (jobShown != jobText)
                    TooltipHandler.TipRegion(jobRect, jobText);

                Text.WordWrap = wrap;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (missing)
                    TooltipHandler.TipRegion(row, "WR_MissingDef".Translate(entry.DefName));
                else if (dead)
                    TooltipHandler.TipRegion(row, "WR_DeadEntryTip".Translate());
                if (!missing && Mouse.IsOver(row))
                {
                    string skillTip = entry.Kind == JobEntryKind.WorkType
                        ? JobSkillProfiles.WorkTypeTip(entry.DefName)
                        : JobSkillProfiles.GiverTip(entry.DefName);
                    if (skillTip != null) TooltipHandler.TipRegion(row, skillTip);
                }

                float btnY = row.y + (RowHeight - IconButton) / 2f;
                float removeX = row.xMax - IconButton - 2f;
                float downX   = removeX - IconButton - 2f;
                float upX     = downX - IconButton - 2f;

                int capturedI = i;
                int capturedRoleId = role.id;

                if (i > 0 && Widgets.ButtonImage(new Rect(upX, btnY, IconButton, IconButton), TexButton.ReorderUp))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI - 1);
                if (i < role.entries.Count - 1 && Widgets.ButtonImage(new Rect(downX, btnY, IconButton, IconButton), TexButton.ReorderDown))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI + 1);
                if (Widgets.ButtonImage(new Rect(removeX, btnY, IconButton, IconButton), TexButton.Delete))
                    RoleCommands.RemoveEntry(capturedRoleId, capturedI);
            }
            Widgets.EndScrollView();
        }

        // Open-window snapshot: entry edits arrive as synced commands, which bump
        // the stamp on execution (locally and on other MP clients alike).
        private static HashSet<int> deadEntriesCache;
        private static int deadEntriesStamp = -1;
        private static int deadEntriesRoleId = -1;

        private static HashSet<int> DeadEntryIndexes(Role role)
        {
            if (deadEntriesCache == null || deadEntriesStamp != UiVersion.Current
                || deadEntriesRoleId != role.id)
            {
                deadEntriesStamp = UiVersion.Current;
                deadEntriesRoleId = role.id;
                deadEntriesCache = JobOrderCompiler.DeadEntryIndexes(role.entries, GameJobCatalog.Instance);
            }
            return deadEntriesCache;
        }

        // Def labels are fixed for the session: cache per (kind, defName) so the
        // entry rows stop doing def lookups + CapitalizeFirst allocs per pass.
        private static readonly Dictionary<(JobEntryKind kind, string defName), (string type, string job, bool missing)>
            entryLabelCache = new Dictionary<(JobEntryKind, string), (string, string, bool)>();

        private static void GetEntryLabels(JobEntry entry, out string typeLabel, out string jobLabel, out bool missing)
        {
            var key = (entry.Kind, entry.DefName);
            if (!entryLabelCache.TryGetValue(key, out var labels))
            {
                GetEntryLabelsUncached(entry, out var type, out var job, out var gone);
                entryLabelCache[key] = labels = (type, job, gone);
            }
            typeLabel = labels.type;
            jobLabel = labels.job;
            missing = labels.missing;
        }

        private static void GetEntryLabelsUncached(JobEntry entry, out string typeLabel, out string jobLabel, out bool missing)
        {
            missing = false;
            if (entry.Kind == JobEntryKind.WorkType)
            {
                var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                {
                    typeLabel = (def.gerundLabel ?? def.labelShort ?? def.defName).CapitalizeFirst();
                    jobLabel = "WR_AllJobs".Translate();
                    return;
                }
            }
            else
            {
                var def = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                {
                    typeLabel = def.workType != null
                        ? (def.workType.gerundLabel ?? def.workType.labelShort ?? def.workType.defName).CapitalizeFirst()
                        : "?";
                    jobLabel = GetGiverDisplayName(def);
                    return;
                }
            }
            missing = true;
            typeLabel = entry.DefName;
            jobLabel = "";
        }

        // ----- Available Jobs: the work type / giver tree -----

        // Open-window snapshot of the job-tree rows; expand/collapse is UI-local
        // state, so the key carries its revision alongside the search text.
        private List<(WorkTypeDef type, WorkGiverDef giver)> treeNodesCache;
        private int treeNodesStamp = -1;
        private int treeNodesRev = -1;
        private string treeNodesFilter;
        private int treeRev;

        private List<(WorkTypeDef type, WorkGiverDef giver)> TreeNodes(bool filtering)
        {
            if (treeNodesCache != null && treeNodesStamp == UiVersion.Current
                && treeNodesRev == treeRev && treeNodesFilter == filter)
                return treeNodesCache;
            treeNodesStamp = UiVersion.Current;
            treeNodesRev = treeRev;
            treeNodesFilter = filter;
            var nodes = treeNodesCache = new List<(WorkTypeDef, WorkGiverDef)>();
            foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading
                .OrderByDescending(t => t.naturalPriority))
            {
                var givers = type.workGiversByPriority;
                string typeDisplayName = (type.gerundLabel ?? type.labelShort ?? type.defName).CapitalizeFirst();
                bool typeMatches = !filtering || Matches(typeDisplayName);

                var matchingGivers = filtering
                    ? givers.Where(g => Matches(GetGiverDisplayName(g))).ToList()
                    : givers.ToList();

                if (filtering && !typeMatches && matchingGivers.Count == 0) continue;

                nodes.Add((type, (WorkGiverDef)null));
                if (filtering || expanded.Contains(type.defName))
                    foreach (var giver in (filtering && !typeMatches) ? matchingGivers : givers.ToList())
                        nodes.Add((type, giver));
            }
            return nodes;
        }

        private void DrawJobTree(Rect rect, Role role)
        {
            const float SearchW = 110f;
            const float SearchLabelW = 46f;
            const float SearchH = 24f;
            float headerW = rect.width - SearchLabelW - SearchW - 8f;

            // HeaderLabel puts the VISIBLE text top at rect.y; directly under the
            // top box that reads as flush, so the top bearing is re-added as gap.
            WrText.HeaderLabel(new Rect(rect.x + 4f, rect.y + WrText.MediumTopBearing, headerW - 4f, 28f),
                "WR_AvailableJobs".Translate());

            // "Search" label immediately left of field; group shifted 4f left from right edge
            const float SearchRightPad = 4f;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - SearchLabelW - SearchW - 4f - SearchRightPad, rect.y + (28f - SearchH) / 2f, SearchLabelW, SearchH), "WR_Search".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float fieldY = rect.y + (28f - SearchH) / 2f;
            filter = Widgets.TextField(
                new Rect(rect.xMax - SearchW - SearchRightPad, fieldY, SearchW - 22f, SearchH), filter);
            if (!filter.NullOrEmpty()
                && Widgets.ButtonImage(new Rect(rect.xMax - SearchRightPad - 18f, fieldY + (SearchH - 18f) / 2f, 18f, 18f),
                    TexButton.CloseXSmall))
            {
                filter = "";
                GUIUtility.keyboardControl = 0; // release the field's edit buffer
            }

            float treeTopY = rect.y + 28f + 4f;
            var scrollRect = new Rect(rect.x, treeTopY, rect.width, rect.height - 28f - 4f);
            bool filtering = !filter.NullOrEmpty();

            // Selection changed: surface the role's first entry (expand its work
            // type when the entry is a job, so the row exists to scroll to).
            (WorkTypeDef type, WorkGiverDef giver)? treeTarget = null;
            if (scrollJobTreeToSelection)
            {
                treeTarget = FirstEntryTreeTarget(role);
                if (treeTarget?.giver != null && expanded.Add(treeTarget.Value.type.defName))
                    treeRev++;
            }

            var nodes = TreeNodes(filtering);

            if (scrollJobTreeToSelection)
            {
                scrollJobTreeToSelection = false;
                if (treeTarget != null)
                {
                    int target = nodes.FindIndex(n =>
                        n.type == treeTarget.Value.type && n.giver == treeTarget.Value.giver);
                    if (target >= 0)
                        treeScroll.y = Mathf.Max(0f,
                            target * RowHeight - (scrollRect.height - RowHeight) / 2f);
                }
            }

            Widgets.BeginScrollView(scrollRect, ref treeScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, nodes.Count * RowHeight));

            for (int i = 0; i < nodes.Count; i++)
            {
                var (type, giver) = nodes[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Text.Anchor = TextAnchor.MiddleLeft;

                if (giver == null)
                {
                    // Work-type header row
                    string typeDisplayName = (type.gerundLabel ?? type.labelShort ?? type.defName).CapitalizeFirst();
                    int giverCount = type.workGiversByPriority.Count;
                    string countHint = $" ({giverCount})";

                    bool isExpanded = filtering || expanded.Contains(type.defName);
                    if (Widgets.ButtonImage(new Rect(row.x + 2f, row.y + 4f, IconButton, IconButton),
                        isExpanded ? TexButton.Collapse : TexButton.Reveal))
                    {
                        if (!expanded.Add(type.defName)) expanded.Remove(type.defName);
                        treeRev++;
                    }

                    var checkboxRect = new Rect(row.x + 26f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = GetWorkTypeState(role, type);
                    // Right-click: add every job as its own reorderable entry.
                    var te = Event.current;
                    if (te.type == EventType.MouseDown && te.button == 1 && row.Contains(te.mousePosition))
                    {
                        te.Use();
                        var capturedType = type;
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                        {
                            new FloatMenuOption("WR_AddAllJobs".Translate(), () =>
                                AddAllGivers(role, capturedType)),
                        }));
                    }
                    // ~ (some jobs selected) clicks like Off: it adds the type
                    // entry; the jobs' own entries stay above it, still live.
                    bool typeAdds = currentState != MultiCheckboxState.On;
                    if (MultiCheckboxClicked(checkboxRect, currentState, typeAdds))
                        ApplyWorkTypeState(role, type,
                            typeAdds ? MultiCheckboxState.On : MultiCheckboxState.Off);

                    // The label toggles like the arrow — a far bigger target.
                    var typeLabelRect = new Rect(row.x + 54f, row.y, row.width - 54f, RowHeight);
                    Widgets.Label(typeLabelRect, typeDisplayName + countHint);
                    if (Widgets.ButtonInvisible(typeLabelRect))
                    {
                        if (!expanded.Add(type.defName)) expanded.Remove(type.defName);
                    }
                    if (Mouse.IsOver(row))
                    {
                        string skillTip = JobSkillProfiles.WorkTypeTip(type.defName);
                        if (skillTip != null) TooltipHandler.TipRegion(row, skillTip);
                    }
                }
                else
                {
                    // Job giver child row
                    string giverName = GetGiverDisplayName(giver);

                    var checkboxRect = new Rect(row.x + 42f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = GetGiverState(role, type, giver);
                    // ~ = covered via the work type; a click promotes to an own
                    // (reorderable) entry.
                    if (currentState == MultiCheckboxState.Partial)
                        TooltipHandler.TipRegion(row, "WR_CoveredByTypeTip".Translate());
                    bool giverAdds = currentState != MultiCheckboxState.On;
                    if (MultiCheckboxClicked(checkboxRect, currentState, giverAdds))
                        ApplyGiverState(role, type, giver,
                            giverAdds ? MultiCheckboxState.On : MultiCheckboxState.Off);

                    Widgets.Label(new Rect(row.x + 70f, row.y, row.width - 70f, RowHeight), giverName);
                    if (Mouse.IsOver(row))
                    {
                        string skillTip = JobSkillProfiles.GiverTip(giver.defName);
                        if (skillTip != null) TooltipHandler.TipRegion(row, skillTip);
                    }
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();
        }

        private bool Matches(string label) =>
            label != null && label.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// The tree row for the role's first resolvable entry: the work-type
        /// header for a WorkType entry, the giver row for a WorkGiver entry.
        private static (WorkTypeDef type, WorkGiverDef giver)? FirstEntryTreeTarget(Role role)
        {
            foreach (var entry in role.entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                    if (type != null) return (type, null);
                }
                else
                {
                    var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                    if (giver?.workType != null) return (giver.workType, giver);
                }
            }
            return null;
        }

        // ----- Disambiguated display names for WorkGiverDefs -----

        private static Dictionary<WorkGiverDef, string> BuildGiverDisplayCache()
        {
            var result = new Dictionary<WorkGiverDef, string>();
            foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var givers = type.workGiversByPriority;
                var byLabel = new Dictionary<string, List<WorkGiverDef>>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var g in givers)
                {
                    string baseName = (g.label ?? g.defName).CapitalizeFirst();
                    if (!byLabel.TryGetValue(baseName, out var list))
                        byLabel[baseName] = list = new List<WorkGiverDef>();
                    list.Add(g);
                }
                foreach (var g in givers)
                {
                    string baseName = (g.label ?? g.defName).CapitalizeFirst();
                    byLabel.TryGetValue(baseName, out var siblings);
                    if (siblings != null && siblings.Count > 1)
                    {
                        // Collision: append " (emergency)" to the one with emergency == true.
                        // If both or neither are emergency, no suffix (just use base name).
                        int emergencyCount = siblings.Count(s => s.emergency);
                        if (emergencyCount == 1 && g.emergency)
                            result[g] = baseName + "WR_EmergencySuffix".Translate();
                        else
                            result[g] = baseName;
                    }
                    else
                    {
                        result[g] = baseName;
                    }
                }
            }
            return result;
        }

        internal static string GetGiverDisplayName(WorkGiverDef g)
        {
            if (_giverDisplayCache == null)
                _giverDisplayCache = BuildGiverDisplayCache();
            if (_giverDisplayCache.TryGetValue(g, out var name))
                return name;
            return (g.label ?? g.defName).CapitalizeFirst();
        }

        // ----- Tri-state helpers -----

        /// CheckboxMulti look-alike whose click sound matches OUR action — the
        /// vanilla widget keys the sound to the state it proposes, which is
        /// wrong for the promote-from-~ click.
        private static bool MultiCheckboxClicked(Rect rect, MultiCheckboxState state, bool adds)
        {
            var tex = state == MultiCheckboxState.On ? Widgets.CheckboxOnTex
                : state == MultiCheckboxState.Off ? Widgets.CheckboxOffTex
                : Widgets.CheckboxPartialTex;
            if (!Widgets.ButtonImage(rect, tex)) return false;
            (adds ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            return true;
        }

        /// On = the WorkType entry itself is in the list (it also covers future
        /// modded jobs — not the same as all givers individually); Partial = no
        /// type entry, but some of its jobs have their own entries.
        // Open-window snapshot of the selected role's entry defNames: checkbox
        // states scanned role.entries with nested LINQ per visible row otherwise.
        // Entry edits are synced commands, so the stamp catches every change.
        private static int entrySetsStamp = -1;
        private static int entrySetsRoleId = -1;
        private static readonly HashSet<string> entryTypeSet = new HashSet<string>();
        private static readonly HashSet<string> entryGiverSet = new HashSet<string>();

        private static void EnsureEntrySets(Role role)
        {
            if (entrySetsStamp == UiVersion.Current && entrySetsRoleId == role.id) return;
            entrySetsStamp = UiVersion.Current;
            entrySetsRoleId = role.id;
            entryTypeSet.Clear();
            entryGiverSet.Clear();
            foreach (var entry in role.entries)
                (entry.Kind == JobEntryKind.WorkType ? entryTypeSet : entryGiverSet).Add(entry.DefName);
        }

        private static MultiCheckboxState GetWorkTypeState(Role role, WorkTypeDef type)
        {
            EnsureEntrySets(role);
            if (entryTypeSet.Contains(type.defName)) return MultiCheckboxState.On;
            var givers = type.workGiversByPriority;
            for (int i = 0; i < givers.Count; i++)
                if (entryGiverSet.Contains(givers[i].defName))
                    return MultiCheckboxState.Partial;
            return MultiCheckboxState.Off;
        }

        /// On = own entry (reorderable); Partial = covered via the work type.
        private static MultiCheckboxState GetGiverState(Role role, WorkTypeDef type, WorkGiverDef giver)
        {
            EnsureEntrySets(role);
            if (entryGiverSet.Contains(giver.defName)) return MultiCheckboxState.On;
            return entryTypeSet.Contains(type.defName)
                ? MultiCheckboxState.Partial
                : MultiCheckboxState.Off;
        }

        /// Adds/removes only the WorkType entry itself — giver entries (and the
        /// player's ordering of them) are never touched from here.
        private static void ApplyWorkTypeState(Role role, WorkTypeDef type, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                if (!role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName))
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkType, type.defName));
            }
            else
            {
                int typeIdx = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
                if (typeIdx >= 0)
                    RoleCommands.RemoveEntry(role.id, typeIdx);
            }
        }

        /// Giver entries and a WorkType entry may coexist: an entry placed above
        /// the type outranks it (the compiler keeps a job's earliest position),
        /// which is how single jobs get ordered inside an all-jobs selection.
        private static void ApplyGiverState(Role role, WorkTypeDef type, WorkGiverDef giver, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                if (role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName))
                    return;
                // Above the type entry when one exists — below it, the entry
                // would never win a position.
                int typeIdx = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
                if (typeIdx >= 0)
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName), typeIdx);
                else
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
            }
            else // Off: only ever removes the giver's own entry
            {
                int giverIdx = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName);
                if (giverIdx >= 0)
                    RoleCommands.RemoveEntry(role.id, giverIdx);
            }
        }

        /// Every giver of the type as its own reorderable entry (existing ones
        /// kept in place), above the type entry when present.
        private static void AddAllGivers(Role role, WorkTypeDef type)
        {
            int insertAt = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
            if (insertAt < 0) insertAt = role.entries.Count;
            foreach (var giver in type.workGiversByPriority)
            {
                if (role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName)) continue;
                RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName), insertAt);
                insertAt++;
            }
        }
    }
}
