using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha, count-independent copy of the complete content area.
    /// It is re-rendered only when its explicit PanelBaseRevision changes;
    /// the publish flows through the channel's asynchronous readback, and the
    /// previous front keeps presenting until the build promotes.
    internal sealed class PanelBaseSurface
    {
        private readonly PanelBufferBackend backend;
        private readonly PanelSurfaceChannel channel;
        private PanelBaseRevision publishedRevision;
        private bool hasPublished;
        private PanelBaseRevision pendingRevision;
        private bool hasPending;

        internal PanelBaseSurface(PanelBufferBackend backend)
        {
            this.backend = backend;
            channel = new PanelSurfaceChannel(backend);
        }

        internal PanelSurfaceChannel Channel => channel;
        internal int PixelWidth => channel.FrontWidth;
        internal int PixelHeight => channel.FrontHeight;

        internal SurfaceEnsureResult Ensure(
            DrawModel draw,
            PanelVisualOptions options,
            PanelBaseRevision next,
            float rasterScale)
        {
            if (hasPending && pendingRevision.Equals(next))
                return SurfaceEnsureResult.InFlight;
            if (hasPublished && publishedRevision.Equals(next)
                && !channel.HasWorkInFlight)
                return SurfaceEnsureResult.Unchanged;
            if (!backend.IsAvailable) return SurfaceEnsureResult.Failed;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                next.Width, next.Width, next.Height, rasterScale);
            if (next.Width <= 0 || next.Height <= 0
                || sizing.PixelWidth > SystemInfo.maxTextureSize
                || sizing.PixelHeight > SystemInfo.maxTextureSize)
                return SurfaceEnsureResult.Failed;

            RenderTexture? working = channel.EnsureWorking(
                sizing.PixelWidth, sizing.PixelHeight);
            if (working == null) return SurfaceEnsureResult.Failed;
            if (!Render(draw, options, working, sizing.RasterScale))
                return SurfaceEnsureResult.Failed;
            channel.RequestPublish();
            pendingRevision = next;
            hasPending = true;
            return SurfaceEnsureResult.InFlight;
        }

        /// Called when the coordinating build promotes; adopts the pending
        /// revision if this surface's publish was part of it.
        internal void OnPromoted()
        {
            if (!channel.Promote()) return;
            publishedRevision = pendingRevision;
            hasPublished = true;
            hasPending = false;
        }

        /// Called when the coordinating build aborts; the next build
        /// re-renders this revision from scratch.
        internal void OnAborted()
        {
            channel.Abandon();
            hasPending = false;
        }

        /// Presents the given pixel-snapped window onto the screen at the
        /// destination origin.
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

        internal void Release()
        {
            channel.Release();
            hasPublished = false;
            hasPending = false;
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
