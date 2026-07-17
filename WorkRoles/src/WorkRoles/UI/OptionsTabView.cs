using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Options tab: per-save toggles, the recommendation order template and
    /// the Training Paths editor. These live here (not in Mod Settings)
    /// because they are per-savegame state, synced in MP.
    public class OptionsTabView
    {
        // One gap for every wrapped chip flow (rec order, paths), buttons included.
        private const float FlowGap = 8f;
        // Hunting chips: pinning them disables the dynamic duty-slot placement.
        private static readonly Color LockedColor = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        // Dark panel + caption styling mirrors the role editor's top box and
        // the Roles tab's filter captions.
        private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        private static readonly Color PanelOutline = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color CaptionColor = new Color(0.60f, 0.62f, 0.64f);
        private static readonly Color DimLabel = new Color(0.6f, 0.6f, 0.6f);
        private Vector2 tabScroll;

        // Open-window snapshot (UiVersion): resolving the template projects the
        // whole catalog through RecRoleOf — that ran twice per pass before.
        private int orderStamp = -1;
        private float orderWidth = -1f;
        private List<int> orderCache;
        private Dictionary<int, RecRole> orderById;
        private List<Role> orderRoles;
        private readonly List<Rect> orderLayout = new List<Rect>();
        private Rect orderAddRect; // Add Role button, last element of the chip flow
        private float orderLayoutHeight;

        // Drag drop, rebuilt only when the reorder target changes (see p2 in
        // DrawRecommendationOrder): RoleDrag clears its slot every frame.
        private int dropStamp = -1;
        private int dropFrom = -1;
        private int dropTo = -1;
        private System.Action dropAction;

        // Training Paths section (open-window snapshot, one per
        // UiVersion x width x selection; geometry is arithmetic over cached ints).
        private int selectedPathId = -1;
        // Synced creation defers in MP: watch for the entered name instead.
        private string pendingSelectPathName;
        private int pathStamp = -1;
        private float pathWidth = -1f;
        private int pathSnapSelected = -1;
        private readonly List<PathChip> pathChips = new List<PathChip>();
        private Rect pathAddRect; // Add New button, last element of the chip flow
        private float pathChipsHeight;
        private PathView pathView; // selected path's editor data; null when none

        // Paths whose "Customize assignment order" row is open without an
        // anchor set yet (mirrors RolesTabView.rulesRevealed).
        private readonly HashSet<int> anchorRevealed = new HashSet<int>();

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

        private sealed class PathChip
        {
            public int id;
            public string name;
            public Rect rect;  // chips-flow local
            public Color color;
        }

        private sealed class PathView
        {
            public int pathId;
            public string name;
            public List<int> roleIds = new List<int>();   // band entries, stored order
            public List<int> mins = new List<int>();
            public List<int> maxes = new List<int>();
            public List<int> rows = new List<int>();      // PackRows result
            public List<Role> roles = new List<Role>();
            public int rowCount;          // >= 1 for layout
            public int displayRows;       // rowCount + one empty affordance line
            public int anchorRoleId;      // -1 = none
            public bool anchorBefore;
            public Color color;           // PathColor at snapshot time
        }

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

        private void EnsurePathSnapshot(RoleStore store, float width)
        {
            if (pathStamp == UiVersion.Current && pathWidth == width
                && pathSnapSelected == selectedPathId) return;
            pathStamp = UiVersion.Current;
            pathWidth = width;
            pathSnapSelected = selectedPathId;

            pathChips.Clear();
            Text.Font = GameFont.Small;
            float x = 0f, y = 0f;
            foreach (var path in store.trainingPaths)
            {
                float w = PathChipUI.WidthFor(path.name);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + FlowGap;
                }
                pathChips.Add(new PathChip
                {
                    id = path.id,
                    name = path.name,
                    rect = new Rect(x, y, w, RoleChipUI.Height),
                    color = PathColor(store, path),
                });
                x += w + FlowGap;
            }
            // Add New wraps like a chip, always the flow's last element.
            float addW = WrText.FitWidth("WR_AddNew".Translate()) + 16f;
            if (x + addW > width && x > 0f)
            {
                x = 0f;
                y += RoleChipUI.Height + FlowGap;
            }
            pathAddRect = new Rect(x, y, addW, RoleChipUI.Height);
            pathChipsHeight = pathAddRect.yMax;

            pathView = null;
            var selected = store.PathById(selectedPathId);
            if (selected == null) return;
            var view = new PathView
            {
                pathId = selected.id,
                name = selected.name,
                anchorRoleId = selected.anchorRoleId,
                anchorBefore = selected.anchorBefore,
                color = PathColor(store, selected),
            };
            // Stored order kept: the player controls packing by reordering.
            for (int i = 0; i < selected.roleIds.Count; i++)
            {
                var role = store.RoleById(selected.roleIds[i]);
                if (role == null) continue;
                view.roleIds.Add(selected.roleIds[i]);
                view.mins.Add(selected.bandMins[i]);
                view.maxes.Add(selected.bandMaxes[i]);
                view.roles.Add(role);
            }
            view.rows = SkillProgressionMath.PackRows(
                view.mins.Select((m, i) => (m, view.maxes[i])).ToList());
            view.rowCount = view.rows.Count == 0 ? 1 : view.rows.Max() + 1;
            // Any non-empty path shows an empty line: slide-to-re-row affordance.
            view.displayRows = view.rowCount + (view.roleIds.Count > 0 ? 1 : 0);
            pathView = view;
        }

        /// Override wins. Else empty path: neutral; else the highest role's
        /// color — greatest bandMax, ties greatest bandMin, ties last in order.
        private static Color PathColor(RoleStore store, TrainingPath path)
        {
            if (path.hasCustomColor) return path.color;
            Role best = null;
            int bestMax = int.MinValue, bestMin = int.MinValue;
            for (int i = 0; i < path.roleIds.Count; i++)
            {
                var role = store.RoleById(path.roleIds[i]);
                if (role == null) continue;
                int max = path.bandMaxes[i], min = path.bandMins[i];
                if (max > bestMax || (max == bestMax && min >= bestMin))
                {
                    best = role;
                    bestMax = max;
                    bestMin = min;
                }
            }
            return best != null && best.hasCustomColor ? best.color : RoleChipUI.DefaultChipColor;
        }

        public void Reset()
        {
            tabScroll = Vector2.zero;
            orderStamp = -1;
            pathStamp = -1;
            selectedPathId = -1;
            pendingSelectPathName = null;
            anchorRevealed.Clear();
            ClearBandDrag();
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

        private void EnsureSnapshot(RoleStore store, float chipWidth)
        {
            if (orderStamp == UiVersion.Current && orderWidth == chipWidth) return;
            orderStamp = UiVersion.Current;
            orderWidth = chipWidth;
            // One projection serves both the resolver and the orderById lookup.
            var recRoles = store.roles.Select(ColonistsTabView.RecRoleOf).ToList();
            orderCache = RecommendationOrder.ResolveTemplate(store.recommendationOrder, recRoles);
            orderById = recRoles.ToDictionary(r => r.Id);
            orderRoles = orderCache.Select(store.RoleById).Where(r => r != null).ToList();
            orderLayout.Clear();
            orderLayoutHeight = LayoutChips(chipWidth, orderRoles, orderLayout, out orderAddRect);
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();

            if (pendingSelectPathName != null)
            {
                for (int i = store.trainingPaths.Count - 1; i >= 0; i--)
                    if (store.trainingPaths[i].name == pendingSelectPathName)
                    {
                        SelectPath(store.trainingPaths[i].id);
                        pendingSelectPathName = null;
                        break;
                    }
            }

            // One column inside one scroll view. The 16px bar reserve is
            // unconditional so wrap widths never depend on the height they produce.
            float viewW = rect.width - 16f;
            const float flowX = 16f;
            float flowW = Mathf.Min(viewW - 32f, 640f);

            EnsureSnapshot(store, flowW - RecPanelPad * 2f);
            EnsurePathSnapshot(store, flowW - ChipsPanelPad * 2f);

            // The whole y-flow is laid out up front: the scroll view needs
            // contentH before anything draws.
            float y = 12f;
            var compatHeader = new Rect(flowX, y, flowW, 28f);
            y += 32f;
            var numericRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var rangeRect = new Rect(flowX, y, flowW, 28f);
            y += 40f;
            var tuningHeader = new Rect(flowX, y, flowW, 28f);
            y += 32f;
            float recHeaderY = y;
            y += 30f;
            var recPanel = new Rect(flowX, y, flowW, orderLayoutHeight + RecPanelPad * 2f);
            y = recPanel.yMax + 12f;
            float pathsHeaderY = y;
            y += 30f;
            var captionRect = new Rect(flowX, y, flowW, 16f);
            y += 18f;
            var chipsPanel = new Rect(flowX, y, flowW, pathChipsHeight + ChipsPanelPad * 2f);
            y = chipsPanel.yMax + 12f;

            // A drag whose path or entry vanished must not block future presses.
            if (dragPathId != -1 && (pathView == null || dragPathId != pathView.pathId
                    || !pathView.roleIds.Contains(dragRoleId)))
                ClearBandDrag();

            float editorY = y;
            bool anchorShown = pathView != null && (pathView.anchorRoleId != -1
                || anchorRevealed.Contains(pathView.pathId));
            Rect whenPanel = default;
            Rect whenCaptionRect = default;
            if (pathView != null)
            {
                float whenPanelTop = editorY + 30f + 32f + (anchorShown ? 34f : 0f);
                whenCaptionRect = new Rect(flowX, whenPanelTop, flowW, 16f);
                whenPanel = new Rect(flowX, whenPanelTop + 18f,
                    flowW, WhenPanelPad * 2f + RowsStartY + pathView.displayRows * BandRowH);
                y = whenPanel.yMax;
            }

            Widgets.BeginScrollView(rect, ref tabScroll, new Rect(0f, 0f, viewW, y + 12f));

            WrText.HeaderLabel(compatHeader, "WR_CompatSection".Translate());

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            TooltipHandler.TipRegion(numericRect, "WR_OptNumericTip".Translate());
            bool numericNew = numeric;
            Widgets.CheckboxLabeled(numericRect, "WR_OptNumeric".Translate(), ref numericNew);
            if (numericNew != numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);

            bool vanillaRange = store.reportVanillaPriorities;
            TooltipHandler.TipRegion(rangeRect, "WR_OptVanillaRangeTip".Translate());
            bool vanillaNew = vanillaRange;
            Widgets.CheckboxLabeled(rangeRect, "WR_OptVanillaRange".Translate(), ref vanillaNew);
            if (vanillaNew != vanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);

            WrText.HeaderLabel(tuningHeader, "WR_TuningSection".Translate());
            MiniHeader(flowX, recHeaderY, flowW, "WR_RecOrderHeader".Translate(),
                "WR_OptRecOrderTip".Translate());
            DrawRecommendationOrder(recPanel, store);

            MiniHeader(flowX, pathsHeaderY, flowW, "WR_TrainingSection".Translate(),
                "WR_TrainingSectionTip".Translate());
            Text.Font = GameFont.Tiny;
            GUI.color = CaptionColor;
            Widgets.Label(captionRect, "WR_PathsPanelCaption".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolidWithOutline(chipsPanel, PanelBg, PanelOutline);
            DrawPathChips(chipsPanel.x + ChipsPanelPad, chipsPanel.y + ChipsPanelPad, store);

            if (pathView != null)
                DrawPathEditor(flowX, flowW, editorY, anchorShown, whenPanel, whenCaptionRect, store);

            Widgets.EndScrollView();

            DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        /// Editor-style mini-header: small dim label over a faint rule.
        private static float MiniHeader(float x, float y, float width, string label, string tip)
        {
            Text.Font = GameFont.Small;
            var labelRect = new Rect(x, y, width, 22f);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(labelRect, label);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            WrText.LineHorizontal(x, y + 24f, width);
            GUI.color = Color.white;
            if (tip != null) TooltipHandler.TipRegion(labelRect, tip);
            return y + 30f;
        }

        private static Rect Offset(Rect r, Vector2 by) =>
            new Rect(r.x + by.x, r.y + by.y, r.width, r.height);

        /// The recommendation order template panel: pinned role chips, drag to
        /// reorder, X to unpin (the role reverts to dynamic placement), Add
        /// Role to pin unlisted roles at their suggested spot.
        private void DrawRecommendationOrder(Rect panel, RoleStore store)
        {
            Widgets.DrawBoxSolidWithOutline(panel, PanelBg, PanelOutline);
            var origin = new Vector2(panel.x + RecPanelPad, panel.y + RecPanelPad);

            Text.Font = GameFont.Small;
            for (int i = 0; i < orderRoles.Count; i++)
            {
                var role = orderRoles[i];
                var chipRect = Offset(orderLayout[i], origin);
                var click = RoleChipUI.Draw(chipRect, role, ChipStyle.Normal,
                    showRemove: true, dragSource: null, onClick: null);
                if (click == ChipClick.Remove)
                {
                    var edited = orderCache.ToList();
                    edited.Remove(role.id);
                    RoleCommands.SetRecommendationOrder(edited);
                }
                if (orderById.TryGetValue(role.id, out var rec) && rec.Hunting)
                {
                    GUI.color = LockedColor;
                    Widgets.DrawBox(chipRect);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(chipRect))
                        TooltipHandler.TipRegion(chipRect, "WR_OptLockedTip".Translate());
                }
            }

            if (Widgets.ButtonText(Offset(orderAddRect, origin), "WR_AddRole".Translate()))
                OpenAddMenu(store, orderCache, orderById);

            if (RoleDrag.Active && orderCache.Contains(RoleDrag.RoleId) && Mouse.IsOver(panel))
            {
                // Layout rects are chips-local: shift the mouse, not the list.
                int insertIndex = RoleDrag.ChipInsertIndex(
                    Event.current.mousePosition - origin, orderLayout, r => r);

                float markerX, markerY;
                if (insertIndex == 0 || orderLayout.Count == 0)
                {
                    markerX = -FlowGap / 2f;
                    markerY = 0f;
                }
                else
                {
                    var prev = orderLayout[insertIndex - 1];
                    markerX = prev.xMax + FlowGap / 2f;
                    markerY = prev.y;
                }
                Widgets.DrawBoxSolid(new Rect(origin.x + markerX - 1f, origin.y + markerY + 3f,
                    2f, RoleChipUI.Height - 6f), new Color(1f, 1f, 1f, 0.9f));

                int draggedId = RoleDrag.RoleId;
                int from = orderCache.IndexOf(draggedId);
                int to = insertIndex > from ? insertIndex - 1 : insertIndex;
                if (to != from)
                {
                    // RoleDrag clears its slot every frame, so reassignment is
                    // per pass — but the list copy and closure are rebuilt only
                    // when the reorder target (or the order itself) changes.
                    if (dropStamp != orderStamp || dropFrom != from || dropTo != to)
                    {
                        dropStamp = orderStamp;
                        dropFrom = from;
                        dropTo = to;
                        var edited = orderCache.ToList();
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
        private static void OpenAddMenu(RoleStore store, List<int> order,
            Dictionary<int, RecRole> byId)
        {
            var options = new List<FloatMenuOption>();
            foreach (int id in RecommendationOrder.AddCandidates(byId.Values.ToList(), order)
                         .OrderBy(candidate => store.RoleById(candidate)?.label))
            {
                var role = store.RoleById(id);
                if (role == null) continue;
                int captured = id;
                options.Add(new FloatMenuOption(role.label, () =>
                {
                    var edited = order.ToList();
                    int at = byId.TryGetValue(captured, out var rec)
                        ? RecommendationOrder.InsertIndex(rec, edited, byId)
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
            Rect whenPanel, Rect whenCaptionRect, RoleStore store)
        {
            y = MiniHeader(x, y, width, "WR_PathEditorHeader".Translate(), null);

            // Assignment order opt-in mirrors the Auto-role checkbox: derived
            // from state (anchor set) OR revealed-by-hand; unchecking clears.
            // Shares the name row, right-aligned (CheckboxLabeled pins its box
            // to the rect's right edge).
            bool anchorSet = pathView.anchorRoleId != -1;
            string custLabel = "WR_CustomizeAssignment".Translate();
            float custW = WrText.FitWidth(custLabel) + 30f;
            var custRect = new Rect(x + width - custW, y + 1f, custW, 24f);
            bool custWanted = anchorShown;
            Widgets.CheckboxLabeled(custRect, custLabel, ref custWanted);
            if (custWanted != anchorShown)
            {
                if (custWanted)
                    anchorRevealed.Add(pathView.pathId);
                else
                {
                    if (anchorSet)
                        RoleCommands.SetTrainingPathAnchor(pathView.pathId, -1, pathView.anchorBefore);
                    anchorRevealed.Remove(pathView.pathId);
                }
            }

            DrawPathNameRow(new Rect(x, y, width - custW - 12f, 26f));
            y += 32f;

            if (anchorShown)
                DrawPathAnchorRow(new Rect(x, y, width, 28f), store, pathView);

            // Caption above the WHEN panel (single line, truncated; full text as tooltip).
            Text.Font = GameFont.Tiny;
            GUI.color = CaptionColor;
            string whenCaption = "WR_WhenPanelCaption".Translate();
            Widgets.Label(whenCaptionRect, whenCaption.Truncate(whenCaptionRect.width));
            TooltipHandler.TipRegion(whenCaptionRect, whenCaption);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            DrawWhenPanel(whenPanel, store);
        }

        /// The WHEN editor panel: the 0..21 axis on top (readout headroom above
        /// it), the packed band rows, then Add Role on the trailing empty row.
        /// Natural height — the tab's scroll view absorbs any overflow.
        private void DrawWhenPanel(Rect panel, RoleStore store)
        {
            float bandW = panel.width - WhenPanelPad * 2f;
            // Below this, a min-span chip can't hold its grips + X.
            if (bandW < 150f) return;

            Widgets.DrawBoxSolidWithOutline(panel, PanelBg, PanelOutline);
            var inner = panel.ContractedBy(WhenPanelPad);
            DrawAxis(new Rect(inner.x, inner.y + RowsTopPad, bandW, AxisH));
            DrawBandRows(store, pathView, inner.x, inner.y, bandW);
        }

        /// One chip per stored path: name + delete X; click selects. The
        /// trailing Add New button is part of the same flow.
        private void DrawPathChips(float baseX, float baseY, RoleStore store)
        {
            var e = Event.current;
            for (int i = 0; i < pathChips.Count; i++)
            {
                var chip = pathChips[i];
                var r = new Rect(baseX + chip.rect.x, baseY + chip.rect.y,
                    chip.rect.width, chip.rect.height);
                PathChipUI.Draw(r, chip.name, chip.color, chip.id == selectedPathId);

                if (e.type != EventType.MouseDown || e.button != 0
                    || !r.Contains(e.mousePosition)) continue;
                if (ChipUI.RemoveRect(r).Contains(e.mousePosition))
                {
                    int id = chip.id;
                    Find.WindowStack.Add(new Dialog_SmallConfirm(
                        "WR_DeletePathConfirm".Translate(chip.name),
                        () => RoleCommands.DeleteTrainingPath(id)));
                }
                else
                    SelectPath(chip.id);
                e.Use();
            }

            if (Widgets.ButtonText(Offset(pathAddRect, new Vector2(baseX, baseY)),
                    "WR_AddNew".Translate()))
                Find.WindowStack.Add(new Dialog_RenameRole("WR_NewPathTitle".Translate(), name =>
                {
                    RoleCommands.CreateTrainingPath(name);
                    pendingSelectPathName = name;
                }));
        }

        private void SelectPath(int id) => selectedPathId = id;

        /// Name row: grey caption, path-color dot, plain name, rename pencil
        /// (dialog commits ONE RenameTrainingPath).
        private void DrawPathNameRow(Rect rect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string caption = "WR_PathNameLabel".Translate();
            float captionW = WrText.FitWidth(caption);
            GUI.color = DimLabel;
            Widgets.Label(new Rect(rect.x, rect.y, captionW, rect.height), caption);
            GUI.color = Color.white;
            float x = rect.x + captionW + 6f;

            var dotRect = new Rect(x, rect.y + (rect.height - 16f) / 2f, 16f, 16f);
            GUI.color = pathView.color;
            GUI.DrawTexture(dotRect, WorkRolesTex.Circle);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(dotRect, "WR_PathColorTip".Translate());
            if (Widgets.ButtonInvisible(dotRect))
                Find.WindowStack.Add(new Dialog_PathColor(pathView.pathId));
            x += 16f + 6f;

            float nameW = Mathf.Min(WrText.FitWidth(pathView.name), rect.xMax - x - 36f);
            Widgets.Label(new Rect(x, rect.y, nameW, rect.height), pathView.name.Truncate(nameW));
            Text.Anchor = TextAnchor.UpperLeft;
            x += nameW + 6f;

            int pathId = pathView.pathId;
            if (Widgets.ButtonImage(new Rect(x, rect.y + (rect.height - 24f) / 2f, 24f, 24f),
                    TexButton.Rename))
                Find.WindowStack.Add(new Dialog_RenameRole("WR_RenamePathTitle".Translate(),
                    name => RoleCommands.RenameTrainingPath(pathId, name), pathView.name));
        }

        /// Grey caption + Before/After toggle + the anchor as a role chip (or
        /// a Pick-role button while unset). The group right-aligns under the
        /// Customize toggle. Reads snapshot only.
        private static void DrawPathAnchorRow(Rect rect, RoleStore store, PathView view)
        {
            Text.Font = GameFont.Small;
            string caption = "WR_AnchorLabel".Translate();
            float captionW = WrText.FitWidth(caption);
            Role anchorRole = view.anchorRoleId != -1 ? store.RoleById(view.anchorRoleId) : null;
            float tailW = anchorRole != null
                ? RoleChipUI.WidthFor(anchorRole, showRemove: false)
                : 110f;
            float x = rect.xMax - (captionW + 4f + 70f + 8f + tailW);
            TooltipHandler.TipRegion(new Rect(x, rect.y, rect.xMax - x, rect.height),
                "WR_AnchorTip".Translate());

            // MiddleLeft over the row height: the caption shares the text
            // baseline with the Before/After button and the chip.
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(x, rect.y, captionW, rect.height), caption);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            x += captionW + 4f;

            string toggleLabel = view.anchorBefore
                ? "WR_AnchorBefore".Translate()
                : "WR_AnchorAfter".Translate();
            if (Widgets.ButtonText(new Rect(x, rect.y, 70f, rect.height), toggleLabel))
                RoleCommands.SetTrainingPathAnchor(view.pathId, view.anchorRoleId, !view.anchorBefore);
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
        /// lists candidates: normal roles (not blocker/managed/rules), alphabetical.
        private static void OpenAnchorMenu(RoleStore store, PathView view)
        {
            var options = new List<FloatMenuOption>();
            foreach (var role in store.roles
                         .Where(r => !r.blocker && !r.managed && !r.HasRules)
                         .OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase))
            {
                int capturedId = role.id;
                options.Add(new FloatMenuOption(role.label, () =>
                    RoleCommands.SetTrainingPathAnchor(view.pathId, capturedId, view.anchorBefore)));
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
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
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
            !role.blocker && !role.managed && !role.HasRules;

        /// Tier 1 plain, tier 2 greyed (mods can rewire XP in driver code, so
        /// players may know better). The first role gets the full axis; later
        /// picks enter min-width at the top, drag to place.
        private static void OpenAddRoleMenu(RoleStore store, PathView view)
        {
            var options = new List<FloatMenuOption>();
            foreach (var (role, tier) in store.roles
                         .Where(r => IsNormal(r) && !view.roleIds.Contains(r.id))
                         .Select(r => (role: r, tier: HasXpJobs(r) ? 1 : 2))
                         .OrderBy(t => t.tier)
                         .ThenBy(t => t.role.label, System.StringComparer.OrdinalIgnoreCase))
            {
                int captured = role.id;
                bool first = view.roleIds.Count == 0;
                var ids = view.roleIds.ToList();
                var mins = view.mins.ToList();
                var maxes = view.maxes.ToList();
                string label = tier == 2
                    ? role.label.Colorize(new Color(0.62f, 0.62f, 0.62f))
                    : role.label;
                var option = new FloatMenuOption(label, () =>
                {
                    ids.Add(captured);
                    mins.Add(first ? 0
                        : SkillProgressionMath.MaxLevel - SkillProgressionMath.MinSpan);
                    maxes.Add(SkillProgressionMath.MaxLevel);
                    RoleCommands.SetTrainingPathBands(view.pathId, ids, mins, maxes);
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
        private void DrawBandRows(RoleStore store, PathView view, float bandX, float baseY, float bandW)
        {
            var e = Event.current;
            float scale = bandW / SkillProgressionMath.MaxLevel;
            float rowsY = baseY + RowsStartY;

            // Section sweep already cleared vanished drags; a live entry here
            // is this path's own drag.
            int dragEntry = dragPathId == view.pathId ? view.roleIds.IndexOf(dragRoleId) : -1;
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
                ? view.roleIds.IndexOf(dragLinkedRoleId) : -1;

            // Displayed (pending-aware) band values; the linked neighbour's
            // touching edge follows the shared pending level.
            int ShownMin(int k) => k == dragEntry ? pendingMin
                : k == linkedEntry && dragKind == BandDragKind.MaxEdge ? pendingMax : view.mins[k];
            int ShownMax(int k) => k == dragEntry ? pendingMax
                : k == linkedEntry && dragKind == BandDragKind.MinEdge ? pendingMin : view.maxes[k];
            int ShownRow(int k) => k == dragEntry && dragKind == BandDragKind.Slide
                ? pendingRow : view.rows[k];

            // Dim divider below every display row, 2px clear of the chips on
            // both sides (BandRowH leaves 5px between chips).
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            for (int r = 1; r < view.displayRows; r++)
                WrText.LineHorizontal(bandX, rowsY + r * BandRowH - 3f, bandW);
            GUI.color = Color.white;

            // Add Role lives on the empty affordance row, left-aligned; a chip
            // slid onto that row draws over it (chips render later).
            if (Widgets.ButtonText(new Rect(bandX,
                    rowsY + (view.displayRows - 1) * BandRowH,
                    110f, RoleChipUI.Height), "WR_AddRole".Translate()))
                OpenAddRoleMenu(store, view);

            for (int i = 0; i < view.roleIds.Count; i++)
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
                RoleChipUI.DrawBandChip(chipRect, view.roles[i]);

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
                    pendingRow = dragStartRow = view.rows[i];
                }
                dragPathId = view.pathId;
                dragRoleId = view.roleIds[i];
                pendingMin = min;
                pendingMax = max;
                e.Use();
            }
        }

        /// Per-frame pending update: clamp arithmetic only. A linked edge moves
        /// both bands via the shared clamp; a vanished neighbour drops the link.
        private void UpdateBandDrag(PathView view, int dragEntry, Event e, float bandX,
            float rowsY, float scale)
        {
            int level = Mathf.RoundToInt((e.mousePosition.x - bandX) / scale);
            int min = view.mins[dragEntry], max = view.maxes[dragEntry];
            if (dragKind == BandDragKind.Slide)
            {
                pendingMin = SkillProgressionMath.ClampSlide(min, max, level - slideGrabOffset);
                pendingMax = pendingMin + (max - min);
                pendingRow = Mathf.Clamp(
                    Mathf.FloorToInt((e.mousePosition.y - rowsY) / BandRowH),
                    0, view.displayRows - 1);
                return;
            }
            int linked = dragLinkedRoleId == -1 ? -1 : view.roleIds.IndexOf(dragLinkedRoleId);
            if (linked < 0) dragLinkedRoleId = -1;
            if (dragKind == BandDragKind.MinEdge)
                pendingMin = linked >= 0
                    ? SkillProgressionMath.ClampSharedEdge(view.mins[linked], max, level)
                    : SkillProgressionMath.ClampEdge(min, max, true, level);
            else
                pendingMax = linked >= 0
                    ? SkillProgressionMath.ClampSharedEdge(min, view.maxes[linked], level)
                    : SkillProgressionMath.ClampEdge(min, max, false, level);
        }

        /// ONE synced command commits the slid/resized band, the linked
        /// neighbour's shared edge and a vertical reorder together.
        private void CommitBandDrag(PathView view, int dragEntry)
        {
            var ids = view.roleIds.ToList();
            var mins = view.mins.ToList();
            var maxes = view.maxes.ToList();
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
                for (int j = 0, k = 0; j < view.roleIds.Count; j++)
                {
                    if (j == dragEntry) continue;
                    if (view.rows[j] == pendingRow) { insert = k; break; }
                    k++;
                }
                ids.Insert(insert, dragRoleId);
                mins.Insert(insert, pendingMin);
                maxes.Insert(insert, pendingMax);
            }
            if (changed)
                RoleCommands.SetTrainingPathBands(view.pathId, ids, mins, maxes);
        }

        /// A same-row neighbour whose band touches the pressed edge; tracked
        /// by id so the link survives (or is dropped on) snapshot changes.
        private static int FindLinked(PathView view, int i, BandDragKind kind)
        {
            for (int j = 0; j < view.roleIds.Count; j++)
            {
                if (j == i || view.rows[j] != view.rows[i]) continue;
                if (kind == BandDragKind.MaxEdge && view.mins[j] == view.maxes[i]) return view.roleIds[j];
                if (kind == BandDragKind.MinEdge && view.maxes[j] == view.mins[i]) return view.roleIds[j];
            }
            return -1;
        }

        /// The chip X drops one entry; the path itself survives empty.
        private static void RemoveEntry(PathView view, int index)
        {
            var ids = view.roleIds.ToList();
            var mins = view.mins.ToList();
            var maxes = view.maxes.ToList();
            ids.RemoveAt(index);
            mins.RemoveAt(index);
            maxes.RemoveAt(index);
            RoleCommands.SetTrainingPathBands(view.pathId, ids, mins, maxes);
        }

        /// Wrapped chip flow with the Add Role button as its last element.
        private static float LayoutChips(float width, List<Role> roles, List<Rect> result,
            out Rect addRect)
        {
            float x = 0f, y = 0f;
            Rect Place(float w)
            {
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + FlowGap;
                }
                var r = new Rect(x, y, w, RoleChipUI.Height);
                x += w + FlowGap;
                return r;
            }
            foreach (var role in roles)
                result.Add(Place(RoleChipUI.WidthFor(role, showRemove: true)));
            Text.Font = GameFont.Small;
            addRect = Place(WrText.FitWidth("WR_AddRole".Translate()) + 16f);
            return y + RoleChipUI.Height;
        }

        private static void DrawDragGhost(RoleStore store)
        {
            if (!RoleDrag.Active) return;
            var role = store.RoleById(RoleDrag.RoleId);
            if (role == null) return;
            var mouse = Event.current.mousePosition;
            float w = RoleChipUI.WidthFor(role, showRemove: false);
            var ghostRect = new Rect(mouse.x + 10f, mouse.y + 6f, w, RoleChipUI.Height);
            RoleChipUI.Draw(ghostRect, role, ChipStyle.Normal, showRemove: false,
                dragSource: null, onClick: null, interactive: false);
        }
    }
}
