using System.Collections.Generic;
using System.Text;
using EPrimeReadouts.Core;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha cached glyph surface for all content counters and
    /// labels. Glyphs are drawn through the font material (the atlas carries
    /// black RGB, so the sprite material would render them black) and the
    /// channel publishes with coverage recovered from the red channel; the
    /// previous front keeps presenting until the build promotes.
    internal sealed class PanelGlyphProduct
    {
        private readonly PanelBufferBackend backend;
        private readonly PanelSurfaceChannel channel;
        private readonly TextGenerator generator = new TextGenerator();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> triangles = new List<int>();

        private PanelTextRevision publishedRevision;
        private bool hasPublished;
        private PanelTextRevision pendingRevision;
        private bool hasPending;

        internal PanelGlyphProduct(PanelBufferBackend backend)
        {
            this.backend = backend;
            channel = new PanelSurfaceChannel(backend, coverageFromRed: true);
        }

        internal PanelSurfaceChannel Channel => channel;

        internal SurfaceEnsureResult Ensure(
            DrawModel draw,
            PanelTextRevision next,
            int width,
            int height,
            float rasterScale)
        {
            if (hasPending && pendingRevision.Equals(next))
                return SurfaceEnsureResult.InFlight;
            if (hasPublished && publishedRevision.Equals(next)
                && !channel.HasWorkInFlight)
                return SurfaceEnsureResult.Unchanged;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                width, width, height, rasterScale);
            if (!backend.IsAvailable
                || width <= 0 || height <= 0
                || sizing.PixelWidth > SystemInfo.maxTextureSize
                || sizing.PixelHeight > SystemInfo.maxTextureSize)
                return SurfaceEnsureResult.Failed;

            using (GuiStateScope.Capture())
            using (TinyText.UseFont())
            {
                GUIStyle style = Text.CurFontStyle;
                Font? font = style.font ?? GUI.skin.font;
                if (font == null || font.material == null
                    || font.material.mainTexture == null)
                    return SurfaceEnsureResult.Failed;

                int fontSize = style.fontSize > 0
                    ? style.fontSize : font.fontSize;
                RequestCharacters(
                    draw, font,
                    ScaledFontSize(fontSize, sizing.RasterScale),
                    style.fontStyle);
                if (!BuildGeometry(
                        draw, style, font, fontSize, sizing.RasterScale))
                    return SurfaceEnsureResult.Failed;

                RenderTexture? working = channel.EnsureWorking(
                    sizing.PixelWidth, sizing.PixelHeight);
                if (working == null) return SurfaceEnsureResult.Failed;
                RenderTexture? previous = RenderTexture.active;
                RenderTexture.active = working;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(
                        0f, sizing.PixelWidth,
                        sizing.PixelHeight, 0f);
                    // DrawFontQuadsToActive clears the reused target itself
                    // before drawing.
                    backend.DrawFontQuadsToActive(
                        vertices, uvs, colors, triangles,
                        font.material);
                }
                finally
                {
                    GL.PopMatrix();
                    RenderTexture.active = previous;
                }
                channel.RequestPublish();
                pendingRevision = next;
                hasPending = true;
                return SurfaceEnsureResult.InFlight;
            }
        }

        internal void OnPromoted()
        {
            if (!channel.Promote()) return;
            publishedRevision = pendingRevision;
            hasPublished = true;
            hasPending = false;
        }

        internal void OnAborted()
        {
            channel.Abandon();
            hasPending = false;
        }

        /// Presents the given pixel-snapped window onto the screen; the glyph
        /// texture shares its dimensions with the base surface, so both use
        /// the same window.
        internal bool PresentWindow(
            float screenX, float screenY, PanelPresentWindow window)
        {
            Texture2D? front = channel.Front;
            if (front == null || !hasPublished || !window.Visible)
                return false;
            backend.Present(front, new Rect(
                    screenX, screenY,
                    window.DestWidth, window.DestHeight),
                new Rect(0f, window.UvY, 1f, window.UvHeight));
            return true;
        }

        /// Draws text through the sprite material: the atlas RGB multiplies
        /// the vertex color, so this suits dark text on a light field (the
        /// search box) and nothing else.
        internal bool DrawTextIntoActive(
            string? text,
            Rect rect,
            GameFont gameFont,
            TextAnchor anchor,
            Color color,
            float rasterScale,
            GUIStyle? styleOverride = null)
        {
            if (text == null || text.Length == 0) return true;
            using (GuiStateScope.Capture())
            {
                Font? font = BuildText(
                    text, rect, gameFont, anchor, color,
                    rasterScale, styleOverride);
                if (font == null) return false;
                if (vertices.Count != 0)
                    backend.DrawQuadsToActive(
                        vertices, uvs, colors,
                        font.material.mainTexture);
                return true;
            }
        }

        /// Draws light text through the FONT material into the active
        /// target, which it clears first. The target must belong to a
        /// coverage-from-red channel and the color's red channel must be one;
        /// see PanelBufferBackend.DrawFontQuadsToActive.
        internal bool DrawFontTextIntoActive(
            string? text,
            Rect rect,
            GameFont gameFont,
            TextAnchor anchor,
            Color color,
            float rasterScale)
        {
            using (GuiStateScope.Capture())
            {
                Font? font = BuildText(
                    text ?? "", rect, gameFont, anchor, color,
                    rasterScale, null);
                if (font == null) return false;
                backend.DrawFontQuadsToActive(
                    vertices, uvs, colors, triangles, font.material);
                return true;
            }
        }

        /// Fills the quad buffers with one text run placed in content pixels;
        /// returns the font whose material and atlas the quads index, or null
        /// when the font cannot be drawn. Callers hold a GuiStateScope.
        private Font? BuildText(
            string text,
            Rect rect,
            GameFont gameFont,
            TextAnchor anchor,
            Color color,
            float rasterScale,
            GUIStyle? styleOverride)
        {
            Text.Font = gameFont;
            GUIStyle style = styleOverride ?? Text.CurFontStyle;
            Font? font = style.font ?? GUI.skin.font;
            if (font == null || font.material == null
                || font.material.mainTexture == null)
                return null;
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            if (text.Length == 0) return font;
            int fontSize = style.fontSize > 0
                ? style.fontSize : font.fontSize;
            font.RequestCharactersInTexture(
                text, ScaledFontSize(fontSize, rasterScale),
                style.fontStyle);
            Rect padded = PaddedRect(rect, style.padding);
            var settings = Settings(
                style, font, fontSize, padded.size,
                anchor, color, rasterScale);
            if (!generator.Populate(text, settings)) return null;
            AppendGenerated(padded, generator.verts, rasterScale);
            return font;
        }

        internal void Release()
        {
            channel.Release();
            hasPublished = false;
            hasPending = false;
            generator.Invalidate();
        }

        private static void RequestCharacters(
            DrawModel draw, Font font, int fontSize, FontStyle fontStyle)
        {
            var characters = new StringBuilder("0123456789-.kM");
            for (int i = 0; i < draw.Model.Cells.Count; i++)
            {
                RenderCell cell = draw.Model.Cells[i];
                if (cell.Kind == CellKind.Counter)
                    characters.Append(cell.Text);
                else if (cell.Kind == CellKind.Label)
                    characters.Append(draw.Labels[i]);
            }
            font.RequestCharactersInTexture(
                characters.ToString(), fontSize, fontStyle);
        }

        private bool BuildGeometry(
            DrawModel draw,
            GUIStyle style,
            Font font,
            int fontSize,
            float rasterScale)
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            for (int i = 0; i < draw.Model.Cells.Count; i++)
            {
                RenderCell cell = draw.Model.Cells[i];
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

                Rect rect = PaddedRect(new Rect(
                    cell.Rect.X, cell.Rect.Y,
                    cell.Rect.W, cell.Rect.H), style.padding);
                TextGenerationSettings settings = Settings(
                    style, font, fontSize, rect.size, anchor,
                    CellRenderer.TextColorFor(cell), rasterScale);
                if (!generator.Populate(text, settings)) return false;
                AppendGenerated(rect, generator.verts, rasterScale);
            }
            return true;
        }

        private void AppendGenerated(
            Rect rect, IList<UIVertex> generated, float rasterScale)
        {
            int usable = GlyphQuadPlan.UsableVertexCount(generated.Count);
            for (int i = 0; i < usable; i += 4)
            {
                int start = vertices.Count;
                for (int j = 0; j < 4; j++)
                {
                    UIVertex vertex = generated[i + j];
                    GlyphRasterPoint point = GlyphRasterMath.Place(
                        rect.x, rect.y,
                        vertex.position.x, vertex.position.y,
                        rasterScale);
                    vertices.Add(new Vector3(
                        point.X, point.Y, 0f));
                    uvs.Add(vertex.uv0);
                    colors.Add(vertex.color);
                }
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
                triangles.Add(start);
            }
        }

        private static Rect PaddedRect(Rect rect, RectOffset padding) =>
            new Rect(
                rect.x + padding.left,
                rect.y + padding.top,
                rect.width - padding.horizontal,
                rect.height - padding.vertical);

        private static TextGenerationSettings Settings(
            GUIStyle style,
            Font font,
            int fontSize,
            Vector2 extents,
            TextAnchor anchor,
            Color color,
            float rasterScale) =>
            new TextGenerationSettings
            {
                font = font,
                color = color,
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
                generationExtents = extents,
                pivot = new Vector2(0f, 1f),
                generateOutOfBounds = true,
            };

        private static int ScaledFontSize(int fontSize, float rasterScale) =>
            Mathf.Max(1, Mathf.RoundToInt(fontSize * rasterScale));
    }
}
