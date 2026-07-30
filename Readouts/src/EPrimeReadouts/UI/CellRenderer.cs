using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// A render model with its defNames resolved to ThingDefs, done once at
    /// rebuild so drawing never touches DefDatabase.
    public sealed class DrawModel
    {
        public RenderModel Model;
        public ThingDef[] Defs;    // parallel to Model.Cells; null for non-icon cells
        public int[] Counts;       // parallel; raw count for icon cells (tooltips)
        public string[] Tokens;    // parallel; raw slot token for icon cells (tooltips)
        public string[] Labels;    // parallel; translated labels for label cells
        public string[] Tooltips;  // parallel; resolved icon hover labels
        public RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> RenderData;

        public static DrawModel Resolve(
            RenderModel model,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData = null)
        {
            var defs = new ThingDef[model.Cells.Count];
            var counts = new int[model.Cells.Count];
            var tokens = new string[model.Cells.Count];
            var labels = new string[model.Cells.Count];
            var tooltips = new string[model.Cells.Count];
            for (int i = 0; i < model.Cells.Count; i++)
            {
                var cell = model.Cells[i];
                if (cell.Kind == CellKind.Icon)
                {
                    defs[i] = DefDatabase<ThingDef>.GetNamedSilentFail(cell.DefName);
                    IconScaleCache.Request(defs[i]);
                    tokens[i] = cell.Token;
                    // Raw count carried on the cell — never parse the display
                    // text, which is compact-formatted ("12.8k") above 10000.
                    counts[i] = cell.Count;
                    if (tokens[i] != null && SlotToken.IsPoolRef(tokens[i])
                        && renderData != null
                        && renderData.Structure.TryGet(SlotToken.PoolId(tokens[i]),
                            out _, out _, out string poolName))
                        tooltips[i] = poolName;
                    else if (tokens[i] != null && SlotToken.IsPool(tokens[i]))
                        tooltips[i] = GameResourceCatalog.Instance.CategoryLabelOf(
                            SlotToken.MemberName(tokens[i])).CapitalizeFirst();
                    else
                        tooltips[i] = defs[i] != null ? defs[i].LabelCap : cell.DefName;
                }
                else if (cell.Kind == CellKind.Label)
                    labels[i] = UiText.Get(cell.Text);
            }
            return new DrawModel
            {
                Model = model,
                Defs = defs,
                Counts = counts,
                Tokens = tokens,
                Labels = labels,
                Tooltips = tooltips,
                RenderData = renderData,
            };
        }
    }

    public static class CellRenderer
    {
        private static readonly Color DimTriangle = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color LowTint = new Color(1f, 0.92f, 0.55f);
        private static readonly Color CriticalTint = new Color(1f, 0.72f, 0.45f);
        private static readonly Color LabelDim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Backing = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color NeutralStripe = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color[] StripePalette =
        {
            new Color(0.36f, 0.66f, 0.86f), // blue
            new Color(0.55f, 0.78f, 0.45f), // green
            new Color(0.88f, 0.72f, 0.35f), // amber
            new Color(0.80f, 0.50f, 0.70f), // plum
            new Color(0.45f, 0.78f, 0.75f), // teal
            new Color(0.85f, 0.55f, 0.40f), // rust
        };

        internal static Color StripeColorFor(int groupIndex) =>
            groupIndex < 0 ? NeutralStripe : StripePalette[groupIndex % StripePalette.Length];

        public static void Draw(DrawModel draw)
        {
            using (new GuiStateScope())
            {
            var cells = draw.Model.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var rect = new Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.W, cell.Rect.H);
                switch (cell.Kind)
                {
                    case CellKind.GroupBack:
                        Widgets.DrawBoxSolid(rect, Backing);
                        Widgets.DrawBoxSolid(
                            new Rect(rect.x, rect.y, LayoutMetrics.StripeW, rect.height),
                            StripeColorFor(cell.GroupIndex));
                        break;
                    case CellKind.Triangle:
                        GUI.color = cell.Triangle == TriangleState.Lit ? Color.white : DimTriangle;
                        GUI.DrawTexture(rect, ReadoutTextures.Triangle);
                        GUI.color = Color.white;
                        break;
                    case CellKind.Highlight:
                        Widgets.DrawHighlight(rect);
                        break;
                    case CellKind.Icon:
                        if (draw.Defs[i] != null)
                        {
                            // Per-def scale correction evens out how much of
                            // each texture is transparent padding, so icons
                            // read as visually same-sized. Cached lookup.
                            float iconScale = IconScaleCache.ScaleFor(draw.Defs[i]);
                            var iconRect = iconScale == 1f ? rect : new Rect(
                                rect.x + rect.width * (1f - iconScale) / 2f,
                                rect.y + rect.height * (1f - iconScale) / 2f,
                                rect.width * iconScale,
                                rect.height * iconScale);
                            Widgets.ThingIcon(iconRect, draw.Defs[i]);
                            if (Mouse.IsOver(rect))
                                IconTips.Tip(rect, draw.Defs[i], draw.Counts[i], cells[i + 1].Band,
                                    draw.Tokens[i], draw.RenderData);
                        }
                        break;
                    case CellKind.Counter:
                        if (cell.Band != Band.Normal)
                            GUI.color = cell.Band == Band.Low ? LowTint : CriticalTint;
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.UpperCenter;
                        Widgets.Label(rect, cell.Text);
                        Text.Anchor = TextAnchor.UpperLeft;
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                        break;
                    case CellKind.Label:
                        GUI.color = LabelDim;
                        Text.Font = GameFont.Tiny;
                        Widgets.Label(rect, draw.Labels[i]);
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                        break;
                    case CellKind.EmptySlot:
                        Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
                        GUI.color = new Color(1f, 1f, 1f, 0.25f);
                        Widgets.DrawBox(rect);
                        GUI.color = Color.white;
                        break;
                }
            }
            }
        }
    }
}
