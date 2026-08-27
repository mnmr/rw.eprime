using EPrimeReadouts.Core;
using UnityEngine;

namespace EPrimeReadouts.UI
{
    internal readonly struct VisiblePanelGeometry
    {
        internal VisiblePanelGeometry(
            float headerWidth,
            int headerHeight,
            int contentHeight,
            float rasterScale)
        {
            HeaderWidth = headerWidth;
            HeaderHeight = headerHeight;
            ContentHeight = contentHeight;
            RasterScale = rasterScale;
        }

        internal float HeaderWidth { get; }
        internal int HeaderHeight { get; }
        internal int ContentHeight { get; }
        internal float RasterScale { get; }
    }

    /// Coordinates the three cached straight-alpha surfaces the buffered
    /// panel presents each repaint: the count-independent base pixels, the
    /// content glyphs, and the header strip. Each surface owns its own
    /// revision, so a change rebuilds exactly the surface it touched;
    /// scrolling rebuilds nothing — presentation selects a pixel-snapped
    /// window into the full-content textures. Layered straight-alpha
    /// presentation is exact source-over (the backend probe validates that
    /// blend), so the on-screen result equals the previously pre-composited
    /// front buffer.
    internal sealed class PanelFrameBuffers
    {
        private readonly PanelBufferPipeline pipeline;
        private readonly PanelBaseSurface baseSurface;
        private readonly PanelGlyphProduct glyphProduct;
        private readonly PanelHeaderSurface headerSurface;
        private bool hasSurfaces;

        internal PanelFrameBuffers(
            PanelBufferPipeline pipeline,
            PanelBufferBackend backend)
        {
            this.pipeline = pipeline;
            baseSurface = new PanelBaseSurface(backend);
            glyphProduct = new PanelGlyphProduct(backend);
            headerSurface = new PanelHeaderSurface(backend);
        }

        internal bool HasSurfaces => hasSurfaces;

        internal bool BuildBack(
            BufferBuildTicket ticket,
            DrawModel draw,
            VisiblePanelGeometry geometry,
            PanelHeaderRevision header,
            PanelVisualOptions options,
            int uiRevision,
            int iconScaleRevision)
        {
            draw.RefreshIconCacheIfNeeded();
            int contentWidth = Mathf.Max(
                1, Mathf.CeilToInt(draw.Model.TotalWidth));
            int contentHeight = Mathf.Max(
                1, Mathf.CeilToInt(draw.Model.TotalHeight));
            var baseRevision = new PanelBaseRevision(
                draw.Model, contentWidth, contentHeight,
                uiRevision, iconScaleRevision,
                draw.IconDataRevision, options);
            if (!baseSurface.Ensure(
                    draw, options, baseRevision, geometry.RasterScale))
                return false;

            PanelTextRevision textRevision = PanelTextRevision.Create(
                draw.Model, uiRevision, contentWidth, contentHeight);
            if (!glyphProduct.Ensure(
                draw, textRevision, contentWidth, contentHeight,
                geometry.RasterScale))
                return false;

            if (!headerSurface.Ensure(header, glyphProduct))
                return false;

            pipeline.CompleteBuild(ticket);
            hasSurfaces = true;
            return true;
        }

        /// Presents header plus the visible content window. Bounded per-frame
        /// work: window arithmetic and three textured draws.
        internal bool Present(
            float screenX,
            float screenY,
            float scrollY,
            float viewportHeight,
            VisiblePanelGeometry geometry)
        {
            if (!hasSurfaces) return false;
            if (!headerSurface.Present(screenX, screenY)) return false;
            PanelPresentWindow window = PanelPresentWindow.Create(
                baseSurface.PixelWidth, baseSurface.PixelHeight,
                geometry.RasterScale, scrollY, viewportHeight);
            if (!window.Visible) return true;
            float contentTop = screenY + geometry.HeaderHeight;
            baseSurface.PresentWindow(screenX, contentTop, window);
            glyphProduct.PresentWindow(screenX, contentTop, window);
            return true;
        }

        internal void Release()
        {
            baseSurface.Release();
            glyphProduct.Release();
            headerSurface.Release();
            hasSurfaces = false;
        }
    }
}
