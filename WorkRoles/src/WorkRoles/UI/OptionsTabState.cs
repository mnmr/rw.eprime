using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    /// Owns the Options tab's open-window projections, cached layout, translated
    /// tips, selection, and disclosure state. The view consumes snapshots and
    /// retains only immediate-mode rendering and pointer interaction state.
    internal sealed class OptionsTabState
    {
        internal const float FlowGap = 8f;

        private int orderStamp = -1;
        private float orderWidth = -1f;
        private List<int> order;
        private Dictionary<int, RoleView> orderById;
        private List<Role> orderRoles;
        private readonly List<Rect> orderLayout = new List<Rect>();

        private int pathStamp = -1;
        private float pathWidth = -1f;
        private int pathSnapSelected = -1;
        private readonly List<OptionsPathChip> pathChips = new List<OptionsPathChip>();
        private string pendingSelectPathName;
        private readonly HashSet<int> anchorRevealed = new HashSet<int>();

        private int tipsStamp = -1;

        internal int OrderStamp => orderStamp;
        internal IReadOnlyList<int> Order => order;
        internal IReadOnlyDictionary<int, RoleView> OrderById => orderById;
        internal IReadOnlyList<Role> OrderRoles => orderRoles;
        internal IReadOnlyList<Rect> OrderLayout => orderLayout;
        internal Rect OrderAddRect { get; private set; }
        internal float OrderLayoutHeight { get; private set; }

        internal int SelectedPathId { get; private set; } = -1;
        internal IReadOnlyList<OptionsPathChip> PathChips => pathChips;
        internal Rect PathAddRect { get; private set; }
        internal float PathChipsHeight { get; private set; }
        internal OptionsPathView Path { get; private set; }

        internal StructuredTip NumericTip { get; private set; }
        internal StructuredTip RangeTip { get; private set; }
        internal StructuredTip RecommendationOrderTip { get; private set; }
        internal StructuredTip TrainingTip { get; private set; }
        internal StructuredTip AnchorTip { get; private set; }

        internal void Reset()
        {
            SelectedPathId = -1;
            pendingSelectPathName = null;
            anchorRevealed.Clear();
            InvalidateLanguageCaches();
        }

        internal void InvalidateLanguageCaches()
        {
            orderStamp = -1;
            order = null;
            orderById = null;
            orderRoles = null;
            orderLayout.Clear();

            pathStamp = -1;
            pathChips.Clear();
            Path = null;

            tipsStamp = -1;
            NumericTip = null;
            RangeTip = null;
            RecommendationOrderTip = null;
            TrainingTip = null;
            AnchorTip = null;
        }

        internal void EnsureOrder(RoleStore store, float width)
        {
            if (orderStamp == UiVersion.Current && orderWidth == width) return;
            orderStamp = UiVersion.Current;
            orderWidth = width;

            List<RoleView> views = RecsAdapter.RoleViewsOf(store.roles);
            order = OrderTemplate.ResolveTemplate(store.recommendationOrder, views);
            orderById = views.ToDictionary(role => role.Id);
            orderRoles = order.Select(store.RoleById).Where(role => role != null).ToList();
            orderLayout.Clear();
            OrderLayoutHeight = LayoutOrderChips(
                width, orderRoles, orderLayout, out Rect addRect);
            OrderAddRect = addRect;
        }

        internal int OrderIndexOf(int roleId)
        {
            if (order == null) return -1;
            for (int i = 0; i < order.Count; i++)
                if (order[i] == roleId) return i;
            return -1;
        }

        internal void EnsurePaths(RoleStore store, float width)
        {
            if (pathStamp == UiVersion.Current && pathWidth == width
                && pathSnapSelected == SelectedPathId) return;
            pathStamp = UiVersion.Current;
            pathWidth = width;
            pathSnapSelected = SelectedPathId;

            pathChips.Clear();
            Text.Font = GameFont.Small;
            float x = 0f;
            float y = 0f;
            foreach (TrainingPath path in store.trainingPaths)
            {
                float chipWidth = PathChipUI.WidthFor(path.name);
                if (x + chipWidth > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + FlowGap;
                }
                pathChips.Add(new OptionsPathChip(
                    path.id,
                    path.name,
                    new Rect(x, y, chipWidth, RoleChipUI.Height),
                    TrainingPathPresentation.ColorFor(store, path)));
                x += chipWidth + FlowGap;
            }

            float addWidth = WrText.FitWidth("WR_AddNew".Translate()) + 16f;
            if (x + addWidth > width && x > 0f)
            {
                x = 0f;
                y += RoleChipUI.Height + FlowGap;
            }
            PathAddRect = new Rect(x, y, addWidth, RoleChipUI.Height);
            PathChipsHeight = PathAddRect.yMax;

            Path = BuildPathView(store, store.PathById(SelectedPathId));
        }

        internal void ResolvePendingPathSelection(RoleStore store)
        {
            if (pendingSelectPathName == null) return;
            for (int i = store.trainingPaths.Count - 1; i >= 0; i--)
                if (store.trainingPaths[i].name == pendingSelectPathName)
                {
                    SelectedPathId = store.trainingPaths[i].id;
                    pendingSelectPathName = null;
                    return;
                }
        }

        internal void SelectPath(int id) => SelectedPathId = id;

        internal void SelectPathWhenCreated(string name) => pendingSelectPathName = name;

        internal bool IsAnchorShown(OptionsPathView path) => path != null
            && (path.AnchorRoleId != -1 || anchorRevealed.Contains(path.PathId));

        internal void SetAnchorRevealed(int pathId, bool revealed)
        {
            if (revealed) anchorRevealed.Add(pathId);
            else anchorRevealed.Remove(pathId);
        }

        internal void EnsureTips()
        {
            if (tipsStamp == UiVersion.Current) return;
            tipsStamp = UiVersion.Current;

            var numeric = new TipModel { Title = "WR_OptNumeric".Translate() };
            numeric.AddSection().Text("WR_OptNumericTipWhat".Translate());
            numeric.AddSection()
                .Fact("WR_TipOff".Translate(), "WR_OptNumericTipOff".Translate())
                .Fact("WR_TipOn".Translate(), "WR_OptNumericTipOn".Translate());
            numeric.AddSection().Text("WR_OptNumericTipWhy".Translate(), dim: true);
            NumericTip = new StructuredTip("options:numeric", numeric);

            var range = new TipModel { Title = "WR_OptVanillaRange".Translate() };
            range.AddSection().Text("WR_OptVanillaRangeTipWhat".Translate());
            range.AddSection()
                .Fact("WR_TipOff".Translate(), "WR_OptVanillaRangeTipOff".Translate())
                .Fact("WR_TipOn".Translate(), "WR_OptVanillaRangeTipOn".Translate());
            RangeTip = new StructuredTip("options:vanilla-range", range);

            var recommendation = new TipModel { Title = "WR_RecOrderHeader".Translate() };
            recommendation.AddSection().Text("WR_OptRecOrderTipWhat".Translate());
            recommendation.AddSection()
                .Action("WR_ActDrag".Translate(), "WR_ActRecDrag".Translate())
                .Action("WR_ActX".Translate(), "WR_ActRecX".Translate());
            recommendation.AddSection().Text("WR_OptRecOrderTipAuto".Translate(), dim: true);
            RecommendationOrderTip =
                new StructuredTip("options:recommendation-order", recommendation);

            var training = new TipModel { Title = "WR_TrainingSection".Translate() };
            training.AddSection().Text("WR_TrainingTipWhat".Translate());
            training.AddSection()
                .Text("WR_TrainingTipBands".Translate(), dim: true)
                .Text("WR_TrainingTipOrder".Translate(), dim: true);
            TrainingTip = new StructuredTip("options:training", training);

            var anchor = new TipModel { Title = "WR_CustomizeAssignment".Translate() };
            anchor.AddSection().Text("WR_AnchorTipWhat".Translate());
            anchor.AddSection().Text("WR_AnchorTipWhy".Translate(), dim: true);
            AnchorTip = new StructuredTip("options:anchor", anchor);
        }

        private static OptionsPathView BuildPathView(RoleStore store, TrainingPath path)
        {
            if (path == null) return null;

            var roleIds = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            var roles = new List<Role>();
            for (int i = 0; i < path.roleIds.Count; i++)
            {
                Role role = store.RoleById(path.roleIds[i]);
                if (role == null) continue;
                roleIds.Add(path.roleIds[i]);
                mins.Add(path.bandMins[i]);
                maxes.Add(path.bandMaxes[i]);
                roles.Add(role);
            }

            List<int> rows = SkillProgressionMath.PackRows(
                mins.Select((min, i) => (min, maxes[i])).ToList());
            int rowCount = rows.Count == 0 ? 1 : rows.Max() + 1;
            int displayRows = rowCount + (roleIds.Count > 0 ? 1 : 0);
            return new OptionsPathView(
                path.id,
                path.name,
                roleIds,
                mins,
                maxes,
                rows,
                roles,
                displayRows,
                path.anchorRoleId,
                path.anchorBefore,
                TrainingPathPresentation.ColorFor(store, path));
        }

        private static float LayoutOrderChips(float width, IReadOnlyList<Role> roles,
            List<Rect> result, out Rect addRect)
        {
            float x = 0f;
            float y = 0f;
            Rect Place(float itemWidth)
            {
                if (x + itemWidth > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + FlowGap;
                }
                var rect = new Rect(x, y, itemWidth, RoleChipUI.Height);
                x += itemWidth + FlowGap;
                return rect;
            }

            for (int i = 0; i < roles.Count; i++)
                result.Add(Place(RoleChipUI.WidthFor(roles[i], showRemove: true)));
            Text.Font = GameFont.Small;
            addRect = Place(WrText.FitWidth("WR_AddRole".Translate()) + 16f);
            return y + RoleChipUI.Height;
        }
    }

    internal sealed class OptionsPathChip
    {
        internal OptionsPathChip(int id, string name, Rect rect, Color color)
        {
            Id = id;
            Name = name;
            Rect = rect;
            Color = color;
        }

        internal int Id { get; }
        internal string Name { get; }
        internal Rect Rect { get; }
        internal Color Color { get; }
    }

    internal sealed class OptionsPathView
    {
        internal OptionsPathView(int pathId, string name, IReadOnlyList<int> roleIds,
            IReadOnlyList<int> mins, IReadOnlyList<int> maxes, IReadOnlyList<int> rows,
            IReadOnlyList<Role> roles, int displayRows, int anchorRoleId,
            bool anchorBefore, Color color)
        {
            PathId = pathId;
            Name = name;
            RoleIds = roleIds;
            Mins = mins;
            Maxes = maxes;
            Rows = rows;
            Roles = roles;
            DisplayRows = displayRows;
            AnchorRoleId = anchorRoleId;
            AnchorBefore = anchorBefore;
            Color = color;
        }

        internal int PathId { get; }
        internal string Name { get; }
        internal IReadOnlyList<int> RoleIds { get; }
        internal IReadOnlyList<int> Mins { get; }
        internal IReadOnlyList<int> Maxes { get; }
        internal IReadOnlyList<int> Rows { get; }
        internal IReadOnlyList<Role> Roles { get; }
        internal int DisplayRows { get; }
        internal int AnchorRoleId { get; }
        internal bool AnchorBefore { get; }
        internal Color Color { get; }

        internal int IndexOfRole(int roleId)
        {
            for (int i = 0; i < RoleIds.Count; i++)
                if (RoleIds[i] == roleId) return i;
            return -1;
        }
    }
}
