using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    public class RolesTabView
    {
        private sealed class RolesTabChromeSnapshot
        {
            internal RolesTabChromeSnapshot(
                string selectOrCreateRole,
                string searchCaption,
                string displayModeCaption,
                string jobFilterCaption,
                string treeNested,
                string treeFlat,
                string anyJob,
                string jobFilterLabel,
                string jobFilterShown,
                string newLabel,
                string copyLabel,
                string deleteLabel)
            {
                SelectOrCreateRole = selectOrCreateRole;
                SearchCaption = searchCaption;
                DisplayModeCaption = displayModeCaption;
                JobFilterCaption = jobFilterCaption;
                TreeNested = treeNested;
                TreeFlat = treeFlat;
                AnyJob = anyJob;
                JobFilterLabel = jobFilterLabel;
                JobFilterShown = jobFilterShown;
                NewLabel = newLabel;
                CopyLabel = copyLabel;
                DeleteLabel = deleteLabel;
            }

            internal string SelectOrCreateRole { get; }
            internal string SearchCaption { get; }
            internal string DisplayModeCaption { get; }
            internal string JobFilterCaption { get; }
            internal string TreeNested { get; }
            internal string TreeFlat { get; }
            internal string AnyJob { get; }
            internal string JobFilterLabel { get; }
            internal string JobFilterShown { get; }
            internal string NewLabel { get; }
            internal string CopyLabel { get; }
            internal string DeleteLabel { get; }

            internal bool ContentEquals(RolesTabChromeSnapshot other) =>
                other != null
                && SelectOrCreateRole == other.SelectOrCreateRole
                && SearchCaption == other.SearchCaption
                && DisplayModeCaption == other.DisplayModeCaption
                && JobFilterCaption == other.JobFilterCaption
                && TreeNested == other.TreeNested
                && TreeFlat == other.TreeFlat
                && AnyJob == other.AnyJob
                && JobFilterLabel == other.JobFilterLabel
                && JobFilterShown == other.JobFilterShown
                && NewLabel == other.NewLabel
                && CopyLabel == other.CopyLabel
                && DeleteLabel == other.DeleteLabel;
        }

        // Pawn source for the holders row, injected by MainTabWindow (the
        // colonist table owns the scope and its pawn snapshot).
        internal System.Func<IReadOnlyList<Pawn>> listedPawns;
        internal System.Func<int> pawnListRevision;

        // Unified role tip (TreeRow context), injected by MainTabWindow: the
        // builder lives on ColonistsTabView (it needs BestFits' pawn snapshot).
        internal System.Func<Role, StructuredTip> roleTip;

        private readonly RolesListState listState = new RolesListState();
        private readonly RoleEditorState editorState = new RoleEditorState();

        // Owner: this Roles-tab view for one open Work Roles window.
        // Key: selected job-filter def name, LanguageChangeCoordinator.Revision,
        // and DefinitionReloadCoordinator.Revision.
        // Value: immutable detached list/editor chrome plus the resolved and
        // Small-font-truncated job-filter label.
        // Dependencies: fixed translated labels, selected job-filter def,
        // localized giver display name, GameFont.Small metrics, and the fixed
        // 200f job-label width available inside the button.
        // Refresh: immediately on the next draw after a key change.
        // Equality: an exact equal rebuild preserves snapshot identity.
        // Teardown: Reset releases the snapshot; language invalidation retains
        // it only for equal-content reuse on the required refresh.
        private RolesTabChromeSnapshot chromeSnapshot;
        private string chromeJobFilter = "\0";
        private int chromeLanguageRevision = -1;
        private int chromeDefinitionRevision = -1;

        // Owner: this Roles-tab view, partitioned by RoleStore identity.
        // Key: store identity, detached role-selection count, and
        // DefinitionReloadCoordinator.Revision.
        // Value: cached scalar desired height (immutable).
        // Dependencies: role-catalog membership count, work-type definition
        // membership, and the fixed Roles-tab geometry constants below.
        // Refresh: immediately on the next size read after a key change.
        // Equality: exact key hits reuse the scalar without rebuilding.
        // Teardown: Reset releases the store reference and invalidates stamps.
        private RoleStore desiredHeightOwner;
        private int desiredHeightRoleCount = -1;
        private int desiredHeightDefinitionRevision = -1;
        private float desiredHeight = DefaultDesiredHeight;

        // Slightly yellow composite labels in the role tree.
        private static readonly Color CompositeLabelColor = new Color(1f, 0.93f, 0.72f);
        // Owner: Roles window. Key: role id. Value: cached stable reorder
        // callback whose closure captures only that primitive id. Dependencies:
        // RoleCommands behavior; no render or model state is captured. Refresh:
        // lazy on the first reorder registration for a role. Equality: equal role
        // ids reuse delegate identity. Teardown: Reset clears all memoized
        // callbacks before the window and its role catalog are released.
        private readonly MemoizedFactory<int, System.Action<int, int>>
            entryReorderCallbacks;
        // Same contract as entryReorderCallbacks, for composite member rows.
        private readonly MemoizedFactory<int, System.Action<int, int>>
            memberReorderCallbacks;
        private Vector2 listScroll;
        private Vector2 entriesScroll;
        private Vector2 treeScroll;
        private int selectedRoleId = -1;
        private int entriesReorderableGroupId = -1;
        private int membersReorderableGroupId = -1;

        private const float ListWidth = 260f;
        private const float RowHeight = 28f;
        private const float IconButton = 20f;
        private const float DefaultDesiredHeight = 684f;

        // Rules section (conditional-role checkbox + active-hours grid + location dropdown).
        // Integer-pixel grid geometry: fixed cell width, even gaps.
        private const int HourCellW = 16;
        private const int HourCellH = 20;
        private const int HourCellGap = 2;
        private const int HourGridW = 24 * (HourCellW + HourCellGap) - HourCellGap;
        private const int HourLabelH = 18;
        // Legend row + hour-number row + cell row.
        private const float RulesSectionH = HourLabelH + 2f + HourLabelH + 2f + HourCellH;
        // Vanilla-schedule look: paint a color over a grey base.
        private static readonly Color HourActiveColor = SwatchPalette.Hex("0E7490"); // Tailwind cyan-700
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

        public RolesTabView()
        {
            entryReorderCallbacks =
                new MemoizedFactory<int, System.Action<int, int>>(roleId =>
                    (from, to) =>
                    {
                        if (to > from) to--;
                        RoleCommands.MoveEntry(roleId, from, to);
                    });
            memberReorderCallbacks =
                new MemoizedFactory<int, System.Action<int, int>>(roleId =>
                    (from, to) =>
                    {
                        if (to > from) to--;
                        RoleCommands.MoveCompositeMember(roleId, from, to);
                    });
        }

        /// Content-driven height for window sizing: the role list on the left and
        /// the editor's collapsed job tree on the right are the tall pieces.
        public float DesiredHeight()
        {
            var store = RoleStore.Current;
            if (store == null) return DefaultDesiredHeight;
            RoleSelectionSnapshot selection = listState.SelectionSnapshot(store);
            int roleCount = selection.Count;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (ReferenceEquals(desiredHeightOwner, store)
                && desiredHeightRoleCount == roleCount
                && desiredHeightDefinitionRevision == definitionRevision)
                return desiredHeight;

            float chrome = 120f; // tabs, margins, editor gaps
            float list = roleCount * RowHeight + 40f + ListFilterRowsH; // rows + buttons + filter rows
            // The job tree lists every work type, hidden ones included.
            int workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Count;
            float editor = 190f + 32f + workTypes * 26f; // top box + tree header + collapsed roots
            desiredHeightOwner = store;
            desiredHeightRoleCount = roleCount;
            desiredHeightDefinitionRevision = definitionRevision;
            desiredHeight = chrome + Mathf.Max(list, editor);
            return desiredHeight;
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
        /// scrub its dead entries. A current published empty result suppresses
        /// the no-op command; otherwise the synced command validates the role.
        public void CommitEdits()
        {
            int roleId = selectedRoleId;
            if (roleId < 0) return;
            if (editorState.TryGetPublishedDeadEntryState(
                    roleId, out bool hasDeadEntries)
                && !hasDeadEntries) return;
            RoleCommands.ScrubDeadEntries(roleId);
        }

        public void Reset()
        {
            listScroll = entriesScroll = treeScroll = Vector2.zero;
            listState.Reset();
            editorState.Reset();
            selectedRoleId = -1;
            paintingHours = false;
            hourPaintValue = false;
            pendingHoursMask = 0;
            paintRoleId = -1;
            entriesReorderableGroupId = -1;
            membersReorderableGroupId = -1;
            entryReorderCallbacks.Clear();
            memberReorderCallbacks.Clear();
            pendingSelectLabel = null;
            scrollToSelected = false;
            scrollJobTreeToSelection = false;
            rulesRevealed.Clear();
            ReleaseChromeSnapshot();
            ReleaseDesiredHeightCache();
            // Opening re-snapshots everything on this tab.
            RolesListState.ReleaseSectionsSnapshot();
        }

        internal void ReleaseWindowData() => Reset();

        /// Shared snapshots that embed translated def or built-in group labels.
        internal static void InvalidateSharedLanguageCaches()
        {
            RolesListState.InvalidateSectionsSnapshot();
        }

        /// Language-only invalidation. Selection, filters, scroll positions and
        /// every disclosure set remain intact.
        internal void InvalidateLanguageCaches()
        {
            listState.InvalidateLanguageCaches();
            editorState.InvalidateLanguageCaches();
            chromeLanguageRevision = -1;
        }

        private void ReleaseChromeSnapshot()
        {
            chromeSnapshot = null;
            chromeJobFilter = "\0";
            chromeLanguageRevision = -1;
            chromeDefinitionRevision = -1;
        }

        private void ReleaseDesiredHeightCache()
        {
            desiredHeightOwner = null;
            desiredHeightRoleCount = -1;
            desiredHeightDefinitionRevision = -1;
            desiredHeight = DefaultDesiredHeight;
        }

        private RolesTabChromeSnapshot ChromeSnapshot()
        {
            string jobFilter = listState.JobFilterDefName;
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (chromeSnapshot != null
                && string.Equals(chromeJobFilter, jobFilter,
                    System.StringComparison.Ordinal)
                && chromeLanguageRevision == languageRevision
                && chromeDefinitionRevision == definitionRevision)
                return chromeSnapshot;

            GameFont oldFont = Text.Font;
            RolesTabChromeSnapshot rebuilt;
            try
            {
                Text.Font = GameFont.Small;
                string anyJob = "WR_FilterAnyJob".Translate().ToString();
                WorkGiverDef giverDef = jobFilter == null
                    ? null
                    : DefDatabase<WorkGiverDef>.GetNamedSilentFail(jobFilter);
                string jobFilterLabel = giverDef == null
                    ? anyJob
                    : WorkJobLabels.GiverDisplayName(giverDef);
                rebuilt = new RolesTabChromeSnapshot(
                    "WR_SelectOrCreateRole".Translate().ToString(),
                    "WR_Search".Translate().ToString(),
                    "WR_DisplayModeLabel".Translate().ToString(),
                    "WR_JobFilterLabel".Translate().ToString(),
                    "WR_TreeNested".Translate().ToString(),
                    "WR_TreeFlat".Translate().ToString(),
                    anyJob,
                    jobFilterLabel,
                    jobFilterLabel.Truncate(200f),
                    "WR_New".Translate().ToString(),
                    "WR_Copy".Translate().ToString(),
                    "WR_Delete".Translate().ToString());
            }
            finally
            {
                Text.Font = oldFont;
            }

            if (chromeSnapshot == null || !chromeSnapshot.ContentEquals(rebuilt))
                chromeSnapshot = rebuilt;
            chromeJobFilter = jobFilter;
            chromeLanguageRevision = languageRevision;
            chromeDefinitionRevision = definitionRevision;
            return chromeSnapshot;
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RolesTabChromeSnapshot chrome = ChromeSnapshot();
            RoleSelectionSnapshot selection = listState.SelectionSnapshot(store);
            RoleDrag.Update();
            if (selectedRoleId == -1 && selection.FirstRoleId >= 0)
                SelectRole(selection.FirstRoleId);

            var listRect = new Rect(rect.x, rect.y, ListWidth, rect.height);
            var editorRect = new Rect(rect.x + ListWidth + 12f, rect.y, rect.width - ListWidth - 12f, rect.height);
            DrawRoleList(listRect, store, chrome, selection);

            Color oldColor = GUI.color;
            try
            {
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                WrText.LineVertical(rect.x + ListWidth + 6f, rect.y, rect.height);
            }
            finally
            {
                GUI.color = oldColor;
            }

            RoleEditorSnapshot editor = editorState.Snapshot(store,
                selectedRoleId, listedPawns, pawnListRevision?.Invoke() ?? 0,
                editorRect.width, rulesRevealed.Contains(selectedRoleId),
                scrollJobTreeToSelection);
            if (editor != null) DrawEditor(editorRect, editor);
            else Widgets.Label(editorRect, chrome.SelectOrCreateRole);

            RoleChipUI.DrawDragGhost();
            DrawGroupDragGhost();
            RoleDrag.ResolveMouseUp();
        }

        // ----- Left: role list + management buttons -----

        /// Two captioned rows: Search + Display Mode (left/right) on top, the
        /// Job Filter below with room for long job names, plus the clear X.
        internal const float ListFilterRowsH = 90f;

        private static void FilterCaption(Rect rect, string text)
        {
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Tiny;
                GUI.color = WrStyle.CaptionText;
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = oldColor;
                Text.Font = oldFont;
            }
        }

        private void DrawListFilterRow(Rect rect, RolesTabChromeSnapshot chrome,
            bool nestedPreference)
        {
            const float LabelH = 16f; // room for Tiny descenders (Job Filter's y)
            const float InputH = 24f;
            const float ToggleW = 64f;
            const float JobBtnW = 220f;

            float y1 = rect.y + LabelH;
            float searchW = rect.width - ToggleW - 8f - 22f;
            FilterCaption(new Rect(rect.x, rect.y, searchW, LabelH),
                chrome.SearchCaption);
            listState.RoleSearch = Widgets.TextField(
                new Rect(rect.x, y1, searchW, InputH), listState.RoleSearch);
            if (!listState.RoleSearch.NullOrEmpty()
                && Widgets.ButtonImage(new Rect(rect.x + searchW + 4f, y1 + (InputH - 18f) / 2f, 18f, 18f),
                    TexButton.CloseXSmall))
            {
                listState.RoleSearch = "";
                GUIUtility.keyboardControl = 0; // release the field's edit buffer
            }

            // Nested/flat toggle: auto-nesting of covered roles on or off.
            var toggleRect = new Rect(rect.xMax - ToggleW, y1, ToggleW, InputH);
            FilterCaption(new Rect(toggleRect.x, rect.y, ToggleW, LabelH),
                chrome.DisplayModeCaption);
            WrTips.Key("WR_TreeToggleTip").Region(toggleRect);
            if (Widgets.ButtonText(toggleRect,
                    nestedPreference ? chrome.TreeNested : chrome.TreeFlat))
                ToggleNestedPreference(nestedPreference);

            float y2Label = y1 + InputH + 6f;
            float y2 = y2Label + LabelH;
            FilterCaption(new Rect(rect.x, y2Label, JobBtnW, LabelH),
                chrome.JobFilterCaption);
            var jobRect = new Rect(rect.x, y2, JobBtnW, InputH);
            string jobLabel = chrome.JobFilterLabel;
            string jobShown = chrome.JobFilterShown;
            if (jobShown != jobLabel)
                TooltipHandler.TipRegion(jobRect, jobLabel);
            if (Widgets.ButtonText(jobRect, jobShown))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(chrome.AnyJob,
                        () => listState.JobFilterDefName = null),
                };
                foreach (var def in DefDatabase<WorkGiverDef>.AllDefsListForReading
                    .Where(d => d.workType != null)
                    .OrderBy(WorkJobLabels.GiverDisplayName,
                        System.StringComparer.OrdinalIgnoreCase))
                {
                    var captured = def.defName;
                    options.Add(new FloatMenuOption(WorkJobLabels.GiverDisplayName(def),
                        () => listState.JobFilterDefName = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (listState.JobFilterDefName != null)
            {
                var clearRect = new Rect(jobRect.xMax + 6f, y2 + (InputH - 18f) / 2f, 18f, 18f);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    listState.JobFilterDefName = null;
            }
        }

        private static void ToggleNestedPreference(bool nestedPreference)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null)
                return;
            settings.nestedRoleTree = !nestedPreference;
            WorkRolesGameComponent.RequestSettingsWrite();
        }

        /// Create/Copy run through synced commands whose execution MP defers,
        /// so selection can't use a return value: the entered name is watched
        /// for instead, and the newest role carrying it gets selected, its
        /// section expanded and its row scrolled into view.
        private string pendingSelectLabel;
        private bool scrollToSelected;

        private void DrawRoleList(
            Rect rect,
            RoleStore store,
            RolesTabChromeSnapshot chrome,
            RoleSelectionSnapshot selection)
        {
            float buttonsHeight = 34f;
            if (pendingSelectLabel != null)
            {
                int pendingRoleId = selection.NewestRoleIdWithLabel(
                    pendingSelectLabel);
                if (pendingRoleId >= 0)
                {
                    SelectRole(pendingRoleId);
                    pendingSelectLabel = null;
                    scrollToSelected = true;
                }
            }
            RoleListSnapshot snapshot = listState.Snapshot(
                store, selectedRoleId, revealSelected: scrollToSelected, roleTip);
            DrawListFilterRow(new Rect(rect.x, rect.y, rect.width,
                ListFilterRowsH - 6f), chrome, snapshot.NestedPreference);
            var scrollRect = new Rect(rect.x, rect.y + ListFilterRowsH, rect.width,
                rect.height - buttonsHeight - 6f - ListFilterRowsH);

            bool filtered = snapshot.Filtered;
            float contentHeight = snapshot.Count * RowHeight;

            if (scrollToSelected)
            {
                scrollToSelected = false;
                int row = -1;
                for (int i = 0; i < snapshot.Count; i++)
                    if (snapshot.RowAt(i).RoleId == selectedRoleId)
                    {
                        row = i;
                        break;
                    }
                if (row >= 0)
                {
                    float y = row * RowHeight;
                    if (y < listScroll.y) listScroll.y = y;
                    else if (y + RowHeight > listScroll.y + scrollRect.height)
                        listScroll.y = y + RowHeight - scrollRect.height;
                }
            }

            int draggedRoleId = !filtered && RoleDrag.Active && RoleDrag.GroupId < 0
                ? RoleDrag.RoleId : -1;
            bool groupDrag = !filtered && RoleDrag.Active && RoleDrag.GroupId >= 0;

            Widgets.BeginScrollView(scrollRect, ref listScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            try
            {
            // Fixed row height: only rows inside the viewport draw.
            int firstRow = Mathf.Max(0, (int)(listScroll.y / RowHeight));
            int lastRow = Mathf.Min(snapshot.Count - 1,
                (int)((listScroll.y + scrollRect.height) / RowHeight));
            for (int i = firstRow; i <= lastRow; i++)
            {
                RoleListRowSnapshot publishedRow = snapshot.RowAt(i);
                RoleListSectionSnapshot section = publishedRow.Section;
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (publishedRow.RoleId < 0)
                {
                    DrawGroupHeader(row, section, draggedRoleId, groupDrag, snapshot);
                    continue;
                }
                float indent = publishedRow.Depth * 18f;

                if (publishedRow.RoleId == selectedRoleId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row) && !RoleDrag.Active) Widgets.DrawHighlight(row);

                if (Mouse.IsOver(row) && publishedRow.Tooltip != null)
                    StructuredTipPresenter.TipRegion(row, publishedRow.Tooltip);

                var swatch = new Rect(Mathf.Round(row.x) + 6f + indent, Mathf.Round(row.y) + 6f, 16f, 16f);
                Widgets.DrawBoxSolid(swatch, publishedRow.HasCustomColor
                    ? publishedRow.Color : RoleChipUI.DefaultChipColor);
                GUI.color = WrStyle.PanelBackground;
                Widgets.DrawBox(swatch.ExpandedBy(1f));
                GUI.color = Color.white;
                if (publishedRow.VirtualRow && Mouse.IsOver(row))
                    WrTips.Key("WR_VirtualRoleTip",
                        publishedRow.VirtualOriginGroupLabel).Region(row);

                var labelRect = new Rect(swatch.xMax + 6f, row.y, row.width - swatch.width - 8f - indent, RowHeight);
                // Invalid roles (no jobs, or every named location gone) render
                // subdued grey — they can never act until fixed. Composites
                // read slightly yellow so bundles stand out from plain roles.
                Text.Anchor = TextAnchor.MiddleLeft;
                if (!publishedRow.Enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                else if (publishedRow.Invalid) GUI.color = new Color(0.55f, 0.55f, 0.55f);
                else if (publishedRow.Composite) GUI.color = CompositeLabelColor;
                // Italics = virtual row: the role belongs to another group and
                // appears here only because this parent covers it.
                if (publishedRow.VirtualRow) Text.CurFontStyle.fontStyle = FontStyle.Italic;
                Widgets.Label(labelRect, publishedRow.Label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (publishedRow.Invalid && Mouse.IsOver(row))
                    WrTips.Warning("WR_InvalidRoleTip").Region(row);

                // Marker strip after the label: the same icons the chips carry
                // (pin excluded — it marks assignments, not role definitions).
                // Measured before the italic reset so markers clear the label.
                float markerX = labelRect.x + WrText.FitWidth(publishedRow.Label) + 4f;
                if (publishedRow.VirtualRow) Text.CurFontStyle.fontStyle = FontStyle.Normal;
                void ListMarker(Texture2D tex, bool tinted)
                {
                    var markerRect = new Rect(markerX, row.y + (RowHeight - 16f) / 2f, 16f, 16f);
                    if (markerRect.xMax > labelRect.xMax) return;
                    var markerColor = tinted ? RoleChipUI.RuleMarkerColor : Color.white;
                    if (!publishedRow.Enabled) markerColor.a *= 0.5f;
                    GUI.color = markerColor;
                    GUI.DrawTexture(markerRect, tex);
                    GUI.color = Color.white;
                    markerX += 18f;
                }
                if (publishedRow.Blocker) ListMarker(WorkRolesTex.BlockerMarker, tinted: false);
                if (publishedRow.HasTimeRule) ListMarker(WorkRolesTex.TimeMarker, tinted: true);
                if (publishedRow.HasLocationRule) ListMarker(WorkRolesTex.LocationMarker, tinted: true);

                // Press registers a potential drag + click callback; a release inside
                // the 6px threshold selects (resolved centrally in ResolveMouseUp).
                // Virtual rows never drag — they select on press.
                int dragControlId = publishedRow.VirtualRow
                    ? 0
                    : GUIUtility.GetControlID(FocusType.Passive, row);
                if (!publishedRow.VirtualRow)
                    RoleDrag.ObserveSource(dragControlId, row);
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    int capturedId = publishedRow.RoleId;
                    if (publishedRow.VirtualRow) SelectRole(capturedId);
                    else RoleDrag.OnPress(dragControlId, publishedRow.Chip, null,
                        () => SelectRole(capturedId));
                    e.Use();
                }

                if (draggedRoleId >= 0 && Mouse.IsOver(row))
                    RegisterRoleDrop(snapshot, i, row, draggedRoleId);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            float bw = (rect.width - 8f) / 3f;
            float by = rect.yMax - buttonsHeight + 4f;
            bool selectedRoleExists = selection.TryGetRole(
                selectedRoleId, out string selectedRoleLabel);
            if (Widgets.ButtonText(new Rect(rect.x, by, bw, 30f),
                chrome.NewLabel))
            {
                Find.WindowStack.Add(new Dialog_RenameRole("WR_NewRoleTitle".Translate(), null, enteredName =>
                {
                    RoleCommands.CreateRole(enteredName);
                    pendingSelectLabel = enteredName;
                }));
            }

            if (Widgets.ButtonText(new Rect(rect.x + bw + 4f, by, bw, 30f),
                chrome.CopyLabel))
            {
                if (selectedRoleExists)
                {
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        "WR_CopyRoleTitle".Translate(), selectedRoleLabel,
                        enteredName =>
                    {
                        RoleCommands.DuplicateRole(selectedRoleId, enteredName);
                        pendingSelectLabel = enteredName;
                    }));
                }
            }

            var deleteRect = new Rect(rect.x + (bw + 4f) * 2f, by, bw, 30f);
            if (Widgets.ButtonText(deleteRect, chrome.DeleteLabel,
                    active: selectedRoleExists)
                && selectedRoleExists)
            {
                int roleId = selectedRoleId;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "WR_DeleteConfirm".Translate(selectedRoleLabel),
                    () => RoleCommands.DeleteRole(roleId), destructive: true));
            }
        }

        // ----- Role-list drag & drop (organize only: membership + order) -----

        private static readonly Color BlockedTint = new Color(0.8f, 0.2f, 0.2f, 0.12f);

        /// Group header row: collapse arrow, title, displayed-member count and a
        /// rename pencil (user groups). Press = collapse toggle (click) or group
        /// reorder (drag, user groups); also a role-drop target (top of group).
        private void DrawGroupHeader(Rect row, RoleListSectionSnapshot section,
            int draggedRoleId, bool groupDrag, RoleListSnapshot snapshot)
        {
            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
            var arrowRect = new Rect(row.x + 4f, row.y + (row.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect,
                section.Collapsed ? TexButton.Reveal : TexButton.Collapse);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(arrowRect.xMax + 6f, row.y, row.width - 60f, row.height),
                section.DisplayTitle);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var pencilRect = new Rect(row.xMax - 26f, row.y + (row.height - 18f) / 2f, 18f, 18f);
            if (section.Renamable)
            {
                WrTips.Key("WR_RenameGroup").Region(pencilRect);
                if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        section.GroupId, section.CommandName));
            }

            var e = Event.current;
            int dragControlId = section.Draggable
                ? GUIUtility.GetControlID(FocusType.Passive, row)
                : 0;
            if (section.Draggable)
                RoleDrag.ObserveSource(dragControlId, row);
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition)
                && !(section.Renamable && pencilRect.Contains(e.mousePosition)))
            {
                string key = section.Key;
                if (section.Draggable)
                    RoleDrag.OnPressGroup(dragControlId, section.GroupId,
                        section.CommandName, section.GroupDragWidth,
                        () => RolesListState.ToggleSectionCollapsed(key));
                else
                    RolesListState.ToggleSectionCollapsed(key);
                e.Use();
            }

            // Role drop on the header: into this group, at the top. A nested
            // child dropped on its OWN group's header is a no-op — blocked.
            if (draggedRoleId >= 0 && Mouse.IsOver(row))
            {
                bool nestedHere = section.ContainsNestedRole(draggedRoleId);
                if (!section.DropTarget || nestedHere)
                {
                    RoleDrag.HoverBlocked = true;
                    Widgets.DrawBoxSolid(row, BlockedTint);
                }
                else
                {
                    DrawInsertMarker(row, row.yMax);
                    int roleId = draggedRoleId;
                    int beforeId = section.FirstRootRoleId;
                    string groupName = section.CommandName;
                    RoleDrag.HoverDropAction = () =>
                        RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
                }
            }

            // Group reorder drop: above/below this header (user groups only;
            // Default, Conditional Roles and Locked are pinned).
            if (groupDrag && Mouse.IsOver(row) && section.GroupId >= 0
                && section.GroupId != RoleDrag.GroupId)
            {
                int from = snapshot.GroupIndexOf(RoleDrag.GroupId);
                if (from < 0) return;
                bool below = e.mousePosition.y - row.y >= row.height / 2f;
                int target = section.GroupIndex + (below ? 1 : 0);
                int to = target > from ? target - 1 : target;
                if (to == from) return;
                DrawInsertMarker(row, below ? row.yMax : row.y);
                RoleDrag.HoverDropAction = () => RoleCommands.MoveGroupInList(from, to);
            }
        }

        /// Organize-only drop while dragging a role: an insertion line at root
        /// granularity — above a root = before its block, below (or anywhere on
        /// its descendants) = after its block. Landing in another group moves
        /// the role (and its tree-children) there; overlay sections block.
        private static void RegisterRoleDrop(
            RoleListSnapshot snapshot,
            int i, Rect row, int draggedRoleId)
        {
            RoleListRowSnapshot publishedRow = snapshot.RowAt(i);
            RoleListSectionSnapshot section = publishedRow.Section;
            // A nested child's within-own-group drop is a no-op — its display
            // position comes from the tree, not the catalog order. Its virtual
            // rows elsewhere don't block: dropping there moves it to that group.
            bool nestedHere = section.ContainsNestedRole(draggedRoleId);
            if (!section.DropTarget || publishedRow.RoleId == draggedRoleId || nestedHere
                || HasAncestor(snapshot, i, draggedRoleId))
            {
                RoleDrag.HoverBlocked = true;
                Widgets.DrawBoxSolid(row, BlockedTint);
                return;
            }
            float my = Event.current.mousePosition.y - row.y;
            int roleId = draggedRoleId;
            string groupName = section.CommandName;
            if (publishedRow.Depth == 0 && my < row.height / 2f)
            {
                DrawInsertMarker(row, row.y);
                int beforeId = publishedRow.RoleId;
                RoleDrag.HoverDropAction = () =>
                    RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
            }
            else
            {
                // Block end: every following row of the root's subtree.
                int end = i;
                while (end + 1 < snapshot.Count
                    && snapshot.RowAt(end + 1).RoleId >= 0
                    && snapshot.RowAt(end + 1).Depth > 0) end++;
                DrawInsertMarker(row, (end + 1) * RowHeight);
                int beforeId = end + 1 < snapshot.Count
                    ? snapshot.RowAt(end + 1).RoleId : -1;
                RoleDrag.HoverDropAction = () =>
                    RoleCommands.MoveRoleTo(roleId, groupName, beforeId, withChildren: true);
            }
        }

        /// Whether the row's display ancestor chain contains the candidate:
        /// walking up, each ancestor is the nearest earlier row one level up.
        private static bool HasAncestor(
            RoleListSnapshot snapshot,
            int i, int candidateRoleId)
        {
            int need = snapshot.RowAt(i).Depth - 1;
            for (int at = i - 1; at >= 0 && need >= 0; at--)
            {
                RoleListRowSnapshot row = snapshot.RowAt(at);
                if (row.RoleId < 0) break;
                if (row.Depth != need) continue;
                if (row.RoleId == candidateRoleId) return true;
                need--;
            }
            return false;
        }

        /// 2px horizontal insertion marker across the row width at the given boundary.
        private static void DrawInsertMarker(Rect row, float y)
            => Widgets.DrawBoxSolid(
                LudeonTK.UIScaling.AdjustRectToUIScaling(new Rect(row.x, y - 1f, row.width, 2f)),
                new Color(1f, 1f, 1f, 0.9f));

        /// Group reorder ghost; role drags use RoleChipUI.DrawDragGhost.
        private static void DrawGroupDragGhost()
        {
            if (!RoleDrag.Active || RoleDrag.GroupId < 0) return;
            string label = RoleDrag.GroupGhostLabel;
            if (label == null) return;
            var mouse = Event.current.mousePosition;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(mouse.x + 12f, mouse.y + 2f,
                    RoleDrag.GroupGhostWidth, 24f), label);
            }
            finally
            {
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
                Text.Font = oldFont;
            }
        }

        // ----- Right: editor for the selected role -----

        private void DrawEditor(Rect rect, RoleEditorSnapshot model)
        {
            RoleEditorHeaderSnapshot header = model.Header;
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
            const float SkillsRowH = 22f;
            const float AgeBlockH = 44f;
            const float CheckRowH = 24f;
            const float RulesRowGap = 6f;
            int customRows = header.CustomRows;
            float swatchGridH = (SwatchSize + SwatchGap) * (SwatchRows + customRows) - SwatchGap;
            float leftContentH = Mathf.Max(
                TitleH + AssignedRowH + 2f + GroupRowH - 4f + 8f + AgeBlockH
                    + 2f + SkillsRowH,
                CheckRowH * 4f);
            bool rulesShown = header.RulesShown;
            float TopBoxHeight = Mathf.Max(swatchGridH, leftContentH)
                + (rulesShown ? RulesRowGap + RulesSectionH : 0f)
                + TopBoxPadding * 2f;

            var topBox = new Rect(rect.x, rect.y, rect.width, TopBoxHeight);
            Widgets.DrawBoxSolidWithOutline(
                topBox, WrStyle.PanelBackground, WrStyle.PanelOutline);

            float swatchGridW = SwatchCols * (SwatchSize + SwatchGap) - SwatchGap;

            // RIGHT half: swatch grid, right-aligned inside box
            float swatchStartX = topBox.xMax - TopBoxPadding - swatchGridW;
            float swatchStartY = topBox.y + TopBoxPadding;
            // Only the first color match carries the selection outline, so
            // legacy duplicate palette entries cannot all light up.
            bool selectionMarked = false;
            for (int i = 0; i < SwatchPalette.Swatches.Length; i++)
            {
                int col = i % SwatchCols;
                int row = i / SwatchCols;
                var swatchRect = new Rect(
                    swatchStartX + col * (SwatchSize + SwatchGap),
                    swatchStartY + row * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                Widgets.DrawBoxSolid(swatchRect, SwatchPalette.Swatches[i]);
                if (!selectionMarked && header.HasCustomColor
                    && header.RoleColor.IndistinguishableFrom(SwatchPalette.Swatches[i]))
                {
                    Widgets.DrawBox(swatchRect.ExpandedBy(2f));
                    selectionMarked = true;
                }
                TooltipHandler.TipRegion(swatchRect, SwatchPalette.Names[i]);
                if (Widgets.ButtonInvisible(swatchRect))
                    RoleCommands.SetRoleColor(model.RoleId, SwatchPalette.Swatches[i]);
            }

            // Custom rows: player-defined slots. Empty slot = pick a color (applies
            // it too); filled = click to apply, right-click to redefine.
            float customY = swatchStartY + SwatchRows * (SwatchSize + SwatchGap);
            for (int c = 0; c < SwatchCols * customRows; c++)
            {
                var slotRect = new Rect(
                    swatchStartX + c % SwatchCols * (SwatchSize + SwatchGap),
                    customY + c / SwatchCols * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                var slotColor = c < header.CustomSwatchCount
                    ? header.CustomSwatchAt(c) : UnityEngine.Color.clear;
                bool empty = slotColor.a < 0.5f;
                int capturedSlot = c;
                int capturedRoleId = model.RoleId;
                Color initialColor = header.HasCustomColor
                    ? header.RoleColor : RoleChipUI.DefaultChipColor;

                if (empty)
                {
                    Widgets.DrawBoxSolid(slotRect, new Color(0.14f, 0.14f, 0.14f));
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    Widgets.DrawBox(slotRect);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(slotRect, "+");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    WrTips.Key("WR_CustomSwatchEmpty").Region(slotRect);
                    if (Widgets.ButtonInvisible(slotRect))
                        SwatchPicking.Open(capturedRoleId, initialColor,
                            capturedSlot, applyToEditedRole: true);
                }
                else
                {
                    Widgets.DrawBoxSolid(slotRect, slotColor);
                    if (!selectionMarked && header.HasCustomColor
                        && header.RoleColor.IndistinguishableFrom(slotColor))
                    {
                        Widgets.DrawBox(slotRect.ExpandedBy(2f));
                        selectionMarked = true;
                    }
                    WrTips.Key("WR_CustomSwatchTip").Region(slotRect);
                    // ButtonInvisible (GUI.Button) eats MouseDown for any
                    // button, so the right-click must be read first.
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 1
                        && slotRect.Contains(e.mousePosition))
                    {
                        e.Use();
                        // Redefining starts from the slot's color, so an
                        // immediate accept is a genuine no-op.
                        SwatchPicking.Open(capturedRoleId, slotColor,
                            capturedSlot, applyToEditedRole: false);
                    }
                    else if (Widgets.ButtonInvisible(slotRect))
                        RoleCommands.SetRoleColor(model.RoleId, slotColor);
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
            // Auto-assign, Blocker, Conditional role stacked (3 rows).
            // Measured first so the title and pencil know their room.
            Text.Font = GameFont.Small;
            float checksW = header.ChecksWidth;
            float checksX = leftContainerW + topBox.x - checksW;
            DrawEditorChecks(new Rect(checksX, rowsStartY, checksW, CheckRowH * 4f),
                model, rulesShown, CheckRowH);

            // Title with the rename pencil directly AFTER the name (the right
            // column now belongs to the four toggles).
            const float PencilSize = 26f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            float titleMaxW = checksX - 8f - leftX - PencilSize - 6f;
            float titleW = header.RoleLabelWidth;
            Widgets.Label(new Rect(leftX, rowsStartY, titleW, TitleH),
                header.ShownRoleLabel);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float pencilY = rowsStartY + (TitleH - PencilSize) / 2f;
            if (Widgets.ButtonImage(new Rect(leftX + titleW + 6f, pencilY, PencilSize, PencilSize), TexButton.Rename))
                Find.WindowStack.Add(Dialog_RenameRole.ForRole(
                    model.RoleId, model.RoleLabel));

            float row2Y = rowsStartY + TitleH;

            // Row 2: small grey "Assigned to" label with the colonist names
            // inline after it (ordered by position in their assignment list).
            GUI.color = WrStyle.DimText;
            float assignedLabelW = header.AssignedLabelWidth;
            Widgets.Label(new Rect(leftX, row2Y, assignedLabelW, AssignedRowH),
                header.AssignedLabel);
            GUI.color = Color.white;
            float namesX = leftX + assignedLabelW + 6f;
            DrawAssignedPawnNames(new Rect(namesX, row2Y,
                checksX - 8f - namesX, AssignedRowH), model);

            // Row 3: group picker button ("Group: <name>") + "New...".
            float groupY = row2Y + AssignedRowH + 2f;
            DrawGroupPickerRow(new Rect(leftX, groupY,
                checksX - 8f - leftX, GroupRowH - 4f), model);

            // Row 4: the role's age gates as one row of toggleable bands,
            // 8px below the group picker.
            float ageY = groupY + GroupRowH - 4f + 8f;
            DrawAgeBandsRow(new Rect(leftX, ageY,
                checksX - 8f - leftX, AgeBlockH), model);

            DrawSkillsUsedRow(new Rect(leftX, ageY + AgeBlockH + 2f,
                checksX - 8f - leftX, SkillsRowH), model);

            // Expanding section (full box width): rules while the conditional-role
            // opt-in is on.
            float sectionY = topBox.y + TopBoxPadding + Mathf.Max(swatchGridH, leftContentH)
                + RulesRowGap;
            if (rulesShown)
                DrawRulesSection(new Rect(leftX, sectionY,
                    topBox.width - TopBoxPadding * 2f, RulesSectionH), model);

            // BOTTOM: split vertically — left = job tree, right = entries table
            float bottomY = topBox.yMax + 6f;
            float bottomH = rect.yMax - bottomY;
            float halfW = (rect.width - 6f) / 2f;

            var treeRect    = new Rect(rect.x, bottomY, halfW, bottomH);
            var entriesRect = new Rect(rect.x + halfW + 6f, bottomY, halfW, bottomH);

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            WrText.LineVertical(rect.x + halfW + 3f, bottomY, bottomH);
            GUI.color = Color.white;

            if (model.Composite != null)
            {
                DrawCompositeCandidates(treeRect, model);
                DrawCompositeMembers(entriesRect, model);
            }
            else
            {
                DrawJobTree(treeRect, model);
                DrawEntries(entriesRect, model);
            }
        }

        /// The role's group as a "Group: <name>" button: a dropdown of the
        /// existing groups plus "New..." (a name dialog; the role moves in, so
        /// no empty group ever exists). A parent moves WITH its nested roles —
        /// a combo role separated from its children would un-nest both. Overlay
        /// members (Conditional Roles) show a disabled "Group: Conditional Roles"
        /// instead — the stored group resumes when rules clear.
        private void DrawGroupPickerRow(Rect rect, RoleEditorSnapshot model)
        {
            RoleEditorHeaderSnapshot header = model.Header;
            Text.Font = GameFont.Small;
            var pickRect = new Rect(rect.x, rect.y, Mathf.Min(rect.width, 180f), rect.height);
            string full = header.GroupButtonFull;
            string shown = header.GroupButtonShown;
            if (shown != full)
                TooltipHandler.TipRegion(pickRect, full);

            if (header.HasRules)
            {
                Widgets.ButtonText(pickRect, shown,
                    drawBackground: true, doMouseoverSound: false, active: false);
                WrTips.Key("WR_GroupOverlayTip").Region(pickRect);
                return;
            }

            if (Widgets.ButtonText(pickRect, shown))
            {
                int roleId = model.RoleId;
                var options = new List<FloatMenuOption>
                {
                    // "" = Default: the synced arg must stay language-independent.
                    new FloatMenuOption(header.DefaultGroupLabel, () =>
                        RoleCommands.SetRoleGroup(roleId, "", withChildren: true)),
                };
                for (int i = 0; i < header.GroupCount; i++)
                {
                    RoleGroupOptionSnapshot group = header.GroupAt(i);
                    string name = group.CommandName;
                    options.Add(new FloatMenuOption(group.Label, () =>
                        RoleCommands.SetRoleGroup(roleId, name, withChildren: true)));
                }
                options.Add(new FloatMenuOption(header.NewGroupLabel, () =>
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        header.NewGroupTitle,
                        name => RoleCommands.SetRoleGroup(roleId, name, withChildren: true)))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        // Age band segments: numeric ranges, language-independent.
        private static readonly string[] AgeBandLabels =
            { "3-6", "7-9", "10-12", "13-17", "18+" };
        private static readonly Color AgeCellPanel = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color AgeSelectedOutline = new Color(
            WrStyle.MinorAccent.r, WrStyle.MinorAccent.g, WrStyle.MinorAccent.b, 0.5f);
        // Selected band with no performable work in the role's coverage.
        private static readonly Color AgeLacksJobsOutline =
            new Color(0.85f, 0.3f, 0.25f, 0.7f);

        /// The role's age gates as one row of toggleable bands: dim caption on
        /// top, one segment per AgeBands entry. Clicking outside the selection
        /// extends it, an end band trims off, an interior band collapses to
        /// itself (AgeBands.Click), so the selection stays contiguous and
        /// never empties.
        private static void DrawAgeBandsRow(Rect rect, RoleEditorSnapshot model)
        {
            RoleEditorHeaderSnapshot header = model.Header;
            WrTips.Key("WR_RoleAgeBandsTip", header.AgeTipArg).Region(rect);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.DimText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 19f),
                header.AgeBandsCaption);
            GUI.color = Color.white;

            const float SegmentH = 24f;
            const float SegmentGap = 2f;
            (int lo, int hi) = header.AgeSelection;
            float segmentW = (rect.width - (AgeBands.Count - 1) * SegmentGap)
                / AgeBands.Count;
            float segmentY = rect.y + 20f;
            Text.Anchor = TextAnchor.MiddleCenter;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            for (int band = 0; band < AgeBands.Count; band++)
            {
                var cell = new Rect(rect.x + band * (segmentW + SegmentGap),
                    segmentY, segmentW, SegmentH);
                bool selected = band >= lo && band <= hi;
                Widgets.DrawBoxSolid(cell, AgeCellPanel);
                if (selected)
                {
                    GUI.color = AgeBands.BandLacksJobs(band, header.AgeMinUnlock)
                        ? AgeLacksJobsOutline
                        : AgeSelectedOutline;
                    Widgets.DrawBox(cell);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = WrStyle.DimText;
                    Widgets.DrawHighlightIfMouseover(cell);
                }
                Widgets.Label(cell, AgeBandLabels[band]);
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(cell))
                {
                    (int newLo, int newHi) = AgeBands.Click(lo, hi, band);
                    if (newLo != lo || newHi != hi)
                    {
                        (int minAge, int maxAge) =
                            AgeBands.StoredFor(newLo, newHi);
                        RoleCommands.SetRoleAgeRange(
                            model.RoleId, minAge, maxAge);
                    }
                }
            }
            Text.WordWrap = wrap;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ----- Rules section: conditional-role opt-in, active-hours grid, location dropdown -----

        /// The editor's checkbox column: Auto-assign, Blocker role, the
        /// Conditional role opt-in and the Composite role toggle.
        /// Conditional role opt-in derives from HasRules — unchecking clears the rules
        /// (confirmed). CheckboxLabeled pins boxes to the right edge for alignment.
        private void DrawEditorChecks(Rect rect, RoleEditorSnapshot model,
            bool rulesShown, float rowH)
        {
            RoleEditorHeaderSnapshot header = model.Header;
            Text.Font = GameFont.Small;
            float y = rect.y;

            var assignRect = new Rect(rect.x, y, rect.width, rowH);
            WrTips.Key("WR_AutoAssignTip").Region(assignRect);
            bool autoAssign = header.AutoAssign;
            Widgets.CheckboxLabeled(assignRect, header.AutoAssignLabel,
                ref autoAssign);
            if (autoAssign != header.AutoAssign)
                RoleCommands.SetRoleAutoAssign(model.RoleId, autoAssign);
            y += rowH;

            // Blocker: the role's jobs become vetoes. Locked for composite
            // members, where flipping the veto would silently change every
            // bundle carrying the role.
            var blockRect = new Rect(rect.x, y, rect.width, rowH);
            bool blocker = header.Blocker;
            if (header.MemberLocked)
            {
                WrTips.Key("WR_MemberLockTip").Region(blockRect);
                Widgets.CheckboxLabeled(blockRect, header.BlockerLabel,
                    ref blocker, disabled: true);
            }
            else
            {
                StructuredTipPresenter.TipRegion(blockRect, header.BlockerTip);
                Widgets.CheckboxLabeled(blockRect, header.BlockerLabel, ref blocker);
                if (blocker != header.Blocker)
                    RoleCommands.SetRoleBlocker(model.RoleId, blocker);
            }
            y += rowH;

            var autoRect = new Rect(rect.x, y, rect.width, rowH);
            bool rulesWanted = rulesShown;
            if (header.MemberLocked)
            {
                WrTips.Key("WR_MemberLockTip").Region(autoRect);
                Widgets.CheckboxLabeled(autoRect, header.ConditionalRoleLabel,
                    ref rulesWanted, disabled: true);
            }
            else
            {
                WrTips.Key("WR_ConditionalRoleTip").Region(autoRect);
                Widgets.CheckboxLabeled(autoRect, header.ConditionalRoleLabel,
                    ref rulesWanted);
                if (rulesWanted != rulesShown)
                {
                    if (rulesWanted)
                    {
                        rulesRevealed.Add(model.RoleId);
                    }
                    else if (header.HasRules)
                    {
                        // The checkbox derives from HasRules, so unchecking means clearing the rules.
                        Find.WindowStack.Add(new Dialog_SmallConfirm(
                            header.ClearRulesConfirmation,
                            () =>
                            {
                                RoleCommands.ClearRoleRules(model.RoleId);
                                rulesRevealed.Remove(model.RoleId);
                            }));
                    }
                    else
                    {
                        rulesRevealed.Remove(model.RoleId);
                    }
                }
            }
            y += rowH;

            // Composite: the role bundles other roles instead of jobs. Flipping
            // it drops the replaced content, so a non-empty side confirms first.
            var compositeRect = new Rect(rect.x, y, rect.width, rowH);
            bool compositeWanted = header.Composite;
            if (header.MemberLocked)
            {
                WrTips.Key("WR_MemberLockTip").Region(compositeRect);
                Widgets.CheckboxLabeled(compositeRect, header.CompositeLabel,
                    ref compositeWanted, disabled: true);
                return;
            }
            WrTips.Key("WR_CompositeRoleTip").Region(compositeRect);
            Widgets.CheckboxLabeled(compositeRect, header.CompositeLabel,
                ref compositeWanted);
            if (compositeWanted != header.Composite)
            {
                int roleId = model.RoleId;
                bool losesContent = header.Composite
                    ? model.Composite != null && model.Composite.MemberCount > 0
                    : model.Entries != null && model.Entries.Count > 0;
                if (losesContent)
                    Find.WindowStack.Add(new Dialog_SmallConfirm(
                        header.Composite
                            ? header.CompositeRevertConfirmation
                            : header.CompositeConfirmation,
                        () => RoleCommands.SetRoleComposite(roleId, compositeWanted)));
                else
                    RoleCommands.SetRoleComposite(roleId, compositeWanted);
            }
        }

        /// "Skills used:" with the primary (most frequent) skill white and the
        /// rest slightly dimmed; labels that don't fit are dropped silently.
        private void DrawSkillsUsedRow(Rect rect, RoleEditorSnapshot model)
        {
            RoleEditorHeaderSnapshot header = model.Header;
            if (header.SkillCount == 0) return;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.DimText;
            string caption = header.SkillsCaption;
            float captionW = WrText.FitWidth(caption);
            Widgets.Label(new Rect(rect.x, rect.y, captionW, rect.height), caption);
            float x = rect.x + captionW + 6f;
            const string Sep = ", ";
            float sepW = WrText.FitWidth(Sep);
            for (int i = 0; i < header.SkillCount; i++)
            {
                RoleSkillPresentation skill = header.SkillAt(i);
                string label = skill.Label;
                bool primary = skill.Primary;
                float w = WrText.FitWidth(label);
                if (x + w > rect.xMax) break;
                GUI.color = primary ? Color.white : new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(x, rect.y, w, rect.height), label);
                x += w;
                if (i < header.SkillCount - 1 && x + sepW <= rect.xMax)
                {
                    GUI.color = new Color(0.55f, 0.55f, 0.55f);
                    Widgets.Label(new Rect(x, rect.y, sepW, rect.height), Sep);
                    x += sepW;
                }
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRulesSection(Rect rect, RoleEditorSnapshot model)
        {
            RoleRulesSnapshot rules = model.Rules;
            // Selecting another role mid-paint abandons the pending edit.
            if (paintingHours && paintRoleId != model.RoleId)
                paintingHours = false;

            int shownMask = paintingHours
                ? pendingHoursMask : rules.ActiveHours;
            bool mouseHeld = Input.GetMouseButton(0);
            // The clock icon ties the grid to the chips' time marker.
            int x0 = Mathf.RoundToInt(rect.x) + 22;
            int legendY = Mathf.RoundToInt(rect.y);
            int labelsY = legendY + HourLabelH + 2;
            int cellsY = labelsY + HourLabelH + 2;

            // Legend top-left, above the grid it explains.
            const float LegendGap = 12f;
            Text.Font = GameFont.Small;
            float legendX = DrawLegendEntry(rect.x, legendY,
                HourActiveColor, rules.ActiveLabel);
            DrawLegendEntry(legendX + LegendGap, legendY,
                HourInactiveColor, rules.InactiveLabel);

            GUI.color = RoleChipUI.RuleMarkerColor;
            GUI.DrawTexture(new Rect(rect.x, cellsY + (HourCellH - 16f) / 2f, 16f, 16f), WorkRolesTex.TimeMarker);
            GUI.color = Color.white;

            // Hour headers: one per cell, Tiny and bottom-anchored (vanilla schedule style).
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = WrStyle.DimText;
            for (int h = 0; h < 24; h++)
                Widgets.Label(new Rect(x0 + h * (HourCellW + HourCellGap), labelsY, HourCellW, HourLabelH), h.ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var gridRect = new Rect(x0, cellsY, HourGridW, HourCellH);
            if (Mouse.IsOver(gridRect))
                WrTips.Key("WR_ActiveHours").Region(gridRect);

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
                    paintRoleId = model.RoleId;
                    pendingHoursMask = rules.ActiveHours;
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
                if (pendingHoursMask != rules.ActiveHours)
                    RoleCommands.SetRoleActiveHours(
                        model.RoleId, pendingHoursMask);
            }

            // Location multi-select right of the grid: Anywhere, or any set of
            // named settlements / ships / Caravans — active where any matches.
            // The location icon ties the picker to the chips' location marker.
            float btnX = gridRect.xMax + 16f;
            GUI.color = RoleChipUI.RuleMarkerColor;
            GUI.DrawTexture(new Rect(btnX, cellsY + (HourCellH - 16f) / 2f, 16f, 16f), WorkRolesTex.LocationMarker);
            GUI.color = Color.white;
            btnX += 22f;
            // Auto-fit prefixed labels (Ship:/Settlement:) up to the panel edge.
            string summary = rules.LocationSummary;
            float locBtnW = Mathf.Clamp(WrText.FitWidth(summary) + 20f, 110f, Mathf.Max(110f, rect.xMax - btnX));
            if (Widgets.ButtonText(new Rect(btnX, cellsY + (HourCellH - 24f) / 2f, locBtnW, 24f), summary))
            {
                int roleId = model.RoleId;
                var options = new List<FloatMenuOption>();
                for (int i = 0; i < rules.LocationCount; i++)
                {
                    RoleLocationOptionSnapshot location = rules.LocationAt(i);
                    string token = location.Token;
                    var item = new FloatMenuOption(location.Label,
                        token == null
                            ? (System.Action)(() =>
                                RoleCommands.ClearRoleLocations(roleId))
                            : () => RoleCommands.ToggleRoleLocation(
                                roleId, token));
                    item.tooltip = location.Tooltip;
                    options.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

        }

        private const float LegendSwatch = 12f;

        private static float DrawLegendEntry(float x, float y, Color color, string label)
        {
            Text.Font = GameFont.Small;
            float labelW = WrText.FitWidth(label);
            Widgets.DrawBoxSolid(new Rect(x, y + (HourLabelH - LegendSwatch) / 2f, LegendSwatch, LegendSwatch), color);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(
                x + LegendSwatch + 4f, y, labelW, HourLabelH), label);
            Text.Anchor = previousAnchor;
            GUI.color = Color.white;
            return x + LegendSwatch + 4f + labelW;
        }

        private void ApplyHourPaint(int hour)
        {
            if (hourPaintValue) pendingHoursMask |= 1 << hour;
            else pendingHoursMask &= ~(1 << hour);
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
            if (token == LocationRules.Nowhere) return "WR_LocationNowhere".Translate();
            string id = token.Substring(token.IndexOf(':') + 1);
            var loc = ColonyScope.Locations().FirstOrDefault(l => l.Id == id);
            return loc != null ? LocationItemLabel(loc) : "WR_LocationGone".Translate().ToString();
        }

        private static string LocationItemLabel(WorkRoles.Core.LocationInfo loc) =>
            (loc.IsShip ? "WR_LocationShipItem" : "WR_LocationSettlementItem")
                .Translate(loc.Label).ToString();

        // ----- Assigned pawn names row -----

        private void DrawAssignedPawnNames(Rect rect, RoleEditorSnapshot model)
        {
            RoleEditorHeaderSnapshot header = model.Header;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (header.HolderCount == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(rect, header.NobodyLabel);
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

            for (int i = 0; i < header.HolderCount; i++)
            {
                string name = header.HolderAt(i).Label;
                float nameW = WrText.FitWidth(name);
                bool hasNext = i < header.HolderCount - 1;

                // Determine how much space remains after this name (sep + overflow reserve if more names follow).
                float needed = nameW + (hasNext ? sepW + OverflowReserve : 0f);
                if (x + needed > rect.xMax && i > 0)
                {
                    // No room — count remaining (including this one)
                    remaining = header.HolderCount - i;
                    break;
                }
                // Even the very first name doesn't fit: show overflow immediately.
                if (i == 0 && x + nameW + (hasNext ? OverflowReserve : 0f) > rect.xMax && hasNext)
                {
                    remaining = header.HolderCount;
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
                string moreText = header.HolderOverflowLabel(remaining);
                GUI.color = SepColor;
                Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), moreText);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ----- Selected Jobs: two-column table with drag reorder + up/down buttons -----

        private void DrawEntries(Rect rect, RoleEditorSnapshot model)
        {
            RoleEntriesSnapshot entries = model.Entries;
            // Same visible-gap correction as the Available Jobs header.
            WrText.HeaderLabel(new Rect(rect.x + 8f, rect.y + WrText.MediumTopBearing, rect.width - 8f, 28f),
                entries.Title);

            // Column headers — 24f height so descenders aren't clipped
            float headerY = rect.y + 28f + 4f;
            float removeW = (IconButton + 4f) * 3f; // room for up + down + [x]
            float typeW = (rect.width - 8f - removeW - 8f) * 0.45f;
            float jobW  = (rect.width - 8f - removeW - 8f) * 0.55f;

            GUI.color = WrStyle.DimText;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f + 4f, headerY, typeW, 24f),
                entries.TypeColumn);
            Widgets.Label(new Rect(rect.x + 8f + 4f + typeW,
                headerY, jobW, 24f), entries.JobColumn);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var scrollRect = new Rect(rect.x + 8f, headerY + 24f, rect.width - 8f, rect.height - 28f - 4f - 24f);
            float contentHeight = entries.Count * RowHeight;

            if (Event.current.type == EventType.Repaint)
            {
                entriesReorderableGroupId = ReorderableWidget.NewGroup(
                    entryReorderCallbacks.For(model.RoleId),
                    ReorderableDirection.Vertical,
                    scrollRect);
            }

            Widgets.BeginScrollView(scrollRect, ref entriesScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            try
            {

            // Rows outside the viewport still register with ReorderableWidget
            // (drag bookkeeping needs every row rect) but skip all text work.
            float cullTop = entriesScroll.y - RowHeight;
            float cullBottom = entriesScroll.y + scrollRect.height;
            for (int i = 0; i < entries.Count; i++)
            {
                RoleEntryRowSnapshot publishedRow = entries.RowAt(i);
                JobEntry entry = publishedRow.Entry;
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);

                bool dragging = ReorderableWidget.Reorderable(entriesReorderableGroupId, row, useRightButton: false, highlightDragged: true);
                if (row.y > cullBottom || row.y < cullTop) continue;

                if (Mouse.IsOver(row) && !dragging) Widgets.DrawHighlight(row);

                RoleEntryPresentation presentation =
                    publishedRow.Presentation;
                string typeLabel = presentation.TypeLabel;
                string jobLabel = presentation.JobLabel;
                bool missing = presentation.Missing;
                bool dead = publishedRow.Dead;

                Text.Anchor = TextAnchor.MiddleLeft;
                if (missing) GUI.color = new Color(1f, 0.4f, 0.4f, 0.8f);
                else if (dead) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                // Long names truncate to their column (never wrap into the next
                // row); the tooltip carries the full name — same treatment as
                // the job filter button.
                bool wrap = Text.WordWrap;
                Text.WordWrap = false;

                var typeRect = new Rect(row.x + 4f, row.y, typeW, RowHeight);
                string typeShown = presentation.TypeShown;
                Widgets.Label(typeRect, typeShown);
                if (typeShown != typeLabel)
                    TooltipHandler.TipRegion(typeRect, typeLabel);

                string jobText = jobLabel;
                var jobRect = new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight);
                string jobShown = presentation.JobShown;
                Widgets.Label(jobRect, jobShown);
                if (jobShown != jobText)
                    TooltipHandler.TipRegion(jobRect, jobText);

                Text.WordWrap = wrap;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (missing)
                    WrTips.Warning("WR_MissingDef", entry.DefName).Region(row);
                if (!missing && Mouse.IsOver(row))
                {
                    StructuredTip skillTip = publishedRow.SkillTip;
                    if (skillTip != null)
                        StructuredTipPresenter.TipRegion(row, skillTip);
                }

                float btnY = row.y + (RowHeight - IconButton) / 2f;
                float removeX = row.xMax - IconButton - 2f;
                float downX   = removeX - IconButton - 2f;
                float upX     = downX - IconButton - 2f;

                int capturedI = i;
                int capturedRoleId = model.RoleId;

                if (i > 0 && Widgets.ButtonImage(new Rect(upX, btnY, IconButton, IconButton), TexButton.ReorderUp))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI - 1);
                if (i < entries.Count - 1
                    && Widgets.ButtonImage(new Rect(downX, btnY,
                        IconButton, IconButton), TexButton.ReorderDown))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI + 1);
                if (Widgets.ButtonImage(new Rect(removeX, btnY, IconButton, IconButton), TexButton.Delete))
                    RoleCommands.RemoveEntry(capturedRoleId, capturedI);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        // ----- Available Jobs: the work type / giver tree -----

        /// Warning colors for uncovered tree rows and the summary panel.
        internal static readonly Color WarningYellow = new Color(0.95f, 0.85f, 0.3f);
        private static readonly Color WarningPanelBorder = new Color(0.12f, 0.08f, 0.02f);
        private static readonly Color WarningPanelBackground = new Color(0.82f, 0.68f, 0.25f);
        private static readonly Color WarningPanelText = new Color(0.18f, 0.09f, 0.01f);

        private void DrawJobTree(Rect rect, RoleEditorSnapshot model)
        {
            RoleJobTreeSnapshot tree = model.JobTree;
            const float SearchW = 110f;
            const float SearchLabelW = 46f;
            const float SearchH = 24f;
            float headerW = rect.width - SearchLabelW - SearchW - 8f;

            // HeaderLabel puts the VISIBLE text top at rect.y; directly under the
            // top box that reads as flush, so the top bearing is re-added as gap.
            WrText.HeaderLabel(new Rect(rect.x + 4f, rect.y + WrText.MediumTopBearing, headerW - 4f, 28f),
                tree.Title);

            // "Search" label immediately left of field; group shifted 4f left from right edge
            const float SearchRightPad = 4f;
            GUI.color = WrStyle.DimText;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - SearchLabelW - SearchW - 4f
                - SearchRightPad, rect.y + (28f - SearchH) / 2f,
                SearchLabelW, SearchH), tree.SearchLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float fieldY = rect.y + (28f - SearchH) / 2f;
            editorState.Filter = Widgets.TextField(
                new Rect(rect.xMax - SearchW - SearchRightPad, fieldY, SearchW - 22f, SearchH),
                editorState.Filter);
            if (!editorState.Filter.NullOrEmpty()
                && Widgets.ButtonImage(new Rect(rect.xMax - SearchRightPad - 18f, fieldY + (SearchH - 18f) / 2f, 18f, 18f),
                    TexButton.CloseXSmall))
            {
                editorState.Filter = "";
                GUIUtility.keyboardControl = 0; // release the field's edit buffer
            }

            float treeTopY = rect.y + 28f + 4f;
            if (tree.Warning != null)
            {
                // Flush with the tree rows left and top; right and bottom keep
                // their margins.
                const float WarningMargin = 8f;
                const float WarningPadding = 8f;
                var warningPanel = new Rect(
                    rect.x,
                    treeTopY,
                    rect.width - WarningMargin,
                    tree.WarningHeight);
                Widgets.DrawBoxSolidWithOutline(
                    warningPanel, WarningPanelBackground, WarningPanelBorder);
                Color previousColor = GUI.color;
                TextAnchor previousAnchor = Text.Anchor;
                GUI.color = WarningPanelText;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(warningPanel.ContractedBy(WarningPadding),
                    tree.Warning);
                GUI.color = previousColor;
                Text.Anchor = previousAnchor;
                treeTopY = warningPanel.yMax + WarningMargin;
            }
            var scrollRect = new Rect(rect.x, treeTopY, rect.width, rect.yMax - treeTopY);
            if (scrollJobTreeToSelection)
            {
                scrollJobTreeToSelection = false;
                if (tree.TargetIndex >= 0)
                    treeScroll.y = Mathf.Max(0f,
                        tree.TargetIndex * RowHeight
                        - (scrollRect.height - RowHeight) / 2f);
            }

            Widgets.BeginScrollView(scrollRect, ref treeScroll,
                new Rect(0f, 0f, scrollRect.width - 16f,
                    tree.Count * RowHeight));
            try
            {

            // Fixed row height: only rows inside the viewport draw.
            int firstNode = Mathf.Max(0, (int)(treeScroll.y / RowHeight));
            int lastNode = Mathf.Min(tree.Count - 1,
                (int)((treeScroll.y + scrollRect.height) / RowHeight));
            for (int i = firstNode; i <= lastNode; i++)
            {
                RoleJobTreeNode node = tree.NodeAt(i);
                string nodeLabel = node.Label;
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Text.Anchor = TextAnchor.MiddleLeft;

                if (node.GiverDefName == null)
                {
                    // Work-type header row
                    if (Widgets.ButtonImage(new Rect(row.x + 2f, row.y + 4f, IconButton, IconButton),
                        node.Expanded ? TexButton.Collapse : TexButton.Reveal))
                        editorState.ToggleWorkTypeExpanded(node.TypeDefName);

                    var checkboxRect = new Rect(row.x + 26f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = node.State;
                    // Right-click: add every job as its own reorderable entry.
                    var te = Event.current;
                    if (te.type == EventType.MouseDown && te.button == 1 && row.Contains(te.mousePosition))
                    {
                        te.Use();
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                        {
                            new FloatMenuOption(tree.AddAllJobsLabel, () =>
                                AddAllGivers(model, node)),
                        }));
                    }
                    // ~ (some jobs selected) clicks like Off: it adds the type
                    // entry; the jobs' own entries stay above it, still live.
                    bool typeAdds = currentState != MultiCheckboxState.On;
                    if (MultiCheckboxClicked(checkboxRect, currentState, typeAdds))
                        ApplyWorkTypeState(model, node,
                            typeAdds ? MultiCheckboxState.On : MultiCheckboxState.Off);

                    // The label toggles like the arrow — a far bigger target.
                    var typeLabelRect = new Rect(row.x + 54f, row.y, row.width - 54f, RowHeight);
                    if (node.Warning) GUI.color = WarningYellow;
                    Widgets.Label(typeLabelRect, nodeLabel);
                    GUI.color = Color.white;
                    if (Widgets.ButtonInvisible(typeLabelRect))
                        editorState.ToggleWorkTypeExpanded(node.TypeDefName);
                    if (Mouse.IsOver(row))
                    {
                        StructuredTip skillTip = node.SkillTip;
                        if (skillTip != null)
                            StructuredTipPresenter.TipRegion(row, skillTip);
                    }
                }
                else
                {
                    // Job giver child row. ~ = covered via the work type; the
                    // promote-click hint rides the node's composed skill tip.
                    var checkboxRect = new Rect(row.x + 42f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = node.State;
                    GUI.color = Mouse.IsOver(checkboxRect) ? GenUI.MouseoverColor : Color.white;
                    GUI.DrawTexture(checkboxRect, StateTex(currentState));
                    GUI.color = Color.white;
                    // Mouse-down starts a paint drag: the anchor's add/remove
                    // spreads to every giver row of this type the drag spans.
                    var ge = Event.current;
                    if (ge.type == EventType.MouseDown && ge.button == 0
                        && checkboxRect.Contains(ge.mousePosition))
                    {
                        paintAnchorRow = i;
                        paintTypeDefName = node.TypeDefName;
                        paintAdds = currentState != MultiCheckboxState.On;
                        paintApplied.Clear();
                        PaintGiver(model, node);
                        ge.Use();
                    }

                    if (node.Warning) GUI.color = WarningYellow;
                    Widgets.Label(new Rect(row.x + 70f, row.y, row.width - 70f, RowHeight), nodeLabel);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(row))
                    {
                        StructuredTip skillTip = node.SkillTip;
                        if (skillTip != null)
                            StructuredTipPresenter.TipRegion(row, skillTip);
                    }
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
            PaintRange(model, tree);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            if (paintAnchorRow >= 0)
                GenUI.DrawMouseAttachment(paintAdds ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
        }

        // ----- Tri-state helpers -----

        // Paint-select drag over giver checkboxes (state lives for one drag).
        private int paintAnchorRow = -1;
        private string paintTypeDefName;
        private bool paintAdds;
        private readonly HashSet<string> paintApplied = new HashSet<string>();

        /// While the paint drag is held, applies the anchor state to every
        /// giver row of the anchor's work type between anchor and cursor. The
        /// whole range applies every frame, so fast drags skip nothing.
        private void PaintRange(RoleEditorSnapshot model,
            RoleJobTreeSnapshot tree)
        {
            if (paintAnchorRow < 0) return;
            var e = Event.current;
            if (e.rawType == EventType.MouseUp || !Input.GetMouseButton(0)
                || tree.Count == 0)
            {
                paintAnchorRow = -1;
                paintTypeDefName = null;
                paintApplied.Clear();
                return;
            }
            int current = Mathf.Clamp((int)(e.mousePosition.y / RowHeight),
                0, tree.Count - 1);
            int low = Mathf.Min(Mathf.Min(paintAnchorRow, tree.Count - 1), current);
            int high = Mathf.Min(Mathf.Max(paintAnchorRow, current), tree.Count - 1);
            for (int i = low; i <= high; i++)
            {
                RoleJobTreeNode node = tree.NodeAt(i);
                if (node.GiverDefName == null
                    || node.TypeDefName != paintTypeDefName) continue;
                PaintGiver(model, node);
            }
        }

        /// One paint application; idempotent per drag so MP-deferred commands
        /// are never double-issued and the toggle sound fires once per change.
        private void PaintGiver(RoleEditorSnapshot model,
            RoleJobTreeNode node)
        {
            if (!paintApplied.Add(node.GiverDefName)) return;
            MultiCheckboxState state = node.State;
            // Off only ever removes an own entry: partial rows have none.
            bool changes = paintAdds
                ? state != MultiCheckboxState.On
                : state == MultiCheckboxState.On;
            if (!changes) return;
            ApplyGiverState(model, node,
                paintAdds ? MultiCheckboxState.On : MultiCheckboxState.Off);
            (paintAdds ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff)
                .PlayOneShotOnCamera();
        }

        private static Texture2D StateTex(MultiCheckboxState state)
            => state == MultiCheckboxState.On ? Widgets.CheckboxOnTex
                : state == MultiCheckboxState.Off ? Widgets.CheckboxOffTex
                : Widgets.CheckboxPartialTex;

        /// CheckboxMulti look-alike whose click sound matches OUR action — the
        /// vanilla widget keys the sound to the state it proposes, which is
        /// wrong for the promote-from-~ click.
        private static bool MultiCheckboxClicked(Rect rect, MultiCheckboxState state, bool adds)
        {
            if (!Widgets.ButtonImage(rect, StateTex(state))) return false;
            (adds ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            return true;
        }

        /// Adds/removes only the WorkType entry itself — giver entries (and the
        /// player's ordering of them) are never touched from here.
        private static void ApplyWorkTypeState(RoleEditorSnapshot model,
            RoleJobTreeNode node, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                if (node.OwnEntryIndex < 0)
                    RoleCommands.AddEntry(model.RoleId,
                        new JobEntry(JobEntryKind.WorkType,
                            node.TypeDefName));
            }
            else if (node.OwnEntryIndex >= 0)
            {
                RoleCommands.RemoveEntry(model.RoleId, node.OwnEntryIndex);
            }
        }

        /// Giver entries and a WorkType entry may coexist: an entry placed above
        /// the type outranks it (the compiler keeps a job's earliest position),
        /// which is how single jobs get ordered inside an all-jobs selection.
        private static void ApplyGiverState(RoleEditorSnapshot model,
            RoleJobTreeNode node, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                if (node.OwnEntryIndex >= 0) return;
                // Above the type entry when one exists — below it, the entry
                // would never win a position.
                if (node.TypeEntryIndex >= 0)
                    RoleCommands.AddEntry(model.RoleId,
                        new JobEntry(JobEntryKind.WorkGiver,
                            node.GiverDefName), node.TypeEntryIndex);
                else
                    RoleCommands.AddEntry(model.RoleId,
                        new JobEntry(JobEntryKind.WorkGiver,
                            node.GiverDefName));
            }
            else if (node.OwnEntryIndex >= 0)
            {
                RoleCommands.RemoveEntry(model.RoleId, node.OwnEntryIndex);
            }
        }

        /// Every giver of the type as its own reorderable entry (existing ones
        /// kept in place), above the type entry when present.
        private static void AddAllGivers(RoleEditorSnapshot model,
            RoleJobTreeNode node)
        {
            int insertAt = node.TypeEntryIndex >= 0
                ? node.TypeEntryIndex : node.EntryCount;
            for (int i = 0; i < node.MissingGiverCount; i++)
            {
                RoleCommands.AddEntry(model.RoleId,
                    new JobEntry(JobEntryKind.WorkGiver,
                        node.MissingGiverAt(i)), insertAt);
                insertAt++;
            }
        }

        // ----- Composite editor: candidate roles (left) + member list (right) -----

        /// One role row shared by both composite lists: swatch, label, and the
        /// blocker marker; disabled roles render dimmed like the roles list.
        private static void DrawCompositeRoleRow(Rect row, CompositeMemberRow role,
            float reserveRight, float indent = 0f)
        {
            var swatch = new Rect(Mathf.Round(row.x) + 4f + indent, Mathf.Round(row.y) + 6f, 16f, 16f);
            Widgets.DrawBoxSolid(swatch, role.HasCustomColor
                ? role.Color : RoleChipUI.DefaultChipColor);
            GUI.color = WrStyle.PanelBackground;
            Widgets.DrawBox(swatch.ExpandedBy(1f));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!role.Enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            var labelRect = new Rect(swatch.xMax + 6f, row.y,
                row.xMax - reserveRight - (swatch.xMax + 6f), RowHeight);
            Widgets.Label(labelRect, role.Label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (role.Blocker)
            {
                var markerRect = new Rect(
                    labelRect.x + WrText.FitWidth(role.Label) + 4f,
                    row.y + (RowHeight - 16f) / 2f, 16f, 16f);
                if (markerRect.xMax <= labelRect.xMax)
                {
                    if (!role.Enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    GUI.DrawTexture(markerRect, WorkRolesTex.BlockerMarker);
                    GUI.color = Color.white;
                }
            }
        }

        private void DrawCompositeCandidates(Rect rect, RoleEditorSnapshot model)
        {
            RoleCompositeSnapshot composite = model.Composite;
            WrText.HeaderLabel(new Rect(rect.x + 4f, rect.y + WrText.MediumTopBearing,
                rect.width - 4f, 28f), composite.CandidatesTitle);
            float topY = rect.y + 28f + 4f;
            var scrollRect = new Rect(rect.x, topY, rect.width, rect.yMax - topY);
            if (composite.CandidateCount == 0)
            {
                GUI.color = WrStyle.DimText;
                Widgets.Label(scrollRect.ContractedBy(4f), composite.NoCandidatesHint);
                GUI.color = Color.white;
                return;
            }
            Widgets.BeginScrollView(scrollRect, ref treeScroll,
                new Rect(0f, 0f, scrollRect.width - 16f,
                    composite.CandidateCount * RowHeight));
            try
            {
            int firstRow = Mathf.Max(0, (int)(treeScroll.y / RowHeight));
            int lastRow = Mathf.Min(composite.CandidateCount - 1,
                (int)((treeScroll.y + scrollRect.height) / RowHeight));
            for (int i = firstRow; i <= lastRow; i++)
            {
                CompositeCandidateRow candidate = composite.CandidateAt(i);
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (candidate.IsHeader)
                {
                    // Same look and interaction as the role list's group
                    // headers: click collapses/expands the section.
                    Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
                    var arrowRect = new Rect(row.x + 6f,
                        row.y + (row.height - 18f) / 2f, 18f, 18f);
                    GUI.DrawTexture(arrowRect, candidate.HeaderCollapsed
                        ? TexButton.Reveal : TexButton.Collapse);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    Widgets.Label(new Rect(arrowRect.xMax + 6f, row.y,
                        row.xMax - arrowRect.xMax - 10f, row.height),
                        candidate.HeaderTitle);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.DrawHighlightIfMouseover(row);
                    if (Widgets.ButtonInvisible(row))
                        editorState.ToggleCandidateSection(candidate.HeaderKey);
                    continue;
                }
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                DrawCompositeRoleRow(row, candidate.Role,
                    reserveRight: IconButton + 4f, indent: candidate.Depth * 18f);
                var addRect = new Rect(row.xMax - IconButton - 2f,
                    row.y + (RowHeight - IconButton) / 2f, IconButton, IconButton);
                GUI.DrawTexture(addRect, TexButton.Plus);
                if (Widgets.ButtonInvisible(row))
                    RoleCommands.AddCompositeMember(model.RoleId, candidate.Role.RoleId);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCompositeMembers(Rect rect, RoleEditorSnapshot model)
        {
            RoleCompositeSnapshot composite = model.Composite;
            WrText.HeaderLabel(new Rect(rect.x + 8f, rect.y + WrText.MediumTopBearing,
                rect.width - 8f, 28f), composite.MembersTitle);
            var scrollRect = new Rect(rect.x + 8f, rect.y + 28f + 4f,
                rect.width - 8f, rect.height - 28f - 4f);
            if (composite.MemberCount == 0)
            {
                GUI.color = WrStyle.DimText;
                Widgets.Label(scrollRect.ContractedBy(4f), composite.NoMembersHint);
                GUI.color = Color.white;
                return;
            }
            float contentHeight = composite.MemberCount * RowHeight;
            if (Event.current.type == EventType.Repaint)
            {
                membersReorderableGroupId = ReorderableWidget.NewGroup(
                    memberReorderCallbacks.For(model.RoleId),
                    ReorderableDirection.Vertical,
                    scrollRect);
            }
            Widgets.BeginScrollView(scrollRect, ref entriesScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            try
            {
            // Rows outside the viewport still register with ReorderableWidget
            // (drag bookkeeping needs every row rect) but skip the text work.
            float cullTop = entriesScroll.y - RowHeight;
            float cullBottom = entriesScroll.y + scrollRect.height;
            float buttonsReserve = (IconButton + 2f) * 3f + 2f;
            for (int i = 0; i < composite.MemberCount; i++)
            {
                CompositeMemberRow memberRow = composite.MemberAt(i);
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                bool dragging = ReorderableWidget.Reorderable(
                    membersReorderableGroupId, row, useRightButton: false,
                    highlightDragged: true);
                if (row.y > cullBottom || row.y < cullTop) continue;
                if (Mouse.IsOver(row) && !dragging) Widgets.DrawHighlight(row);
                DrawCompositeRoleRow(row, memberRow, buttonsReserve);

                float btnY = row.y + (RowHeight - IconButton) / 2f;
                float removeX = row.xMax - IconButton - 2f;
                float downX = removeX - IconButton - 2f;
                float upX = downX - IconButton - 2f;
                int capturedI = i;
                int capturedRoleId = model.RoleId;
                if (i > 0 && Widgets.ButtonImage(
                        new Rect(upX, btnY, IconButton, IconButton), TexButton.ReorderUp))
                    RoleCommands.MoveCompositeMember(capturedRoleId, capturedI, capturedI - 1);
                if (i < composite.MemberCount - 1 && Widgets.ButtonImage(
                        new Rect(downX, btnY, IconButton, IconButton), TexButton.ReorderDown))
                    RoleCommands.MoveCompositeMember(capturedRoleId, capturedI, capturedI + 1);
                if (Widgets.ButtonImage(
                        new Rect(removeX, btnY, IconButton, IconButton), TexButton.Delete))
                    RoleCommands.RemoveCompositeMember(capturedRoleId, capturedI);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}
