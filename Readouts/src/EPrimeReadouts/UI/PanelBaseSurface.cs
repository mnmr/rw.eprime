using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha, count-independent copy of the complete content area.
    /// It is rebuilt only when its explicit PanelBaseRevision changes.
    internal sealed class PanelBaseSurface
    {

        private readonly PanelBufferBackend backend;
        private Texture2D? texture;
        private RenderTexture? working;
        private int workingPixelWidth;
        private int workingPixelHeight;
        private PanelBaseRevision revision;
        private bool hasRevision;

        internal PanelBaseSurface(PanelBufferBackend backend)
        {
            this.backend = backend;
        }

        internal int PixelWidth => texture != null ? texture.width : 0;
        internal int PixelHeight => texture != null ? texture.height : 0;

        internal bool Ensure(
            DrawModel draw,
            PanelVisualOptions options,
            PanelBaseRevision next,
            float rasterScale)
        {
            if (hasRevision && revision.Equals(next)) return true;
            if (!backend.IsAvailable) return false;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                next.Width, next.Width, next.Height, rasterScale);
            if (next.Width <= 0 || next.Height <= 0
                || sizing.PixelWidth > SystemInfo.maxTextureSize
                || sizing.PixelHeight > SystemInfo.maxTextureSize)
                return false;

            EnsureWorking(sizing.PixelWidth, sizing.PixelHeight);
            if (working == null) return false;
            Texture2D? replacement = null;
            try
            {
                replacement = backend.CreatePublishedTexture(
                    sizing.PixelWidth, sizing.PixelHeight,
                    FilterMode.Point);
                if (!Render(
                        draw, options, working, sizing.RasterScale))
                    return false;
                backend.Publish(working, replacement);

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

        /// Presents the given pixel-snapped window onto the screen at the
        /// destination origin.
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

        internal void Release()
        {
            PanelBufferBackend.ReleaseTexture(texture);
            PanelBufferBackend.ReleaseTexture(working);
            texture = null;
            working = null;
            workingPixelWidth = 0;
            workingPixelHeight = 0;
            hasRevision = false;
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

        private bool Render(
            DrawModel draw,
            PanelVisualOptions options,
            RenderTexture working,
            float rasterScale)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = working;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0f, working.width, working.height, 0f);
                GL.Clear(clearDepth: true, clearColor: true, Color.clear);
                List<RenderCell> cells = draw.Model.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    RenderCell cell = cells[i];
                    var rect = Scale(new Rect(
                        cell.Rect.X, cell.Rect.Y,
                        cell.Rect.W, cell.Rect.H), rasterScale);
                    switch (cell.Kind)
                    {
                        case CellKind.GroupBack:
                            DrawSolid(rect,
                                CellRenderer.BackingColorFor(options));
                            // Stripe width scales with the raster like every
                            // other dimension; an unscaled width diverges from
                            // the direct-render reference at fractional UI
                            // scales.
                            DrawSolid(new Rect(
                                    rect.x, rect.y,
                                    LayoutMetrics.StripeW * rasterScale,
                                    rect.height),
                                CellRenderer.StripeColorFor(cell.GroupIndex));
                            break;
                        case CellKind.Triangle:
                            backend.DrawToActive(
                                rect, ReadoutTextures.Triangle,
                                CellRenderer.TriangleColorFor(cell.Triangle));
                            break;
                        case CellKind.Highlight:
                            backend.DrawToActive(
                                rect, TexUI.HighlightTex, Color.white);
                            break;
                        case CellKind.Icon:
                            if (!DrawIcon(draw, i, rect)) return false;
                            break;
                        case CellKind.EmptySlot:
                            // Invisible append/drop target in editor models.
                            break;
                    }
                }
                return true;
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
            }
        }

        private bool DrawIcon(DrawModel draw, int index, Rect cellRect)
        {
            Texture2D? icon = draw.IconTextures[index];
            if (icon == null) return false;

            Rect fitted = Fit(cellRect, icon, draw.IconFittedScales[index]);
            backend.DrawToActive(
                fitted, icon, draw.IconColors[index]);
            return true;
        }

        private void DrawSolid(Rect rect, Color color) =>
            backend.DrawToActive(rect, BaseContent.WhiteTex, color);

        private void DrawBorder(Rect rect, Color color, float rasterScale)
        {
            DrawSolid(new Rect(
                rect.x, rect.y, rect.width, rasterScale), color);
            DrawSolid(new Rect(
                rect.x, rect.yMax - rasterScale,
                rect.width, rasterScale), color);
            DrawSolid(new Rect(
                rect.x, rect.y, rasterScale, rect.height), color);
            DrawSolid(new Rect(
                rect.xMax - rasterScale, rect.y,
                rasterScale, rect.height), color);
        }

        private static Rect Scale(Rect rect, float scale) =>
            new Rect(
                rect.x * scale,
                rect.y * scale,
                rect.width * scale,
                rect.height * scale);

        private static Rect Fit(Rect outer, Texture texture, float scale)
        {
            float sourceAspect = texture.width / (float)texture.height;
            float targetWidth;
            float targetHeight;
            if (outer.width / outer.height > sourceAspect)
            {
                targetHeight = outer.height * scale;
                targetWidth = targetHeight * sourceAspect;
            }
            else
            {
                targetWidth = outer.width * scale;
                targetHeight = targetWidth / sourceAspect;
            }
            return new Rect(
                outer.x + (outer.width - targetWidth) * 0.5f,
                outer.y + (outer.height - targetHeight) * 0.5f,
                targetWidth, targetHeight);
        }
    }
}
