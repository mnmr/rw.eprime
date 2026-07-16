using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Options tab: per-save toggles and the recommendation order template.
    /// These live here (not in Mod Settings) because they are per-savegame
    /// state, synced in MP.
    public class OptionsTabView
    {
        private const float ChipGap = 4f;
        // Hunting chips: pinning them disables the dynamic duty-slot placement.
        private static readonly Color LockedColor = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        private Vector2 orderScroll;

        // Open-window snapshot (UiVersion): resolving the template projects the
        // whole catalog through RecRoleOf — that ran twice per pass before.
        private int snapStamp = -1;
        private float snapWidth = -1f;
        private List<int> order;
        private Dictionary<int, RecRole> byId;
        private List<Role> orderRoles;
        private readonly List<Rect> layout = new List<Rect>();
        private float layoutHeight;

        // Drag drop, rebuilt only when the reorder target changes (see p2 in
        // DrawRecommendationOrder): RoleDrag clears its slot every frame.
        private int dropStamp = -1;
        private int dropFrom = -1;
        private int dropTo = -1;
        private System.Action dropAction;

        public void Reset()
        {
            orderScroll = Vector2.zero;
            snapStamp = -1;
        }

        private void EnsureSnapshot(RoleStore store, float chipWidth)
        {
            if (snapStamp == UiVersion.Current && snapWidth == chipWidth) return;
            snapStamp = UiVersion.Current;
            snapWidth = chipWidth;
            // One projection serves both the resolver and the byId lookup.
            var recRoles = store.roles.Select(ColonistsTabView.RecRoleOf).ToList();
            order = RecommendationOrder.ResolveTemplate(store.recommendationOrder, recRoles);
            byId = recRoles.ToDictionary(r => r.Id);
            orderRoles = order.Select(store.RoleById).Where(r => r != null).ToList();
            layout.Clear();
            layoutHeight = LayoutChips(chipWidth, orderRoles, layout);
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();

            float y = rect.y + 12f;
            float rowW = Mathf.Min(rect.width - 32f, 560f);
            float rowX = rect.x + 16f;

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            var numericRect = new Rect(rowX, y, rowW, 28f);
            TooltipHandler.TipRegion(numericRect, "WR_OptNumericTip".Translate());
            bool numericNew = numeric;
            Widgets.CheckboxLabeled(numericRect, "WR_OptNumeric".Translate(), ref numericNew);
            if (numericNew != numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);
            y += 34f;

            bool vanillaRange = store.reportVanillaPriorities;
            var rangeRect = new Rect(rowX, y, rowW, 28f);
            TooltipHandler.TipRegion(rangeRect, "WR_OptVanillaRangeTip".Translate());
            bool vanillaNew = vanillaRange;
            Widgets.CheckboxLabeled(rangeRect, "WR_OptVanillaRange".Translate(), ref vanillaNew);
            if (vanillaNew != vanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);
            y += 40f;

            DrawRecommendationOrder(new Rect(rowX, y, rowW, rect.yMax - y - 8f), store);

            DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        /// The recommendation order template: pinned role chips, drag to
        /// reorder, X to unpin (the role reverts to dynamic placement), Add to
        /// pin unlisted roles at their suggested spot.
        private void DrawRecommendationOrder(Rect rect, RoleStore store)
        {
            // Full catalog: coverage anchors (autos included) shape positions.
            EnsureSnapshot(store, rect.width - 20f);

            Text.Font = GameFont.Small;
            var headerRect = new Rect(rect.x, rect.y, rect.width - 110f, 24f);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(headerRect, "WR_OptRecOrder".Translate());
            GUI.color = Color.white;
            TooltipHandler.TipRegion(headerRect, "WR_OptRecOrderTip".Translate());
            if (Widgets.ButtonText(new Rect(rect.xMax - 100f, rect.y - 2f, 100f, 26f),
                    "WR_OptRecAdd".Translate()))
                OpenAddMenu(store, order, byId);

            float listY = rect.y + 30f;
            var listRect = new Rect(rect.x, listY, rect.width, rect.yMax - listY);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, layoutHeight);
            Widgets.BeginScrollView(listRect, ref orderScroll, viewRect);

            for (int i = 0; i < orderRoles.Count; i++)
            {
                var role = orderRoles[i];
                var chipRect = layout[i];
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

            // Reorder drag: mouse coords are content-local inside the scroll view.
            var hoverRect = new Rect(0f, 0f, viewRect.width,
                Mathf.Max(viewRect.height, listRect.height));
            if (RoleDrag.Active && order.Contains(RoleDrag.RoleId) && Mouse.IsOver(hoverRect))
            {
                var mouse = Event.current.mousePosition;
                int insertIndex = 0;
                for (int i = 0; i < layout.Count; i++)
                {
                    var r = layout[i];
                    if (mouse.y > r.yMax)
                    {
                        insertIndex = i + 1;
                        continue;
                    }
                    if (mouse.y >= r.y && mouse.x > r.x + r.width / 2f)
                        insertIndex = i + 1;
                }

                float markerX, markerY;
                if (insertIndex == 0 || layout.Count == 0)
                {
                    markerX = -ChipGap / 2f;
                    markerY = 0f;
                }
                else
                {
                    var prev = layout[insertIndex - 1];
                    markerX = prev.xMax + ChipGap / 2f;
                    markerY = prev.y;
                }
                Widgets.DrawBoxSolid(new Rect(markerX - 1f, markerY + 3f, 2f, RoleChipUI.Height - 6f),
                    new Color(1f, 1f, 1f, 0.9f));

                int draggedId = RoleDrag.RoleId;
                int from = order.IndexOf(draggedId);
                int to = insertIndex > from ? insertIndex - 1 : insertIndex;
                if (to != from)
                {
                    // RoleDrag clears its slot every frame, so reassignment is
                    // per pass — but the list copy and closure are rebuilt only
                    // when the reorder target (or the order itself) changes.
                    if (dropStamp != snapStamp || dropFrom != from || dropTo != to)
                    {
                        dropStamp = snapStamp;
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

            Widgets.EndScrollView();
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

        private static float LayoutChips(float width, List<Role> roles, List<Rect> result)
        {
            float x = 0f, y = 0f;
            foreach (var role in roles)
            {
                float w = RoleChipUI.WidthFor(role, showRemove: true);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + ChipGap;
                }
                result.Add(new Rect(x, y, w, RoleChipUI.Height));
                x += w + ChipGap;
            }
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
