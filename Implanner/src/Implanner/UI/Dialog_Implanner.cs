using System.Collections.Generic;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using RimWorld.Planet;
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
        private enum Tab { Overview, Plans, Automation, Options, Help }

        // Session-scoped view state: survives close/reopen, never persisted.
        private static Tab curTab = Tab.Overview;

        /// Whether any Implanner dialog is open. Maintained by
        /// PreOpen/PostClose for Patch_MouseoverReadout, which runs every
        /// OnGUI frame and must not scan the window stack.
        internal static bool AnyOpen;

        private readonly OverviewState overview = new OverviewState();
        private readonly PlansState plans = new PlansState();
        private readonly AutomationState automation = new AutomationState();
        private readonly AutomationTips automationTips = new AutomationTips();
        private readonly OptionsState options = new OptionsState();
        private readonly OptionsTips optionsTips = new OptionsTips();
        private readonly HelpTabView help = new HelpTabView();
        private readonly StripTipSource productionTip =
            new StripTipSource(StripTipKind.Production);
        private readonly StripTipSource surgeryTip =
            new StripTipSource(StripTipKind.Surgery);

        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: Find.World identity.
        // Value: the world's ImplannerStore component reference (or null
        //   while no world is loaded).
        // Dependencies: the current world only; the component lives as
        //   long as its world.
        // Refresh policy: immediate when the world reference changes
        //   (checked once per WindowUpdate and on a null hit in OnGUI).
        // Equality policy: the same world reuses the reference without a
        //   component scan.
        // Teardown: PostClose drops both references.
        private ImplannerStore? store;
        private World? storeWorld;

        private ImplannerStore? EnsureStore()
        {
            World? world = Find.World;
            if (!ReferenceEquals(world, storeWorld))
            {
                storeWorld = world;
                store = world?.GetComponent<ImplannerStore>();
            }
            return store;
        }

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
            help.Reset();
        }

        public override void PostClose()
        {
            base.PostClose();
            AnyOpen = false;
            help.FlushPendingWrites();
            overview.Release();
            plans.Release();
            automation.Release();
            automationTips.Release();
            options.Release();
            optionsTips.Release();
            help.ReleaseWindowData();
            productionTip.Release();
            surgeryTip.Release();
            // The presenter's frozen tip and geometry belong to this
            // window's hover session; idempotent.
            StructuredTipPresenter.Reset();
            overviewFront = null;
            plansFront = null;
            automationFront = null;
            optionsFront = null;
            store = null;
            storeWorld = null;
            PlannerDrag.Cancel();
            tabs = null;
            tabsLanguageStamp = -1;
        }

        public override void DoWindowContents(Rect inRect)
        {
            ImplannerStore? store = this.store ?? EnsureStore();
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
                    case Tab.Options: DrawOptions(content, store); break;
                    default: help.Draw(content); break;
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
        /// changes desync Layout from later passes. The active tab's front
        /// snapshot refreshes here too, so every pass of the coming frame
        /// (Layout, the event, Repaint) draws one snapshot; a tab switched
        /// mid-frame takes the null-fallback in its draw method once.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (pendingWindowRect.TryConsume(out Rect nextWindowRect))
                windowRect = nextWindowRect;
            help.FlushPendingWrites();
            ImplannerStore? store = EnsureStore();
            if (store == null) return;
            UiVersion.ObserveCurrentMetrics();
            // Builders read translated labels (priority names, No plan), so
            // the label cache must be current before any snapshot rebuilds
            // here: WindowUpdate runs before the first draw of a new window.
            PlannerLabels.Ensure();
            switch (curTab)
            {
                case Tab.Overview: overviewFront = overview.Current(store); break;
                case Tab.Plans: plansFront = plans.Current(store); break;
                case Tab.Automation:
                    if (PlannerAutomation.Available)
                        automationFront = automation.Current(store);
                    break;
                case Tab.Options: optionsFront = options.Current(store); break;
            }
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
                new TabRecord(PlannerLabels.TabHelp,
                    static () => curTab = Tab.Help, () => curTab == Tab.Help),
            };
        }

        // ---------------------------------------------------------- Overview

        private OverviewSnapshot? overviewFront;

        private void DrawOverview(Rect rect, ImplannerStore store)
        {
            // The front snapshot refreshes in WindowUpdate; the fallback
            // covers only a tab switched mid-frame.
            OverviewSnapshot snapshot = overviewFront ??= overview.Current(store);
            OverviewData data = snapshot.Data;

            const float RightWidth = 300f;
            Rect left = new Rect(rect.x, rect.y, rect.width - RightWidth - Pad, rect.height);
            Rect right = new Rect(left.xMax + Pad, rect.y, RightWidth, rect.height);

            // Colony summary strip above the controls and table: three
            // columns of three lines each.
            float stripHeight = Pad * 2f + 66f;
            Rect stripRect = new Rect(left.x, left.y, left.width, stripHeight);
            DrawColonyStrip(stripRect, data);

            // Location selector and group-by selector.
            Rect selectorRect = new Rect(left.x, stripRect.yMax + Pad, 240f, RowHeight);
            string groupingLabel = data.GroupingIndex >= 0
                ? data.GroupingLabels[data.GroupingIndex]
                : "";
            if (Widgets.ButtonText(selectorRect, groupingLabel))
                OpenGroupingMenu(data);
            Rect groupByRect = new Rect(selectorRect.xMax + 8f, selectorRect.y, 180f, RowHeight);
            if (Widgets.ButtonText(groupByRect, snapshot.GroupByLabel))
                OpenGroupByMenu(data);

            // Colonist table takes the remaining left height; colonist
            // details the full right height.
            Rect tableRect = new Rect(left.x, selectorRect.yMax + Pad, left.width,
                left.yMax - selectorRect.yMax - Pad);
            DrawColonistTable(tableRect, snapshot);
            DrawColonistDetails(right, snapshot);
        }

        /// The colony summary strip: three equal columns of three lines on
        /// the darker band fill — the colony (name, colonist count,
        /// automation chip), production (coverage percent, free-stock and
        /// queued counts, activity sentence), and surgery (installed
        /// percent, installed units, active batch sentence). Hovering the
        /// production or surgery column opens its per-implant breakdown
        /// tooltip (StripTipSource).
        private void DrawColonyStrip(Rect rect, OverviewData snapshot)
        {
            productionTip.Offer(snapshot);
            surgeryTip.Offer(snapshot);
            PlannerStyle.Panel(rect);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f,
                rect.width - 2f, rect.height - 2f), DetailHeaderFill);
            Rect body = rect.ContractedBy(Pad);
            float colWidth = (body.width - Pad * 2f) / 3f;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                float lineH = Mathf.Max(22f,
                    Mathf.Ceil(Text.LineHeightOf(GameFont.Small)));

                var top = new Rect(body.x, body.y, colWidth, lineH);
                var mid = new Rect(body.x, body.y + 22f, colWidth, lineH);
                var low = new Rect(body.x, body.y + 44f, colWidth, lineH);
                Widgets.Label(top, snapshot.ColonyLabelText);
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(mid, snapshot.StatsText);
                GUI.color = Color.white;
                DrawAutomationChip(low, snapshot);

                top.x = mid.x = low.x = body.x + colWidth + Pad;
                StructuredTipPresenter.TipRegion(
                    new Rect(top.x, body.y, colWidth, body.height), productionTip);
                Widgets.Label(top, snapshot.ProductionTitleText);
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(mid, snapshot.StockQueuedText);
                GUI.color = Color.white;
                if (snapshot.ProductionSubText.Length > 0)
                {
                    GUI.color = snapshot.ProductionSubActive
                        ? PlannerStyle.ActiveText
                        : PlannerStyle.CaptionText;
                    Widgets.Label(low, snapshot.ProductionSubText);
                    GUI.color = Color.white;
                }

                top.x = mid.x = low.x = body.x + (colWidth + Pad) * 2f;
                StructuredTipPresenter.TipRegion(
                    new Rect(top.x, body.y, colWidth, body.height), surgeryTip);
                Widgets.Label(top, snapshot.SurgeryTitleText);
                GUI.color = PlannerStyle.CaptionText;
                Widgets.Label(mid, snapshot.UnitsText);
                GUI.color = Color.white;
                if (snapshot.SurgeryBatchText.Length > 0)
                {
                    // The tier stars sit at the end so they can be dropped
                    // when the column is too narrow for the full sentence
                    // (width measured at snapshot build, never wrapped or
                    // clipped).
                    GUI.color = PlannerStyle.ActiveText;
                    Widgets.Label(low,
                        snapshot.SurgeryBatchWidth <= low.width
                            ? snapshot.SurgeryBatchText
                            : snapshot.SurgeryBatchShortText);
                    GUI.color = Color.white;
                }
            }
        }

        // Automation chip palette, mirroring WorkRoles' auto-managed
        // indicator; the off state swaps the green tones for muted gray.
        private static readonly Color ChipOnText = new Color(0.55f, 0.8f, 0.45f);
        private static readonly Color ChipOnFill = new Color(0.10f, 0.16f, 0.09f, 0.95f);
        private static readonly Color ChipOnOutline = new Color(0.55f, 0.8f, 0.45f, 0.65f);
        private static readonly Color ChipOffText = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color ChipOffFill = new Color(0.13f, 0.13f, 0.13f, 0.95f);
        private static readonly Color ChipOffOutline = new Color(0.62f, 0.62f, 0.62f, 0.40f);

        /// The strip's automation state chip: a status dot and label on a
        /// tinted pill, sized to the label's width measured at snapshot build.
        private static void DrawAutomationChip(Rect row, OverviewData snapshot)
        {
            const float ChipHeight = 20f;
            const float DotSize = 8f;
            bool on = snapshot.AutomationOn;
            float width = Mathf.Min(row.width,
                22f + snapshot.AutomationTextWidth + 8f);
            var chip = new Rect(row.x, row.y + (row.height - ChipHeight) / 2f,
                width, ChipHeight);
            PixelBox.SolidWithOutline(chip,
                on ? ChipOnFill : ChipOffFill,
                on ? ChipOnOutline : ChipOffOutline);
            GUI.color = on ? ChipOnText : ChipOffText;
            GUI.DrawTexture(new Rect(chip.x + 8f,
                chip.y + (ChipHeight - DotSize) / 2f, DotSize, DotSize),
                Patches.ImplannerTex.CircleFill);
            Widgets.Label(new Rect(chip.x + 22f, chip.y,
                chip.width - 24f, ChipHeight), snapshot.AutomationText);
            GUI.color = Color.white;
        }

        private void OpenGroupingMenu(OverviewData snapshot)
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

        private void OpenGroupByMenu(OverviewData snapshot)
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
                    PlannerLabels.ColShootingTip, center: true);
                DrawSortHeader(header.x + xMelee, header.y, SkillColWidth,
                    PlannerLabels.ColMelee, OverviewColumn.Melee,
                    PlannerLabels.ColMeleeTip, center: true);
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
            // The collapsed flags are resolved per row behind the fold
            // revision, so no set lookup runs here.
            bool[] collapsed = overview.CollapsedFlags(snapshot);
            List<OverviewRow> rows = snapshot.Rows;
            int visible = 0;
            bool skipping = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Header)
                {
                    skipping = collapsed[i];
                    visible++;
                }
                else if (!skipping)
                    visible++;
            }

            Rect outer = new Rect(rect.x, header.yMax + 2f, rect.width,
                rect.height - PlannerStyle.SectionHeaderHeight - 2f);
            Rect inner = new Rect(0f, 0f, contentWidth, visible * RowHeight);
            Widgets.BeginScrollView(outer, ref overviewScroll, inner);
            try
            {
                DrawColonistRows(inner.width, rows, collapsed, nameWidth, planWidth,
                    progressWidth, stateWidth, xName, xShooting, xMelee, xPlan,
                    xProgress, xPriority, xState);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawColonistRows(float width, List<OverviewRow> rows,
            bool[] collapsed, float nameWidth, float planWidth,
            float progressWidth, float stateWidth, float xName, float xShooting,
            float xMelee, float xPlan, float xProgress, float xPriority,
            float xState)
        {
            float y = 0f;
            bool skipping = false;
            for (int i = 0; i < rows.Count; i++)
            {
                OverviewRow row = rows[i];
                if (row.Header)
                {
                    skipping = collapsed[i];
                    DrawTableGroupHeader(new Rect(0f, y, width, RowHeight), row,
                        skipping);
                    y += RowHeight;
                    continue;
                }
                if (skipping) continue;
                Rect rowRect = new Rect(0f, y, width, RowHeight);
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
                    PlannerStyle.ProgressBar(barRect, row.Progress);
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
        private void DrawTableGroupHeader(Rect rect, OverviewRow row, bool collapsed)
        {
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
                overview.ToggleTableGroup(row.SectionKey);
        }

        /// A clickable column header; the active sort column shows a
        /// direction arrow after the label (bar-order Name shows none).
        private void DrawSortHeader(float x, float y, float width,
            string label, OverviewColumn column, string? tip,
            bool center = false)
        {
            var cell = new Rect(x, y, width, 20f);
            if (center)
            {
                // Narrow value columns (S/M) center the label exactly like
                // the value cells below; the sort arrow flips to the
                // label's left where the free space is.
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(cell, label);
                Text.Anchor = TextAnchor.MiddleLeft;
            }
            else
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
                float arrowX = center
                    ? x + (width - Mathf.Min(labelWidth, width)) / 2f - 14f
                    : x + Mathf.Min(labelWidth, width - 14f) + 2f;
                GUI.DrawTexture(new Rect(arrowX, y + 4f, 12f, 12f),
                    up ? TexButton.ReorderUp : TexButton.ReorderDown);
            }
            if (Widgets.ButtonInvisible(cell))
                overview.ClickColumn(column);
        }

        // Distinct payload TYPES per paintable column. Vanilla keys the paint
        // session on typeof(Payload) and matches options by payload value
        // (Widgets.Dropdown: dropdownPainting_Type / object.Equals on the
        // payload), so two int-payload columns would paint into each other:
        // a plan drag over a Priority cell would set that pawn's priority to
        // the plan id, and vice versa. A struct per column keeps the
        // sessions apart; value equality is what the match needs.

        /// Plan column payload: the plan id (0 = no plan).
        private readonly struct PlanChoice : System.IEquatable<PlanChoice>
        {
            internal readonly int Id;
            internal PlanChoice(int id) => Id = id;
            public bool Equals(PlanChoice other) => Id == other.Id;
            public override bool Equals(object obj) => obj is PlanChoice o && Equals(o);
            public override int GetHashCode() => Id;
        }

        /// Priority column payload: the priority level (0 first, 4 last).
        private readonly struct PriorityChoice : System.IEquatable<PriorityChoice>
        {
            internal readonly int Level;
            internal PriorityChoice(int level) => Level = level;
            public bool Equals(PriorityChoice other) => Level == other.Level;
            public override bool Equals(object obj) => obj is PriorityChoice o && Equals(o);
            public override int GetHashCode() => Level;
        }

        // Static cached delegates: the dropdown draws every row every frame
        // and must not allocate on cache hits. The menu generators run on
        // click, and while paint-dragging vanilla calls the generator once
        // per hovered cell per pass (its option list is rebuilt to find the
        // dragged payload); that cost is bounded to the drag's duration.
        private static readonly System.Func<OverviewRow, PriorityChoice> PriorityPayloadGetter =
            static row => new PriorityChoice(row.Priority);

        private static readonly
            System.Func<OverviewRow, IEnumerable<Widgets.DropdownMenuElement<PriorityChoice>>>
            PriorityMenuGenerator = GeneratePriorityMenu;

        private static IEnumerable<Widgets.DropdownMenuElement<PriorityChoice>>
            GeneratePriorityMenu(OverviewRow row)
        {
            int pawnId = row.PawnId;
            for (int level = 0; level <= 4; level++)
            {
                int captured = level;
                yield return new Widgets.DropdownMenuElement<PriorityChoice>
                {
                    option = new FloatMenuOption(PlannerLabels.PriorityLabel(captured),
                        () => PlannerCommands.SetPawnPriority(pawnId, captured)),
                    payload = new PriorityChoice(captured),
                };
            }
        }

        private static readonly System.Func<OverviewRow, PlanChoice> PlanPayloadGetter =
            static row => new PlanChoice(row.PlanId);

        private static readonly
            System.Func<OverviewRow, IEnumerable<Widgets.DropdownMenuElement<PlanChoice>>>
            PlanMenuGenerator = GeneratePlanMenu;

        private static IEnumerable<Widgets.DropdownMenuElement<PlanChoice>> GeneratePlanMenu(
            OverviewRow row)
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) yield break;
            int pawnId = row.PawnId;
            yield return new Widgets.DropdownMenuElement<PlanChoice>
            {
                option = new FloatMenuOption(PlannerLabels.NoPlan,
                    () => PlannerCommands.AssignPlan(pawnId, 0)),
                payload = new PlanChoice(0),
            };
            IReadOnlyList<Plan> allPlans = store.Model.Plans;
            for (int i = 0; i < allPlans.Count; i++)
            {
                int planId = allPlans[i].Id;
                yield return new Widgets.DropdownMenuElement<PlanChoice>
                {
                    option = new FloatMenuOption(allPlans[i].Name,
                        () => PlannerCommands.AssignPlan(pawnId, planId)),
                    payload = new PlanChoice(planId),
                };
            }
        }

        private void DrawColonistDetails(Rect rect, OverviewSnapshot snapshot)
        {
            PlannerStyle.Panel(rect);
            Rect body = rect.ContractedBy(Pad);
            IReadOnlyList<DetailRow> details =
                overview.Details(snapshot, body.width);
            bool hasSelection = overview.DetailTitle.Length > 0;

            int headerCount = 0;
            for (int i = 0; i < details.Count; i++)
                if (details[i].Header)
                    headerCount++;
            float innerHeight = headerCount * PlannerStyle.SectionHeaderHeight
                + (details.Count - headerCount) * CompactRowHeight
                + (headerCount > 0 ? (headerCount - 1) * PlannerStyle.SectionGap : 0f);

            // Header block: name and percent plus one status sentence, on a
            // darker band that separates it from the sections below. The
            // header spans the full body width regardless of whether the
            // list below reserves a scroll gutter.
            float contentH = 30f
                + (hasSelection ? overview.DetailStatusHeight : 0f);
            float outerTop = hasSelection ? contentH + 14f : 34f;
            if (hasSelection)
            {
                float bandH = Pad + contentH + 5f;
                Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f,
                    rect.width - 2f, bandH), DetailHeaderFill);
                Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f + bandH,
                    rect.width - 2f, rect.height - 2f - bandH),
                    PlannerStyle.PanelTint);
                // The divider between the two halves: one device pixel in
                // the outer border color.
                Widgets.DrawBoxSolid(PixelBox.HairlineHorizontal(
                    rect.x + 1f, rect.y + 1f + bandH, rect.width - 2f),
                    SegmentedControl.PanelOutline);
            }
            else
                Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f,
                    rect.width - 2f, rect.height - 2f), PlannerStyle.PanelTint);

            Rect outer = new Rect(body.x, body.y + outerTop,
                body.width, body.height - outerTop);
            bool scrolls = innerHeight > outer.height;
            float alignedWidth = scrolls ? body.width - ScrollGutter : body.width;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(body.x, body.y, body.width, 30f),
                hasSelection ? overview.DetailTitle : PlannerLabels.ColonistDetails);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(body.x, body.y, body.width, 30f),
                overview.DetailPercent);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (hasSelection)
            {
                using (GuiStateScope.Capture())
                {
                    Text.WordWrap = true;
                    GUI.color = PlannerStyle.HeaderText;
                    Widgets.Label(new Rect(body.x, body.y + 30f, body.width,
                        overview.DetailStatusHeight), overview.DetailStatusText);
                }
            }
            if (details.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(body.x, body.y + outerTop, body.width, RowHeight),
                    PlannerLabels.NoSelection);
                GUI.color = Color.white;
                return;
            }

            Rect inner = new Rect(0f, 0f, alignedWidth, innerHeight);
            Widgets.BeginScrollView(outer, ref detailScroll, inner);
            try
            {
                DrawDetailRows(inner.width, details);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawDetailRows(float width, IReadOnlyList<DetailRow> details)
        {
            float y = 0f;
            for (int i = 0; i < details.Count; i++)
            {
                DetailRow row = details[i];
                if (row.Header)
                {
                    if (i > 0) y += PlannerStyle.SectionGap;
                    float headerY = y;
                    if (row.Stars) GUI.color = PlannerStyle.TierStarColor;
                    y += PlannerStyle.SectionHeader(0f, y, width, row.Label);
                    GUI.color = Color.white;
                    using (GuiStateScope.Capture())
                    {
                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleRight;
                        GUI.color = PlannerStyle.HeaderText;
                        Widgets.Label(new Rect(0f, headerY, width, 20f),
                            row.StatusText);
                    }
                    continue;
                }
                GUI.color = row.Satisfied ? Color.white
                    : row.Blocked ? PlannerStyle.BlockedText
                    : row.Active ? PlannerStyle.ActiveText
                    : PlannerStyle.PendingText;
                Text.Anchor = TextAnchor.MiddleLeft;
                float detailTextH = Mathf.Max(
                    CompactRowHeight, TinyText.LineHeight);
                float detailTextY = y
                    + (CompactRowHeight - detailTextH) / 2f;
                TinyText.Label(new Rect(0f, detailTextY,
                    width * 0.62f, detailTextH),
                    row.Label);
                Text.Anchor = TextAnchor.MiddleRight;
                TinyText.Label(new Rect(width * 0.62f, detailTextY,
                    width * 0.38f, detailTextH), row.StatusText);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                y += CompactRowHeight;
            }
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
            // Refreshed in WindowUpdate; the fallback covers a mid-frame
            // tab switch only.
            PlansSnapshot snapshot = plansFront ??= plans.Current(store);

            // The picker carries the longest text (implant labels plus
            // their override captions), so it takes the larger share of
            // the room beside the plan list; the tier panel's rows are
            // icon, label, count and a button.
            const float ListWidth = 230f;
            const float RankingsShare = 0.4f;
            float paneRoom = rect.width - ListWidth - Pad * 2f;
            float rankingsWidth = Mathf.Floor(paneRoom * RankingsShare);
            Rect list = new Rect(rect.x, rect.y, ListWidth, rect.height);
            Rect center = new Rect(list.xMax + Pad, rect.y,
                paneRoom - rankingsWidth, rect.height);
            Rect rankingsCol = new Rect(center.xMax + Pad, rect.y, rankingsWidth, rect.height);

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

        /// The plan list as cards: name with the base-plan link right-aligned
        /// beside it, counts caption with a right-aligned percent, and a
        /// thin delivery-progress bar, framed by the shared
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
            try
            {
                DrawPlanCards(inner.width, cardHeight, nameH, captionH, snapshot);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            // Bottom row: New plan beside compact Import/Export.
            float shareWidth = Mathf.Floor(rect.width * 0.24f);
            Rect addRect = new Rect(rect.x, frame.yMax + Pad,
                rect.width - shareWidth * 2f - 8f, RowHeight);
            if (Widgets.ButtonText(addRect, PlannerLabels.AddPlan))
                Find.WindowStack.Add(new Dialog_NewPlan());
            if (Widgets.ButtonText(new Rect(addRect.xMax + 4f, addRect.y,
                    shareWidth, RowHeight), PlannerLabels.ImportPlans))
                Find.WindowStack.Add(new Dialog_ImportPlans());
            if (Widgets.ButtonText(new Rect(addRect.xMax + shareWidth + 8f,
                    addRect.y, shareWidth, RowHeight), PlannerLabels.ExportPlans))
                Find.WindowStack.Add(new Dialog_ExportPlans());
        }

        private void DrawPlanCards(float innerWidth, float cardHeight,
            float nameH, float captionH, PlansSnapshot snapshot)
        {
            using (GuiStateScope.Capture())
            {
                Text.WordWrap = false;
                // One logical pixel short of the clip edge: a device-grid
                // right border rounded up at a fractional scale would land
                // outside the scroll group's clip and vanish.
                float cardWidth = innerWidth - 1f;
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
                    float nameWidth = textWidth;
                    if (row.ExtendsText.Length > 0)
                    {
                        // The base-plan link sits right-aligned on the name
                        // row (the caption row below has no room beside the
                        // percent); the name clips against its width
                        // measured at snapshot build, capped at half the
                        // card.
                        float extendsWidth = Mathf.Min(
                            row.ExtendsWidth, textWidth * 0.5f);
                        using (GuiStateScope.Capture())
                        {
                            Text.Anchor = TextAnchor.MiddleRight;
                            GUI.color = PlannerStyle.CaptionText;
                            TinyText.Label(new Rect(
                                card.xMax - CardPad - extendsWidth,
                                card.y + 4f, extendsWidth, nameH),
                                row.ExtendsText);
                        }
                        nameWidth = textWidth - extendsWidth - 6f;
                    }
                    Widgets.Label(new Rect(card.x + CardPad, card.y + 4f,
                        nameWidth, nameH), row.Name);

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
        }

        // --------------------------------------------- Plans: center column

        private void DrawPlanEditor(Rect rect, ImplannerStore store, PlansSnapshot snapshot)
        {
            // Row labels stay on one line; overflow clips instead of wrapping.
            Text.WordWrap = false;
            float y = rect.y;

            // Header: plan name (width measured at snapshot build), rename
            // icon, delete button.
            Text.Font = GameFont.Medium;
            float nameWidth = snapshot.SelectedPlanNameWidth;
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
            // Folded flags resolved per row behind the fold revision: no
            // set lookup in the row loop.
            bool[] folded = plans.FoldedFlags(snapshot);
            float treeHeight = TreeVisibleHeight(snapshot.Tree, folded);
            Rect inner = new Rect(0f, 0f,
                treeHeight > outer.height ? outer.width - 16f : outer.width,
                treeHeight);
            // Captions fitted to this row width behind their own gate
            // (snapshot identity + width): a resize or a scroll gutter
            // appearing re-fits them, an unchanged frame indexes them.
            PickerCaption[] captions = plans.Captions(snapshot, inner.width);
            Widgets.BeginScrollView(outer, ref pickerScroll, inner);
            try
            {
                DrawTree(inner.width, snapshot.Tree, folded, captions, plans,
                    snapshot.SelectedPlanId);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            Text.WordWrap = true;
        }

        private const float TreeLine = PickerGeometry.Line;
        private const float TreeIndent = PickerGeometry.Indent;
        private const float TreeArrow = PickerGeometry.Arrow;

        /// Rows hidden inside folded subtrees contribute nothing. Pure
        /// arithmetic over already-built rows and their resolved fold flags
        /// (true = collapsed node; every group starts expanded).
        private static float TreeVisibleHeight(List<PickerRow> tree, bool[] folded)
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
                if (folded[i])
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
        /// Captions arrive fitted to the row width (parallel to the tree);
        /// a truncated one keeps its end and shows the full text as a
        /// tooltip when hovered, in place of the implant tooltip.
        private static void DrawTree(float width, List<PickerRow> tree,
            bool[] folded, PickerCaption[] captions, PlansState state, int planId)
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
                    bool expanded = !folded[i];
                    Widgets.DrawHighlightIfMouseover(rowRect);
                    GUI.DrawTexture(
                        new Rect(x, y + (TreeLine - TreeArrow) / 2f, TreeArrow, TreeArrow),
                        expanded ? TexButton.Collapse : TexButton.Reveal);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(x + TreeArrow + 2f, y,
                        width - x - TreeArrow - 2f, TreeLine), row.Label);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonInvisible(rowRect))
                        state.ToggleSection(row.SectionKey);
                    if (!expanded) foldedDepth = row.Depth;
                    y += TreeLine;
                    continue;
                }

                PickerCaption caption = captions[i];
                float labelX = x + TreeArrow + 2f;
                float labelRight = rowRect.xMax - PickerGeometry.CheckboxZone;
                Rect captionRect = default;
                if (caption.Text != null)
                {
                    captionRect = new Rect(labelRight - caption.Width, y,
                        caption.Width, TreeLine);
                    labelRight = captionRect.x - PickerGeometry.LabelCaptionGap;
                }
                if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawHighlight(rowRect);
                    if (caption.Truncated && Mouse.IsOver(captionRect))
                    {
                        TooltipHandler.TipRegion(captionRect, caption.Full);
                    }
                    else
                    {
                        // Tooltip content builds lazily for the hovered row
                        // only and is cached per definition.
                        string? tip = ImplantTip(row.DefName);
                        if (tip != null)
                            TooltipHandler.TipRegion(rowRect, tip);
                    }
                }
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(x, y + 2f, TreeLine - 4f, TreeLine - 4f),
                        row.IconDef);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(labelX, y,
                    Mathf.Max(0f, labelRight - labelX), TreeLine), row.Label);
                if (caption.Text != null)
                {
                    using (GuiStateScope.Capture())
                    {
                        Text.Anchor = TextAnchor.MiddleRight;
                        GUI.color = row.OverridesLabel.Length > 0
                            ? OverrideText : PlannerStyle.CaptionText;
                        TinyText.Label(captionRect, caption.Text);
                    }
                }
                Text.Anchor = TextAnchor.UpperLeft;

                bool now = row.Selected;
                Widgets.Checkbox(new Vector2(rowRect.xMax - 26f, y + 1f),
                    ref now, 20f, disabled: false, paintable: true);
                if (now != row.Selected)
                {
                    // A slot the game only offers on an artificial part the
                    // plan does not cover yet: the requirement dialog picks
                    // the host replacement and adds both, or nothing. The
                    // row keeps drawing the published (unchecked) state
                    // until the commands land.
                    if (now && row.Requirement != null)
                        Find.WindowStack.Add(new Dialog_ImplantRequirements(
                            planId, row, row.Requirement));
                    else
                        PlannerCommands.SetImplantSlot(planId, row.DefName, row.Ordinal, now);
                }
                y += TreeLine;
            }
        }

        private static string? ImplantTip(string defName)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            return entry != null ? PlannerTips.ForImplant(entry) : null;
        }

        // -------------------------------------- Plans: ranking right column

        private static readonly Color DropTargetFill = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color DetailHeaderFill = new Color(0f, 0f, 0f, 0.25f);

        /// The plan's ranking panel: a title, a collapsible help section
        /// explaining the star classification, then one combined list with
        /// five star tiers over the plan's OWN implants (inherited goals are
        /// ranked where their own plan ranks them). A new implant lands in
        /// the three-star tier; dragging a row to another tier re-ranks the
        /// implant kind globally.
        private void DrawPlanRankings(Rect rect, PlansSnapshot snapshot)
        {
            Text.WordWrap = false;
            PlannerStyle.ShadedPanel(rect);
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
            try
            {
                float y = 0f;
                for (int t = 0; t < snapshot.RankTiers.Length; t++)
                    y = DrawRankingTier(inner.width, y, snapshot.RankTiers[t], t,
                        snapshot.SelectedPlanId);
            }
            finally
            {
                Widgets.EndScrollView();
            }
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
                GUI.color = PlannerStyle.TierStarColor;
                PlannerStyle.SectionHeader(0f, y, width,
                    PlannerStyle.TierStars[tier]);
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
                PlannerCommands.RemoveImplant(planId, row.DefName);
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
        /// (iteration + doctor assignment) on the right. Every value drawn
        /// comes from the automation snapshot; the click paths inside the
        /// static stepper delegates read the live model once, issue a
        /// synced command, and the next snapshot renders the published state.
        private void DrawAutomation(Rect rect, ImplannerStore store)
        {
            // A level mod stood automation down; none of these controls does
            // anything, so explain instead of offering them.
            if (!PlannerAutomation.Available)
            {
                DrawAutomationUnavailable(rect);
                return;
            }

            // Refreshed in WindowUpdate; the fallback covers a mid-frame
            // tab switch only.
            AutomationSnapshot snapshot = automationFront ??= automation.Current(store);
            automationTips.Ensure();
            AutomationTips tips = automationTips;

            float leftHeight = ProductionColumnHeight(snapshot);
            float rightHeight = SurgeryColumnHeight(snapshot);
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
            try
            {
                automation.ReserveFieldNames.Clear();

                bool enabled = !snapshot.AutomationPaused;
                bool now = enabled;
                var enableRect = new Rect(0f, 0f, columnWidth, RowHeight);
                tips.Enable.Region(enableRect);
                Widgets.CheckboxLabeled(enableRect, PlannerLabels.OptEnable, ref now);
                if (now != enabled)
                {
                    // Turning automation OFF goes through the hand-back
                    // dialog: the pause command is only issued from its OK
                    // (with the bill cleanup), so Cancel/ESC keeps automation
                    // on. With no owned bills it pauses directly.
                    if (now) PlannerCommands.SetAutomationPaused(false);
                    else Dialog_AutomationCleanup.ShowToTurnOffAutomation(store);
                }

                float top = RowHeight + Pad;
                DrawProductionColumn(new Rect(0f, top, columnWidth, leftHeight),
                    snapshot, tips);
                DrawSurgeryColumn(new Rect(innerWidth - columnWidth, top,
                    columnWidth, rightHeight), store, snapshot, tips);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawSurgeryColumn(Rect rect, ImplannerStore store,
            AutomationSnapshot snapshot, AutomationTips tips)
        {
            float width = rect.width;
            float y = rect.y;
            y += SectionHeader.Primary(rect.x, y, width,
                PlannerLabels.OptSurgery) + 2f;
            tips.Iteration.Region(
                new Rect(rect.x, y, width, HeaderHeight + 2f + 30f));
            Widgets.Label(new Rect(rect.x, y, width, HeaderHeight),
                PlannerLabels.OptIteration);
            y += HeaderHeight + 2f;
            // Display order: tier batching (the default) first; the snapshot
            // table maps display index to the persisted enum values.
            int display = snapshot.IterationDisplayIndex;
            int clicked = SegmentedControl.Row(
                new Rect(rect.x, y, width, 30f),
                PlannerLabels.IterationModes, display);
            if (clicked >= 0 && clicked != display)
                PlannerCommands.SetIteration(
                    (int)AutomationSnapshot.IterationByDisplay[clicked]);
            y += 30f + Pad;

            DrawStepperRow(rect.x, ref y, width,
                PlannerLabels.OptSurgeryConcurrency,
                snapshot.SurgeryConcurrencyText,
                static () => PlannerCommands.SetSurgeryConcurrency(
                    (ImplannerStore.Current?.Model.SurgeryConcurrency ?? 1) - 1),
                static () => PlannerCommands.SetSurgeryConcurrency(
                    (ImplannerStore.Current?.Model.SurgeryConcurrency ?? 1) + 1),
                tips.SurgeryConcurrency);

            // Nested: refines what the concurrency limit above counts.
            bool hospitalized = snapshot.CountHospitalized;
            bool nowHospitalized = hospitalized;
            var hospitalizedRect = new Rect(rect.x + 12f, y, width - 12f, RowHeight);
            tips.CountHospitalized.Region(hospitalizedRect);
            Widgets.CheckboxLabeled(hospitalizedRect,
                PlannerLabels.OptCountHospitalized, ref nowHospitalized);
            if (nowHospitalized != hospitalized)
                PlannerCommands.SetCountHospitalized(nowHospitalized);
            y += RowHeight + 2f;

            bool autoFloor = snapshot.AutoDoctorFloor;
            bool now = autoFloor;
            var autoFloorRect = new Rect(rect.x, y, width, RowHeight);
            tips.AutoFloor.Region(autoFloorRect);
            Widgets.CheckboxLabeled(autoFloorRect,
                PlannerLabels.OptAutoFloor, ref now);
            if (now != autoFloor) PlannerCommands.SetAutoDoctorFloor(now);
            y += RowHeight + 2f;
            if (!snapshot.AutoDoctorFloor)
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
                        (ImplannerStore.Current?.Model.ManualDoctorFloor ?? 0) + 1),
                    tips.ManualFloor);
            }
            y += Pad;

            // Implant reservations: stock held back for manual use; surgery
            // automation waits until items beyond these counts exist.
            tips.ImplantReserves.Region(
                new Rect(rect.x, y, width, SectionHeader.SubHeight));
            y += SectionHeader.Sub(rect.x, y, width,
                PlannerLabels.OptImplantReserves) + 2f;
            List<ReserveRow> rows = snapshot.ImplantReserves;
            string[] buffers = automation.ImplantBuffers;
            for (int i = 0; i < rows.Count; i++)
            {
                ReserveRow row = rows[i];
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(rect.x, y + 1f,
                        CompactRowHeight, CompactRowHeight), row.IconDef);
                Widgets.Label(new Rect(rect.x + CompactRowHeight + 6f, y,
                    width - 148f, CompactRowHeight + 2f), row.Label);
                // The control name is precomputed on the row and the edit
                // buffer sits in a parallel array seeded with the snapshot:
                // a steady render pass neither concatenates strings nor
                // looks anything up.
                string buffer = buffers[i];
                int value = row.Amount;
                automation.ReserveFieldNames.Add(row.FieldName);
                if (NumericField.DrawRightAligned(
                        new Rect(rect.x + width - 118f, y, 90f, CompactRowHeight + 2f),
                        row.FieldName, ref value, ref buffer, 0, 999))
                {
                    automation.StoreBuffer(buffers, i, row, buffer);
                    if (value != row.Amount)
                        PlannerCommands.SetImplantReserve(row.DefName, value);
                }
                if (Widgets.ButtonImage(new Rect(rect.x + width - 22f, y,
                        CompactRowHeight, CompactRowHeight), TexButton.Delete))
                    PlannerCommands.SetImplantReserve(row.DefName, 0);
                y += CompactRowHeight + 4f;
            }
            var addReserveRect = new Rect(rect.x, y, Mathf.Min(200f, width), RowHeight);
            tips.AddImplantReserve.Region(addReserveRect);
            if (Widgets.ButtonText(addReserveRect, PlannerLabels.AddImplantReserve))
                OpenImplantReserveMenu(store.Model);
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
                if (entry.PurchaseOnly && !model.ShowPurchaseOnly) continue;
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
            Rect rect, AutomationSnapshot snapshot, AutomationTips tips)
        {
            float width = rect.width;
            float y = rect.y;
            y += SectionHeader.Primary(rect.x, y, width,
                PlannerLabels.OptProduction) + 2f;
            bool autoProduction = snapshot.AutoProduction;
            bool now = autoProduction;
            var autoProductionRect = new Rect(rect.x, y, width, RowHeight);
            tips.AutoProduction.Region(autoProductionRect);
            Widgets.CheckboxLabeled(autoProductionRect,
                PlannerLabels.OptAutoProduction, ref now);
            if (now != autoProduction) PlannerCommands.SetAutoProduction(now);
            y += RowHeight + 2f;
            if (!snapshot.AutoProduction) return;

            DrawStepperRow(rect.x, ref y, width, PlannerLabels.OptConcurrency,
                snapshot.ConcurrencyText,
                static () => PlannerCommands.SetProductionConcurrency(
                    (ImplannerStore.Current?.Model.ProductionConcurrency ?? 1) - 1),
                static () => PlannerCommands.SetProductionConcurrency(
                    (ImplannerStore.Current?.Model.ProductionConcurrency ?? 1) + 1),
                tips.Concurrency);

            bool idle = snapshot.OnlyIdleBenches;
            now = idle;
            var idleRect = new Rect(rect.x, y, width, RowHeight);
            tips.IdleBenches.Region(idleRect);
            Widgets.CheckboxLabeled(idleRect,
                PlannerLabels.OptIdleBenches, ref now);
            if (now != idle) PlannerCommands.SetOnlyIdleBenches(now);
            y += RowHeight + 2f;

            DrawStepperRow(rect.x, ref y, width, PlannerLabels.OptProductionSkill,
                snapshot.ProductionSkillText,
                static () => PlannerCommands.SetProductionSkill(
                    (ImplannerStore.Current?.Model.ProductionSkill ?? 0) - 1),
                static () => PlannerCommands.SetProductionSkill(
                    (ImplannerStore.Current?.Model.ProductionSkill ?? 0) + 1),
                tips.ProductionSkill);

            bool intermediaries = snapshot.AllowIntermediaries;
            now = intermediaries;
            var intermediariesRect = new Rect(rect.x, y, width, RowHeight);
            tips.Intermediaries.Region(intermediariesRect);
            Widgets.CheckboxLabeled(intermediariesRect,
                PlannerLabels.OptIntermediaries, ref now);
            if (now != intermediaries) PlannerCommands.SetAllowIntermediaries(now);
            y += RowHeight + 2f + Pad;

            tips.Reserves.Region(
                new Rect(rect.x, y, width, SectionHeader.SubHeight));
            y += SectionHeader.Sub(rect.x, y, width,
                PlannerLabels.OptReserves) + 2f;
            List<ReserveRow> rows = snapshot.Reserves;
            string[] buffers = automation.ResourceBuffers;
            for (int i = 0; i < rows.Count; i++)
            {
                ReserveRow row = rows[i];
                if (row.IconDef != null)
                    Widgets.DefIcon(new Rect(rect.x, y + 1f,
                        CompactRowHeight, CompactRowHeight), row.IconDef);
                Widgets.Label(new Rect(rect.x + CompactRowHeight + 6f, y,
                    width - 120f, CompactRowHeight + 2f), row.Label);
                string buffer = buffers[i];
                int value = row.Amount;
                automation.ReserveFieldNames.Add(row.FieldName);
                if (NumericField.DrawRightAligned(
                        new Rect(rect.x + width - 90f, y, 90f, CompactRowHeight + 2f),
                        row.FieldName, ref value, ref buffer, 0, 999999))
                {
                    automation.StoreBuffer(buffers, i, row, buffer);
                    if (value != row.Amount)
                        PlannerCommands.SetResourceReserve(row.DefName, value);
                }
                y += CompactRowHeight + 4f;
            }
        }

        /// A label with -/value/+ controls right-aligned on one row. The
        /// label sits flush at x — indentation is the CALLER's statement of
        /// dependency, not this row's default. The optional tip covers the
        /// whole row through the shared tooltip presenter.
        private static void DrawStepperRow(float x, ref float y, float width,
            string label, string valueText,
            System.Action decrement, System.Action increment, WrTip? tip = null)
        {
            tip?.Region(new Rect(x, y, width, HeaderHeight));
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

        /// Layout heights: pure arithmetic over the snapshot (no traversal,
        /// no measurement).
        private static float SurgeryColumnHeight(AutomationSnapshot snapshot)
        {
            float height = SectionHeader.PrimaryHeight + 2f
                + HeaderHeight + 2f + 30f + Pad                  // iteration
                + HeaderHeight + 4f                              // concurrency
                + RowHeight + 2f                                 // hospitalized
                + RowHeight + 2f;                                // auto floor
            if (!snapshot.AutoDoctorFloor)
                height += HeaderHeight + 4f;                     // manual skill
            height += Pad + SectionHeader.SubHeight + 2f         // reservations
                + snapshot.ImplantReserves.Count * (CompactRowHeight + 4f)
                + RowHeight;                                     // add button
            return height;
        }

        private static float ProductionColumnHeight(AutomationSnapshot snapshot)
        {
            float height = SectionHeader.PrimaryHeight + 2f
                + RowHeight + 2f;                                // auto bills
            if (snapshot.AutoProduction)
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

        private OptionsSnapshot? optionsFront;

        /// The Options tab: settings that shape plans rather than
        /// automation. Mod compatibility holds the curated cross-mod rules
        /// the game's data cannot express; Catalog holds what the picker
        /// lists. Laid out in one column of the automation tab's column
        /// width.
        private void DrawOptions(Rect rect, ImplannerStore store)
        {
            // Refreshed in WindowUpdate; the fallback covers a mid-frame
            // tab switch only.
            OptionsSnapshot snapshot = optionsFront ??= options.Current(store);
            optionsTips.Ensure();
            OptionsTips tips = optionsTips;

            float width = Mathf.Floor((rect.width - ColumnGap) / 2f);
            float y = rect.y;
            y += SectionHeader.Primary(rect.x, y, width, PlannerLabels.OptModCompat) + 2f;
            DrawOptionToggle(rect.x, ref y, width, PlannerLabels.OptAllowMultipleBladders,
                snapshot.AllowMultipleBladders, tips.AllowMultipleBladders,
                SetAllowMultipleBladders);
            DrawOptionToggle(rect.x, ref y, width,
                PlannerLabels.OptAllowMultipleHygieneEnhancers,
                snapshot.AllowMultipleHygieneEnhancers, tips.AllowMultipleHygieneEnhancers,
                SetAllowMultipleHygieneEnhancers);
            y += Pad;

            y += SectionHeader.Primary(rect.x, y, width, PlannerLabels.OptCatalog) + 2f;
            DrawOptionToggle(rect.x, ref y, width, PlannerLabels.OptShowPurchaseOnly,
                snapshot.ShowPurchaseOnly, tips.ShowPurchaseOnly, SetShowPurchaseOnly);
        }

        // Cached static delegates: the toggle rows take the command by
        // reference so the steady pass never creates one.
        private static readonly System.Action<bool> SetAllowMultipleBladders =
            PlannerCommands.SetAllowMultipleBladders;
        private static readonly System.Action<bool> SetAllowMultipleHygieneEnhancers =
            PlannerCommands.SetAllowMultipleHygieneEnhancers;
        private static readonly System.Action<bool> SetShowPurchaseOnly =
            PlannerCommands.SetShowPurchaseOnly;

        /// A labeled checkbox row over a tip region; a change issues the
        /// synced command and the next snapshot renders the published state.
        private static void DrawOptionToggle(float x, ref float y, float width,
            string label, bool value, WrTip tip, System.Action<bool> command)
        {
            bool now = value;
            var rowRect = new Rect(x, y, width, RowHeight);
            tip.Region(rowRect);
            Widgets.CheckboxLabeled(rowRect, label, ref now);
            if (now != value) command(now);
            y += RowHeight + 2f;
        }
    }
}
