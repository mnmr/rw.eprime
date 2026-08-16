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
        private System.Action? dropAction;

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
        private string? whenCaptionText;
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

            state.EnsurePanels(flowW);
            RecOrderSnapshot order = state.Order!; // EnsureOrder ran above
            RecRoleDetailSnapshot? detail = expandedRoleId != -1
                ? state.EnsureDetail(store, expandedRoleId, flowW - PanelPad * 2f)
                : null;
            if (detail == null && expandedRoleId != -1)
                // The expanded role vanished.
                expandedRoleId = -1;

            // A drag whose path or entry vanished must not block future presses.
            if (dragPathId != -1)
            {
                RecPathView? dragView = FindPathView(detail, dragPathId);
                if (dragView == null || !dragView.ContainsRole(dragRoleId))
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
                order.LayoutHeight + RecPanelPad * 2f);
            float columnsTop = recPanel.yMax + 16f;

            // LEFT column: the role panels.
            y = columnsTop;
            var leftHeader = new Rect(flowX, y, flowW, 28f);
            y += 26f;
            RecRolePanelsSnapshot panels = state.Panels!; // EnsurePanels ran above
            var panelsHelpRect = new Rect(flowX, y, flowW,
                panels.HelpHeight);
            y = panelsHelpRect.yMax + 8f;
            float panelsStartY = y;
            float bodyHeight = detail == null
                ? 0f : BodyHeight(detail, flowW - PanelPad * 2f);
            for (int i = 0; i < panels.Count; i++)
            {
                y += PanelHeaderH;
                if (detail != null
                    && panels.PanelAt(i).Chip.RoleId == detail.RoleId)
                    y += bodyHeight + PanelPad * 2f;
                y += PanelGap;
            }

            // RIGHT column: the accordion tuning sections; a window too
            // narrow for both stacks them below the role panels.
            float gx = sideBySide ? rightX : flowX;
            float gy = sideBySide ? columnsTop : y;
            var rightHeader = new Rect(gx, gy, gw, 28f);
            gy += 26f;
            RecTuningSnapshot tuning = state.Tuning!; // EnsureTuning ran above
            var globalHelpRect = new Rect(gx, gy, gw, tuning.GlobalHelpHeight);
            gy = globalHelpRect.yMax + 8f;
            float tuningStartY = gy;
            for (int i = 0; i < tuning.Count; i++)
            {
                gy += PanelHeaderH;
                if (expandedSections.Contains(i))
                    gy += SectionExpandedExtra(tuning.SectionAt(i));
                gy += PanelGap;
            }

            float contentH = Mathf.Max(y, gy) + 12f;
            Widgets.BeginScrollView(rect, ref tabScroll,
                new Rect(0f, 0f, viewW, contentH));
            try
            {

            MiniHeader(flowX, recHeaderY, orderW, order.HeaderLabel,
                state.RecommendationOrderTip);
            DrawRecommendationOrder(recPanel);
            DrawHelpParagraph(recOrderHelpRect, state.RecommendationOrderHelp);

            WrText.HeaderLabel(leftHeader, panels.HeaderLabel);
            DrawHelpParagraph(panelsHelpRect, panels.Help);
            DrawRolePanels(flowX, panelsStartY, flowW, panels,
                detail, bodyHeight);

            WrText.HeaderLabel(rightHeader, tuning.HeaderLabel);
            DrawHelpParagraph(globalHelpRect, tuning.GlobalHelp);
            float ty = tuningStartY;
            for (int i = 0; i < tuning.Count; i++)
            {
                RecTuningSection section = tuning.SectionAt(i);
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
                    var resetRect = new Rect(gx + gw - 70f, ty + 4f, 70f, 24f);
                    WrTips.Key("WR_RecTuneResetTip").Region(resetRect);
                    if (Widgets.ButtonText(resetRect, tuning.ResetLabel))
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

            }
            finally
            {
                Widgets.EndScrollView();
            }

            RoleChipUI.DrawDragGhost();
            RoleDrag.ResolveMouseUp();
        }

        private static RecPathView? FindPathView(
            RecRoleDetailSnapshot? detail, int pathId)
        {
            if (detail == null) return null;
            for (int i = 0; i < detail.PathCount; i++)
                if (detail.PathAt(i).PathId == pathId) return detail.PathAt(i);
            return null;
        }

        // ----- Right: accordion of tuning-eligible roles -----

        private void DrawRolePanels(float x, float y, float width,
            RecRolePanelsSnapshot panels,
            RecRoleDetailSnapshot? detail, float bodyHeight)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                RecRolePanel panel = panels.PanelAt(i);
                RoleChipRenderData chip = panel.Chip;
                // Keyed to the snapshot, not expandedRoleId: a mid-pass click
                // must not pair a later panel with another role's detail.
                bool expanded = detail != null && chip.RoleId == detail.RoleId;

                var headerRect = new Rect(x, y, width, PanelHeaderH);
                Widgets.DrawBoxSolid(headerRect, new Color(1f, 1f, 1f, 0.06f));
                var arrowRect = new Rect(x + 6f,
                    y + (PanelHeaderH - 18f) / 2f, 18f, 18f);
                GUI.DrawTexture(arrowRect,
                    expanded ? TexButton.Collapse : TexButton.Reveal);
                float chipW = Mathf.Min(panel.ChipWidth,
                    width - (arrowRect.xMax - x) - 10f);
                var chipRect = new Rect(arrowRect.xMax + 6f,
                    y + (PanelHeaderH - RoleChipUI.Height) / 2f,
                    chipW, RoleChipUI.Height);
                RoleChipUI.Draw(chipRect, chip, ChipStyle.Normal,
                    showRemove: false, dragSource: null, onClick: null,
                    interactive: false);
                Widgets.DrawHighlightIfMouseover(headerRect);
                if (Widgets.ButtonInvisible(headerRect))
                {
                    expandedRoleId = expanded ? -1 : chip.RoleId;
                    ClearBandDrag();
                }
                y += PanelHeaderH;

                if (expanded)
                {
                    var body = new Rect(x, y, width,
                        bodyHeight + PanelPad * 2f);
                    Widgets.DrawBoxSolidWithOutline(
                        body, WrStyle.PanelBackground, WrStyle.PanelOutline);
                    DrawExpandedBody(body.ContractedBy(PanelPad), detail!); // expanded ⇒ detail != null
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
                + detail.WorkTypesHeight
                + detail.UsedHeight
                + detail.TrainedHeight
                + (detail.GatedChipCount > 0 ? detail.GatedHeight : 0f)
                + detail.RequiredHeight;
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

        private void DrawExpandedBody(Rect inner, RecRoleDetailSnapshot detail)
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
            ry = DrawDerivedSkillTable(rightX, ry, halfW,
                detail.WorkTypesCaption, "WR_WorkTypesTip",
                detail, DerivedSkillSection.WorkTypes);
            ry = DrawDerivedSkillTable(rightX, ry, halfW,
                detail.UsedCaption, "WR_UsedSkillsTip",
                detail, DerivedSkillSection.Used);
            ry = DrawDerivedSkillTable(rightX, ry, halfW,
                detail.TrainedCaption, "WR_TrainedSkillsTip",
                detail, DerivedSkillSection.Trained);
            if (detail.GatedChipCount > 0)
                ry = DrawDerivedSkillTable(rightX, ry, halfW,
                    detail.GatedCaption, "WR_GatedSkillsTip",
                    detail, DerivedSkillSection.Gated);
            ry = DrawRequiredSkillTable(rightX, ry, halfW,
                "WR_RequiredSkillsTip", roleId, detail);

            if (!detail.ShowTrainingSection) return;
            ry += 8f;
            ry = MiniHeader(rightX, ry, halfW, detail.TrainingHeader,
                state.TrainingTip);
            for (int i = 0; i < detail.PathCount; i++)
                ry = DrawPathBlock(rightX, ry, halfW,
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
            string tipKey, string? tipArg, string valueLabel, int value,
            int roleId, ScalingField field, string controlName,
            string? unitSuffix = null)
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

        private enum DerivedSkillSection { WorkTypes, Used, Trained, Gated }

        /// Read-only derived skill rows. All labels and tips were resolved in
        /// the detail snapshot; drawing only indexes that immutable render
        /// data and registers prebuilt tip regions.
        private static float DrawDerivedSkillTable(
            float x, float y, float width, string caption, string tipKey,
            RecRoleDetailSnapshot detail, DerivedSkillSection section)
        {
            float pitch = detail.DerivedSkillRowHeight;
            float captionW = detail.SkillCaptionWidth;
            var captionRect = new Rect(x, y, captionW, pitch);
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            bool wrap = Text.WordWrap;
            Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = WrStyle.DimText;
                Widgets.Label(captionRect, caption);
                GUI.color = Color.white;
                Text.WordWrap = wrap;
                WrTips.Key(tipKey).Region(captionRect);
                int count = section switch
                {
                    DerivedSkillSection.WorkTypes => detail.WorkTypeChipCount,
                    DerivedSkillSection.Trained => detail.TrainedChipCount,
                    DerivedSkillSection.Gated => detail.GatedChipCount,
                    _ => detail.UsedChipCount,
                };
                for (int index = 0; index < count; index++)
                {
                    RecSkillChip chip = section switch
                    {
                        DerivedSkillSection.WorkTypes => detail.WorkTypeChipAt(index),
                        DerivedSkillSection.Trained => detail.TrainedChipAt(index),
                        DerivedSkillSection.Gated => detail.GatedChipAt(index),
                        _ => detail.UsedChipAt(index),
                    };
                    if (chip.Rect.width <= 0f) break;
                    var rowRect = new Rect(
                        x + captionW + chip.Rect.x,
                        y + chip.Rect.y,
                        chip.Rect.width,
                        chip.Rect.height);
                    Text.WordWrap = false;
                    Widgets.Label(rowRect, chip.Label);
                    Text.WordWrap = wrap;
                    if (chip.Tip != null)
                        StructuredTipPresenter.TipRegion(rowRect, chip.Tip);
                }
            }
            finally
            {
                GUI.color = color;
                Text.WordWrap = wrap;
                Text.Anchor = anchor;
                Text.Font = font;
            }
            return y + pitch;
        }

        /// Editable hard-gate rows: one selected skill and remove control per
        /// row, with the Add button sharing the first row.
        private static float DrawRequiredSkillTable(
            float x, float y, float width, string tipKey, int roleId,
            RecRoleDetailSnapshot detail)
        {
            float pitch = detail.RequiredSkillRowHeight;
            float addW = detail.AddSkillWidth;
            float captionW = detail.SkillCaptionWidth;
            float skillX = x + captionW;
            float buttonX = x + detail.RequiredAddX;
            float removeX = x + detail.RequiredRemoveX;
            float removeW = detail.RequiredRemoveWidth;

            var captionRect = new Rect(x, y, captionW, pitch);
            int count = detail.RequiredChipCount;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            bool wrap = Text.WordWrap;
            Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = WrStyle.DimText;
                Widgets.Label(captionRect, detail.RequiredCaption);
                GUI.color = Color.white;
                Text.WordWrap = wrap;
                WrTips.Key(tipKey).Region(captionRect);

                for (int i = 0; i < count; i++)
                {
                    RecSkillChip chip = detail.RequiredChipAt(i);
                    float rowY = y + i * pitch;
                    Text.WordWrap = false;
                    Widgets.Label(new Rect(skillX, rowY,
                        detail.RequiredSkillWidth, pitch), chip.Label);
                    Text.WordWrap = wrap;
                    var removeRect = new Rect(removeX,
                        rowY + (pitch - 24f) / 2f, removeW, 24f);
                    if (removeW > 0f)
                    {
                        WrTips.Key("WR_RemoveSkillTip").Region(removeRect);
                        if (Widgets.ButtonImage(removeRect, TexButton.Delete))
                            RoleCommands.RemoveRoleSkill(roleId, chip.DefName);
                    }
                }
                var addRect = new Rect(buttonX, y, addW, pitch);
                if (addW > 0f)
                {
                    WrTips.Key("WR_AddSkillTip").Region(addRect);
                    Text.WordWrap = false;
                    bool addClicked = Widgets.ButtonText(
                        addRect, detail.AddSkillLabel);
                    Text.WordWrap = wrap;
                    if (addClicked) OpenSkillMenu(roleId, detail);
                }
            }
            finally
            {
                GUI.color = color;
                Text.WordWrap = wrap;
                Text.Anchor = anchor;
                Text.Font = font;
            }
            return y + Mathf.Max(1, count) * pitch;
        }

        /// Menu-click-only def resolution, like the order panel's Add Role.
        private static void OpenSkillMenu(int roleId,
            RecRoleDetailSnapshot detail)
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
                    () => RoleCommands.AddRoleSkill(roleId, captured)));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        // ----- Per-role training path editor -----

        /// The role's own path block: wrapped caption and the frameless WHEN
        /// band area spanning the full block width. The owner role always
        /// stays in its path (its band chip carries no X).
        private float DrawPathBlock(float x, float y, float width,
            RecPathView view)
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
            DrawWhenPanel(whenPanel, view);
            return whenPanel.yMax;
        }

        /// The WHEN editor area: the 0..21 axis on top, the packed band rows,
        /// then Add Role on the trailing empty row. Frameless: bands span the
        /// full block width.
        private void DrawWhenPanel(Rect panel, RecPathView view)
        {
            float bandW = panel.width;
            // Below this, a min-span chip can't hold its grips + X.
            if (bandW < 150f) return;

            DrawAxis(new Rect(panel.x, panel.y, bandW, AxisH));
            DrawBandRows(view, panel.x, panel.y, bandW);
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

        /// Tier 1 plain, tier 2 greyed (mods can rewire XP in driver code, so
        /// players may know better). New picks enter min-width at the top,
        /// drag to place.
        private static void OpenAddRoleMenu(RecPathView view)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < view.AddOptionCount; i++)
            {
                RecRoleMenuOption published = view.AddOptionAt(i);
                int captured = published.RoleId;
                List<int> ids = view.CopyRoleIds();
                List<int> mins = view.CopyMins();
                List<int> maxes = view.CopyMaxes();
                var option = new FloatMenuOption(published.Label, () =>
                {
                    ids.Add(captured);
                    mins.Add(SkillProgressionMath.MaxLevel - SkillProgressionMath.MinSpan);
                    maxes.Add(SkillProgressionMath.MaxLevel);
                    RoleCommands.SetRoleTraining(view.PathId, ids, mins, maxes);
                });
                option.tooltip = published.Tooltip;
                options.Add(option);
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        /// The selected path's band rows; the trailing empty row carries the
        /// Add Role button (and stays the slide-to-re-row affordance). baseY is
        /// the panel's inner top; chips are display-only, all interaction is
        /// the explicit block below (X, handles, body slide).
        private void DrawBandRows(RecPathView view,
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
                : k == linkedEntry && dragKind == BandDragKind.MaxEdge
                    ? pendingMax : view.MinAt(k);
            int ShownMax(int k) => k == dragEntry ? pendingMax
                : k == linkedEntry && dragKind == BandDragKind.MinEdge
                    ? pendingMin : view.MaxAt(k);
            int ShownRow(int k) => k == dragEntry && dragKind == BandDragKind.Slide
                ? pendingRow : view.RowAt(k);

            // Dim divider below every display row, 2px clear of the chips on
            // both sides (BandRowH leaves 5px between chips).
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            for (int r = 1; r < view.DisplayRows; r++)
                WrText.LineHorizontal(bandX, rowsY + r * BandRowH - 3f, bandW);
            GUI.color = Color.white;

            // Add Role lives on the empty affordance row, right-aligned; a chip
            // slid onto that row draws over it (chips render later).
            var pathAddRect = new Rect(bandX + bandW - 110f,
                rowsY + (view.DisplayRows - 1) * BandRowH,
                110f, RoleChipUI.Height);
            WrTips.Key("WR_PathAddRoleTip").Region(pathAddRect);
            if (Widgets.ButtonText(pathAddRect, state.Order!.AddLabel)) // order built before any panel draws
                OpenAddRoleMenu(view);

            for (int i = 0; i < view.Count; i++)
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
                bool ownerEntry = view.RoleIdAt(i) == view.PathId;
                RoleChipUI.DrawBandChip(chipRect, view.ChipAt(i),
                    showRemove: !ownerEntry);
                if (dragPathId == -1)
                {
                    StructuredTip? entryTip = view.TipAt(i);
                    if (entryTip != null)
                        StructuredTipPresenter.TipRegion(chipRect, entryTip);
                    else
                        WrTips.Key("WR_BandChipTip").Region(chipRect);
                }

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
                    pendingRow = dragStartRow = view.RowAt(i);
                }
                dragPathId = view.PathId;
                dragRoleId = view.RoleIdAt(i);
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
            int min = view.MinAt(dragEntry), max = view.MaxAt(dragEntry);
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
                    ? SkillProgressionMath.ClampSharedEdge(
                        view.MinAt(linked), max, level)
                    : SkillProgressionMath.ClampEdge(min, max, true, level);
            else
                pendingMax = linked >= 0
                    ? SkillProgressionMath.ClampSharedEdge(
                        min, view.MaxAt(linked), level)
                    : SkillProgressionMath.ClampEdge(min, max, false, level);
        }

        /// ONE synced command commits the slid/resized band, the linked
        /// neighbour's shared edge and a vertical reorder together.
        private void CommitBandDrag(RecPathView view, int dragEntry)
        {
            List<int> ids = view.CopyRoleIds();
            List<int> mins = view.CopyMins();
            List<int> maxes = view.CopyMaxes();
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
                for (int j = 0, k = 0; j < view.Count; j++)
                {
                    if (j == dragEntry) continue;
                    if (view.RowAt(j) == pendingRow) { insert = k; break; }
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
            for (int j = 0; j < view.Count; j++)
            {
                if (j == i || view.RowAt(j) != view.RowAt(i)) continue;
                if (kind == BandDragKind.MaxEdge
                    && view.MinAt(j) == view.MaxAt(i))
                    return view.RoleIdAt(j);
                if (kind == BandDragKind.MinEdge
                    && view.MaxAt(j) == view.MinAt(i))
                    return view.RoleIdAt(j);
            }
            return -1;
        }

        /// The chip X drops one trainee entry; the owner role always stays.
        private static void RemoveEntry(RecPathView view, int index)
        {
            if (view.RoleIdAt(index) == view.PathId) return;
            List<int> ids = view.CopyRoleIds();
            List<int> mins = view.CopyMins();
            List<int> maxes = view.CopyMaxes();
            ids.RemoveAt(index);
            mins.RemoveAt(index);
            maxes.RemoveAt(index);
            RoleCommands.SetRoleTraining(view.PathId, ids, mins, maxes);
        }

        // ----- Left: global options -----

        /// Editor-style mini-header: small dim label over a faint rule.
        private static float MiniHeader(float x, float y, float width, string label, StructuredTip? tip)
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

        private static void DrawHelpParagraph(Rect rect, string? text)
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
                    else DrawTuningRow(item.Row!, origin); // exactly one of Row/Table is set
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
            // Controls occupy the rightmost 108px; captions keep 20px clear.
            Widgets.Label(new Rect(
                rect.x, rect.y, rect.width - 128f, 21f), row.Label);
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(new Rect(
                rect.x,
                rect.y + 21f,
                rect.width - 128f,
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
            var minusRect = new Rect(controlsX, rect.y + 6f, 26f, 26f);
            WrTips.Key("WR_StepModifiersTip").Region(minusRect);
            if (Widgets.ButtonText(minusRect, "−"))
                RoleCommands.SetRecommendationTuningOption(
                    (int)row.Descriptor.Option,
                    row.Value - NumericStepperUI.StepSize(row.Descriptor.Step));
            if (row.Descriptor.ValueKind == RecommendationTuningValueKind.Integer)
            {
                var fieldRect = new Rect(controlsX + 30f, rect.y + 6f, 48f, 26f);
                WrTips.Key("WR_StepModifiersTip").Region(fieldRect);
                int? committed = NumericStepperUI.DrawNumericField(
                    fieldRect, row.ControlName, 0, row.ValueLabel);
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
            var plusRect = new Rect(controlsX + 82f, rect.y + 6f, 26f, 26f);
            WrTips.Key("WR_StepModifiersTip").Region(plusRect);
            if (Widgets.ButtonText(plusRect, "+"))
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
                WrTips.Key(row.EnumTipKeys[i]).Region(cell);
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
                WrTips.Key("WR_StepModifiersTip").Region(cellRect);
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
        private void DrawRecommendationOrder(Rect panel)
        {
            Widgets.DrawBoxSolidWithOutline(
                panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
            var origin = new Vector2(panel.x + RecPanelPad, panel.y + RecPanelPad);
            RecOrderSnapshot order = state.Order!; // order built before any panel draws

            Text.Font = GameFont.Small;
            for (int i = 0; i < order.Count; i++)
            {
                RecOrderChip published = order.ChipAt(i);
                RoleChipRenderData chip = published.Chip;
                Rect chipRect = Offset(published.Rect, origin);
                WrTips.Key("WR_RecOrderChipTip").Region(chipRect);
                ChipClick click = RoleChipUI.Draw(
                    chipRect, chip, ChipStyle.Normal,
                    showRemove: true, dragSource: null, onClick: null);
                if (click == ChipClick.Remove)
                {
                    List<int> edited = order.CopyRoleIds();
                    edited.Remove(chip.RoleId);
                    RoleCommands.SetRecommendationOrder(edited);
                }
                if (published.Locked)
                {
                    GUI.color = LockedColor;
                    Widgets.DrawBox(chipRect);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(chipRect))
                        WrTips.Key("WR_OptLockedTip").Region(chipRect);
                }
            }

            Rect orderAddRect = Offset(order.AddRect, origin);
            WrTips.Key("WR_RecOrderAddTip").Region(orderAddRect);
            if (Widgets.ButtonText(orderAddRect, order.AddLabel))
                OpenAddMenu(order);

            if (RoleDrag.Active && order.ContainsRole(RoleDrag.RoleId)
                && Mouse.IsOver(panel))
            {
                // Layout rects are chips-local: shift the mouse, not the list.
                int insertIndex = order.ChipInsertIndex(
                    Event.current.mousePosition - origin);

                float markerX, markerY;
                if (insertIndex == 0 || order.Count == 0)
                {
                    markerX = -RecommendationsTabState.FlowGap / 2f;
                    markerY = 0f;
                }
                else
                {
                    Rect prev = order.ChipAt(insertIndex - 1).Rect;
                    markerX = prev.xMax + RecommendationsTabState.FlowGap / 2f;
                    markerY = prev.y;
                }
                Widgets.DrawBoxSolid(new Rect(origin.x + markerX - 1f, origin.y + markerY + 3f,
                    2f, RoleChipUI.Height - 6f), new Color(1f, 1f, 1f, 0.9f));

                int draggedId = RoleDrag.RoleId;
                int from = order.IndexOfRole(draggedId);
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
                        List<int> edited = order.CopyRoleIds();
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

        /// Pin an unlisted role: it appends at the end of the list; the player
        /// drags it into place. Candidate selection is Core logic
        /// (AddCandidates); this only maps ids to labels.
        private static void OpenAddMenu(RecOrderSnapshot order)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < order.AddOptionCount; i++)
            {
                RecRoleMenuOption published = order.AddOptionAt(i);
                int captured = published.RoleId;
                List<int> edited = order.CopyRoleIds();
                options.Add(new FloatMenuOption(published.Label, () =>
                {
                    edited.Add(captured);
                    RoleCommands.SetRecommendationOrder(edited);
                }));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
