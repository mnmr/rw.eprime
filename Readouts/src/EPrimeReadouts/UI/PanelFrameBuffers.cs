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
    /// revision, so a change re-renders exactly the surface it touched;
    /// scrolling rebuilds nothing — presentation selects a pixel-snapped
    /// window into the full-content textures. Publishes complete through
    /// asynchronous readback: the previous fronts keep presenting until every
    /// changed surface's publish lands, then the whole set promotes
    /// atomically between repaints, so presentation never mixes surfaces
    /// from different builds and no build stalls the GPU pipeline.
    internal sealed class PanelFrameBuffers
    {
        /// Publish failures are transient (a device reset can invalidate a
        /// working target mid-readback); the build aborts and retries. This
        /// many consecutive failures disable the buffered renderer.
        private const int MaxConsecutivePublishFailures = 3;

        private readonly PanelBufferPipeline pipeline;
        private readonly PanelBaseSurface baseSurface;
        private readonly PanelGlyphProduct glyphProduct;
        private readonly PanelHeaderSurface headerSurface;
        private bool hasSurfaces;
        private bool buildInFlight;
        private BufferBuildTicket inFlightTicket;
        private int consecutivePublishFailures;

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
        internal bool BuildInFlight => buildInFlight;

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
            SurfaceEnsureResult baseResult = baseSurface.Ensure(
                draw, options, baseRevision, geometry.RasterScale);
            if (baseResult == SurfaceEnsureResult.Failed) return false;

            PanelTextRevision textRevision = PanelTextRevision.Create(
                draw.Model, uiRevision, contentWidth, contentHeight);
            SurfaceEnsureResult glyphResult = glyphProduct.Ensure(
                draw, textRevision, contentWidth, contentHeight,
                geometry.RasterScale);
            if (glyphResult == SurfaceEnsureResult.Failed) return false;

            SurfaceEnsureResult headerResult = headerSurface.Ensure(
                header, glyphProduct);
            if (headerResult == SurfaceEnsureResult.Failed) return false;

            if (baseResult == SurfaceEnsureResult.Unchanged
                && glyphResult == SurfaceEnsureResult.Unchanged
                && headerResult == SurfaceEnsureResult.Unchanged)
            {
                pipeline.CompleteBuild(ticket);
                return true;
            }
            buildInFlight = true;
            inFlightTicket = ticket;
            return true;
        }

        /// Polls in-flight publishes once per frame. Returns false only when
        /// repeated failures exhausted the retry budget and the buffered
        /// renderer must disable itself.
        internal bool PumpBuild()
        {
            if (!buildInFlight) return true;
            SurfacePublishState baseState = baseSurface.Channel.Pump();
            SurfacePublishState glyphState = glyphProduct.Channel.Pump();
            SurfacePublishState headerState = headerSurface.Pump();
            if (baseState == SurfacePublishState.Failed
                || glyphState == SurfacePublishState.Failed
                || headerState == SurfacePublishState.Failed)
            {
                AbortInFlight();
                consecutivePublishFailures++;
                return consecutivePublishFailures
                    < MaxConsecutivePublishFailures;
            }
            if (baseState == SurfacePublishState.Pending
                || glyphState == SurfacePublishState.Pending
                || headerState == SurfacePublishState.Pending)
                return true;

            // Every changed surface is Ready (unchanged ones are Idle):
            // promote the complete set between repaints so presentation
            // never mixes surfaces from different builds.
            baseSurface.OnPromoted();
            glyphProduct.OnPromoted();
            headerSurface.OnPromoted();
            pipeline.CompleteBuild(inFlightTicket);
            buildInFlight = false;
            inFlightTicket = default;
            hasSurfaces = true;
            consecutivePublishFailures = 0;
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
            buildInFlight = false;
            inFlightTicket = default;
            consecutivePublishFailures = 0;
        }

        private void AbortInFlight()
        {
            baseSurface.OnAborted();
            glyphProduct.OnAborted();
            headerSurface.OnAborted();
            pipeline.AbortBuild(inFlightTicket);
            buildInFlight = false;
            inFlightTicket = default;
        }
    }
}
