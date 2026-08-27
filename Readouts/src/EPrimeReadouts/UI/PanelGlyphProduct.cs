using System.Collections.Generic;
using System.Text;
using EPrimeReadouts.Core;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha cached glyph surface for all content counters and labels.
    internal sealed class PanelGlyphProduct
    {
        private readonly PanelBufferBackend backend;
        private readonly TextGenerator generator = new TextGenerator();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> triangles = new List<int>();

        private Texture2D? texture;
        private RenderTexture? working;
        private int workingPixelWidth;
        private int workingPixelHeight;
        private PanelTextRevision revision;
        private bool hasRevision;

        internal PanelGlyphProduct(PanelBufferBackend backend)
        {
            this.backend = backend;
        }

        internal bool Ensure(
            DrawModel draw,
            PanelTextRevision next,
            int width,
            int height,
            float rasterScale)
        {
            if (hasRevision && revision.Equals(next)) return true;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                width, width, height, rasterScale);
            if (!backend.IsAvailable
                || width <= 0 || height <= 0
                || sizing.PixelWidth > SystemInfo.maxTextureSize
                || sizing.PixelHeight > SystemInfo.maxTextureSize)
                return false;

            using (new GuiStateScope())
            using (TinyText.UseFont())
            {
                GUIStyle style = Text.CurFontStyle;
                Font? font = style.font ?? GUI.skin.font;
                if (font == null || font.material == null
                    || font.material.mainTexture == null)
                    return false;

                int fontSize = style.fontSize > 0
                    ? style.fontSize : font.fontSize;
                RequestCharacters(
                    draw, font,
                    ScaledFontSize(fontSize, sizing.RasterScale),
                    style.fontStyle);
                if (!BuildGeometry(
                        draw, style, font, fontSize, sizing.RasterScale))
                    return false;

                EnsureWorking(sizing.PixelWidth, sizing.PixelHeight);
                if (working == null) return false;
                Texture2D? replacement = null;
                try
                {
                    replacement = backend.CreatePublishedTexture(
                        sizing.PixelWidth, sizing.PixelHeight,
                        FilterMode.Point);
                    RenderTexture? previous = RenderTexture.active;
                    RenderTexture.active = working;
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(
                            0f, sizing.PixelWidth,
                            sizing.PixelHeight, 0f);
                        // DrawFontQuadsToActive clears the reused target
                        // itself before drawing.
                        backend.DrawFontQuadsToActive(
                            vertices, uvs, colors, triangles,
                            font.material);
                    }
                    finally
                    {
                        GL.PopMatrix();
                        RenderTexture.active = previous;
                    }
                    backend.PublishFont(working, replacement);

                    Texture2D? old = texture;
                    texture = replacement;
                    replacement = null;
                    revision = next;
                    hasRevision = true;
                    PanelBufferBackend.ReleaseTexture(old);
                    return true;
                }
                finally
                {
                    PanelBufferBackend.ReleaseTexture(replacement);
                }
            }
        }

        /// Presents the given pixel-snapped window onto the screen; the glyph
        /// texture shares its dimensions with the base surface, so both use
        /// the same window.
        internal bool PresentWindow(
            float screenX, float screenY, PanelPresentWindow window)
        {
            if (texture == null || !hasRevision || !window.Visible)
                return false;
            backend.Present(texture, new Rect(
                    screenX, screenY,
                    window.DestWidth, window.DestHeight),
                new Rect(0f, window.UvY, 1f, window.UvHeight));
            return true;
        }

        internal bool DrawTextIntoActive(
            string? text,
            Rect rect,
            GameFont gameFont,
            TextAnchor anchor,
            Color color,
            float rasterScale,
            GUIStyle? styleOverride = null)
        {
            if (string.IsNullOrEmpty(text)) return true;
            using (new GuiStateScope())
            {
                Text.Font = gameFont;
                GUIStyle style = styleOverride ?? Text.CurFontStyle;
                Font? font = style.font ?? GUI.skin.font;
                if (font == null || font.material == null
                    || font.material.mainTexture == null)
                    return false;
                int fontSize = style.fontSize > 0
                    ? style.fontSize : font.fontSize;
                font.RequestCharactersInTexture(
                    text, ScaledFontSize(fontSize, rasterScale),
                    style.fontStyle);
                vertices.Clear();
                uvs.Clear();
                colors.Clear();
                triangles.Clear();
                Rect padded = PaddedRect(rect, style.padding);
                var settings = Settings(
                    style, font, fontSize, padded.size,
                    anchor, color, rasterScale);
                if (!generator.Populate(text, settings)) return false;
                AppendGenerated(padded, generator.verts, rasterScale);
                if (vertices.Count != 0)
                    backend.DrawQuadsToActive(
                        vertices, uvs, colors,
                        font.material.mainTexture);
                return true;
            }
        }

        internal void Release()
        {
            PanelBufferBackend.ReleaseTexture(texture);
            PanelBufferBackend.ReleaseTexture(working);
            texture = null;
            working = null;
            workingPixelWidth = 0;
            workingPixelHeight = 0;
            hasRevision = false;
            generator.Invalidate();
        }

        private void EnsureWorking(int pixelWidth, int pixelHeight)
        {
            if (working != null
                && workingPixelWidth == pixelWidth
                && workingPixelHeight == pixelHeight)
                return;
            PanelBufferBackend.ReleaseTexture(working);
            working = backend.CreateWorkingSurface(pixelWidth, pixelHeight);
            workingPixelWidth = pixelWidth;
            workingPixelHeight = pixelHeight;
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

        private static Rect Scale(Rect rect, float scale) =>
            new Rect(
                rect.x * scale,
                rect.y * scale,
                rect.width * scale,
                rect.height * scale);
    }
}
