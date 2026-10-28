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
    /// The text revision includes the resolved counter font. Counters and
    /// game-preference captions are separate material batches in one surface.
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
        internal bool CanPresent => hasPublished && channel.Front != null;

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
            {
                RenderTexture? working = channel.EnsureWorking(
                    sizing.PixelWidth, sizing.PixelHeight);
                if (working == null) return SurfaceEnsureResult.Failed;
                RenderTexture? previous = RenderTexture.active;
                RenderTexture.active = working;
                GL.PushMatrix();
                bool visibleGeometry = false;
                try
                {
                    GL.LoadPixelMatrix(
                        0f, sizing.PixelWidth,
                        sizing.PixelHeight, 0f);
                    // Each font has its own atlas/material. Both batches
                    // publish as one surface; only the first clears it.
                    for (int pass = 0; pass < 2; pass++)
                    using (TinyText.UseFont(pass == 0 ? draw.CounterFont : TinyText.Metrics.Font))
                    {
                        GUIStyle style = Text.CurFontStyle;
                        Font? font = style.font ?? GUI.skin.font;
                        if (font == null || font.material == null
                            || font.material.mainTexture == null)
                            return SurfaceEnsureResult.Failed;
                        int fontSize = style.fontSize > 0 ? style.fontSize : font.fontSize;
                        RequestCharacters(draw, font,
                            ScaledFontSize(fontSize, sizing.RasterScale), style.fontStyle);
                        if (!BuildGeometry(draw, style, font, fontSize,
                                sizing.RasterScale, pass == 0 ? CellKind.Counter : CellKind.Label))
                            return SurfaceEnsureResult.Failed;
                        visibleGeometry |= HasVisibleGeometry(sizing.PixelWidth, sizing.PixelHeight);
                        backend.DrawFontQuadsToActive(vertices, uvs, colors, triangles,
                            font.material, clear: pass == 0);
                    }
                }
                finally
                {
                    GL.PopMatrix();
                    RenderTexture.active = previous;
                }
                channel.RequestPublish(visibleGeometry);
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
            float screenX, float screenY, PanelPresentWindow window, float rasterScale)
        {
            Texture2D? front = channel.Front;
            if (front == null || !hasPublished || !window.Visible)
                return false;
            if (PanelBufferBackend.UsesGuiRenderTargets)
            {
                // Independent layers can have different transparent padding
                // after builds at different physical display sizes. Select
                // the same physical rows from each, never rescale either.
                return backend.Present(front, new Rect(screenX, screenY,
                        front.width / rasterScale, window.DestHeight),
                    new Rect(0f, (front.height - window.TopPixels - window.HeightPixels)
                        / (float)front.height, 1f, window.HeightPixels / (float)front.height));
            }
            return backend.Present(front, new Rect(
                    screenX, screenY,
                    window.DestWidth, window.DestHeight),
                new Rect(0f, window.UvY, 1f, window.UvHeight));
        }

        /// Called only by a surface builder, immediately after generating its
        /// geometry. Empty/whitespace runs, degenerate quads and clipped text
        /// do not promise visible pixels. Require a whole glyph inside the
        /// target: a clipped sliver might contain only transparent padding.
        /// No font/atlas reads are needed.
        internal bool HasVisibleGeometry(int width, int height)
        {
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                if (colors[i].r == 0 || colors[i].a == 0) continue;
                float left = vertices[i].x;
                float right = left;
                float top = vertices[i].y;
                float bottom = top;
                for (int j = 1; j < 4; j++)
                {
                    Vector3 point = vertices[i + j];
                    left = Mathf.Min(left, point.x);
                    right = Mathf.Max(right, point.x);
                    top = Mathf.Min(top, point.y);
                    bottom = Mathf.Max(bottom, point.y);
                }
                if (left >= 0f && right <= width && right - left >= 1f
                    && top >= 0f && bottom <= height && bottom - top >= 1f)
                    return true;
            }
            return false;
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
            Rect clip = padded;
            if (PanelBufferBackend.UsesGuiRenderTargets && styleOverride != null)
            {
                // Native single-line fields align vertically in the original
                // rectangle; padding only restricts their clip and horizontal
                // origin. They must never draw over the adjacent clear button.
                padded.y = rect.y;
                padded.height = rect.height;
            }
            var settings = Settings(
                style, font, fontSize, padded.size,
                anchor, color, rasterScale);
            if (!generator.Populate(text, settings)) return null;
            AppendGenerated(padded, generator.verts, rasterScale);
            if (PanelBufferBackend.UsesGuiRenderTargets && styleOverride != null)
                ClipQuads(new Rect(clip.x * rasterScale, clip.y * rasterScale,
                    clip.width * rasterScale, clip.height * rasterScale));
            return font;
        }

        private void ClipQuads(Rect clip)
        {
            // Builder-only clipping of axis-aligned font quads. Interpolate
            // UVs with their positions so a partial final glyph stays sharp;
            // fully outside quads collapse to zero area.
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                Vector3 first = vertices[i];
                Vector3 opposite = vertices[i + 2];
                Vector2 firstUv = uvs[i];
                Vector2 oppositeUv = uvs[i + 2];
                float dx = opposite.x - first.x;
                float dy = opposite.y - first.y;
                if (dx == 0f || dy == 0f) continue;
                for (int j = 0; j < 4; j++)
                {
                    Vector3 point = vertices[i + j];
                    point.x = Mathf.Clamp(point.x, clip.xMin, clip.xMax);
                    point.y = Mathf.Clamp(point.y, clip.yMin, clip.yMax);
                    uvs[i + j] = new Vector2(
                        firstUv.x + (point.x - first.x) / dx * (oppositeUv.x - firstUv.x),
                        firstUv.y + (point.y - first.y) / dy * (oppositeUv.y - firstUv.y));
                    vertices[i + j] = point;
                }
            }
        }

        internal void Release()
        {
            channel.Release();
            hasPublished = false;
            hasPending = false;
            generator.Invalidate();
        }

        private void RequestCharacters(
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
            string requested = characters.ToString();
            font.RequestCharactersInTexture(requested, fontSize, fontStyle);
            if (PanelBufferBackend.UsesGuiRenderTargets)
            {
                // RequestCharacters is a no-op on Waylith. Populate the full
                // alphabet before retaining any UVs, since adding a later run
                // can otherwise grow the atlas beneath earlier geometry.
                generator.Populate(requested, Settings(Text.CurFontStyle,
                    font, fontSize, new Vector2(32768f, fontSize * 2f),
                    TextAnchor.UpperLeft, Color.white, 1f));
            }
        }

        private bool BuildGeometry(
            DrawModel draw,
            GUIStyle style,
            Font font,
            int fontSize,
            float rasterScale,
            CellKind kind)
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
                if (cell.Kind != kind) continue;
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
            // Waylith leaves centering inside the generated offsets (e.g.
            // x=13.625 at 125% scale). Snap the run, not individual glyphs.
            Vector3 runOrigin = PanelBufferBackend.UsesGuiRenderTargets && usable > 0
                ? generated[0].position : Vector3.zero;
            for (int i = 0; i < usable; i += 4)
            {
                int start = vertices.Count;
                for (int j = 0; j < 4; j++)
                {
                    UIVertex vertex = generated[i + j];
                    GlyphRasterPoint point = GlyphRasterMath.Place(
                        rect.x, rect.y,
                        vertex.position.x, vertex.position.y,
                        rasterScale, runOrigin.x, runOrigin.y);
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
                fontSize = PanelBufferBackend.UsesGuiRenderTargets
                    ? ScaledFontSize(fontSize, rasterScale) : fontSize,
                lineSpacing = 1f,
                richText = style.richText,
                scaleFactor = PanelBufferBackend.UsesGuiRenderTargets ? 1f : rasterScale,
                fontStyle = style.fontStyle,
                textAnchor = anchor,
                alignByGeometry = false,
                resizeTextForBestFit = false,
                resizeTextMinSize = fontSize,
                resizeTextMaxSize = fontSize,
                updateBounds = false,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                generationExtents = PanelBufferBackend.UsesGuiRenderTargets
                    ? extents * rasterScale : extents,
                pivot = new Vector2(0f, 1f),
                generateOutOfBounds = true,
            };

        private static int ScaledFontSize(int fontSize, float rasterScale) =>
            Mathf.Max(1, Mathf.RoundToInt(fontSize * rasterScale));
    }
}
