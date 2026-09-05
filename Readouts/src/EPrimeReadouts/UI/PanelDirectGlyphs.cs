using System.Collections.Generic;
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
    //      revision, content dimensions), and the raster scale.
    // Value: glyph quads in content-local physical pixels (vertices, uvs,
    //        vertex colors), immutable between rebuilds.
    // Dependencies: the keys above; a count refresh changes the text
    //        revision, a scroll or panel move changes only the draw origin.
    // Refresh policy: immediate rebuild on the first draw after a key moves.
    // Equality policy: matching keys reuse the geometry lists as they are.
    // Teardown: Release clears the geometry and invalidates the generator.
    internal sealed class PanelDirectGlyphs
    {
        private readonly TextGenerator generator = new TextGenerator();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color32> colors = new List<Color32>();
        private DrawModel? builtDraw;
        private PanelTextRevision builtRevision;
        private float builtScale = -1f;
        private Material? builtMaterial;

        /// Rebuilds the geometry when its key moved. Returns false when the
        /// font has no usable material, in which case the caller draws IMGUI
        /// labels instead.
        internal bool Ensure(DrawModel draw, int uiRevision, float rasterScale)
        {
            RenderModel model = draw.Model;
            PanelTextRevision revision = PanelTextRevision.Create(
                model, uiRevision,
                Mathf.Max(1, Mathf.CeilToInt(model.TotalWidth)),
                Mathf.Max(1, Mathf.CeilToInt(model.TotalHeight)));
            if (ReferenceEquals(builtDraw, draw)
                && builtRevision.Equals(revision)
                && builtScale == rasterScale
                && builtMaterial != null)
                return true;

            using (GuiStateScope.Capture())
            using (TinyText.UseFont())
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
                GL.Viewport(new Rect(
                    clipLeft, Screen.height - clipBottom,
                    clipRight - clipLeft, clipBottom - clipTop));
                GL.LoadPixelMatrix(clipLeft, clipRight, clipBottom, clipTop);
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
                GL.Viewport(new Rect(0f, 0f, Screen.width, Screen.height));
            }
        }

        internal void Release()
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            builtDraw = null;
            builtRevision = default;
            builtScale = -1f;
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
                    fontSize = fontSize,
                    lineSpacing = 1f,
                    richText = style.richText,
                    scaleFactor = rasterScale,
                    fontStyle = style.fontStyle,
                    textAnchor = anchor,
                    alignByGeometry = false,
                    resizeTextForBestFit = false,
                    resizeTextMinSize = fontSize,
                    resizeTextMaxSize = fontSize,
                    updateBounds = false,
                    verticalOverflow = VerticalWrapMode.Overflow,
                    horizontalOverflow = HorizontalWrapMode.Overflow,
                    generationExtents = rect.size,
                    pivot = new Vector2(0f, 1f),
                    generateOutOfBounds = true,
                };
                if (!generator.Populate(text!, settings)) return false;
                IList<UIVertex> generated = generator.verts;
                int usable = GlyphQuadPlan.UsableVertexCount(generated.Count);
                for (int v = 0; v < usable; v++)
                {
                    UIVertex vertex = generated[v];
                    GlyphRasterPoint point = GlyphRasterMath.Place(
                        rect.x, rect.y,
                        vertex.position.x, vertex.position.y,
                        rasterScale);
                    vertices.Add(new Vector3(point.X, point.Y, 0f));
                    uvs.Add(vertex.uv0);
                    colors.Add(vertex.color);
                }
            }
            return true;
        }

        private static float Snap(float value) =>
            (float)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
    }
}
