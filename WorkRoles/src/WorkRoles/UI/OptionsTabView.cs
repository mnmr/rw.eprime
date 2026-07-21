using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    /// Options tab: per-save toggles, the recommendation order template and
    /// the Training Paths editor. These live here (not in Mod Settings)
    /// because they are per-savegame state, synced in MP.
    public class OptionsTabView
    {
        // Hunting chips: pinning them disables the dynamic duty-slot placement.
        private static readonly Color LockedColor = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        private readonly OptionsTabState state = new OptionsTabState();
        private Vector2 tabScroll;

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
        // Strip above the axis that keeps row 0's live drag readout visible
        // inside the panel (axis + rows share it).
        private const float RowsTopPad = 16f;
        // Axis baseline sits at axis bottom + 1; rows follow 2px below it.
        private const float RowsStartY = RowsTopPad + AxisH + 4f;
        private const float ChipsPanelPad = 10f; // 4px air around the selection box (chip+1px)
        private const float RecPanelPad = 8f;
        private const float WhenPanelPad = 8f;
        // Tiny-font captions over the paths and progression panels: full line
        // height (16f clipped descenders) and one shared caption -> panel gap.
        private const float PanelCaptionH = 18f;
        private const float PanelCaptionGap = 2f;

        public void Reset()
        {
            tabScroll = Vector2.zero;
            state.Reset();
            ClearBandDrag();
        }

        /// Language-only invalidation. The active path, scroll position and
        /// disclosure state remain unchanged.
        internal void InvalidateLanguageCaches()
        {
            state.InvalidateLanguageCaches();
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

            state.ResolvePendingPathSelection(store);

            // One column inside one scroll view. The 16px bar reserve is
            // unconditional so wrap widths never depend on the height they produce.
            float viewW = rect.width - 16f;
            const float flowX = 16f;
            float flowW = Mathf.Min(viewW - 32f, 640f);

            state.EnsureOrder(store, flowW - RecPanelPad * 2f);
            state.EnsurePaths(store, flowW - ChipsPanelPad * 2f);
            state.EnsureTips();
            OptionsPathView pathView = state.Path;

            // The whole y-flow is laid out up front: the scroll view needs
            // contentH before anything draws.
            float y = 12f;
            // Compatibility Settings live in their own column right of the main
            // flow; a window too narrow for both stacks them on top as before.
            float rightX = flowX + flowW + 24f;
            float rightW = Mathf.Min(viewW - rightX - 8f, 420f);
            bool sideBySide = rightW >= 240f;
            float compatX = sideBySide ? rightX : flowX;
            float compatW = sideBySide ? rightW : flowW;
            float cy = 12f;
            var compatHeader = new Rect(compatX, cy, compatW, 28f);
            cy += 32f;
            var numericRect = new Rect(compatX, cy, compatW, 28f);
            cy += 34f;
            var rangeRect = new Rect(compatX, cy, compatW, 28f);
            cy += 40f;
            if (!sideBySide) y = cy;
            var tuningHeader = new Rect(flowX, y, flowW, 28f);
            y += 32f;
            float recHeaderY = y;
            y += 30f;
            var recPanel = new Rect(flowX, y, flowW,
                state.OrderLayoutHeight + RecPanelPad * 2f);
            Text.Font = GameFont.Small;
            string recOrderHelp = "WR_RecOrderHelp".Translate();
            float recOrderHelpHeight = Text.CalcHeight(recOrderHelp, flowW);
            var recOrderHelpRect = new Rect(flowX, recPanel.yMax + 8f, flowW, recOrderHelpHeight);
            y = recOrderHelpRect.yMax + 12f;
            float pathsHeaderY = y;
            y += 30f;
            string trainingHelp = "WR_TrainingHelp".Translate();
            float trainingHelpHeight = Text.CalcHeight(trainingHelp, flowW);
            var trainingHelpRect = new Rect(flowX, y, flowW, trainingHelpHeight);
            y = trainingHelpRect.yMax + 8f;
            var captionRect = new Rect(flowX, y, flowW, PanelCaptionH);
            y += PanelCaptionH + PanelCaptionGap;
            var chipsPanel = new Rect(flowX, y, flowW,
                state.PathChipsHeight + ChipsPanelPad * 2f);
            y = chipsPanel.yMax + 12f;

            // A drag whose path or entry vanished must not block future presses.
            if (dragPathId != -1 && (pathView == null || dragPathId != pathView.PathId
                    || !pathView.RoleIds.Contains(dragRoleId)))
                ClearBandDrag();

            float editorY = y;
            bool anchorShown = state.IsAnchorShown(pathView);
            Rect whenPanel = default;
            Rect whenCaptionRect = default;
            if (pathView != null)
            {
                float whenPanelTop = editorY + 30f + 32f + (anchorShown ? 34f : 0f);
                whenCaptionRect = new Rect(flowX, whenPanelTop, flowW, PanelCaptionH);
                whenPanel = new Rect(flowX, whenPanelTop + PanelCaptionH + PanelCaptionGap,
                    flowW, WhenPanelPad * 2f + RowsStartY + pathView.DisplayRows * BandRowH);
                y = whenPanel.yMax;
            }

            Widgets.BeginScrollView(rect, ref tabScroll, new Rect(0f, 0f, viewW, y + 12f));

            WrText.HeaderLabel(compatHeader, "WR_CompatSection".Translate());

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            TooltipHandler.TipRegion(numericRect, state.NumericTip.Activate());
            bool numericNew = numeric;
            Widgets.CheckboxLabeled(numericRect, "WR_OptNumeric".Translate(), ref numericNew);
            if (numericNew != numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);

            bool vanillaRange = store.reportVanillaPriorities;
            TooltipHandler.TipRegion(rangeRect, state.RangeTip.Activate());
            bool vanillaNew = vanillaRange;
            Widgets.CheckboxLabeled(rangeRect, "WR_OptVanillaRange".Translate(), ref vanillaNew);
            if (vanillaNew != vanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);

            WrText.HeaderLabel(tuningHeader, "WR_TuningSection".Translate());
            MiniHeader(flowX, recHeaderY, flowW, "WR_RecOrderHeader".Translate(),
                state.RecommendationOrderTip);
            DrawRecommendationOrder(recPanel, store);
            DrawHelpParagraph(recOrderHelpRect, recOrderHelp);

            MiniHeader(flowX, pathsHeaderY, flowW, "WR_TrainingSection".Translate(),
                state.TrainingTip);
            DrawHelpParagraph(trainingHelpRect, trainingHelp);
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(captionRect, "WR_PathsPanelCaption".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolidWithOutline(
                chipsPanel, WrStyle.PanelBackground, WrStyle.PanelOutline);
            DrawPathChips(chipsPanel.x + ChipsPanelPad, chipsPanel.y + ChipsPanelPad, store);

            if (pathView != null)
                DrawPathEditor(flowX, flowW, editorY, anchorShown, whenPanel,
                    whenCaptionRect, store, pathView);

            Widgets.EndScrollView();

            RoleChipUI.DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

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
            if (tip != null) TooltipHandler.TipRegion(labelRect, tip.Activate());
            return y + 30f;
        }

        private static void DrawHelpParagraph(Rect rect, string text)
        {
            Text.Font = GameFont.Small;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(rect, text);
            GUI.color = Color.white;
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
                        TooltipHandler.TipRegion(chipRect, "WR_OptLockedTip".Translate());
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
                    markerX = -OptionsTabState.FlowGap / 2f;
                    markerY = 0f;
                }
                else
                {
                    var prev = layout[insertIndex - 1];
                    markerX = prev.xMax + OptionsTabState.FlowGap / 2f;
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

        /// The selected path's editor: mini-header, name row + assignment
        /// order opt-in, optional Assign row, then the WHEN panel.
        private void DrawPathEditor(float x, float width, float y, bool anchorShown,
            Rect whenPanel, Rect whenCaptionRect, RoleStore store, OptionsPathView view)
        {
            y = MiniHeader(x, y, width, "WR_PathEditorHeader".Translate(), null);

            // Assignment order opt-in mirrors the Auto-role checkbox: derived
            // from state (anchor set) OR revealed-by-hand; unchecking clears.
            // Shares the name row, right-aligned (CheckboxLabeled pins its box
            // to the rect's right edge).
            bool anchorSet = view.AnchorRoleId != -1;
            string custLabel = "WR_CustomizeAssignment".Translate();
            float custW = WrText.FitWidth(custLabel) + 30f;
            var custRect = new Rect(x + width - custW, y + 1f, custW, 24f);
            bool custWanted = anchorShown;
            Widgets.CheckboxLabeled(custRect, custLabel, ref custWanted);
            if (custWanted != anchorShown)
            {
                if (custWanted)
                    state.SetAnchorRevealed(view.PathId, revealed: true);
                else
                {
                    if (anchorSet)
                        RoleCommands.SetTrainingPathAnchor(view.PathId, -1, view.AnchorBefore);
                    state.SetAnchorRevealed(view.PathId, revealed: false);
                }
            }

            DrawPathNameRow(new Rect(x, y, width - custW - 12f, 26f), view);
            y += 32f;

            if (anchorShown)
                DrawPathAnchorRow(new Rect(x, y, width, 28f), store, view);

            // Caption above the WHEN panel (single line, truncated; full text as tooltip).
            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            string whenCaption = "WR_WhenPanelCaption".Translate();
            Widgets.Label(whenCaptionRect, whenCaption.Truncate(whenCaptionRect.width));
            TooltipHandler.TipRegion(whenCaptionRect, whenCaption);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            DrawWhenPanel(whenPanel, store, view);
        }

        /// The WHEN editor panel: the 0..21 axis on top (readout headroom above
        /// it), the packed band rows, then Add Role on the trailing empty row.
        /// Natural height — the tab's scroll view absorbs any overflow.
        private void DrawWhenPanel(Rect panel, RoleStore store, OptionsPathView view)
        {
            float bandW = panel.width - WhenPanelPad * 2f;
            // Below this, a min-span chip can't hold its grips + X.
            if (bandW < 150f) return;

            Widgets.DrawBoxSolidWithOutline(
                panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
            var inner = panel.ContractedBy(WhenPanelPad);
            DrawAxis(new Rect(inner.x, inner.y + RowsTopPad, bandW, AxisH));
            DrawBandRows(store, view, inner.x, inner.y, bandW);
        }

        /// One chip per stored path: name + delete X; click selects. The
        /// trailing Add New button is part of the same flow.
        private void DrawPathChips(float baseX, float baseY, RoleStore store)
        {
            var e = Event.current;
            var pathChips = state.PathChips;
            for (int i = 0; i < pathChips.Count; i++)
            {
                var chip = pathChips[i];
                var r = new Rect(baseX + chip.Rect.x, baseY + chip.Rect.y,
                    chip.Rect.width, chip.Rect.height);
                PathChipUI.Draw(r, chip.Name, chip.Color, chip.Id == state.SelectedPathId);

                if (e.type != EventType.MouseDown || e.button != 0
                    || !r.Contains(e.mousePosition)) continue;
                if (ChipUI.RemoveRect(r).Contains(e.mousePosition))
                {
                    int id = chip.Id;
                    Find.WindowStack.Add(new Dialog_SmallConfirm(
                        "WR_DeletePathConfirm".Translate(chip.Name),
                        () => RoleCommands.DeleteTrainingPath(id)));
                }
                else
                    state.SelectPath(chip.Id);
                e.Use();
            }

            if (Widgets.ButtonText(Offset(state.PathAddRect, new Vector2(baseX, baseY)),
                    "WR_AddNew".Translate()))
                Find.WindowStack.Add(new Dialog_RenameRole("WR_NewPathTitle".Translate(), name =>
                {
                    RoleCommands.CreateTrainingPath(name);
                    state.SelectPathWhenCreated(name);
                }));
        }

        /// Name row: grey caption, path-color dot, plain name, rename pencil
        /// (dialog commits ONE RenameTrainingPath).
        private static void DrawPathNameRow(Rect rect, OptionsPathView view)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string caption = "WR_PathNameLabel".Translate();
            float captionW = WrText.FitWidth(caption);
            GUI.color = WrStyle.DimText;
            Widgets.Label(new Rect(rect.x, rect.y, captionW, rect.height), caption);
            GUI.color = Color.white;
            float x = rect.x + captionW + 6f;

            var dotRect = new Rect(x, rect.y + (rect.height - 16f) / 2f, 16f, 16f);
            GUI.color = view.Color;
            GUI.DrawTexture(dotRect, WorkRolesTex.Circle);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(dotRect, "WR_PathColorTip".Translate());
            if (Widgets.ButtonInvisible(dotRect))
                Find.WindowStack.Add(new Dialog_PathColor(view.PathId));
            x += 16f + 6f;

            float nameW = Mathf.Min(WrText.FitWidth(view.Name), rect.xMax - x - 36f);
            Widgets.Label(new Rect(x, rect.y, nameW, rect.height), view.Name.Truncate(nameW));
            Text.Anchor = TextAnchor.UpperLeft;
            x += nameW + 6f;

            int pathId = view.PathId;
            if (Widgets.ButtonImage(new Rect(x, rect.y + (rect.height - 24f) / 2f, 24f, 24f),
                    TexButton.Rename))
                Find.WindowStack.Add(new Dialog_RenameRole("WR_RenamePathTitle".Translate(),
                    name => RoleCommands.RenameTrainingPath(pathId, name), view.Name));
        }

        /// Grey caption + Before/After toggle + the anchor as a role chip (or
        /// a Pick-role button while unset). The group right-aligns under the
        /// Customize toggle. Reads snapshot only.
        private void DrawPathAnchorRow(Rect rect, RoleStore store, OptionsPathView view)
        {
            Text.Font = GameFont.Small;
            string caption = "WR_AnchorLabel".Translate();
            float captionW = WrText.FitWidth(caption);
            Role anchorRole = view.AnchorRoleId != -1 ? store.RoleById(view.AnchorRoleId) : null;
            float tailW = anchorRole != null
                ? RoleChipUI.WidthFor(anchorRole, showRemove: false)
                : 110f;
            float x = rect.xMax - (captionW + 4f + 70f + 8f + tailW);
            TooltipHandler.TipRegion(new Rect(x, rect.y, rect.xMax - x, rect.height),
                state.AnchorTip.Activate());

            // MiddleLeft over the row height: the caption shares the text
            // baseline with the Before/After button and the chip.
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(x, rect.y, captionW, rect.height), caption);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            x += captionW + 4f;

            string toggleLabel = view.AnchorBefore
                ? "WR_AnchorBefore".Translate()
                : "WR_AnchorAfter".Translate();
            if (Widgets.ButtonText(new Rect(x, rect.y, 70f, rect.height), toggleLabel))
                RoleCommands.SetTrainingPathAnchor(view.PathId, view.AnchorRoleId, !view.AnchorBefore);
            x += 78f;

            if (anchorRole != null)
            {
                var chipRect = new Rect(x, rect.y + (rect.height - RoleChipUI.Height) / 2f,
                    tailW, RoleChipUI.Height);
                RoleChipUI.Draw(chipRect, anchorRole, ChipStyle.Normal, showRemove: false,
                    dragSource: null, onClick: null, interactive: false);
                if (Widgets.ButtonInvisible(chipRect))
                    OpenAnchorMenu(store, view);
            }
            else if (Widgets.ButtonText(new Rect(x, rect.y, tailW, rect.height),
                    "WR_PickRole".Translate()))
                OpenAnchorMenu(store, view);
        }

        /// Clearing runs through the Customize checkbox, so the menu only
        /// lists candidates: normal roles (not blocker/rules), alphabetical.
        private static void OpenAnchorMenu(RoleStore store, OptionsPathView view)
        {
            var options = new List<FloatMenuOption>();
            foreach (var role in store.roles
                         .Where(r => !r.blocker && !r.HasRules)
                         .OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase))
            {
                int capturedId = role.id;
                options.Add(new FloatMenuOption(role.label, () =>
                    RoleCommands.SetTrainingPathAnchor(view.PathId, capturedId, view.AnchorBefore)));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        /// Level numbers, a 1px tick under each level and the baseline, all on
        /// the exact band span. Labels center on their ticks; 0 and 21 would
        /// overhang the panel edge, so only their ticks render.
        private static void DrawAxis(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = WrStyle.DimText;
            float scale = rect.width / SkillProgressionMath.MaxLevel;
            for (int lvl = 1; lvl < SkillProgressionMath.MaxLevel; lvl++)
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
        /// players may know better). The first role gets the full axis; later
        /// picks enter min-width at the top, drag to place.
        private static void OpenAddRoleMenu(RoleStore store, OptionsPathView view)
        {
            var options = new List<FloatMenuOption>();
            foreach (var (role, tier) in store.roles
                         .Where(r => IsNormal(r) && !view.RoleIds.Contains(r.id))
                         .Select(r => (role: r, tier: HasXpJobs(r) ? 1 : 2))
                         .OrderBy(t => t.tier)
                         .ThenBy(t => t.role.label, System.StringComparer.OrdinalIgnoreCase))
            {
                int captured = role.id;
                bool first = view.RoleIds.Count == 0;
                var ids = view.RoleIds.ToList();
                var mins = view.Mins.ToList();
                var maxes = view.Maxes.ToList();
                string label = tier == 2
                    ? role.label.Colorize(new Color(0.62f, 0.62f, 0.62f))
                    : role.label;
                var option = new FloatMenuOption(label, () =>
                {
                    ids.Add(captured);
                    mins.Add(first ? 0
                        : SkillProgressionMath.MaxLevel - SkillProgressionMath.MinSpan);
                    maxes.Add(SkillProgressionMath.MaxLevel);
                    RoleCommands.SetTrainingPathBands(view.PathId, ids, mins, maxes);
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
        private void DrawBandRows(RoleStore store, OptionsPathView view,
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
                RoleChipUI.DrawBandChip(chipRect, view.Roles[i]);

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
                if (ChipUI.BandRemoveRect(chipRect).Contains(e.mousePosition))
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
        private void UpdateBandDrag(OptionsPathView view, int dragEntry, Event e, float bandX,
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
        private void CommitBandDrag(OptionsPathView view, int dragEntry)
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
                RoleCommands.SetTrainingPathBands(view.PathId, ids, mins, maxes);
        }

        /// A same-row neighbour whose band touches the pressed edge; tracked
        /// by id so the link survives (or is dropped on) snapshot changes.
        private static int FindLinked(OptionsPathView view, int i, BandDragKind kind)
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

        /// The chip X drops one entry; the path itself survives empty.
        private static void RemoveEntry(OptionsPathView view, int index)
        {
            var ids = view.RoleIds.ToList();
            var mins = view.Mins.ToList();
            var maxes = view.Maxes.ToList();
            ids.RemoveAt(index);
            mins.RemoveAt(index);
            maxes.RemoveAt(index);
            RoleCommands.SetTrainingPathBands(view.PathId, ids, mins, maxes);
        }

    }
}
