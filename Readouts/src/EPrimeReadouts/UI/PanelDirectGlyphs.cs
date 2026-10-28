using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EPrimeReadouts.Core;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Draws the panel's counters and labels straight to the screen from the
    /// same generated glyph geometry the buffered glyph surface uses: the
    /// font rasterized at the physical size for the current UI scale, every
    /// run snapped to the pixel grid, pure vertex color through the font
    /// material. IMGUI labels would instead magnify base-size glyphs through
    /// the GUI matrix, which is why the two renderers used to disagree.
    // Cache contract:
    // Owner: process/current main readout panel (via ReadoutPanel).
    // Key: draw-model identity, PanelTextRevision (text content, UI metric
    //      revision, resolved counter font, content dimensions), raster scale, and Waylith's font
    //      atlas revision (Unity retains its existing generation contract).
    // Value: glyph quads in content-local physical pixels (vertices, uvs,
    //        vertex colors), immutable between rebuilds.
    // Dependencies: the keys above; a count refresh changes the text
    //        revision, a scroll or panel move changes only the draw origin.
    // Refresh policy: immediate rebuild on the first draw after a key moves.
    // Equality policy: matching keys reuse the geometry lists as they are.
    // Teardown: Release clears the geometry and invalidates the generator.
    [StaticConstructorOnStartup]
    internal sealed class PanelDirectGlyphs
    {
        // Waylith's Unity compatibility layer implements GL.Viewport as a
        // no-op. Its primitives instead honor the enclosing GUI group's clip
        // rectangle, and GL.LoadIdentity submits absolute screen pixels.
        // Resolve the immutable engine contract once, never per render pass.
        internal static readonly bool UsesGuiClip =
            typeof(GL).Assembly.GetName().Name == "UnityStubs";

        // Engine-owned process service; the compiled getter retains no font,
        // texture, world or map. Resolve optional APIs once at type startup.
        // The steady path reads one revision without reflection or allocation.
        private static readonly Func<int>? ReadAtlasRevision = CreateAtlasReader();

        private readonly TextGenerator generator = new TextGenerator();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color32> colors = new List<Color32>();
        private DrawModel? builtDraw;
        private PanelTextRevision builtRevision;
        private float builtScale = -1f;
        private int builtAtlasRevision = -1;
        private Material? builtMaterial;
        private readonly bool countersOnly;
        private readonly PanelDirectGlyphs? counters;

        internal PanelDirectGlyphs(bool countersOnly = false)
        {
            this.countersOnly = countersOnly;
            if (!countersOnly) counters = new PanelDirectGlyphs(true);
        }

        /// Rebuilds the geometry when its key moved. Returns false when the
        /// font has no usable material, in which case the caller draws IMGUI
        /// labels instead.
        internal bool Ensure(DrawModel draw, int uiRevision, float rasterScale)
        {
            int atlasRevision = UsesGuiClip ? ReadAtlasRevision?.Invoke() ?? -1 : 0;
            // If a future compatibility layer omits this contract, use native
            // labels rather than retain UVs whose lifetime cannot be tracked.
            if (atlasRevision < 0) return false;
            RenderModel model = draw.Model;
            PanelTextRevision revision = PanelTextRevision.Create(
                model, uiRevision,
                Mathf.Max(1, Mathf.CeilToInt(model.TotalWidth)),
                Mathf.Max(1, Mathf.CeilToInt(model.TotalHeight)), (int)draw.CounterFont);
            if (ReferenceEquals(builtDraw, draw)
                && builtRevision.Equals(revision)
                && builtScale == rasterScale
                && builtAtlasRevision == atlasRevision
                && builtMaterial != null)
                return true;

            if (counters != null)
            {
                // Warm both fonts before either batch retains atlas UVs.
                // Waylith can repack the shared atlas when a font is first used.
                WarmFonts(draw, rasterScale);
                if (!counters.Ensure(draw, uiRevision, rasterScale)) return false;
            }
            using (GuiStateScope.Capture())
            using (TinyText.UseFont(countersOnly ? draw.CounterFont : TinyText.Metrics.Font))
            {
                GUIStyle style = Text.CurFontStyle;
                Font? font = style.font ?? GUI.skin.font;
                if (font == null || font.material == null
                    || font.material.mainTexture == null)
                {
                    Release();
                    return false;
                }
                int fontSize = style.fontSize > 0 ? style.fontSize : font.fontSize;
                int scaledSize = Mathf.Max(1,
                    Mathf.RoundToInt(fontSize * rasterScale));
                if (!Build(draw, style, font, fontSize, scaledSize, rasterScale))
                {
                    Release();
                    return false;
                }
                builtDraw = draw;
                builtRevision = revision;
                builtScale = rasterScale;
                builtAtlasRevision = UsesGuiClip ? ReadAtlasRevision?.Invoke() ?? -1 : 0;
                builtMaterial = font.material;
                return true;
            }
        }

        /// Draws the cached geometry during Repaint. originUi is the content
        /// origin in UI units (already scroll-adjusted); clipUi is the
        /// visible content window in UI units. Bounded per-frame work: one
        /// material pass and one vertex loop over the cached lists.
        internal void Draw(Vector2 originUi, Rect clipUi, float rasterScale)
        {
            counters?.Draw(originUi, clipUi, rasterScale);
            if (builtMaterial == null || vertices.Count == 0) return;
            if (clipUi.width <= 0f || clipUi.height <= 0f) return;

            float originX = Snap(originUi.x * rasterScale);
            float originY = Snap(originUi.y * rasterScale);
            int clipLeft = Mathf.FloorToInt(clipUi.x * rasterScale);
            int clipTop = Mathf.FloorToInt(clipUi.y * rasterScale);
            int clipRight = Mathf.CeilToInt(clipUi.xMax * rasterScale);
            int clipBottom = Mathf.CeilToInt(clipUi.yMax * rasterScale);
            clipLeft = Mathf.Clamp(clipLeft, 0, Screen.width);
            clipRight = Mathf.Clamp(clipRight, 0, Screen.width);
            clipTop = Mathf.Clamp(clipTop, 0, Screen.height);
            clipBottom = Mathf.Clamp(clipBottom, 0, Screen.height);
            if (clipRight <= clipLeft || clipBottom <= clipTop) return;

            if (!builtMaterial.SetPass(0)) return;
            GL.PushMatrix();
            try
            {
                // A viewport the size of the visible window clips the glyphs
                // exactly like the scroll view clips IMGUI content; the pixel
                // matrix keeps screen pixel coordinates inside it.
                if (UsesGuiClip)
                    GL.LoadIdentity();
                else
                {
                    GL.Viewport(new Rect(
                        clipLeft, Screen.height - clipBottom,
                        clipRight - clipLeft, clipBottom - clipTop));
                    GL.LoadPixelMatrix(clipLeft, clipRight, clipBottom, clipTop);
                }
                GL.Begin(GL.QUADS);
                try
                {
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        GL.Color(colors[i]);
                        Vector2 uv = uvs[i];
                        GL.TexCoord2(uv.x, uv.y);
                        Vector3 vertex = vertices[i];
                        GL.Vertex3(vertex.x + originX, vertex.y + originY, 0f);
                    }
                }
                finally
                {
                    GL.End();
                }
            }
            finally
            {
                GL.PopMatrix();
                if (!UsesGuiClip)
                    GL.Viewport(new Rect(0f, 0f, Screen.width, Screen.height));
            }
        }

        internal void Release()
        {
            counters?.Release();
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            builtDraw = null;
            builtRevision = default;
            builtScale = -1f;
            builtAtlasRevision = -1;
            builtMaterial = null;
            generator.Invalidate();
        }

        private bool Build(
            DrawModel draw, GUIStyle style, Font font,
            int fontSize, int scaledSize, float rasterScale)
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            List<RenderCell> cells = draw.Model.Cells;
            var characters = new System.Text.StringBuilder("0123456789-.kM");
            for (int i = 0; i < cells.Count; i++)
            {
                RenderCell cell = cells[i];
                if (cell.Kind == CellKind.Counter) characters.Append(cell.Text);
                else if (cell.Kind == CellKind.Label) characters.Append(draw.Labels[i]);
            }
            font.RequestCharactersInTexture(
                characters.ToString(), scaledSize, style.fontStyle);

            for (int i = 0; i < cells.Count; i++)
            {
                RenderCell cell = cells[i];
                string? text;
                TextAnchor anchor;
                if (cell.Kind != (countersOnly ? CellKind.Counter : CellKind.Label)) continue;
                if (cell.Kind == CellKind.Counter)
                {
                    text = cell.Text;
                    anchor = TextAnchor.UpperCenter;
                }
                else if (cell.Kind == CellKind.Label)
                {
                    text = draw.Labels[i];
                    anchor = TextAnchor.UpperLeft;
                }
                else continue;
                if (string.IsNullOrEmpty(text)) continue;

                RectOffset padding = style.padding;
                var rect = new Rect(
                    cell.Rect.X + padding.left, cell.Rect.Y + padding.top,
                    cell.Rect.W - padding.horizontal,
                    cell.Rect.H - padding.vertical);
                var settings = new TextGenerationSettings
                {
                    font = font,
                    color = CellRenderer.TextColorFor(cell),
                    // Waylith's TextGenerator ignores scaleFactor. Supply
                    // physical font size/extents explicitly on that engine;
                    // Unity retains its existing generation contract.
                    fontSize = UsesGuiClip ? scaledSize : fontSize,
                    lineSpacing = 1f,
                    richText = style.richText,
                    scaleFactor = UsesGuiClip ? 1f : rasterScale,
                    fontStyle = style.fontStyle,
                    textAnchor = anchor,
                    alignByGeometry = false,
                    resizeTextForBestFit = false,
                    resizeTextMinSize = fontSize,
                    resizeTextMaxSize = fontSize,
                    updateBounds = false,
                    verticalOverflow = VerticalWrapMode.Overflow,
                    horizontalOverflow = HorizontalWrapMode.Overflow,
                    generationExtents = UsesGuiClip
                        ? rect.size * rasterScale : rect.size,
                    pivot = new Vector2(0f, 1f),
                    generateOutOfBounds = true,
                };
                if (!generator.Populate(text!, settings)) return false;
                IList<UIVertex> generated = generator.verts;
                int usable = GlyphQuadPlan.UsableVertexCount(generated.Count);
                Vector3 runOrigin = UsesGuiClip && usable > 0
                    ? generated[0].position : Vector3.zero;
                for (int v = 0; v < usable; v++)
                {
                    UIVertex vertex = generated[v];
                    GlyphRasterPoint point = GlyphRasterMath.Place(
                        rect.x, rect.y,
                        vertex.position.x, vertex.position.y,
                        rasterScale, runOrigin.x, runOrigin.y);
                    vertices.Add(new Vector3(point.X, point.Y, 0f));
                    uvs.Add(vertex.uv0);
                    colors.Add(vertex.color);
                }
            }
            return true;
        }

        private static float Snap(float value) =>
            (float)System.Math.Round(value, System.MidpointRounding.AwayFromZero);

        private void WarmFonts(DrawModel draw, float scale)
        {
            if (!UsesGuiClip) return;
            var characters = new System.Text.StringBuilder("0123456789-.kM");
            for (int i = 0; i < draw.Model.Cells.Count; i++)
            {
                RenderCell cell = draw.Model.Cells[i];
                if (cell.Kind == CellKind.Counter) characters.Append(cell.Text);
                else if (cell.Kind == CellKind.Label) characters.Append(draw.Labels[i]);
            }
            string requested = characters.ToString();
            using (GuiStateScope.Capture())
            for (int pass = 0; pass < 2; pass++)
            using (TinyText.UseFont(pass == 0 ? draw.CounterFont : TinyText.Metrics.Font))
            {
                GUIStyle style = Text.CurFontStyle;
                Font? font = style.font ?? GUI.skin.font;
                if (font == null) continue;
                int size = Mathf.Max(1, Mathf.RoundToInt(
                    (style.fontSize > 0 ? style.fontSize : font.fontSize) * scale));
                generator.Populate(requested, new TextGenerationSettings
                {
                    font = font, fontSize = size, color = Color.white,
                    fontStyle = style.fontStyle, scaleFactor = 1f, lineSpacing = 1f,
                    generationExtents = new Vector2(32768f, size * 2f),
                    pivot = new Vector2(0f, 1f),
                    horizontalOverflow = HorizontalWrapMode.Overflow,
                    verticalOverflow = VerticalWrapMode.Overflow,
                });
            }
        }

        private static Func<int>? CreateAtlasReader()
        {
            if (!UsesGuiClip) return null;
            Type? backend = typeof(GL).Assembly.GetType("UnityStubs.Gui.TextMeshProBackend");
            var current = backend?.GetProperty("Current");
            var revision = current?.PropertyType.GetProperty("AtlasVersion");
            if (current?.GetGetMethod() == null || revision?.PropertyType != typeof(int)
                || revision.GetGetMethod() == null) return null;
            var instance = Expression.Variable(current.PropertyType, "backend");
            return Expression.Lambda<Func<int>>(Expression.Block(
                new[] { instance },
                Expression.Assign(instance, Expression.Property(null, current)),
                Expression.Condition(
                    Expression.Equal(instance, Expression.Constant(null, current.PropertyType)),
                    Expression.Constant(-1), Expression.Property(instance, revision)))).Compile();
        }
    }
}
