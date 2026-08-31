using System.Collections.Generic;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// The main Implanner dialog: Overview, Plans, Automation, and Options
    /// tabs. Renders exclusively from cached immutable presentation
    /// snapshots; interactions issue commands and render the resulting
    /// published state.
    public class Dialog_Implanner : Window
    {
        private enum Tab { Overview, Plans, Automation, Options }

        // Session-scoped view state: survives close/reopen, never persisted.
        private static Tab curTab = Tab.Overview;

        /// Whether any Implanner dialog is open. Maintained by
        /// PreOpen/PostClose for Patch_MouseoverReadout, which runs every
        /// OnGUI frame and must not scan the window stack.
        internal static bool AnyOpen;

        private readonly OverviewState overview = new OverviewState();
        private readonly PlansState plans = new PlansState();
        private readonly AutomationState automation = new AutomationState();

        private List<TabRecord>? tabs;
        private int tabsLanguageStamp = -1;

        private Vector2 overviewScroll;
        private Vector2 detailScroll;
        private Vector2 planListScroll;
        private Vector2 pickerScroll;
        private Vector2 rankingsScroll;
        private Vector2 automationScroll;

        private const float TabHeight = 32f;
        private const float Pad = 10f;
        private const float RowHeight = 28f;
        private const float HeaderHeight = 24f;
        private const float CompactRowHeight = 20f;
        /// Scrollbar (16) plus the 4px breathing room content keeps from it.
        private const float ScrollGutter = 20f;

        /// Gap between the Automation tab's two option columns.
        private const float ColumnGap = 20f;

        // Between TabRecord's normal white and its hover yellow.
        private static readonly Color ActiveTabLabelColor = new Color(1f, 0.95f, 0.55f);

        public Dialog_Implanner()
        {
            doCloseX = true;
            // Window movement is frame-only (DoFrameDragZones): content drags
            // must stay with the content (paintable pickers, whose draggable
            // widgets poll Input and never consume the IMGUI event), so
            // vanilla whole-window dragging cannot be used.
            draggable = false;
            resizeable = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            // Cached per-zone delegates: immediate windows draw every frame.
            dragZoneDraws = new System.Action[]
            {
                () => DragZoneContents(0),
                () => DragZoneContents(1),
                () => DragZoneContents(2),
                () => DragZoneContents(3),
            };
        }

        public override Vector2 InitialSize => new Vector2(1220f, 760f);

        public override void PreOpen()
        {
            base.PreOpen();
            AnyOpen = true;
        }

        public override void PostClose()
        {
            base.PostClose();
            AnyOpen = false;
            overview.Release();
            plans.Release();
            automation.Release();
            overviewFront = null;
            plansFront = null;
            automationFront = null;
            PlannerDrag.Cancel();
            tabs = null;
            tabsLanguageStamp = -1;
        }

        public override void DoWindowContents(Rect inRect)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;

            // While the dialog is open, Tab cycles focus between its input
            // fields instead of triggering the architect-menu hotkey. Unity
            // delivers Tab as TWO KeyDown events — one carrying the key code
            // and one carrying the '\t' character; the character event
            // reaches the focused TextField and kills its focus, so both
            // must be consumed. The focus change runs on the key-code event.
            Event current = Event.current;
            if (current.type == EventType.KeyDown
                && (current.keyCode == KeyCode.Tab || current.character == '\t'))
            {
                if (current.keyCode == KeyCode.Tab)
                    CycleFieldFocus();
                current.Use();
            }

            using (GuiStateScope.Capture())
            {
                UiVersion.ObserveCurrentMetrics();
                PlannerLabels.Ensure();
                EnsureTabs();

                Rect content = new Rect(
                    inRect.x, inRect.y + TabHeight,
                    inRect.width, inRect.height - TabHeight);
                Widgets.DrawMenuSection(content);
                // Active-tab emphasis: TabRecord reads labelColor per pass, so
                // a per-frame field write is how selection tints the label.
                for (int i = 0; i < tabs!.Count; i++)
                    tabs[i].labelColor = i == (int)curTab ? ActiveTabLabelColor : (Color?)null;
                // Our own tab-strip drawer: vanilla geometry, minus the
                // cap/middle sub-pixel seam at fractional UI scales.
                PlannerTabs.DrawTabs(content, tabs);
                // Vanilla leaves the menu-section top border visible under the
                // active tab. Overpaint its span with the section fill so the
                // active tab connects seamlessly to the content (geometry
                // mirrors the tab strip: tabWidth capped at 200, 10px
                // overlap). Inset 2px per side so the rounded tab corners
                // keep their border pixel.
                float tabWidth = Mathf.Min(200f,
                    (content.width + (tabs.Count - 1) * 10f) / tabs.Count);
                float activeTabX = content.x + (int)curTab * (tabWidth - 10f);
                Widgets.DrawBoxSolid(new Rect(activeTabX + 2f, content.y, tabWidth - 4f, 2f),
                    Widgets.MenuSectionBGFillColor);

                content = content.ContractedBy(Pad);
                PlannerDrag.Update();
                switch (curTab)
                {
                    case Tab.Overview: DrawOverview(content, store); break;
                    case Tab.Plans: DrawPlans(content, store); break;
                    case Tab.Automation: DrawAutomation(content, store); break;
                    default: DrawOptionsPlaceholder(content); break;
                }
                if (curTab != Tab.Plans) PlannerDrag.Cancel();
                DrawDragGhost();
                PlannerDrag.ResolveMouseUp();
            }
        }

        /// Moves keyboard focus to the next input field (draw order, wrapping)
        /// using the field names registered on the previous frame; with no
        /// fields on screen the Tab press is simply swallowed. Focus goes
        /// through Verse's window-aware helper so it survives the window
        /// stack's own focus bookkeeping.
        private void CycleFieldFocus()
        {
            List<string> fields = automation.ReserveFieldNames;
            if (curTab != Tab.Automation || fields.Count == 0) return;
            string focused = GUI.GetNameOfFocusedControl();
            int index = fields.IndexOf(focused);
            Verse.UI.FocusControl(fields[(index + 1) % fields.Count], this);
        }

        /// The dragged ranking row follows the mouse as a floating label.
        private static void DrawDragGhost()
        {
            if (!PlannerDrag.Active || PlannerDrag.Payload == null) return;
            Vector2 mouse = Event.current.mousePosition;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(mouse.x + 12f, mouse.y - 11f, 260f, TreeLine),
                    PlannerDrag.PayloadLabel);
            }
        }

        // Frame-only window dragging via immediate windows over the margin
        // strips (the proven WorkRoles resize-grip pattern): they sit on the
        // Dialog layer with their own event flow, so content controls and
        // Verse's own MouseDown consumption never interfere. Movement tracks
        // the real cursor on Repaint and applies between frames through
        // WindowUpdate — mid-event windowRect changes desync IMGUI passes.
        private const int DragZoneIdBase = 173350010;
        private const float CloseClearance = 48f;
        private const float GripClearance = 30f;

        private readonly RimShared.Common.PendingUpdate<Rect> pendingWindowRect =
            new RimShared.Common.PendingUpdate<Rect>();
        private readonly System.Action[] dragZoneDraws;
        private readonly Rect[] dragZoneScreens = new Rect[4];
        private bool frameDragging;
        private Vector2 frameDragGrab;

        private void DoFrameDragZones()
        {
            float m = Margin;
            dragZoneScreens[0] = new Rect(windowRect.x, windowRect.y,
                windowRect.width - CloseClearance, m);
            dragZoneScreens[1] = new Rect(windowRect.x, windowRect.y + m,
                m, windowRect.height - m * 2f);
            dragZoneScreens[2] = new Rect(windowRect.xMax - m,
                windowRect.y + CloseClearance, m,
                windowRect.height - CloseClearance - GripClearance);
            dragZoneScreens[3] = new Rect(windowRect.x, windowRect.yMax - m,
                windowRect.width - GripClearance, m);
            // SubSuper layer: clicking the dialog refocuses it to the top of
            // its own (Dialog) layer, which would bury same-layer zones —
            // runtime-verified: same-layer zones never receive their events.
            for (int i = 0; i < 4; i++)
                Find.WindowStack.ImmediateWindow(DragZoneIdBase + i,
                    dragZoneScreens[i], WindowLayer.SubSuper, dragZoneDraws[i],
                    doBackground: false, absorbInputAroundWindow: false, 0f);
        }

        private void DragZoneContents(int zone)
        {
            Rect screen = dragZoneScreens[zone];
            var local = new Rect(0f, 0f, screen.width, screen.height);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0
                && local.Contains(current.mousePosition))
            {
                frameDragging = true;
                frameDragGrab = screen.position + current.mousePosition
                    - windowRect.position;
                current.Use();
            }
            if (!frameDragging) return;
            if (current.type == EventType.Repaint)
            {
                // Real cursor in screen-UI coords (game-window origin, y down).
                var gamePx = new Vector2(Input.mousePosition.x,
                    Screen.height - Input.mousePosition.y);
                Vector2 mouseUI = gamePx / Prefs.UIScale;
                Vector2 position = mouseUI - frameDragGrab;
                position.x = Mathf.Clamp(position.x,
                    100f - windowRect.width, Verse.UI.screenWidth - 100f);
                position.y = Mathf.Clamp(position.y,
                    0f, Verse.UI.screenHeight - 100f);
                pendingWindowRect.QueueUser(new Rect(position, windowRect.size));
            }
            if (current.type == EventType.MouseUp || !Input.GetMouseButton(0))
            {
                frameDragging = false;
                if (current.type == EventType.MouseUp) current.Use();
            }
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            DoFrameDragZones();
        }

        /// Pending geometry applies between frames: mid-event windowRect
        /// changes desync Layout from later passes.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (pendingWindowRect.TryConsume(out Rect nextWindowRect))
                windowRect = nextWindowRect;
        }

        private void EnsureTabs()
        {
            if (tabs != null && tabsLanguageStamp == UiVersion.LanguageCurrent) return;
            tabsLanguageStamp = UiVersion.LanguageCurrent;
            tabs = new List<TabRecord>
            {
                new TabRecord(PlannerLabels.TabOverview,
                    static () => curTab = Tab.Overview, () => curTab == Tab.Overview),
                new TabRecord(PlannerLabels.TabPlans,
                    static () => curTab = Tab.Plans, () => curTab == Tab.Plans),
                new TabRecord(PlannerLabels.TabAutomation,
                    static () => curTab = Tab.Automation, () => curTab == Tab.Automation),
                new TabRecord(PlannerLabels.TabOptions,
                    static () => curTab = Tab.Options, () => curTab == Tab.Options),
            };
        }

        // ---------------------------------------------------------- Overview

        private OverviewSnapshot? overviewFront;

        private void DrawOverview(Rect rect, ImplannerStore store)
        {
            // Snapshot refresh happens at the Repaint boundary; other passes
            // reuse the front snapshot so all passes of one frame agree.
            if (Event.current.type == EventType.Repaint || overviewFront == null)
                overviewFront = overview.Current(store);
            OverviewSnapshot snapshot = overviewFront;

            const float RightWidth = 300f;
            Rect left = new Rect(rect.x, rect.y, rect.width - RightWidth - Pad, rect.height);
            Rect right = new Rect(left.xMax + Pad, rect.y, RightWidth, rect.height);

            // Location selector and group-by selector.
            Rect selectorRect = new Rect(left.x, left.y, 240f, RowHeight);
            int groupingIndex = IndexOfGrouping(snapshot);
            string groupingLabel = groupingIndex >= 0
                ? snapshot.GroupingLabels[groupingIndex]
                : "";
            if (Widgets.ButtonText(selectorRect, groupingLabel))
                OpenGroupingMenu(snapshot);
            Rect groupByRect = new Rect(selectorRect.xMax + 8f, left.y, 180f, RowHeight);
            if (Widgets.ButtonText(groupByRect, snapshot.GroupByLabel))
                OpenGroupByMenu(snapshot);

            // Colonist table.
            Rect tableRect = new Rect(left.x, selectorRect.yMax + Pad, left.width,
                left.height - RowHeight - Pad * 2f);
            DrawColonistTable(tableRect, snapshot);

            // Right side: the Colony panel sized to its content, colonist
            // details taking the rest.
            float colonyHeight = Pad * 2f + 32f + RowHeight + 4f
                + PlannerStyle.SectionHeaderHeight
                + snapshot.NextWork.Count * CompactRowHeight;
            Rect colonyRect = new Rect(right.x, right.y, right.width, colonyHeight);
            Rect detailRect = new Rect(right.x, colonyRect.yMax + Pad, right.width,
                right.height - colonyHeight - Pad);
            DrawColonyOverview(colonyRect, snapshot);
            DrawColonistDetails(detailRect, snapshot);
        }

        private int IndexOfGrouping(OverviewSnapshot snapshot)
        {
            GroupingOption? current = overview.Grouping;
            if (current == null) return -1;
            // By value, not reference: All/Current selections keep their old
            // instance across snapshot rebuilds (Revalidate only re-resolves
            // named locations into the fresh options list).
            for (int i = 0; i < snapshot.GroupingOptions.Count; i++)
            {
                GroupingOption option = snapshot.GroupingOptions[i];
                if (option.Kind != current.Kind) continue;
                if (option.Kind != GroupingKind.Location
                    || string.Equals(option.LocationId, current.LocationId,
                        System.StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private void OpenGroupingMenu(OverviewSnapshot snapshot)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < snapshot.GroupingOptions.Count; i++)
            {
                GroupingOption option = snapshot.GroupingOptions[i];
                string label = snapshot.GroupingLabels[i];
                options.Add(new FloatMenuOption(label, () => overview.Grouping = option));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenGroupByMenu(OverviewSnapshot snapshot)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < snapshot.GroupByKeys.Count; i++)
            {
                string key = snapshot.GroupByKeys[i];
                options.Add(new FloatMenuOption(snapshot.GroupByLabels[i],
                    () => overview.GroupByKey = key));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private const float SkillColWidth = 28f;
        private const float PriorityColWidth = 92f;
        private const float CellPad = 6f;

        private void DrawColonistTable(Rect rect, OverviewSnapshot snapshot)
        {
            float contentWidth = rect.width - 16f;
            float flexible = contentWidth - SkillColWidth * 2f - PriorityColWidth;
            float nameWidth = Mathf.Floor(flexible * 0.30f);
            float planWidth = Mathf.Floor(flexible * 0.28f);
            float progressWidth = Mathf.Floor(flexible * 0.22f);
            float stateWidth = flexible - nameWidth - planWidth - progressWidth;

            // Column x offsets: Name | S | M | Plan | Progress | Priority | State.
            float xName = 0f;
            float xShooting = xName + nameWidth;
            float xMelee = xShooting + SkillColWidth;
            float xPlan = xMelee + SkillColWidth;
            float xProgress = xPlan + planWidth;
            float xPriority = xProgress + progressWidth;
            float xState = xPriority + PriorityColWidth;

            // Table header: section-header treatment, one shared hairline,
            // every label a sort button with an arrow on the active column.
            Rect header = new Rect(rect.x, rect.y, rect.width, PlannerStyle.SectionHeaderHeight);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = PlannerStyle.HeaderText;
                DrawSortHeader(header.x + xName + CellPad, header.y, nameWidth - CellPad,
                    PlannerLabels.ColColonist, OverviewColumn.Name, null);
                DrawSortHeader(header.x + xShooting, header.y, SkillColWidth,
                    PlannerLabels.ColShooting, OverviewColumn.Shooting,
                    PlannerLabels.ColShootingTip);
                DrawSortHeader(header.x + xMelee, header.y, SkillColWidth,
                    PlannerLabels.ColMelee, OverviewColumn.Melee,
                    PlannerLabels.ColMeleeTip);
                DrawSortHeader(header.x + xPlan, header.y, planWidth,
                    PlannerLabels.ColPlan, OverviewColumn.Plan, null);
                DrawSortHeader(header.x + xProgress, header.y, progressWidth,
                    PlannerLabels.ColProgress, OverviewColumn.Progress, null);
                DrawSortHeader(header.x + xPriority, header.y, PriorityColWidth,
                    PlannerLabels.ColPriority, OverviewColumn.Priority, null);
                DrawSortHeader(header.x + xState, header.y, stateWidth,
                    PlannerLabels.ColState, OverviewColumn.State, null);
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                WrText.LineHorizontal(header.x, header.y + 22f, contentWidth);
            }

            // Visible rows: collapsed sections contribute their header only.
            int visible = 0;
            bool skipping = false;
            for (int i = 0; i < snapshot.Rows.Count; i++)
            {
                if (snapshot.Rows[i].Header)
                {
                    skipping = overview.CollapsedTableGroups.Contains(
                        snapshot.Rows[i].SectionKey);
                    visible++;
                }
                else if (!skipping)
                    visible++;
            }

            Rect outer = new Rect(rect.x, header.yMax + 2f, rect.width,
                rect.height - PlannerStyle.SectionHeaderHeight - 2f);
            Rect inner = new Rect(0f, 0f, contentWidth, visible * RowHeight);
            Widgets.BeginScrollView(outer, ref overviewScroll, inner);
            float y = 0f;
            skipping = false;
            for (int i = 0; i < snapshot.Rows.Count; i++)
            {
                OverviewRow row = snapshot.Rows[i];
                if (row.Header)
                {
                    skipping = overview.CollapsedTableGroups.Contains(row.SectionKey);
                    DrawTableGroupHeader(new Rect(0f, y, inner.width, RowHeight), row);
                    y += RowHeight;
                    continue;
                }
                if (skipping) continue;
                Rect rowRect = new Rect(0f, y, inner.width, RowHeight);
                y += RowHeight;
                if (row.PawnId == overview.SelectedPawnId)
                    Widgets.DrawHighlightSelected(rowRect);
                else if (Mouse.IsOver(rowRect))
                    Widgets.DrawHighlight(rowRect);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(xName + CellPad, rowRect.y,
                    nameWidth - CellPad - 66f, RowHeight), row.Name);
                // Gear cutouts at fixed offsets so they column-align across
                // rows: weapon rightmost (8px clear of the skill column),
                // utility beside it when worn. Display only — no Implanner
                // logic is tied to equipped gear.
                float weaponCenterX = xName + nameWidth - 21f;
                float centerY = rowRect.y + RowHeight / 2f;
                if (row.WeaponDef != null)
                    DrawGearCutout(row.WeaponDef, weaponCenterX, centerY, scaled: true);
                if (row.UtilityDef != null)
                    DrawGearCutout(row.UtilityDef, weaponCenterX - 40f, centerY, scaled: false);

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(xShooting, rowRect.y, SkillColWidth, RowHeight),
                    row.ShootingText);
                Widgets.Label(new Rect(xMelee, rowRect.y, SkillColWidth, RowHeight),
                    row.MeleeText);
                Text.Anchor = TextAnchor.MiddleLeft;

                Rect planCell = new Rect(xPlan, rowRect.y + 2f,
                    planWidth - 8f, RowHeight - 4f);
                // Paintable, like the vanilla Assign-tab policy pickers:
                // click opens the menu, click-and-drag copies this row's plan
                // onto the rows dragged over.
                Widgets.Dropdown(planCell, row, PlanPayloadGetter, PlanMenuGenerator,
                    row.PlanName, null, row.PlanName, null, null, paintable: true);

                if (row.PlanId != 0)
                {
                    Rect barRect = new Rect(xProgress, rowRect.y + 5f,
                        progressWidth - 12f, RowHeight - 10f);
                    Widgets.FillableBar(barRect, row.Progress);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    float progressTextH = Mathf.Max(
                        barRect.height, TinyText.LineHeight);
                    TinyText.Label(new Rect(barRect.x,
                        barRect.center.y - progressTextH / 2f,
                        barRect.width, progressTextH), row.ProgressText);
                    Text.Anchor = TextAnchor.MiddleLeft;
                }

                Rect priorityCell = new Rect(xPriority, rowRect.y + 2f,
                    PriorityColWidth - 8f, RowHeight - 4f);
                Widgets.Dropdown(priorityCell, row, PriorityPayloadGetter,
                    PriorityMenuGenerator, row.PriorityText, null,
                    row.PriorityText, null, null, paintable: true);

                if (row.PlanId != 0)
                    Widgets.Label(new Rect(xState + CellPad, rowRect.y,
                        stateWidth - CellPad, RowHeight), row.StateText);
                Text.Anchor = TextAnchor.UpperLeft;

                // Row click (outside interactive cells) selects the colonist.
                Rect selectRect = new Rect(xName, rowRect.y, nameWidth, RowHeight);
                if (Widgets.ButtonInvisible(selectRect))
                    overview.SelectedPawnId = row.PawnId;
            }
            Widgets.EndScrollView();
        }

        /// A circular bright cutout with the def's icon centered on its
        /// measured opaque pixels. Weapons draw scaled (doubled 44px rect
        /// with coverage normalization); belt gear draws unscaled (the
        /// natural 26px slot size).
        private static void DrawGearCutout(ThingDef def, float centerX, float centerY,
            bool scaled)
        {
            GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.30f);
            GUI.DrawTexture(new Rect(centerX - 13f, centerY - 13f, 26f, 26f),
                Patches.ImplannerTex.CircleFill);
            GUI.color = Color.white;
            GearIconMetrics.Correction fix = GearIconMetrics.For(def);
            float size = scaled ? 44f : 26f;
            float iconScale = scaled && fix.Coverage > 0f
                ? Mathf.Clamp(0.80f / fix.Coverage, 1f, 1.5f)
                : 1f;
            Widgets.DefIcon(new Rect(
                    centerX - size / 2f + fix.Offset.x * size,
                    centerY - size / 2f + fix.Offset.y * size,
                    size, size),
                def, null, iconScale);
        }

        /// Grouped-colonist section header, WorkRoles-style: a full-row band
        /// with fold arrow and title; clicking anywhere toggles the section.
        private void DrawTableGroupHeader(Rect rect, OverviewRow row)
        {
            bool collapsed = overview.CollapsedTableGroups.Contains(row.SectionKey);
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
            var arrowRect = new Rect(rect.x + CellPad,
                rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect, collapsed ? TexButton.Reveal : TexButton.Collapse);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(arrowRect.xMax + 6f, rect.y,
                rect.width - arrowRect.xMax - 10f, rect.height), row.Name);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect))
            {
                if (!overview.CollapsedTableGroups.Remove(row.SectionKey))
                    overview.CollapsedTableGroups.Add(row.SectionKey);
            }
        }

        /// A clickable column header; the active sort column shows a
        /// direction arrow after the label (bar-order Name shows none).
        private void DrawSortHeader(float x, float y, float width,
            string label, OverviewColumn column, string? tip)
        {
            var cell = new Rect(x, y, width, 20f);
            Widgets.Label(cell, label);
            if (tip != null && Mouse.IsOver(cell))
                TooltipHandler.TipRegion(cell, tip);
            if (overview.SortColumn == column
                && (column != OverviewColumn.Name || overview.NameAlphabetical))
            {
                float labelWidth = WrText.FitWidth(label);
                bool up = column == OverviewColumn.Name
                    ? true
                    : !overview.SortDescending;
                GUI.DrawTexture(new Rect(x + Mathf.Min(labelWidth, width - 14f) + 2f,
                    y + 4f, 12f, 12f),
                    up ? TexButton.ReorderUp : TexButton.ReorderDown);
            }
            if (Widgets.ButtonInvisible(cell))
                overview.ClickColumn(column);
        }

        private static readonly System.Func<OverviewRow, int> PriorityPayloadGetter =
            static row => row.Priority;

        private static readonly
            System.Func<OverviewRow, IEnumerable<Widgets.DropdownMenuElement<int>>>
            PriorityMenuGenerator = GeneratePriorityMenu;

        private static IEnumerable<Widgets.DropdownMenuElement<int>> GeneratePriorityMenu(
            OverviewRow row)
        {
            int pawnId = row.PawnId;
            for (int level = 0; level <= 4; level++)
            {
                int captured = level;
                yield return new Widgets.DropdownMenuElement<int>
                {
                    option = new FloatMenuOption(PlannerLabels.PriorityLabel(captured),
                        () => PlannerCommands.SetPawnPriority(pawnId, captured)),
                    payload = captured,
                };
            }
        }

        // Static cached delegates: the dropdown draws every row every frame
        // and must not allocate on cache hits. The menu generator itself runs
        // only on click or while paint-dragging over a cell.
        private static readonly System.Func<OverviewRow, int> PlanPayloadGetter =
            static row => row.PlanId;

        private static readonly
            System.Func<OverviewRow, IEnumerable<Widgets.DropdownMenuElement<int>>>
            PlanMenuGenerator = GeneratePlanMenu;

        private static IEnumerable<Widgets.DropdownMenuElement<int>> GeneratePlanMenu(
            OverviewRow row)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) yield break;
            int pawnId = row.PawnId;
            yield return new Widgets.DropdownMenuElement<int>
            {
                option = new FloatMenuOption(PlannerLabels.NoPlan,
                    () => PlannerCommands.AssignPlan(pawnId, 0)),
                payload = 0,
            };
            IReadOnlyList<Plan> allPlans = store.Model.Plans;
            for (int i = 0; i < allPlans.Count; i++)
            {
                int planId = allPlans[i].Id;
                yield return new Widgets.DropdownMenuElement<int>
                {
                    option = new FloatMenuOption(allPlans[i].Name,
                        () => PlannerCommands.AssignPlan(pawnId, planId)),
                    payload = planId,
                };
            }
        }

        private void DrawColonyOverview(Rect rect, OverviewSnapshot snapshot)
        {
            Widgets.DrawMenuSection(rect);
            Rect body = rect.ContractedBy(Pad);
            float y = body.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(body.x, y, body.width, 30f), PlannerLabels.ColonyOverview);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(body.x, y, body.width, 30f), snapshot.PercentText);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 32f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(body.x, y, body.width, RowHeight), snapshot.StatsText);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(body.x, y, body.width, RowHeight), snapshot.BlockersText);
            Text.Anchor = TextAnchor.UpperLeft;
            y += RowHeight + 4f;
            y += PlannerStyle.SectionHeader(body.x, y, body.width, PlannerLabels.NextWork);
            for (int i = 0; i < snapshot.NextWork.Count && y + CompactRowHeight <= body.yMax; i++)
            {
                Widgets.Label(new Rect(body.x, y, body.width, CompactRowHeight),
                    snapshot.NextWork[i]);
                y += CompactRowHeight;
            }
            Text.Font = GameFont.Small;
        }

        private void DrawColonistDetails(Rect rect, OverviewSnapshot snapshot)
        {
            Widgets.DrawMenuSection(rect);
            Rect body = rect.ContractedBy(Pad);
            IReadOnlyList<DetailRow> details = overview.Details(snapshot);
            bool hasSelection = overview.DetailTitle.Length > 0;

            int headerCount = 0;
            for (int i = 0; i < details.Count; i++)
                if (details[i].Header)
                    headerCount++;
            float innerHeight = headerCount * PlannerStyle.SectionHeaderHeight
                + (details.Count - headerCount) * CompactRowHeight
                + (headerCount > 0 ? (headerCount - 1) * PlannerStyle.SectionGap : 0f);

            Rect outer = new Rect(body.x, body.y + 34f, body.width, body.height - 34f);
            // The scroll gutter is reserved only when the content overflows;
            // the header's percent right-aligns with the content edge either
            // way.
            bool scrolls = innerHeight > outer.height;
            float alignedWidth = scrolls ? body.width - ScrollGutter : body.width;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(body.x, body.y, alignedWidth, 30f),
                hasSelection ? overview.DetailTitle : PlannerLabels.ColonistDetails);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(body.x, body.y, alignedWidth, 30f),
                overview.DetailPercent);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (overview.DetailHasRegression)
            {
                Rect reEnlistRect = new Rect(
                    body.x + alignedWidth - 190f, body.y + 2f, 100f, 24f);
                if (Widgets.ButtonText(reEnlistRect, PlannerLabels.ReEnlist))
                    PlannerCommands.ReEnlist(overview.SelectedPawnId);
            }
            if (details.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(body.x, body.y + 34f, body.width, RowHeight),
                    PlannerLabels.NoSelection);
                GUI.color = Color.white;
                return;
            }

            Rect inner = new Rect(0f, 0f, alignedWidth, innerHeight);
            Widgets.BeginScrollView(outer, ref detailScroll, inner);
            float y = 0f;
            for (int i = 0; i < details.Count; i++)
            {
                DetailRow row = details[i];
                if (row.Header)
                {
                    if (i > 0) y += PlannerStyle.SectionGap;
                    float headerY = y;
                    y += PlannerStyle.SectionHeader(0f, y, inner.width, row.Label);
                    using (GuiStateScope.Capture())
                    {
                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleRight;
                        GUI.color = PlannerStyle.HeaderText;
                        Widgets.Label(new Rect(0f, headerY, inner.width, 20f),
                            row.StatusText);
                    }
                    continue;
                }
                GUI.color = row.Satisfied ? Color.white
                    : row.Blocked ? new Color(1f, 0.55f, 0.4f)
                    : row.Muted ? PlannerStyle.CaptionText
                    : new Color(1f, 0.85f, 0.4f);
                Text.Anchor = TextAnchor.MiddleLeft;
                float detailTextH = Mathf.Max(
                    CompactRowHeight, TinyText.LineHeight);
                float detailTextY = y
                    + (CompactRowHeight - detailTextH) / 2f;
                TinyText.Label(new Rect(8f, detailTextY,
                    inner.width * 0.62f - 8f, detailTextH),
                    row.Label);
                Text.Anchor = TextAnchor.MiddleRight;
                TinyText.Label(new Rect(inner.width * 0.62f, detailTextY,
                    inner.width * 0.38f, detailTextH), row.StatusText);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                y += CompactRowHeight;
            }
            Widgets.EndScrollView();
        }

        // ------------------------------------------------------------- Plans

        private PlansSnapshot? plansFront;

        // Plan-list panel and card palette: one shared card fill (selection
        // is the gold outline alone; hover is the only background change), a
        // blue-tinted delivery bar, and the solid panel the cards sit in.
        private static readonly Color PlanListFill = new Color(0.10f, 0.11f, 0.12f);
        private static readonly Color PlanListOutline = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color CardSelectedOutline = new Color(1f, 0.95f, 0.55f, 0.65f);
        private static readonly Color CardProgressTrack = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color CardProgressFill = new Color(0.38f, 0.61f, 0.90f, 0.9f);

        private const float CardPad = 8f;
        private const float CardGap = 6f;
        private const float CardBarHeight = 4f;
        private const float CardPercentWidth = 44f;

        private void DrawPlans(Rect rect, ImplannerStore store)
        {
            if (Event.current.type == EventType.Repaint || plansFront == null)
                plansFront = plans.Current(store);
            PlansSnapshot snapshot = plansFront;

            const float ListWidth = 230f;
            float paneWidth = Mathf.Floor((rect.width - ListWidth - Pad * 2f) / 2f);
            Rect list = new Rect(rect.x, rect.y, ListWidth, rect.height);
            Rect center = new Rect(list.xMax + Pad, rect.y, paneWidth, rect.height);
            Rect rankingsCol = new Rect(center.xMax + Pad, rect.y, paneWidth, rect.height);

            DrawPlanList(list, snapshot);

            if (snapshot.SelectedPlanId != 0)
            {
                DrawPlanEditor(center, store, snapshot);
                DrawPlanRankings(rankingsCol, snapshot);
            }
            else
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(center, PlannerLabels.NoPlans);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ----------------------------------------------- Plans: left column

        /// The plan list as cards: name, counts caption with a right-aligned
        /// percent, and a thin delivery-progress bar, framed by the shared
        /// device-pixel-snapped panel box (the old logical-unit DrawBox
        /// border covered one-or-two physical pixels at fractional UI
        /// scales). Card geometry derives from the active fonts' measured
        /// line boxes.
        private void DrawPlanList(Rect rect, PlansSnapshot snapshot)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), PlannerLabels.PlansHeader);
            Text.Font = GameFont.Small;

            float nameH = Mathf.Ceil(Text.LineHeightOf(GameFont.Small));
            float captionH = TinyText.LineHeight;
            float cardHeight = 4f + nameH + captionH + 4f + CardBarHeight + 6f;

            // The solid panel the cards sit inside, device-pixel-snapped.
            Rect frame = new Rect(rect.x, rect.y + 34f, rect.width,
                rect.height - 34f - RowHeight - Pad);
            PixelBox.SolidWithOutline(frame, PlanListFill, PlanListOutline);

            Rect outer = frame.ContractedBy(4f);
            float listHeight = snapshot.Plans.Count > 0
                ? snapshot.Plans.Count * (cardHeight + CardGap) - CardGap
                : 0f;
            Rect inner = new Rect(0f, 0f,
                listHeight > outer.height ? outer.width - 16f : outer.width,
                listHeight);
            Widgets.BeginScrollView(outer, ref planListScroll, inner);
            using (GuiStateScope.Capture())
            {
                Text.WordWrap = false;
                // One logical pixel short of the clip edge: a device-grid
                // right border rounded up at a fractional scale would land
                // outside the scroll group's clip and vanish.
                float cardWidth = inner.width - 1f;
                float y = 0f;
                for (int i = 0; i < snapshot.Plans.Count; i++)
                {
                    PlanListRow row = snapshot.Plans[i];
                    var card = new Rect(0f, y, cardWidth, cardHeight);
                    bool selected = row.PlanId == snapshot.SelectedPlanId;
                    PixelBox.SolidWithOutline(card,
                        SegmentedControl.PanelBackground,
                        selected ? CardSelectedOutline : SegmentedControl.PanelOutline);
                    if (!selected && Mouse.IsOver(card))
                        Widgets.DrawHighlight(card);

                    float textWidth = card.width - CardPad * 2f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(card.x + CardPad, card.y + 4f,
                        textWidth, nameH), row.Name);

                    float captionY = card.y + 4f + nameH;
                    GUI.color = PlannerStyle.CaptionText;
                    TinyText.Label(new Rect(card.x + CardPad, captionY,
                        textWidth - CardPercentWidth, captionH), row.CountsText);
                    if (row.PercentText.Length > 0)
                    {
                        Text.Anchor = TextAnchor.MiddleRight;
                        TinyText.Label(new Rect(
                            card.xMax - CardPad - CardPercentWidth, captionY,
                            CardPercentWidth, captionH), row.PercentText);
                        Text.Anchor = TextAnchor.MiddleLeft;
                    }
                    GUI.color = Color.white;

                    var track = new Rect(card.x + CardPad,
                        captionY + captionH + 2f, textWidth, CardBarHeight);
                    Widgets.DrawBoxSolid(track, CardProgressTrack);
                    float fill = Mathf.Round(
                        track.width * Mathf.Clamp01(row.Progress));
                    if (fill > 0f)
                        Widgets.DrawBoxSolid(new Rect(track.x, track.y,
                            fill, track.height), CardProgressFill);

                    if (Widgets.ButtonInvisible(card))
                        plans.SelectedPlanId = row.PlanId;
                    y += cardHeight + CardGap;
                }
            }
            Widgets.EndScrollView();

            Rect addRect = new Rect(rect.x, frame.yMax + Pad, rect.width, RowHeight);
            if (Widgets.ButtonText(addRect, PlannerLabels.AddPlan))
                Find.WindowStack.Add(new Dialog_NewPlan());
        }

        // --------------------------------------------- Plans: center column

        private void DrawPlanEditor(Rect rect, ImplannerStore store, PlansSnapshot snapshot)
        {
            // Row labels stay on one line; overflow clips instead of wrapping.
            Text.WordWrap = false;
            float y = rect.y;

            // Header: plan name, rename icon, delete button.
            Text.Font = GameFont.Medium;
            float nameWidth = WrText.FitWidth(snapshot.SelectedPlanName);
            Widgets.Label(new Rect(rect.x, y, nameWidth, 30f), snapshot.SelectedPlanName);
            Text.Font = GameFont.Small;
            Rect renameRect = new Rect(rect.x + nameWidth + 6f, y + 3f, 24f, 24f);
            if (Widgets.ButtonImage(renameRect, TexButton.Rename))
            {
                int planId = snapshot.SelectedPlanId;
                Find.WindowStack.Add(new NameDialog(PlannerLabels.PlanNameTitle,
                    snapshot.SelectedPlanName,
                    name => PlannerCommands.RenamePlan(planId, name)));
            }
            Rect deleteRect = new Rect(rect.xMax - 120f, y, 120f, RowHeight);
            if (Widgets.ButtonText(deleteRect, PlannerLabels.DeletePlan))
            {
                int planId = snapshot.SelectedPlanId;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "IMP_DeletePlanConfirm".Translate(snapshot.SelectedPlanName),
                    () => PlannerCommands.DeletePlan(planId), destructive: true));
            }
            y += 34f;

            // Catalog filter: anatomy region, clustering mutually blocking
            // slots in one view. A presentation filter over the picker; it
            // never changes plan content.
            int clickedRegion = SegmentedControl.Row(
                new Rect(rect.x, y, rect.width, 28f),
                PlannerLabels.ImplantRegions, plans.Region);
            if (clickedRegion >= 0 && clickedRegion != plans.Region)
                plans.Region = clickedRegion;
            y += 28f + 6f;

            // The selection tree, rendered in the vanilla storage-filter
            // style: 22px lines, 11px indent per level, triangle fold
            // widgets, def icons on leaves. The scroll gutter is reserved
            // only while the tree overflows.
            Rect outer = new Rect(rect.x, y, rect.width, rect.yMax - y);
            float treeHeight = TreeVisibleHeight(snapshot.Tree, plans.CollapsedSections);
            Rect inner = new Rect(0f, 0f,
                treeHeight > outer.height ? outer.width - 16f : outer.width,
                treeHeight);
            Widgets.BeginScrollView(outer, ref pickerScroll, inner);
            DrawTree(inner.width, snapshot.Tree, plans.CollapsedSections,
                snapshot.SelectedPlanId);
            Widgets.EndScrollView();
            Text.WordWrap = true;
        }

        private const float TreeLine = 22f;
        private const float TreeIndent = 11f;
        private const float TreeArrow = 18f;

        /// The fold set holds COLLAPSED keys: every group starts expanded.
        private static bool IsExpanded(HashSet<string> foldSet, string key) =>
            !foldSet.Contains(key);

        /// Rows hidden inside folded subtrees contribute nothing. Pure
        /// arithmetic over already-built rows.
        private static float TreeVisibleHeight(List<PickerRow> tree,
            HashSet<string> foldSet)
        {
            int visible = 0;
            int foldedDepth = -1;
            for (int i = 0; i < tree.Count; i++)
            {
                PickerRow row = tree[i];
                if (foldedDepth >= 0)
                {
                    if (row.Depth > foldedDepth) continue;
                    foldedDepth = -1;
                }
                visible++;
                if (row.Node && !IsExpanded(foldSet, row.SectionKey))
                    foldedDepth = row.Depth;
            }
            return visible * TreeLine;
        }

        private static readonly Color OverrideText = new Color(1f, 0.85f, 0.4f);

        /// The plan selection-tree renderer: checking a slot leaf adds the
        /// implant goal to the selected plan. Slots covered by a base plan
        /// show an "inherited" caption and checking one re-includes the slot
        /// as this plan's own goal; slots conflicting with a planned goal
        /// carry an "overrides X" caption — checking them deselects an own
        /// blocker (the command removes it) or suppresses an inherited one.
        private static void DrawTree(float width, List<PickerRow> tree,
            HashSet<string> foldSet, int planId)
        {
            float y = 0f;
            int foldedDepth = -1;
            for (int i = 0; i < tree.Count; i++)
            {
                PickerRow row = tree[i];
                if (foldedDepth >= 0)
                {
                    if (row.Depth > foldedDepth) continue;
                    foldedDepth = -1;
                }
                Rect rowRect = new Rect(0f, y, width, TreeLine);
                float x = row.Depth * TreeIndent;
                if (row.Node)
                {
                    bool expanded = IsExpanded(foldSet, row.SectionKey);
                    Widgets.DrawHighlightIfMouseover(rowRect);
                    GUI.DrawTexture(
                        new Rect(x, y + (TreeLine - TreeArrow) / 2f, TreeArrow, TreeArrow),
                        expanded ? TexButton.Collapse : TexButton.Reveal);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(x + TreeArrow + 2f, y,
                        width - x - TreeArrow - 2f, TreeLine), row.Label);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        if (!foldSet.Remove(row.SectionKey))
                            foldSet.Add(row.SectionKey);
                    }
                    if (!expanded) foldedDepth = row.Depth;
                    y += TreeLine;
                    continue;
                }

                if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawHighlight(rowRect);
                    // Tooltip content builds lazily for the hovered row only
                    // and is cached per definition.
                    string? tip = ImplantTip(row.DefName);
                    if (tip != null)
                        TooltipHandler.TipRegion(rowRect, tip);
                }
                bool overrides = row.OverridesLabel.Length > 0;
                float labelX = x + TreeArrow + 2f;
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(x, y + 2f, TreeLine - 4f, TreeLine - 4f),
                        row.IconDef);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(labelX, y, width - labelX - 176f, TreeLine),
                    row.Label);
                string caption = overrides ? row.OverridesLabel
                    : row.Inherited ? PlannerLabels.Inherited
                    : "";
                if (caption.Length > 0)
                {
                    using (GuiStateScope.Capture())
                    {
                        Text.Anchor = TextAnchor.MiddleRight;
                        GUI.color = overrides ? OverrideText : PlannerStyle.CaptionText;
                        TinyText.Label(new Rect(rowRect.xMax - 176f, y, 144f, TreeLine),
                            caption);
                    }
                }
                Text.Anchor = TextAnchor.UpperLeft;

                bool now = row.Selected;
                Widgets.Checkbox(new Vector2(rowRect.xMax - 26f, y + 1f),
                    ref now, 20f, disabled: false, paintable: true);
                if (now != row.Selected)
                    PlannerCommands.SetImplantSlot(planId, row.DefName, row.Ordinal, now);
                y += TreeLine;
            }
        }

        private static string? ImplantTip(string defName)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            return entry != null ? PlannerTips.ForImplant(entry) : null;
        }

        // -------------------------------------- Plans: ranking right column

        /// Tier headers are symbols, not translations.
        private static readonly string[] TierStars =
            { "★★★★★", "★★★★",
              "★★★", "★★", "★" };
        private static readonly Color TierStarColor = new Color(1f, 0.9f, 0.5f);
        private static readonly Color DropTargetFill = new Color(1f, 1f, 1f, 0.08f);

        /// The plan's ranking panel: a title, a collapsible help section
        /// explaining the star classification, then one combined list with
        /// five star tiers over the plan's OWN implants (inherited goals are
        /// ranked where their own plan ranks them). A new implant lands in
        /// the three-star tier; dragging a row to another tier re-ranks the
        /// implant kind globally.
        private void DrawPlanRankings(Rect rect, PlansSnapshot snapshot)
        {
            Text.WordWrap = false;
            Widgets.DrawMenuSection(rect);
            Rect body = rect.ContractedBy(Pad);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(body.x, body.y, body.width, 30f),
                PlannerLabels.RankTiersTitle);
            Text.Font = GameFont.Small;
            float top = body.y + 32f;

            bool folded = ImplannerMod.Settings.helpPlanTiersFolded;
            top += PlannerStyle.HelpGroup(body.x, top, body.width,
                PlannerLabels.Help, PlannerLabels.RankTiersHelp, ref folded);
            if (folded != ImplannerMod.Settings.helpPlanTiersFolded)
            {
                ImplannerMod.Settings.helpPlanTiersFolded = folded;
                ImplannerMod.Instance.WriteSettings();
            }

            var listRect = new Rect(body.x, top, body.width, body.yMax - top);
            float innerHeight = 0f;
            for (int t = 0; t < snapshot.RankTiers.Length; t++)
                innerHeight += PlannerStyle.SectionHeaderHeight
                    + Mathf.Max(snapshot.RankTiers[t].Count, 1) * TreeLine;

            Rect inner = new Rect(0f, 0f,
                innerHeight > listRect.height ? body.width - 16f : body.width,
                innerHeight);
            Widgets.BeginScrollView(listRect, ref rankingsScroll, inner);
            float y = 0f;
            for (int t = 0; t < snapshot.RankTiers.Length; t++)
                y = DrawRankingTier(inner.width, y, snapshot.RankTiers[t], t,
                    snapshot.SelectedPlanId);
            Widgets.EndScrollView();
            Text.WordWrap = true;
        }

        private static readonly Color DragMarker = new Color(1f, 0.95f, 0.55f);

        private static float DrawRankingTier(
            float width, float y, List<RankedRow> rows, int tier, int planId)
        {
            float sectionHeight = PlannerStyle.SectionHeaderHeight
                + Mathf.Max(rows.Count, 1) * TreeLine;
            var sectionRect = new Rect(0f, y, width, sectionHeight);
            float rowsTop = y + PlannerStyle.SectionHeaderHeight;

            // While a drag hovers this tier, the nearest row boundary is the
            // insertion point: register it and remember where to draw the
            // marker line (after the rows, so it stays on top).
            float markerY = -1f;
            if (PlannerDrag.Active && Mouse.IsOver(sectionRect))
            {
                int index = rows.Count == 0
                    ? 0
                    : Mathf.Clamp(Mathf.RoundToInt(
                        (Event.current.mousePosition.y - rowsTop) / TreeLine),
                        0, rows.Count);
                markerY = rowsTop + index * TreeLine;
                PlannerDrag.SetDrop(StarRanking.Max - tier,
                    index < rows.Count ? rows[index].DefName : "");
            }

            using (GuiStateScope.Capture())
            {
                GUI.color = TierStarColor;
                PlannerStyle.SectionHeader(0f, y, width, TierStars[tier]);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(new Rect(0f, y, width, 20f),
                    PlannerLabels.TierPriorities[tier]);
            }
            y += PlannerStyle.SectionHeaderHeight;

            if (rows.Count == 0)
            {
                using (GuiStateScope.Capture())
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = PlannerStyle.CaptionText;
                    Widgets.Label(new Rect(TreeArrow, y, width - TreeArrow, TreeLine),
                        PlannerLabels.DragItemsHere);
                }
                y += TreeLine;
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    DrawRankingRow(new Rect(0f, y, width, TreeLine), rows[i], planId);
                    y += TreeLine;
                }
            }

            if (markerY >= 0f)
                Widgets.DrawBoxSolid(
                    new Rect(0f, markerY - 1f, width, 2f), DragMarker);
            return y;
        }

        private static void DrawRankingRow(Rect rowRect, RankedRow row, int planId)
        {
            bool dragged = PlannerDrag.Active
                && string.Equals(PlannerDrag.Payload, row.DefName,
                    System.StringComparison.Ordinal);
            if (dragged)
                Widgets.DrawBoxSolid(rowRect, DropTargetFill);
            else if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
                string? tip = ImplantTip(row.DefName);
                if (tip != null)
                    TooltipHandler.TipRegion(rowRect, tip);
            }

            if (row.IconDef != null)
                Widgets.DefIcon(new Rect(rowRect.x + 2f, rowRect.y + 2f,
                    TreeLine - 4f, TreeLine - 4f), row.IconDef);

            // Right to left: delete button, then the slot-count column.
            var deleteRect = new Rect(rowRect.xMax - 25f, rowRect.y + 1f, 22f, 22f);
            var countRect = new Rect(deleteRect.x - 32f, rowRect.y, 26f, TreeLine);
            using (GuiStateScope.Capture())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rowRect.x + TreeLine + 2f, rowRect.y,
                    countRect.x - rowRect.x - TreeLine - 4f, TreeLine), row.Label);
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(countRect, row.CountText);
            }

            if (Widgets.ButtonImage(deleteRect, TexButton.Delete))
            {
                PlannerCommands.RemoveImplant(planId, row.GoalId);
                return;
            }

            // Presses on the delete button belong to the button, not a drag.
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && Mouse.IsOver(rowRect)
                && !Mouse.IsOver(deleteRect))
            {
                PlannerDrag.OnPress(row.DefName, row.Label);
                Event.current.Use();
            }
        }

        // -------------------------------------------------------- Automation

        private AutomationSnapshot? automationFront;

        /// The Automation tab: the master enable toggle, then two columns —
        /// production (auto bills, bench limits, crafting skill,
        /// intermediaries, resource reserves) on the left and surgery
        /// (iteration + doctor assignment) on the right. Shared scalars read
        /// directly (no traversal); every control issues a synced command
        /// and renders the resulting published state.
        private void DrawAutomation(Rect rect, ImplannerStore store)
        {
            // A level mod stood automation down; none of these controls does
            // anything, so explain instead of offering them.
            if (!PlannerAutomation.Available)
            {
                DrawAutomationUnavailable(rect);
                return;
            }

            if (Event.current.type == EventType.Repaint || automationFront == null)
                automationFront = automation.Current(store);
            AutomationSnapshot snapshot = automationFront;
            PlannerModel model = store.Model;

            float leftHeight = ProductionColumnHeight(model, snapshot);
            float rightHeight = SurgeryColumnHeight(model, snapshot);
            float innerHeight = RowHeight + Pad + Mathf.Max(leftHeight, rightHeight);
            // Symmetric layout: the scrollbar gutter is reserved only while
            // the content overflows, both columns share one width, and the
            // right column ends flush at the content edge — the leftover
            // floor-rounding pixel widens the middle gap, never a margin.
            bool scrolls = innerHeight > rect.height;
            float innerWidth = scrolls ? rect.width - 16f : rect.width;
            float columnWidth = Mathf.Floor((innerWidth - ColumnGap) / 2f);
            Rect inner = new Rect(0f, 0f, innerWidth, innerHeight);
            Widgets.BeginScrollView(rect, ref automationScroll, inner);
            automation.ReserveFieldNames.Clear();

            bool enabled = !model.AutomationPaused;
            bool now = enabled;
            Widgets.CheckboxLabeled(new Rect(0f, 0f, columnWidth, RowHeight),
                PlannerLabels.OptEnable, ref now);
            if (now != enabled) PlannerCommands.SetAutomationPaused(!now);

            float top = RowHeight + Pad;
            DrawProductionColumn(new Rect(0f, top, columnWidth, leftHeight),
                model, snapshot);
            DrawSurgeryColumn(new Rect(innerWidth - columnWidth, top,
                columnWidth, rightHeight), model, snapshot);
            Widgets.EndScrollView();
        }

        private void DrawSurgeryColumn(
            Rect rect, PlannerModel model, AutomationSnapshot snapshot)
        {
            float width = rect.width;
            float y = rect.y;
            y += SectionHeader.Primary(rect.x, y, width,
                PlannerLabels.OptSurgery) + 2f;
            Widgets.Label(new Rect(rect.x, y, width, HeaderHeight),
                PlannerLabels.OptIteration);
            y += HeaderHeight + 2f;
            // Display order: tier iteration (the default) first; map display
            // index to the persisted enum values.
            int display = model.Iteration == IterationStrategy.ImplantTier ? 0 : 1;
            int clicked = SegmentedControl.Row(
                new Rect(rect.x, y, width, 30f),
                PlannerLabels.IterationModes, display);
            if (clicked >= 0 && clicked != display)
                PlannerCommands.SetIteration(clicked == 0
                    ? (int)IterationStrategy.ImplantTier
                    : (int)IterationStrategy.Colonist);
            y += 30f + Pad;

            bool autoFloor = model.AutoDoctorFloor;
            bool now = autoFloor;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, width, RowHeight),
                PlannerLabels.OptAutoFloor, ref now);
            if (now != autoFloor) PlannerCommands.SetAutoDoctorFloor(now);
            y += RowHeight + 2f;
            if (!model.AutoDoctorFloor)
            {
                // The manual minimum applies only while the automatic mode is
                // off; it is seeded from the best doctor when auto is
                // switched off. The indent is meaningful nesting: this row
                // exists only under the toggle above it.
                DrawStepperRow(rect.x + 12f, ref y, width - 12f,
                    PlannerLabels.OptManualFloor,
                    snapshot.ManualFloorText,
                    static () => PlannerCommands.SetManualDoctorFloor(
                        (ImplannerStore.Current?.Model.ManualDoctorFloor ?? 0) - 1),
                    static () => PlannerCommands.SetManualDoctorFloor(
                        (ImplannerStore.Current?.Model.ManualDoctorFloor ?? 0) + 1));
            }
            y += Pad;

            // Implant reservations: stock held back for manual use; surgery
            // automation waits until items beyond these counts exist.
            y += SectionHeader.Sub(rect.x, y, width,
                PlannerLabels.OptImplantReserves) + 2f;
            for (int i = 0; i < snapshot.ImplantReserves.Count; i++)
            {
                ReserveRow row = snapshot.ImplantReserves[i];
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(rect.x, y + 1f,
                        CompactRowHeight, CompactRowHeight), row.IconDef);
                Widgets.Label(new Rect(rect.x + CompactRowHeight + 6f, y,
                    width - 148f, CompactRowHeight + 2f), row.Label);
                // Buffer and control-name keys are precomputed on the row in
                // the gated snapshot build: a steady render pass never
                // concatenates strings.
                if (!automation.ReserveBuffers.TryGetValue(row.BufferKey, out string buffer))
                    buffer = row.Amount.ToStringCached();
                int value = row.Amount;
                automation.ReserveFieldNames.Add(row.FieldName);
                GUI.SetNextControlName(row.FieldName);
                NumericFieldRight(
                    new Rect(rect.x + width - 118f, y, 90f, CompactRowHeight + 2f),
                    ref value, ref buffer, 999f);
                automation.ReserveBuffers[row.BufferKey] = buffer;
                if (value != row.Amount)
                    PlannerCommands.SetImplantReserve(row.DefName, value);
                if (Widgets.ButtonImage(new Rect(rect.x + width - 22f, y,
                        CompactRowHeight, CompactRowHeight), TexButton.Delete))
                    PlannerCommands.SetImplantReserve(row.DefName, 0);
                y += CompactRowHeight + 4f;
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x, y, Mathf.Min(200f, width), RowHeight),
                    PlannerLabels.AddImplantReserve))
                OpenImplantReserveMenu(model);
        }

        /// Catalog implants (with an item to hold back) not already listed,
        /// alphabetically — the catalog's group-then-label order reads as
        /// random in a flat menu.
        private static void OpenImplantReserveMenu(PlannerModel model)
        {
            var options = new List<FloatMenuOption>();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
            {
                ImplantCatalogEntry entry = catalog[i];
                if (entry.Def.spawnThingOnRemoved == null) continue;
                string defName = entry.Def.defName;
                if (model.ImplantReserveOf(defName) > 0) continue;
                options.Add(new FloatMenuOption(entry.Label,
                    () => PlannerCommands.SetImplantReserve(defName, 1)));
            }
            options.Sort(static (a, b) => string.Compare(
                a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase));
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawProductionColumn(
            Rect rect, PlannerModel model, AutomationSnapshot snapshot)
        {
            float width = rect.width;
            float y = rect.y;
            y += SectionHeader.Primary(rect.x, y, width,
                PlannerLabels.OptProduction) + 2f;
            bool autoProduction = model.AutoProduction;
            bool now = autoProduction;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, width, RowHeight),
                PlannerLabels.OptAutoProduction, ref now);
            if (now != autoProduction) PlannerCommands.SetAutoProduction(now);
            y += RowHeight + 2f;
            if (!model.AutoProduction) return;

            DrawStepperRow(rect.x, ref y, width, PlannerLabels.OptConcurrency,
                snapshot.ConcurrencyText,
                static () => PlannerCommands.SetProductionConcurrency(
                    (ImplannerStore.Current?.Model.ProductionConcurrency ?? 1) - 1),
                static () => PlannerCommands.SetProductionConcurrency(
                    (ImplannerStore.Current?.Model.ProductionConcurrency ?? 1) + 1));

            bool idle = model.OnlyIdleBenches;
            now = idle;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, width, RowHeight),
                PlannerLabels.OptIdleBenches, ref now);
            if (now != idle) PlannerCommands.SetOnlyIdleBenches(now);
            y += RowHeight + 2f;

            DrawStepperRow(rect.x, ref y, width, PlannerLabels.OptProductionSkill,
                snapshot.ProductionSkillText,
                static () => PlannerCommands.SetProductionSkill(
                    (ImplannerStore.Current?.Model.ProductionSkill ?? 0) - 1),
                static () => PlannerCommands.SetProductionSkill(
                    (ImplannerStore.Current?.Model.ProductionSkill ?? 0) + 1));

            bool intermediaries = model.AllowIntermediaries;
            now = intermediaries;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, width, RowHeight),
                PlannerLabels.OptIntermediaries, ref now);
            if (now != intermediaries) PlannerCommands.SetAllowIntermediaries(now);
            y += RowHeight + 2f + Pad;

            y += SectionHeader.Sub(rect.x, y, width,
                PlannerLabels.OptReserves) + 2f;
            for (int i = 0; i < snapshot.Reserves.Count; i++)
            {
                ReserveRow row = snapshot.Reserves[i];
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(rect.x, y + 1f,
                        CompactRowHeight, CompactRowHeight), row.IconDef);
                Widgets.Label(new Rect(rect.x + CompactRowHeight + 6f, y,
                    width - 120f, CompactRowHeight + 2f), row.Label);
                if (!automation.ReserveBuffers.TryGetValue(
                        row.BufferKey, out string buffer))
                    buffer = row.Amount.ToStringCached();
                int value = row.Amount;
                automation.ReserveFieldNames.Add(row.FieldName);
                GUI.SetNextControlName(row.FieldName);
                NumericFieldRight(
                    new Rect(rect.x + width - 90f, y, 90f, CompactRowHeight + 2f),
                    ref value, ref buffer, 999999f);
                automation.ReserveBuffers[row.BufferKey] = buffer;
                if (value != row.Amount)
                    PlannerCommands.SetResourceReserve(row.DefName, value);
                y += CompactRowHeight + 4f;
            }
        }

        /// A numeric entry field with right-aligned text, reading like a
        /// number column. The shared text-field style is global GUI state,
        /// so the alignment is restored through try/finally.
        private static void NumericFieldRight(
            Rect rect, ref int value, ref string buffer, float max)
        {
            GUIStyle style = Text.CurTextFieldStyle;
            TextAnchor alignment = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            try
            {
                Widgets.TextFieldNumeric(rect, ref value, ref buffer, 0f, max);
            }
            finally
            {
                style.alignment = alignment;
            }
        }

        /// A label with -/value/+ controls right-aligned on one row. The
        /// label sits flush at x — indentation is the CALLER's statement of
        /// dependency, not this row's default.
        private static void DrawStepperRow(float x, ref float y, float width,
            string label, string valueText,
            System.Action decrement, System.Action increment)
        {
            Widgets.Label(new Rect(x, y + 2f, width - 90f, HeaderHeight),
                label);
            if (Widgets.ButtonText(new Rect(x + width - 86f, y, 24f, HeaderHeight), "-"))
                decrement();
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x + width - 60f, y, 34f, HeaderHeight), valueText);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(x + width - 24f, y, 24f, HeaderHeight), "+"))
                increment();
            y += HeaderHeight + 4f;
        }

        /// Layout heights: pure arithmetic over the snapshot and shared
        /// scalars (no traversal, no measurement).
        private static float SurgeryColumnHeight(
            PlannerModel model, AutomationSnapshot snapshot)
        {
            float height = SectionHeader.PrimaryHeight + 2f
                + HeaderHeight + 2f + 30f + Pad                  // iteration
                + RowHeight + 2f;                                // auto floor
            if (!model.AutoDoctorFloor)
                height += HeaderHeight + 4f;                     // manual skill
            height += Pad + SectionHeader.SubHeight + 2f         // reservations
                + snapshot.ImplantReserves.Count * (CompactRowHeight + 4f)
                + RowHeight;                                     // add button
            return height;
        }

        private static float ProductionColumnHeight(
            PlannerModel model, AutomationSnapshot snapshot)
        {
            float height = SectionHeader.PrimaryHeight + 2f
                + RowHeight + 2f;                                // auto bills
            if (model.AutoProduction)
                height += HeaderHeight + 4f                      // concurrency
                    + RowHeight + 2f                             // idle benches
                    + HeaderHeight + 4f                          // crafting skill
                    + RowHeight + 2f                             // intermediaries
                    + Pad + SectionHeader.SubHeight + 2f         // keep-in-stock
                    + snapshot.Reserves.Count * (CompactRowHeight + 4f);
            return height;
        }

        /// Shown in place of the automation controls when a level mod is
        /// active (PlannerAutomation). Static text only — no snapshot, no
        /// store read.
        private static void DrawAutomationUnavailable(Rect rect)
        {
            const float ColumnWidth = 560f;
            float width = Mathf.Min(ColumnWidth, rect.width);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(rect.x, rect.y, width, 32f),
                    PlannerLabels.AutomationOffTitle);
                Text.Font = GameFont.Small;
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(
                    new Rect(rect.x, rect.y + 36f, width, rect.height - 36f),
                    PlannerLabels.AutomationOffBody);
            }
        }

        // ----------------------------------------------------------- Options

        /// Reserved for display options; nothing ships here yet.
        private static void DrawOptionsPlaceholder(Rect rect)
        {
            using (GuiStateScope.Capture())
            {
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, RowHeight),
                    PlannerLabels.OptionsEmpty);
            }
        }
    }
}
