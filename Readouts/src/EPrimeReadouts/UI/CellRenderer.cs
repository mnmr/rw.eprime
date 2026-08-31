using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// A render model with its defNames resolved to ThingDefs, done once at
    /// rebuild so drawing never touches DefDatabase.
    public sealed class DrawModel
    {
        // Instances are created only through Resolve, which assigns every field.
        public RenderModel Model = null!;
        public ThingDef?[] Defs = null!;    // parallel to Model.Cells; null for non-icon cells
        public int[] Counts = null!;        // parallel; raw count for icon cells (tooltips)
        public string?[] Tokens = null!;    // parallel; raw slot token for icon cells (tooltips)
        public string?[] Labels = null!;    // parallel; translated labels for label cells
        public string?[] Tooltips = null!;  // parallel; resolved icon hover labels
        public Texture2D?[] IconTextures = null!;
        public Color[] IconColors = null!;
        public float[] IconCorrections = null!;
        public float[] IconFittedScales = null!;
        public GUIContent?[] CounterContents = null!;
        public RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? RenderData;
        private int iconScaleRevision = -1;
        internal int IconDataRevision => iconScaleRevision;

        public static DrawModel Resolve(
            RenderModel model,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? renderData = null)
        {
            var defs = new ThingDef?[model.Cells.Count];
            var counts = new int[model.Cells.Count];
            var tokens = new string?[model.Cells.Count];
            var labels = new string?[model.Cells.Count];
            var tooltips = new string?[model.Cells.Count];
            var iconTextures = new Texture2D?[model.Cells.Count];
            var iconColors = new Color[model.Cells.Count];
            var iconCorrections = new float[model.Cells.Count];
            var iconFittedScales = new float[model.Cells.Count];
            var counterContents = new GUIContent?[model.Cells.Count];
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
                    if (tokens[i] != null && SlotToken.IsPoolRef(tokens[i]!)
                        && renderData != null
                        && renderData.Structure.TryGet(SlotToken.PoolId(tokens[i]!),
                            out _, out _, out string? poolName))
                        tooltips[i] = poolName;
                    else if (tokens[i] != null && SlotToken.IsPool(tokens[i]!))
                        tooltips[i] = GameResourceCatalog.Instance.CategoryLabelOf(
                            SlotToken.MemberName(tokens[i]!)).CapitalizeFirst();
                    else
                        tooltips[i] = defs[i] != null ? defs[i]!.LabelCap : cell.DefName;
                }
                else if (cell.Kind == CellKind.Counter)
                    counterContents[i] = new GUIContent(cell.Text ?? "");
                else if (cell.Kind == CellKind.Label)
                    // Count > 0 marks a parameterized label ("…and {0} more");
                    // formatted here so drawing never builds strings.
                    labels[i] = cell.Count > 0
                        ? string.Format(UiText.Get(cell.Text!), cell.Count) // label cells carry a key
                        : UiText.Get(cell.Text!);
            }
            var resolved = new DrawModel
            {
                Model = model,
                Defs = defs,
                Counts = counts,
                Tokens = tokens,
                Labels = labels,
                Tooltips = tooltips,
                IconTextures = iconTextures,
                IconColors = iconColors,
                IconCorrections = iconCorrections,
                IconFittedScales = iconFittedScales,
                CounterContents = counterContents,
                RenderData = renderData,
            };
            resolved.RefreshIconCacheIfNeeded();
            return resolved;
        }

        public void RefreshCounts(
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
        {
            RenderData = renderData;
            for (int i = 0; i < Model.Cells.Count; i++)
                if (Model.Cells[i].Kind == CellKind.Icon)
                    Counts[i] = Model.Cells[i].Count;
                else if (Model.Cells[i].Kind == CellKind.Counter)
                {
                    GUIContent? content = CounterContents[i];
                    string text = Model.Cells[i].Text ?? "";
                    if (content != null && content.text != text)
                        content.text = text;
                }
        }

        public void RefreshIconCacheIfNeeded()
        {
            int revision = IconScaleCache.Revision;
            if (iconScaleRevision == revision) return;
            for (int i = 0; i < Model.Cells.Count; i++)
            {
                ThingDef? def = Defs[i];
                if (def == null) continue;
                float correction = IconScaleCache.ScaleFor(def);
                Texture2D? texture = def.uiIcon;
                bool usable = texture != null && texture != BaseContent.BadTex;
                IconRenderPlan plan = IconRenderPlan.Create(
                    usable, correction, GenUI.IconDrawScale(def));
                IconCorrections[i] = plan.CorrectionScale;
                IconFittedScales[i] = plan.FittedScale;
                IconTextures[i] = plan.UseDirectRendering ? texture : null;
                IconColors[i] = def.uiIconColor;
            }
            iconScaleRevision = revision;
        }

        internal void RegisterHoveredTip(int cellIndex)
        {
            if ((uint)cellIndex >= (uint)Defs.Length) return;
            ThingDef? def = Defs[cellIndex];
            if (def == null) return;
            IconTips.TipHovered(
                def, Counts[cellIndex], Tokens[cellIndex], RenderData);
        }
    }

    public static class CellRenderer
    {
        private static readonly Color DimTriangle = new Color(1f, 1f, 1f, 0.3f);
        // Tiers shown only through hover expansion, so the white (configured)
        // depth stays readable while cycling markers.
        private static readonly Color HoverTriangle = new Color(1f, 0.9f, 0.45f);
        private static readonly Color LowTint = new Color(1f, 0.92f, 0.55f);
        private static readonly Color CriticalTint = new Color(1f, 0.72f, 0.45f);
        private static readonly Color LabelDim = new Color(1f, 1f, 1f, 0.6f);
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

        internal static Color TriangleColorFor(TriangleState state) =>
            state == TriangleState.Lit
                ? Color.white
                : state == TriangleState.HoverLit
                    ? HoverTriangle : DimTriangle;

        internal static Color BackingColorFor(PanelVisualOptions options) =>
            new Color(0f, 0f, 0f, options.BandOpacity);

        internal static Color TextColorFor(RenderCell cell) =>
            cell.Kind == CellKind.Label
                ? LabelDim
                : cell.Band == Band.Low
                    ? LowTint
                    : cell.Band == Band.Critical
                        ? CriticalTint : Color.white;

        public static void Draw(DrawModel draw)
            => DrawDirect(draw, float.MinValue, float.MaxValue,
                inputBlocked: false, PanelVisualOptions.Default);

        public static void Draw(
            DrawModel draw, float viewportTop, float viewportBottom)
            => DrawDirect(draw, viewportTop, viewportBottom,
                inputBlocked: false, PanelVisualOptions.Default);

        public static void Draw(
            DrawModel draw,
            float viewportTop,
            float viewportBottom,
            bool inputBlocked)
            => DrawDirect(draw, viewportTop, viewportBottom, inputBlocked,
                PanelVisualOptions.Default);

        public static void DrawDirect(
            DrawModel draw,
            float viewportTop,
            float viewportBottom,
            bool inputBlocked,
            PanelVisualOptions options)
        {
            PanelRenderPolicy policy = PanelRenderPolicy.For(
                Event.current.type == EventType.Repaint, inputBlocked);
            if (!policy.DrawCells) return;
            draw.RefreshIconCacheIfNeeded();
            using (GuiStateScope.Capture())
            {
                var bands = draw.Model.Bands;
                PanelBandRange visible = bands.Count == 0
                    ? default
                    : PanelViewport.VisibleBands(bands, viewportTop, viewportBottom);
                if (bands.Count == 0)
                    DrawVisualCells(draw, 0, draw.Model.Cells.Count,
                        viewportTop, viewportBottom, options);
                else
                {
                    for (int bandIndex = visible.Start;
                         bandIndex < visible.End;
                         bandIndex++)
                    {
                        RenderBand band = bands[bandIndex];
                        DrawVisualCells(draw, band.CellStart, band.CellCount,
                            viewportTop, viewportBottom, options);
                    }
                }

                using (TinyText.UseFont())
                {
                    TextAnchor textAnchor = Text.Anchor;
                    if (bands.Count == 0)
                        DrawTextCells(draw, 0, draw.Model.Cells.Count,
                            viewportTop, viewportBottom, ref textAnchor);
                    else
                    {
                        for (int bandIndex = visible.Start;
                             bandIndex < visible.End;
                             bandIndex++)
                        {
                            RenderBand band = bands[bandIndex];
                            DrawTextCells(
                                draw, band.CellStart, band.CellCount,
                                viewportTop, viewportBottom, ref textAnchor);
                        }
                    }
                }
            }
        }

        private static void DrawVisualCells(
            DrawModel draw,
            int start,
            int count,
            float viewportTop,
            float viewportBottom,
            PanelVisualOptions options)
        {
            List<RenderCell> cells = draw.Model.Cells;
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                RenderCell cell = cells[i];
                if (RenderCellPass.PassOf(cell.Kind) != CellRenderPass.Visual)
                    continue;
                if (!PanelViewport.IntersectsVertically(
                    cell.Rect, viewportTop, viewportBottom)) continue;
                var rect = new Rect(
                    cell.Rect.X, cell.Rect.Y, cell.Rect.W, cell.Rect.H);
                switch (cell.Kind)
                {
                    case CellKind.GroupBack:
                    {
                        Widgets.DrawBoxSolid(
                            rect, BackingColorFor(options));
                        Widgets.DrawBoxSolid(
                            new Rect(rect.x, rect.y,
                                LayoutMetrics.StripeW, rect.height),
                            StripeColorFor(cell.GroupIndex));
                        break;
                    }
                    case CellKind.Triangle:
                    {
                        GUI.color = TriangleColorFor(cell.Triangle);
                        GUI.DrawTexture(rect, ReadoutTextures.Triangle);
                        GUI.color = Color.white;
                        break;
                    }
                    case CellKind.Highlight:
                    {
                        Widgets.DrawHighlight(rect);
                        break;
                    }
                    case CellKind.Icon:
                    {
                        ThingDef? def = draw.Defs[i];
                        if (def == null) break;
                        Texture2D? texture = draw.IconTextures[i];
                        if (texture != null)
                        {
                            GUI.color = draw.IconColors[i];
                            Widgets.DrawTextureFitted(
                                rect, texture, draw.IconFittedScales[i]);
                            GUI.color = Color.white;
                        }
                        else
                        {
                            float correction = draw.IconCorrections[i];
                            var iconRect = correction == 1f
                                ? rect : new Rect(
                                    rect.x + rect.width
                                        * (1f - correction) / 2f,
                                    rect.y + rect.height
                                        * (1f - correction) / 2f,
                                    rect.width * correction,
                                    rect.height * correction);
                            Widgets.ThingIcon(iconRect, def);
                        }
                        break;
                    }
                    case CellKind.EmptySlot:
                        // Invisible append/drop target in the editor band.
                        break;
                }
            }
        }

        private static void DrawTextCells(
            DrawModel draw,
            int start,
            int count,
            float viewportTop,
            float viewportBottom,
            ref TextAnchor textAnchor)
        {
            var cells = draw.Model.Cells;
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                RenderCell cell = cells[i];
                if (cell.Kind != CellKind.Counter
                    && cell.Kind != CellKind.Label) continue;
                if (!PanelViewport.IntersectsVertically(
                    cell.Rect, viewportTop, viewportBottom)) continue;
                var rect = new Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.W, cell.Rect.H);
                switch (cell.Kind)
                {
                    case CellKind.Counter:
                    {
                        if (cell.Band != Band.Normal)
                            GUI.color = cell.Band == Band.Low
                                ? LowTint : CriticalTint;
                        if (textAnchor != TextAnchor.UpperCenter)
                        {
                            Text.Anchor = TextAnchor.UpperCenter;
                            textAnchor = TextAnchor.UpperCenter;
                        }
                        GUIContent? content = draw.CounterContents[i];
                        if (content != null)
                            GUI.Label(rect, content, Text.CurFontStyle);
                        else
                            Widgets.Label(rect, cell.Text);
                        GUI.color = Color.white;
                        break;
                    }
                    case CellKind.Label:
                    {
                        GUI.color = LabelDim;
                        if (textAnchor != TextAnchor.UpperLeft)
                        {
                            Text.Anchor = TextAnchor.UpperLeft;
                            textAnchor = TextAnchor.UpperLeft;
                        }
                        Widgets.Label(rect, draw.Labels[i]);
                        GUI.color = Color.white;
                        break;
                    }
                }
            }
        }
    }
}
