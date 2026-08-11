using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    /// Recommendations tab: global options on the left (recommendation order
    /// template, formula tuning parameters), per-role options on the right
    /// (an accordion of tuning-eligible roles: category/time/penalty, skills,
    /// holder scale and the role's training paths). Per-save state, MP-synced.
    public class RecommendationsTabView
    {
        // Hunting chips: pinning them disables the dynamic duty-slot placement.
        private static readonly Color LockedColor = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        private readonly RecommendationsTabState state = new RecommendationsTabState();
        private Vector2 tabScroll;

        // Accordion: at most one expanded role panel.
        private int expandedRoleId = -1;
        // Global tuning sections expand independently (no shared editor state).
        private readonly HashSet<int> expandedSections = new HashSet<int>();

        // Drag drop, rebuilt only when the reorder target changes (see p2 in
        // DrawRecommendationOrder): RoleDrag clears its slot every frame.
        private int dropStamp = -1;
        private int dropFrom = -1;
        private int dropTo = -1;
        private System.Action dropAction;

        // Band drag in flight: committed as ONE synced command on release.
        private int dragPathId = -1;
        private int dragRoleId = -1;
        private int dragLinkedRoleId = -1; // same-row neighbour sharing the dragged edge
        private BandDragKind dragKind;
        private int pendingMin, pendingMax;
        private int pendingRow;      // slide only: display row under the cursor
        private int dragStartRow;
        private int slideGrabOffset; // level distance from band min to grab point

        private enum BandDragKind { None, MinEdge, MaxEdge, Slide }

        // Row pitch leaves 5px between chips: a 1px separator with 2px
        // clearance to the chips on both sides.
        private const float BandRowH = RoleChipUI.Height + 5f;
        private const float AxisH = 22f; // 16px numbers + 1px ticks above the baseline
        // Axis baseline sits at axis bottom + 1; rows follow 2px below it. The
        // axis numbers sit flush with the inner top so their clearance equals
        // the side padding.
        private const float RowsStartY = AxisH + 4f;
        private const float RecPanelPad = 8f;
        private const float WhenPanelPad = 8f;
        // Tiny-font caption over the WHEN panel: full line height (16f clipped
        // descenders) and one shared caption -> panel gap.
        private const float PanelCaptionH = 18f;
        private const float PanelCaptionGap = 2f;

        // Role panel geometry: header row, inner padding, editor row heights.
        private const float PanelHeaderH = 28f;
        private const float PanelPad = 8f;
        private const float PanelGap = 4f;
        private const float CheckRowH = 24f;
        private const float MiniHeaderH = 30f;
        // Option slider block: caption+value row, the rail, then the end
        // labels below the rail ends.
        private const float OptionBlockH = 48f;
        private const float ScalingRowH = 28f;
        private const float ColumnGap = 12f;

        public void Reset()
        {
            tabScroll = Vector2.zero;
            state.Reset();
            expandedRoleId = -1;
            expandedSections.Clear();
            dropStamp = dropFrom = dropTo = -1;
            dropAction = null;
            ClearBandDrag();
        }

        internal void ReleaseWindowData() => Reset();

        /// Language-only invalidation. The expanded panel and scroll position
        /// remain unchanged.
        internal void InvalidateLanguageCaches()
        {
            state.InvalidateLanguageCaches();
            whenCaptionWidth = -1f;
        }

        // WHEN caption: translate + wrap measurement per pass are
        // render-forbidden. Owner: view. Key: caption width (single slot).
        // Value: wrapped caption text and height. Dependencies: width,
        // language. Refresh: on width change. Teardown: language invalidation
        // resets.
        private float whenCaptionWidth = -1f;
        private string whenCaptionText;
        private float whenCaptionHeight;

        private float WhenCaptionHeight(float width)
        {
            if (whenCaptionWidth != width)
            {
                whenCaptionWidth = width;
                GameFont previousFont = Text.Font;
                Text.Font = GameFont.Tiny;
                whenCaptionText = "WR_WhenPanelCaption".Translate();
                whenCaptionHeight = Text.CalcHeight(whenCaptionText, width);
                Text.Font = previousFont;
            }
            return whenCaptionHeight;
        }

        private void ClearBandDrag()
        {
            dragPathId = -1;
            dragRoleId = -1;
            dragLinkedRoleId = -1;
            dragKind = BandDragKind.None;
            pendingRow = 0;
            dragStartRow = 0;
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();

            // One scroll view: a full-width recommendation-order panel on top,
            // the tuning sections and role panels in two columns below. The
            // 16px bar reserve is unconditional so wrap widths never depend on
            // the height they produce.
            float viewW = rect.width - 16f;
            const float flowX = 16f;
            float orderW = viewW - flowX - 8f;
            // Equal columns below the order panel, scaling with the window
            // like the panel itself; stacked when too narrow.
            float flowW = (orderW - 24f) / 2f;
            float rightX = flowX + flowW + 24f;
            float rightW = orderW - flowW - 24f;
            bool sideBySide = rightW >= 240f;
            if (!sideBySide) flowW = orderW;

            float gw = sideBySide ? rightW : flowW;
            state.EnsureOrder(store, orderW - RecPanelPad * 2f);
            state.EnsureTuning(store, gw - RecPanelPad * 2f);
            state.EnsureTips();
            state.EnsureHelpLayout(orderW);

            state.EnsurePanels(store, flowW);
            RecRoleDetailSnapshot detail = expandedRoleId != -1
                ? state.EnsureDetail(store, expandedRoleId, flowW - PanelPad * 2f)
                : null;
            if (detail == null && expandedRoleId != -1)
                // The expanded role vanished.
                expandedRoleId = -1;

            // A drag whose path or entry vanished must not block future presses.
            if (dragPathId != -1)
            {
                RecPathView dragView = FindPathView(detail, dragPathId);
                if (dragView == null || !dragView.RoleIds.Contains(dragRoleId))
                    ClearBandDrag();
            }

            // Y-flow laid out up front: the scroll view needs contentH before
            // anything draws. TOP: the full-width order panel, help below the
            // header like every other section.
            float y = 12f;
            float recHeaderY = y;
            y += 26f;
            var recOrderHelpRect = new Rect(flowX, y, orderW,
                state.RecommendationOrderHelpHeight);
            y = recOrderHelpRect.yMax + 6f;
            var recPanel = new Rect(flowX, y, orderW,
                state.OrderLayoutHeight + RecPanelPad * 2f);
            float columnsTop = recPanel.yMax + 16f;

            // LEFT column: the role panels.
            y = columnsTop;
            var leftHeader = new Rect(flowX, y, flowW, 28f);
            y += 26f;
            var panelsHelpRect = new Rect(flowX, y, flowW,
                state.PanelsHelpHeight);
            y = panelsHelpRect.yMax + 8f;
            float panelsStartY = y;
            IReadOnlyList<RecRolePanel> panels = state.Panels;
            float bodyHeight = detail == null
                ? 0f : BodyHeight(detail, flowW - PanelPad * 2f);
            for (int i = 0; i < panels.Count; i++)
            {
                y += PanelHeaderH;
                if (detail != null && panels[i].Role.id == detail.RoleId)
                    y += bodyHeight + PanelPad * 2f;
                y += PanelGap;
            }

            // RIGHT column: the accordion tuning sections; a window too
            // narrow for both stacks them below the role panels.
            float gx = sideBySide ? rightX : flowX;
            float gy = sideBySide ? columnsTop : y;
            var rightHeader = new Rect(gx, gy, gw, 28f);
            gy += 26f;
            var globalHelpRect = new Rect(gx, gy, gw, state.GlobalHelpHeight);
            gy = globalHelpRect.yMax + 8f;
            float tuningStartY = gy;
            IReadOnlyList<RecTuningSection> tuningSections = state.TuningSections;
            for (int i = 0; i < tuningSections.Count; i++)
            {
                gy += PanelHeaderH;
                if (expandedSections.Contains(i))
                    gy += SectionExpandedExtra(tuningSections[i]);
                gy += PanelGap;
            }

            float contentH = Mathf.Max(y, gy) + 12f;
            Widgets.BeginScrollView(rect, ref tabScroll,
                new Rect(0f, 0f, viewW, contentH));

            MiniHeader(flowX, recHeaderY, orderW, "WR_RecOrderHeader".Translate(),
                state.RecommendationOrderTip);
            DrawRecommendationOrder(recPanel, store);
            DrawHelpParagraph(recOrderHelpRect, state.RecommendationOrderHelp);

            WrText.HeaderLabel(leftHeader, "WR_RecRolePanel".Translate());
            DrawHelpParagraph(panelsHelpRect, state.PanelsHelp);
            DrawRolePanels(flowX, panelsStartY, flowW, store, panels,
                detail, bodyHeight);

            WrText.HeaderLabel(rightHeader, "WR_RecGlobalPanel".Translate());
            DrawHelpParagraph(globalHelpRect, state.GlobalHelp);
            float ty = tuningStartY;
            for (int i = 0; i < tuningSections.Count; i++)
            {
                RecTuningSection section = tuningSections[i];
                bool expanded = expandedSections.Contains(i);
                var headerRect = new Rect(gx, ty, gw, PanelHeaderH);
                Widgets.DrawBoxSolid(headerRect, new Color(1f, 1f, 1f, 0.06f));
                var arrowRect = new Rect(gx + 6f,
                    ty + (PanelHeaderH - 18f) / 2f, 18f, 18f);
                GUI.DrawTexture(arrowRect,
                    expanded ? TexButton.Collapse : TexButton.Reveal);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(arrowRect.xMax + 6f, ty,
                    gw - (arrowRect.xMax - gx) - 10f, PanelHeaderH),
                    section.Label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.DrawHighlightIfMouseover(headerRect);
                if (Widgets.ButtonInvisible(headerRect)
                    && !expandedSections.Add(i))
                    expandedSections.Remove(i);
                ty += PanelHeaderH;
                if (expanded)
                {
                    // Reset shares the expanded area's top row with the intro.
                    if (Widgets.ButtonText(new Rect(gx + gw - 70f, ty + 4f,
                            70f, 24f), state.TuningReset))
                        RoleCommands.ResetRecommendationTuningSection(section.Key);
                    DrawHelpParagraph(new Rect(gx, ty + 4f, gw - 82f,
                        section.IntroHeight), section.Intro);
                    ty += 4f + Mathf.Max(section.IntroHeight, 24f) + 6f;
                    var panel = new Rect(gx, ty, gw,
                        section.Height + RecPanelPad * 2f);
                    DrawTuningSection(panel, section);
                    ty += panel.height + 8f;
                }
                ty += PanelGap;
            }

            Widgets.EndScrollView();

            RoleChipUI.DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        private static RecPathView FindPathView(
            RecRoleDetailSnapshot detail, int pathId)
        {
            if (detail == null) return null;
            for (int i = 0; i < detail.PathCount; i++)
                if (detail.PathAt(i).PathId == pathId) return detail.PathAt(i);
            return null;
        }

        // ----- Right: accordion of tuning-eligible roles -----

        private void DrawRolePanels(float x, float y, float width,
            RoleStore store, IReadOnlyList<RecRolePanel> panels,
            RecRoleDetailSnapshot detail, float bodyHeight)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                Role role = panels[i].Role;
                // Keyed to the snapshot, not expandedRoleId: a mid-pass click
                // must not pair a later panel with another role's detail.
                bool expanded = detail != null && role.id == detail.RoleId;

                var headerRect = new Rect(x, y, width, PanelHeaderH);
                Widgets.DrawBoxSolid(headerRect, new Color(1f, 1f, 1f, 0.06f));
                var arrowRect = new Rect(x + 6f,
                    y + (PanelHeaderH - 18f) / 2f, 18f, 18f);
                GUI.DrawTexture(arrowRect,
                    expanded ? TexButton.Collapse : TexButton.Reveal);
                float chipW = Mathf.Min(panels[i].ChipWidth,
                    width - (arrowRect.xMax - x) - 10f);
                var chipRect = new Rect(arrowRect.xMax + 6f,
                    y + (PanelHeaderH - RoleChipUI.Height) / 2f,
                    chipW, RoleChipUI.Height);
                RoleChipUI.Draw(chipRect, role, ChipStyle.Normal,
                    showRemove: false, dragSource: null, onClick: null,
                    interactive: false);
                Widgets.DrawHighlightIfMouseover(headerRect);
                if (Widgets.ButtonInvisible(headerRect))
                {
                    expandedRoleId = expanded ? -1 : role.id;
                    ClearBandDrag();
                }
                y += PanelHeaderH;

                if (expanded)
                {
                    var body = new Rect(x, y, width,
                        bodyHeight + PanelPad * 2f);
                    Widgets.DrawBoxSolidWithOutline(
                        body, WrStyle.PanelBackground, WrStyle.PanelOutline);
                    DrawExpandedBody(body.ContractedBy(PanelPad), store, detail);
                    y += body.height;
                }
                y += PanelGap;
            }
        }

        /// Expanded panel content height. MUST mirror DrawExpandedBody's flow.
        private float BodyHeight(RecRoleDetailSnapshot detail, float width)
        {
            float halfW = (width - ColumnGap) / 2f;
            float left = MiniHeaderH + OptionBlockH + 4f + OptionBlockH + 4f
                + CheckRowH + 8f
                + MiniHeaderH + ScalingRowH + 2f + ScalingRowH;
            float right = MiniHeaderH
                + detail.RequiredHeight + 6f + detail.OptionalHeight;
            if (detail.ShowTrainingSection)
            {
                right += 8f + MiniHeaderH;
                for (int i = 0; i < detail.PathCount; i++)
                    right += PathBlockHeight(detail.PathAt(i), halfW) + 8f;
            }
            return Mathf.Max(left, right);
        }

        // The trailing 5px row gap is trimmed; the WHEN area is frameless and
        // spans the full block width.
        private float PathBlockHeight(RecPathView view, float width) =>
            WhenCaptionHeight(width) + PanelCaptionGap
                + RowsStartY + view.DisplayRows * BandRowH - 5f;

        private void DrawExpandedBody(Rect inner, RoleStore store,
            RecRoleDetailSnapshot detail)
        {
            float x = inner.x;
            float width = inner.width;
            float y = inner.y;
            int roleId = detail.RoleId;
            float halfW = (width - ColumnGap) / 2f;
            float rightX = x + halfW + ColumnGap;

            // LEFT: Classification (importance and time sliders, penalty),
            // then Assignment Scaling.
            Text.Font = GameFont.Small;
            float ly = MiniHeader(x, y, halfW, detail.ClassificationHeader, null);
            int categoryPosition = detail.CategoryValue == 0
                ? 1 : 3 - detail.CategoryValue;
            int pickedCategory = DrawOptionSegments(x, ly, halfW,
                detail.CategoryCaption, "WR_RoleCategoryTip", categoryPosition,
                detail.CategoryOptions);
            if (pickedCategory != categoryPosition)
                RoleCommands.SetRoleCategory(roleId, 3 - pickedCategory);
            ly += OptionBlockH + 4f;
            int timePosition = detail.TimeValue == 0 ? 1 : detail.TimeValue - 1;
            int pickedTime = DrawOptionSegments(x, ly, halfW,
                detail.TimeCaption, "WR_RoleTimeTip", timePosition,
                detail.TimeOptions);
            if (pickedTime != timePosition)
                RoleCommands.SetRoleTime(roleId, pickedTime + 1);
            ly += OptionBlockH + 4f;
            var championRect = new Rect(x, ly, halfW, CheckRowH);
            WrTips.Key("WR_ChampionPenaltyTip").Region(championRect);
            bool champion = detail.ChampionPenalty;
            GUI.color = WrStyle.DimText;
            Widgets.CheckboxLabeled(championRect, detail.ChampionLabel, ref champion);
            GUI.color = Color.white;
            if (champion != detail.ChampionPenalty)
                RoleCommands.SetRoleChampionPenalty(roleId, champion);
            ly += CheckRowH + 8f;

            ly = MiniHeader(x, ly, halfW, detail.ScalingHeader, null);
            DrawScalingRow(new Rect(x, ly, halfW, ScalingRowH),
                detail.ColonyMinCaption, "WR_RoleColonyMinTip", null,
                detail.ColonyMinLabel, detail.ColonyMin,
                roleId, ScalingField.ColonyMin, "WR_ScaleColonyMin");
            ly += ScalingRowH + 2f;
            DrawScalingRow(new Rect(x, ly, halfW, ScalingRowH),
                detail.CoverageCaption, "WR_RoleCoverageTip", null,
                detail.CoverageLabel, detail.Coverage,
                roleId, ScalingField.Coverage, "WR_ScaleCoverage",
                unitSuffix: "%");

            // RIGHT: Skills, then the training path below them.
            float ry = MiniHeader(rightX, y, halfW, detail.SkillsHeader, null);
            ry = DrawSkillTable(rightX, ry, halfW, detail.RequiredCaption,
                "WR_RequiredSkillsTip", roleId, detail, optional: false) + 6f;
            ry = DrawSkillTable(rightX, ry, halfW, detail.OptionalCaption,
                "WR_OptionalSkillsTip", roleId, detail, optional: true);

            if (!detail.ShowTrainingSection) return;
            ry += 8f;
            ry = MiniHeader(rightX, ry, halfW, "WR_TrainingSection".Translate(),
                state.TrainingTip);
            for (int i = 0; i < detail.PathCount; i++)
                ry = DrawPathBlock(rightX, ry, halfW, store,
                    detail.PathAt(i)) + 8f;
        }

        /// One classification option: dim caption on the top row, a segmented
        /// single-click selector below. Every choice is visible; the active
        /// segment gets the accent outline and full-brightness label.
        /// Returns the picked position.
        private static int DrawOptionSegments(float x, float y, float width,
            string caption, string tipKey, int position,
            IReadOnlyList<string> options)
        {
            var block = new Rect(x, y, width, OptionBlockH);
            WrTips.Key(tipKey).Region(block);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.DimText;
            // 19 tall, not 18: descenders need the extra pixel to render, and
            // the row below only starts at y+20 so no layout space is added.
            Widgets.Label(new Rect(x, y, width, 19f), caption);
            GUI.color = Color.white;

            const float SegmentH = 24f;
            const float SegmentGap = 2f;
            int picked = position;
            float segmentW = (width - (options.Count - 1) * SegmentGap)
                / options.Count;
            float segmentY = y + 20f;
            Text.Anchor = TextAnchor.MiddleCenter;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            for (int i = 0; i < options.Count; i++)
            {
                var cell = new Rect(x + i * (segmentW + SegmentGap), segmentY,
                    segmentW, SegmentH);
                bool active = i == position;
                Widgets.DrawBoxSolid(cell, CellPanel);
                if (active)
                {
                    GUI.color = SegmentActiveOutline;
                    Widgets.DrawBox(cell);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = WrStyle.DimText;
                    Widgets.DrawHighlightIfMouseover(cell);
                }
                Widgets.Label(cell, options[i]);
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(cell)) picked = i;
            }
            Text.WordWrap = wrap;
            Text.Anchor = TextAnchor.UpperLeft;
            return picked;
        }

        private enum ScalingField { ColonyMin, Coverage }

        /// One assignment-scaling input row: the shared stepper row plus this
        /// panel's tip and command routing. The commands clamp and no-op at
        /// the bounds.
        private static void DrawScalingRow(Rect rect, string caption,
            string tipKey, string tipArg, string valueLabel, int value,
            int roleId, ScalingField field, string controlName,
            string unitSuffix = null)
        {
            (tipArg == null ? WrTips.Key(tipKey) : WrTips.Key(tipKey, tipArg))
                .Region(rect);
            int? requested = NumericStepperUI.DrawRow(rect, caption,
                valueLabel, value, controlName, roleId, unitSuffix);
            if (requested.HasValue)
                CommitScaling(roleId, field, requested.Value);
        }

        private static void CommitScaling(int roleId, ScalingField field, int value)
        {
            switch (field)
            {
                case ScalingField.ColonyMin:
                    RoleCommands.SetRoleColonyMinimum(roleId, value); break;
                default:
                    RoleCommands.SetRoleCoverage(roleId, value); break;
            }
        }

        /// One skill table block, three columns: the strip caption, one
        /// selected skill per row with its remove X right-aligned inside the
        /// skill column, and the Add button on the top row.
        private static float DrawSkillTable(float x, float y, float width,
            string caption, string tipKey, int roleId,
            RecRoleDetailSnapshot detail, bool optional)
        {
            const float pitch = RecommendationsTabState.SkillRowPitch;
            Text.Font = GameFont.Small;
            float addW = WrText.FitWidth(detail.AddSkillLabel) + 16f;
            float captionW = Mathf.Max(
                WrText.FitWidth(detail.RequiredCaption),
                WrText.FitWidth(detail.OptionalCaption)) + 8f;
            float skillX = x + captionW;
            float buttonX = x + width - addW;
            float removeX = buttonX - 8f - 24f;

            var captionRect = new Rect(x, y, captionW, pitch);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.DimText;
            Widgets.Label(captionRect, caption);
            GUI.color = Color.white;
            WrTips.Key(tipKey).Region(captionRect);

            int count = optional ? detail.OptionalChipCount : detail.RequiredChipCount;
            for (int i = 0; i < count; i++)
            {
                RecSkillChip chip = optional
                    ? detail.OptionalChipAt(i) : detail.RequiredChipAt(i);
                float rowY = y + i * pitch;
                Widgets.Label(new Rect(skillX, rowY,
                    removeX - skillX - 4f, pitch), chip.Label);
                if (Widgets.ButtonImage(new Rect(removeX,
                        rowY + (pitch - 24f) / 2f, 24f, 24f), TexButton.Delete))
                    RoleCommands.RemoveRoleSkill(roleId, chip.DefName, optional);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(buttonX, y + 1f, addW,
                    pitch - 4f), detail.AddSkillLabel))
                OpenSkillMenu(roleId, detail, optional);
            return y + Mathf.Max(1, count) * pitch;
        }

        /// Menu-click-only def resolution, like the order panel's Add Role.
        private static void OpenSkillMenu(int roleId,
            RecRoleDetailSnapshot detail, bool optional)
        {
            var options = new List<FloatMenuOption>();
            foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefsListForReading
                         .OrderBy(s => (s.skillLabel ?? s.label ?? s.defName)
                                 .CapitalizeFirst(),
                             System.StringComparer.OrdinalIgnoreCase))
            {
                if (detail.HasSkill(skill.defName)) continue;
                string captured = skill.defName;
                options.Add(new FloatMenuOption(
                    (skill.skillLabel ?? skill.label ?? skill.defName)
                        .CapitalizeFirst(),
                    () => RoleCommands.AddRoleSkill(roleId, captured, optional)));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        // ----- Per-role training path editor -----

        /// The role's own path block: wrapped caption and the frameless WHEN
        /// band area spanning the full block width. The owner role always
        /// stays in its path (its band chip carries no X).
        private float DrawPathBlock(float x, float y, float width,
            RoleStore store, RecPathView view)
        {
            float captionHeight = WhenCaptionHeight(width);
            var whenCaptionRect = new Rect(x, y, width, captionHeight);
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(whenCaptionRect, whenCaptionText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += captionHeight + PanelCaptionGap;

            var whenPanel = new Rect(x, y, width,
                RowsStartY + view.DisplayRows * BandRowH - 5f);
            DrawWhenPanel(whenPanel, store, view);
            return whenPanel.yMax;
        }

        /// The WHEN editor area: the 0..21 axis on top, the packed band rows,
        /// then Add Role on the trailing empty row. Frameless: bands span the
        /// full block width.
        private void DrawWhenPanel(Rect panel, RoleStore store, RecPathView view)
        {
            float bandW = panel.width;
            // Below this, a min-span chip can't hold its grips + X.
            if (bandW < 150f) return;

            DrawAxis(new Rect(panel.x, panel.y, bandW, AxisH));
            DrawBandRows(store, view, panel.x, panel.y, bandW);
        }

        /// Level numbers (even levels only, for breathing room), a 1px tick
        /// under each level and the baseline, all on the exact band span.
        /// Labels center on their ticks; 0 and 21 would overhang the edge, so
        /// only their ticks render.
        private static void DrawAxis(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = WrStyle.DimText;
            float scale = rect.width / SkillProgressionMath.MaxLevel;
            for (int lvl = 2; lvl < SkillProgressionMath.MaxLevel; lvl += 2)
                Widgets.Label(new Rect(rect.x + lvl * scale - 9f, rect.y, 18f, rect.height - 6f),
                    lvl.ToStringCached());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            var dim = new Color(1f, 1f, 1f, 0.25f);
            for (int lvl = 0; lvl <= SkillProgressionMath.MaxLevel; lvl++)
                Widgets.DrawBoxSolid(new Rect(
                    Mathf.Min(rect.x + lvl * scale, rect.xMax - 1f),
                    rect.yMax - 3f, 1f, 4f), dim);
            GUI.color = dim;
            WrText.LineHorizontal(rect.x, rect.yMax + 1f, rect.width);
            GUI.color = Color.white;
        }

        /// Tier 1: some coverage giver grants XP in some skill; tier 2: no
        /// known XP-giving job at all. Menu-click only, never per frame.
        private static bool HasXpJobs(Role role)
        {
            foreach (var giverName in role.Coverage())
            {
                var profile = JobSkillProfiles.ForGiver(giverName);
                if (profile != null && profile.GivesXp) return true;
            }
            return false;
        }

        private static bool IsNormal(Role role) =>
            !role.blocker && !role.HasRules;

        /// Tier 1 plain, tier 2 greyed (mods can rewire XP in driver code, so
        /// players may know better). New picks enter min-width at the top,
        /// drag to place.
        private static void OpenAddRoleMenu(RoleStore store, RecPathView view)
        {
            var options = new List<FloatMenuOption>();
            foreach (var (role, tier) in store.roles
                         .Where(r => IsNormal(r) && !view.RoleIds.Contains(r.id))
                         .Select(r => (role: r, tier: HasXpJobs(r) ? 1 : 2))
                         .OrderBy(t => t.tier)
                         .ThenBy(t => t.role.label, System.StringComparer.OrdinalIgnoreCase))
            {
                int captured = role.id;
                var ids = view.RoleIds.ToList();
                var mins = view.Mins.ToList();
                var maxes = view.Maxes.ToList();
                string label = tier == 2
                    ? role.label.Colorize(new Color(0.62f, 0.62f, 0.62f))
                    : role.label;
                var option = new FloatMenuOption(label, () =>
                {
                    ids.Add(captured);
                    mins.Add(SkillProgressionMath.MaxLevel - SkillProgressionMath.MinSpan);
                    maxes.Add(SkillProgressionMath.MaxLevel);
                    RoleCommands.SetRoleTraining(view.PathId, ids, mins, maxes);
                });
                if (tier == 2) option.tooltip = "WR_NoXpRoleTip".Translate();
                options.Add(option);
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        /// The selected path's band rows; the trailing empty row carries the
        /// Add Role button (and stays the slide-to-re-row affordance). baseY is
        /// the panel's inner top; chips are display-only, all interaction is
        /// the explicit block below (X, handles, body slide).
        private void DrawBandRows(RoleStore store, RecPathView view,
            float bandX, float baseY, float bandW)
        {
            var e = Event.current;
            float scale = bandW / SkillProgressionMath.MaxLevel;
            float rowsY = baseY + RowsStartY;

            // Section sweep already cleared vanished drags; a live entry here
            // is this path's own drag.
            int dragEntry = dragPathId == view.PathId ? view.IndexOfRole(dragRoleId) : -1;
            if (dragEntry >= 0)
            {
                if (Input.GetMouseButton(0))
                    UpdateBandDrag(view, dragEntry, e, bandX, rowsY, scale);
                else
                {
                    CommitBandDrag(view, dragEntry);
                    ClearBandDrag();
                    dragEntry = -1;
                }
            }
            int linkedEntry = dragEntry >= 0 && dragLinkedRoleId != -1
                ? view.IndexOfRole(dragLinkedRoleId) : -1;

            // Displayed (pending-aware) band values; the linked neighbour's
            // touching edge follows the shared pending level.
            int ShownMin(int k) => k == dragEntry ? pendingMin
                : k == linkedEntry && dragKind == BandDragKind.MaxEdge ? pendingMax : view.Mins[k];
            int ShownMax(int k) => k == dragEntry ? pendingMax
                : k == linkedEntry && dragKind == BandDragKind.MinEdge ? pendingMin : view.Maxes[k];
            int ShownRow(int k) => k == dragEntry && dragKind == BandDragKind.Slide
                ? pendingRow : view.Rows[k];

            // Dim divider below every display row, 2px clear of the chips on
            // both sides (BandRowH leaves 5px between chips).
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            for (int r = 1; r < view.DisplayRows; r++)
                WrText.LineHorizontal(bandX, rowsY + r * BandRowH - 3f, bandW);
            GUI.color = Color.white;

            // Add Role lives on the empty affordance row, left-aligned; a chip
            // slid onto that row draws over it (chips render later).
            if (Widgets.ButtonText(new Rect(bandX,
                    rowsY + (view.DisplayRows - 1) * BandRowH,
                    110f, RoleChipUI.Height), "WR_AddRole".Translate()))
                OpenAddRoleMenu(store, view);

            for (int i = 0; i < view.RoleIds.Count; i++)
            {
                int min = ShownMin(i), max = ShownMax(i);
                int row = ShownRow(i);
                float rowY = rowsY + row * BandRowH;
                float bx = bandX + min * scale;
                float bandPx = (max - min) * scale;
                // 2px band inset per side keeps daylight at shared boundaries;
                // the drawn rect regrows BandOuterPad so 1px of chip surface
                // backs each grip's outer edge.
                var chipRect = new Rect(bx + 2f - ChipUI.BandOuterPad, rowY,
                    bandPx - 4f + ChipUI.BandOuterPad * 2f, RoleChipUI.Height);
                // The owner role cannot leave its own path: no X, resize and
                // slide only.
                bool ownerEntry = view.RoleIds[i] == view.PathId;
                RoleChipUI.DrawBandChip(chipRect, view.Roles[i],
                    showRemove: !ownerEntry);

                if (i == dragEntry)
                {
                    // Live level readout at the moving edge (slide: the min edge).
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.LowerCenter;
                    int shown = dragKind == BandDragKind.MaxEdge ? max : min;
                    float shownX = dragKind == BandDragKind.MaxEdge ? bx + bandPx : bx;
                    // Edge readouts clamp inside the band span (panel edge).
                    float readX = Mathf.Clamp(shownX - 12f, bandX, bandX + bandW - 24f);
                    Widgets.Label(new Rect(readX, rowY - 16f, 24f, 16f), shown.ToStringCached());
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }

                if (e.type != EventType.MouseDown || e.button != 0 || dragPathId != -1) continue;
                if (!chipRect.Contains(e.mousePosition)) continue;
                // Hit order: X, grip zones, remaining body (slide). The X is
                // inset past the right grip, so resize and dismiss can't collide.
                if (!ownerEntry
                    && ChipUI.BandRemoveRect(chipRect).Contains(e.mousePosition))
                {
                    RemoveEntry(view, i);
                    e.Use();
                    return;
                }
                bool onLeft = ChipUI.BandLeftHandle(chipRect).Contains(e.mousePosition);
                if (onLeft || ChipUI.BandRightHandle(chipRect).Contains(e.mousePosition))
                {
                    dragKind = onLeft ? BandDragKind.MinEdge : BandDragKind.MaxEdge;
                    dragLinkedRoleId = FindLinked(view, i, dragKind);
                }
                else
                {
                    dragKind = BandDragKind.Slide;
                    slideGrabOffset = Mathf.RoundToInt((e.mousePosition.x - bandX) / scale) - min;
                    pendingRow = dragStartRow = view.Rows[i];
                }
                dragPathId = view.PathId;
                dragRoleId = view.RoleIds[i];
                pendingMin = min;
                pendingMax = max;
                e.Use();
            }
        }

        /// Per-frame pending update: clamp arithmetic only. A linked edge moves
        /// both bands via the shared clamp; a vanished neighbour drops the link.
        private void UpdateBandDrag(RecPathView view, int dragEntry, Event e, float bandX,
            float rowsY, float scale)
        {
            int level = Mathf.RoundToInt((e.mousePosition.x - bandX) / scale);
            int min = view.Mins[dragEntry], max = view.Maxes[dragEntry];
            if (dragKind == BandDragKind.Slide)
            {
                pendingMin = SkillProgressionMath.ClampSlide(min, max, level - slideGrabOffset);
                pendingMax = pendingMin + (max - min);
                pendingRow = Mathf.Clamp(
                    Mathf.FloorToInt((e.mousePosition.y - rowsY) / BandRowH),
                    0, view.DisplayRows - 1);
                return;
            }
            int linked = dragLinkedRoleId == -1 ? -1 : view.IndexOfRole(dragLinkedRoleId);
            if (linked < 0) dragLinkedRoleId = -1;
            if (dragKind == BandDragKind.MinEdge)
                pendingMin = linked >= 0
                    ? SkillProgressionMath.ClampSharedEdge(view.Mins[linked], max, level)
                    : SkillProgressionMath.ClampEdge(min, max, true, level);
            else
                pendingMax = linked >= 0
                    ? SkillProgressionMath.ClampSharedEdge(min, view.Maxes[linked], level)
                    : SkillProgressionMath.ClampEdge(min, max, false, level);
        }

        /// ONE synced command commits the slid/resized band, the linked
        /// neighbour's shared edge and a vertical reorder together.
        private void CommitBandDrag(RecPathView view, int dragEntry)
        {
            var ids = view.RoleIds.ToList();
            var mins = view.Mins.ToList();
            var maxes = view.Maxes.ToList();
            bool changed = pendingMin != mins[dragEntry] || pendingMax != maxes[dragEntry];
            mins[dragEntry] = pendingMin;
            maxes[dragEntry] = pendingMax;
            if (dragKind != BandDragKind.Slide && dragLinkedRoleId != -1)
            {
                int linked = ids.IndexOf(dragLinkedRoleId);
                if (linked >= 0 && dragKind == BandDragKind.MaxEdge && mins[linked] != pendingMax)
                {
                    mins[linked] = pendingMax;
                    changed = true;
                }
                else if (linked >= 0 && dragKind == BandDragKind.MinEdge && maxes[linked] != pendingMin)
                {
                    maxes[linked] = pendingMin;
                    changed = true;
                }
            }
            if (dragKind == BandDragKind.Slide && pendingRow != dragStartRow)
            {
                changed = true;
                ids.RemoveAt(dragEntry);
                mins.RemoveAt(dragEntry);
                maxes.RemoveAt(dragEntry);
                // Drop before the first remaining entry packed on the target row.
                int insert = ids.Count;
                for (int j = 0, k = 0; j < view.RoleIds.Count; j++)
                {
                    if (j == dragEntry) continue;
                    if (view.Rows[j] == pendingRow) { insert = k; break; }
                    k++;
                }
                ids.Insert(insert, dragRoleId);
                mins.Insert(insert, pendingMin);
                maxes.Insert(insert, pendingMax);
            }
            if (changed)
                RoleCommands.SetRoleTraining(view.PathId, ids, mins, maxes);
        }

        /// A same-row neighbour whose band touches the pressed edge; tracked
        /// by id so the link survives (or is dropped on) snapshot changes.
        private static int FindLinked(RecPathView view, int i, BandDragKind kind)
        {
            for (int j = 0; j < view.RoleIds.Count; j++)
            {
                if (j == i || view.Rows[j] != view.Rows[i]) continue;
                if (kind == BandDragKind.MaxEdge && view.Mins[j] == view.Maxes[i])
                    return view.RoleIds[j];
                if (kind == BandDragKind.MinEdge && view.Maxes[j] == view.Mins[i])
                    return view.RoleIds[j];
            }
            return -1;
        }

        /// The chip X drops one trainee entry; the owner role always stays.
        private static void RemoveEntry(RecPathView view, int index)
        {
            if (view.RoleIds[index] == view.PathId) return;
            var ids = view.RoleIds.ToList();
            var mins = view.Mins.ToList();
            var maxes = view.Maxes.ToList();
            ids.RemoveAt(index);
            mins.RemoveAt(index);
            maxes.RemoveAt(index);
            RoleCommands.SetRoleTraining(view.PathId, ids, mins, maxes);
        }

        // ----- Left: global options -----

        /// Editor-style mini-header: small dim label over a faint rule.
        private static float MiniHeader(float x, float y, float width, string label, StructuredTip tip)
        {
            Text.Font = GameFont.Small;
            var labelRect = new Rect(x, y, width, 22f);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(labelRect, label);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            WrText.LineHorizontal(x, y + 24f, width);
            GUI.color = Color.white;
            if (tip != null) StructuredTipPresenter.TipRegion(labelRect, tip);
            return y + 30f;
        }

        private static void DrawHelpParagraph(Rect rect, string text)
        {
            GameFont previousFont = Text.Font;
            Color previousColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                GUI.color = WrStyle.CaptionText;
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = previousColor;
                Text.Font = previousFont;
            }
        }

        /// Layout extra below an expanded section's accordion header. MUST
        /// mirror the expanded branch of the section draw loop.
        private static float SectionExpandedExtra(RecTuningSection section) =>
            4f + Mathf.Max(section.IntroHeight, 24f) + 6f
                + section.Height + RecPanelPad * 2f + 8f;

        private static readonly Color CellPanel = new Color(1f, 1f, 1f, 0.06f);
        // Accent at half strength: marks the active segment without glowing.
        private static readonly Color SegmentActiveOutline = new Color(
            WrStyle.MinorAccent.r, WrStyle.MinorAccent.g, WrStyle.MinorAccent.b, 0.5f);

        private static void DrawTuningSection(Rect panel, RecTuningSection section)
        {
            GameFont previousFont = Text.Font;
            Color previousColor = GUI.color;
            try
            {
                Widgets.DrawBoxSolidWithOutline(
                    panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
                var origin = new Vector2(
                    panel.x + RecPanelPad, panel.y + RecPanelPad);
                for (int index = 0; index < section.Count; index++)
                {
                    RecTuningItem item = section.ItemAt(index);
                    if (item.Table != null) DrawTuningTable(item.Table, origin);
                    else DrawTuningRow(item.Row, origin);
                }
            }
            finally
            {
                GUI.color = previousColor;
                Text.Font = previousFont;
            }
        }

        private static void DrawTuningRow(
            RecommendationTuningRow row, Vector2 origin)
        {
            Rect rect = Offset(row.RowRect, origin);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(
                rect.x, rect.y, rect.width - 116f, 21f), row.Label);
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(new Rect(
                rect.x,
                rect.y + 21f,
                rect.width - 116f,
                rect.height - 21f),
                row.Description);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float controlsX = rect.xMax - 108f;
            if (row.EnumOptions != null)
            {
                DrawTuningEnumSegments(
                    new Rect(controlsX, rect.y + 6f, 108f, 26f), row);
                return;
            }
            if (Widgets.ButtonText(
                    new Rect(controlsX, rect.y + 6f, 26f, 26f), "−"))
                RoleCommands.SetRecommendationTuningOption(
                    (int)row.Descriptor.Option,
                    row.Value - NumericStepperUI.StepSize(row.Descriptor.Step));
            if (row.Descriptor.ValueKind == RecommendationTuningValueKind.Integer)
            {
                int? committed = NumericStepperUI.DrawNumericField(
                    new Rect(controlsX + 30f, rect.y + 6f, 48f, 26f),
                    row.ControlName, 0, row.ValueLabel);
                if (committed.HasValue)
                    RoleCommands.SetRecommendationTuningOption(
                        (int)row.Descriptor.Option, committed.Value);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(
                    new Rect(controlsX + 26f, rect.y + 6f, 56f, 26f),
                    row.ValueLabel);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (Widgets.ButtonText(
                    new Rect(controlsX + 82f, rect.y + 6f, 26f, 26f), "+"))
                RoleCommands.SetRecommendationTuningOption(
                    (int)row.Descriptor.Option,
                    row.Value + NumericStepperUI.StepSize(row.Descriptor.Step));
        }

        /// Enum-valued option rows: one single-click cell per value (signal
        /// letters in their verdict colors); the active cell gets the accent
        /// outline and full-strength letter, inactive letters render dimmed.
        private static void DrawTuningEnumSegments(Rect area,
            RecommendationTuningRow row)
        {
            int count = row.EnumOptions.Count;
            const float gap = 2f;
            float cellW = (area.width - (count - 1) * gap) / count;
            int active = row.Value - row.Descriptor.MinimumValue;
            Text.Anchor = TextAnchor.MiddleCenter;
            for (int i = 0; i < count; i++)
            {
                var cell = new Rect(area.x + i * (cellW + gap), area.y,
                    cellW, area.height);
                Widgets.DrawBoxSolid(cell, CellPanel);
                if (i == active)
                {
                    GUI.color = SegmentActiveOutline;
                    Widgets.DrawBox(cell);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(cell);
                }
                Color letter = row.EnumColors[i];
                GUI.color = new Color(letter.r, letter.g, letter.b,
                    i == active ? 1f : 0.45f);
                Widgets.Label(cell, row.EnumOptions[i]);
                GUI.color = Color.white;
                if (i != active && Widgets.ButtonInvisible(cell))
                    RoleCommands.SetRecommendationTuningOption(
                        (int)row.Descriptor.Option,
                        row.Descriptor.MinimumValue + i);
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// A grouped table row: label and caption on the left, colored column
        /// headers over compact value cells right-aligned like the single-row
        /// controls, the click hint below the cells. Click a cell to raise the
        /// value one step, right-click to lower it; the command clamps and
        /// no-ops at the bounds.
        private static void DrawTuningTable(RecTuningTable table, Vector2 origin)
        {
            Event e = Event.current;
            Text.Font = GameFont.Small;
            Widgets.Label(Offset(table.LabelRect, origin), table.Label);
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(Offset(table.DescriptionRect, origin),
                table.Description);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Offset(table.HintRect, origin), table.Hint);
            GUI.color = Color.white;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            for (int index = 0; index < table.CellCount; index++)
            {
                RecTuningTableCell cell = table.CellAt(index);
                Rect headerRect = Offset(cell.HeaderRect, origin);
                GUI.color = cell.HeaderColor;
                Widgets.Label(headerRect, cell.Header);
                GUI.color = Color.white;
                if (cell.HeaderTip != null)
                    WrTips.Key(cell.HeaderTip).Region(headerRect);

                Rect cellRect = Offset(cell.CellRect, origin);
                Widgets.DrawBoxSolid(cellRect, CellPanel);
                Widgets.Label(cellRect, cell.ValueLabel);
                Widgets.DrawHighlightIfMouseover(cellRect);
                if (e.type == EventType.MouseDown
                    && (e.button == 0 || e.button == 1)
                    && cellRect.Contains(e.mousePosition))
                {
                    int step = NumericStepperUI.StepSize(cell.Descriptor.Step)
                        * (e.button == 0 ? 1 : -1);
                    RoleCommands.SetRecommendationTuningOption(
                        (int)cell.Descriptor.Option, cell.Value + step);
                    e.Use();
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static Rect Offset(Rect r, Vector2 by) =>
            new Rect(r.x + by.x, r.y + by.y, r.width, r.height);

        /// The recommendation order template panel: pinned role chips, drag to
        /// reorder, X to unpin (the role reverts to dynamic placement), Add
        /// Role to pin unlisted roles at their suggested spot.
        private void DrawRecommendationOrder(Rect panel, RoleStore store)
        {
            Widgets.DrawBoxSolidWithOutline(
                panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
            var origin = new Vector2(panel.x + RecPanelPad, panel.y + RecPanelPad);
            var order = state.Order;
            var roles = state.OrderRoles;
            var layout = state.OrderLayout;
            var byId = state.OrderById;

            Text.Font = GameFont.Small;
            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                var chipRect = Offset(layout[i], origin);
                var click = RoleChipUI.Draw(chipRect, role, ChipStyle.Normal,
                    showRemove: true, dragSource: null, onClick: null);
                if (click == ChipClick.Remove)
                {
                    var edited = order.ToList();
                    edited.Remove(role.id);
                    RoleCommands.SetRecommendationOrder(edited);
                }
                if (byId.TryGetValue(role.id, out var rec) && rec.Hunting)
                {
                    GUI.color = LockedColor;
                    Widgets.DrawBox(chipRect);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(chipRect))
                        WrTips.Key("WR_OptLockedTip").Region(chipRect);
                }
            }

            if (Widgets.ButtonText(Offset(state.OrderAddRect, origin), "WR_AddRole".Translate()))
                OpenAddMenu(store, order, byId);

            if (RoleDrag.Active && order.Contains(RoleDrag.RoleId) && Mouse.IsOver(panel))
            {
                // Layout rects are chips-local: shift the mouse, not the list.
                int insertIndex = RoleDrag.ChipInsertIndex(
                    Event.current.mousePosition - origin, layout, rect => rect);

                float markerX, markerY;
                if (insertIndex == 0 || layout.Count == 0)
                {
                    markerX = -RecommendationsTabState.FlowGap / 2f;
                    markerY = 0f;
                }
                else
                {
                    var prev = layout[insertIndex - 1];
                    markerX = prev.xMax + RecommendationsTabState.FlowGap / 2f;
                    markerY = prev.y;
                }
                Widgets.DrawBoxSolid(new Rect(origin.x + markerX - 1f, origin.y + markerY + 3f,
                    2f, RoleChipUI.Height - 6f), new Color(1f, 1f, 1f, 0.9f));

                int draggedId = RoleDrag.RoleId;
                int from = state.OrderIndexOf(draggedId);
                int to = insertIndex > from ? insertIndex - 1 : insertIndex;
                if (to != from)
                {
                    // RoleDrag clears its slot every frame, so reassignment is
                    // per pass — but the list copy and closure are rebuilt only
                    // when the reorder target (or the order itself) changes.
                    if (dropStamp != state.OrderStamp || dropFrom != from || dropTo != to)
                    {
                        dropStamp = state.OrderStamp;
                        dropFrom = from;
                        dropTo = to;
                        var edited = order.ToList();
                        RoleDrag.HoverDropAction = () =>
                        {
                            edited.RemoveAt(from);
                            edited.Insert(to, draggedId);
                            RoleCommands.SetRecommendationOrder(edited);
                        };
                        dropAction = RoleDrag.HoverDropAction;
                    }
                    else
                        RoleDrag.HoverDropAction = dropAction;
                }
            }
        }

        /// Pin an unlisted role: it enters at its suggested (dynamic) spot,
        /// not at the end. Candidate selection is Core logic (AddCandidates);
        /// this only maps ids to labels.
        private static void OpenAddMenu(RoleStore store, IReadOnlyList<int> order,
            IReadOnlyDictionary<int, RoleView> byId)
        {
            var options = new List<FloatMenuOption>();
            foreach (int id in OrderTemplate.AddCandidates(byId.Values.ToList(), order)
                         .OrderBy(candidate => store.RoleById(candidate)?.label,
                             System.StringComparer.OrdinalIgnoreCase))
            {
                var role = store.RoleById(id);
                if (role == null) continue;
                int captured = id;
                options.Add(new FloatMenuOption(role.label, () =>
                {
                    var edited = order.ToList();
                    int at = byId.TryGetValue(captured, out var rec)
                        ? OrderTemplate.InsertIndex(rec, edited, byId.Values.ToList())
                        : edited.Count;
                    edited.Insert(at, captured);
                    RoleCommands.SetRecommendationOrder(edited);
                }));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
