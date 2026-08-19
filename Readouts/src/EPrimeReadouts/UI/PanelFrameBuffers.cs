using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    internal readonly struct VisiblePanelGeometry
    {
        internal VisiblePanelGeometry(
            PanelSurfaceSizing surface,
            int headerHeight,
            int contentHeight,
            float scrollY)
        {
            Width = surface.LogicalWidth;
            HeaderWidth = surface.HeaderWidth;
            PixelWidth = surface.PixelWidth;
            PixelHeight = surface.PixelHeight;
            RasterScale = surface.RasterScale;
            PresentationWidth = surface.PresentationWidth;
            PresentationHeight = surface.PresentationHeight;
            HeaderHeight = headerHeight;
            ContentHeight = contentHeight;
            ScrollY = scrollY;
        }

        internal int Width { get; }
        internal float HeaderWidth { get; }
        internal int PixelWidth { get; }
        internal int PixelHeight { get; }
        internal float RasterScale { get; }
        internal float PresentationWidth { get; }
        internal float PresentationHeight { get; }
        internal int HeaderHeight { get; }
        internal int ContentHeight { get; }
        internal float ScrollY { get; }
        internal int Height => HeaderHeight + ContentHeight;
    }

    internal readonly struct PanelHeaderBufferData
    {
        internal PanelHeaderBufferData(
            bool showSearch,
            bool showTitle,
            string searchText,
            string title,
            float titleWidth)
        {
            ShowSearch = showSearch;
            ShowTitle = showTitle;
            SearchText = searchText ?? "";
            Title = title ?? "";
            TitleWidth = titleWidth;
        }

        internal bool ShowSearch { get; }
        internal bool ShowTitle { get; }
        internal string SearchText { get; }
        internal string Title { get; }
        internal float TitleWidth { get; }
        internal string RevisionKey => ShowSearch
            ? "search:" + SearchText
            : ShowTitle ? "title:" + Title : "none";
    }

    /// Owns immutable front and writable back published textures.
    internal sealed class PanelFrameBuffers
    {
        private const float ClearColumnWidth = 22f;

        private readonly PanelBufferPipeline pipeline;
        private readonly PanelBufferBackend backend;
        private readonly PanelBaseSurface baseSurface;
        private readonly PanelGlyphProduct glyphProduct;

        private Texture2D? front;
        private Texture2D? back;
        private RenderTexture? working;
        private int frontPixelWidth;
        private int frontPixelHeight;
        private float frontLogicalWidth;
        private float frontLogicalHeight;
        private int backPixelWidth;
        private int backPixelHeight;
        private float backLogicalWidth;
        private float backLogicalHeight;
        private int workingPixelWidth;
        private int workingPixelHeight;
        private bool hasFront;

        internal PanelFrameBuffers(
            PanelBufferPipeline pipeline,
            PanelBufferBackend backend)
        {
            this.pipeline = pipeline;
            this.backend = backend;
            baseSurface = new PanelBaseSurface(backend);
            glyphProduct = new PanelGlyphProduct(backend);
        }

        internal bool HasFront => hasFront && front != null;

        internal bool BuildBack(
            BufferBuildTicket ticket,
            DrawModel draw,
            VisiblePanelGeometry geometry,
            PanelHeaderBufferData header,
            PanelVisualOptions options,
            int uiRevision,
            int iconScaleRevision)
        {
            if (!backend.IsAvailable || geometry.Height <= 0) return false;
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
                draw.Model, header.RevisionKey,
                uiRevision, contentWidth, contentHeight);
            if (!glyphProduct.Ensure(
                draw, textRevision, contentWidth, contentHeight,
                geometry.RasterScale))
                return false;

            EnsureBuffers(geometry.PixelWidth, geometry.PixelHeight);
            if (working == null || back == null) return false;
            if (!Compose(draw, geometry, header)) return false;
            backend.Publish(working, back);
            var visibleContent = new RectF(
                0f, geometry.ScrollY,
                draw.Model.TotalWidth, geometry.ContentHeight);
            if (!glyphProduct.CompositeVisibleInto(
                    back, visibleContent,
                    geometry.HeaderHeight, geometry.RasterScale))
                return false;
            backLogicalWidth = geometry.PresentationWidth;
            backLogicalHeight = geometry.PresentationHeight;
            pipeline.CompleteBuild(ticket);
            return true;
        }

        internal bool Swap()
        {
            if (!pipeline.TrySwapOnRepaint()) return false;
            Texture2D? oldFront = front;
            int oldFrontPixelWidth = frontPixelWidth;
            int oldFrontPixelHeight = frontPixelHeight;
            float oldFrontLogicalWidth = frontLogicalWidth;
            float oldFrontLogicalHeight = frontLogicalHeight;
            front = back;
            frontPixelWidth = backPixelWidth;
            frontPixelHeight = backPixelHeight;
            frontLogicalWidth = backLogicalWidth;
            frontLogicalHeight = backLogicalHeight;
            hasFront = front != null;
            if (oldFront != null
                && oldFrontPixelWidth == frontPixelWidth
                && oldFrontPixelHeight == frontPixelHeight)
            {
                back = oldFront;
                backPixelWidth = oldFrontPixelWidth;
                backPixelHeight = oldFrontPixelHeight;
                backLogicalWidth = oldFrontLogicalWidth;
                backLogicalHeight = oldFrontLogicalHeight;
            }
            else
            {
                PanelBufferBackend.ReleaseTexture(oldFront);
                back = null;
                backPixelWidth = 0;
                backPixelHeight = 0;
                backLogicalWidth = 0f;
                backLogicalHeight = 0f;
            }
            return hasFront;
        }

        internal bool Present(float screenX, float screenY)
        {
            if (!HasFront) return false;
            backend.Present(front!, new Rect(
                    screenX, screenY,
                    frontLogicalWidth, frontLogicalHeight),
                new Rect(0f, 0f, 1f, 1f));
            return true;
        }

        internal void Release()
        {
            baseSurface.Release();
            glyphProduct.Release();
            PanelBufferBackend.ReleaseTexture(front);
            PanelBufferBackend.ReleaseTexture(back);
            PanelBufferBackend.ReleaseTexture(working);
            front = null;
            back = null;
            working = null;
            frontPixelWidth = 0;
            frontPixelHeight = 0;
            frontLogicalWidth = 0f;
            frontLogicalHeight = 0f;
            backPixelWidth = 0;
            backPixelHeight = 0;
            backLogicalWidth = 0f;
            backLogicalHeight = 0f;
            workingPixelWidth = 0;
            workingPixelHeight = 0;
            hasFront = false;
        }

        private bool Compose(
            DrawModel draw,
            VisiblePanelGeometry geometry,
            PanelHeaderBufferData header)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = working;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(
                    0f, geometry.PixelWidth, geometry.PixelHeight, 0f);
                GL.Clear(clearDepth: true, clearColor: true, Color.clear);
                if (!DrawHeader(geometry, header)) return false;

                float visibleHeight = geometry.ContentHeight;
                var source = new RectF(
                    0f, geometry.ScrollY,
                    draw.Model.TotalWidth, visibleHeight);
                var destination = new Rect(
                    0f, geometry.HeaderHeight,
                    draw.Model.TotalWidth, visibleHeight);
                return baseSurface.DrawVisibleIntoActive(
                    destination, source, geometry.RasterScale);
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
            }
        }

        private bool DrawHeader(
            VisiblePanelGeometry geometry,
            PanelHeaderBufferData header)
        {
            backend.DrawToActive(
                Scale(new Rect(0f, 2f, 22f, 22f), geometry.RasterScale),
                ReadoutTextures.Gear, Color.white);

            if (header.ShowSearch)
            {
                using (new GuiStateScope())
                {
                    Text.Font = GameFont.Small;
                    GUIStyle style = Text.CurTextFieldStyle;
                    var fieldRect = new Rect(
                        26f, 1f,
                        geometry.HeaderWidth - 26f - ClearColumnWidth, 22f);
                    Texture2D? background = style.normal.background;
                    if (background == null) return false;
                    backend.DrawNineSliceToActive(
                        Scale(fieldRect, geometry.RasterScale),
                        background, Scale(style.border, geometry.RasterScale),
                        Color.white);
                    if (!glyphProduct.DrawTextIntoActive(
                        header.SearchText, fieldRect,
                        GameFont.Small, style.alignment,
                        style.normal.textColor,
                        geometry.RasterScale, style))
                        return false;
                    if (header.SearchText.Length != 0)
                        backend.DrawToActive(
                            Scale(new Rect(
                                    geometry.HeaderWidth - 20f,
                                    5f, 16f, 16f),
                                geometry.RasterScale),
                            TexButton.CloseXSmall, Color.white);
                }
                return true;
            }

            if (!header.ShowTitle) return true;
            return glyphProduct.DrawTextIntoActive(
                header.Title,
                new Rect(26f, 0f, header.TitleWidth,
                    geometry.HeaderHeight),
                GameFont.Small, TextAnchor.MiddleLeft,
                EprStyle.HeaderText, geometry.RasterScale);
        }

        private void EnsureBuffers(int nextPixelWidth, int nextPixelHeight)
        {
            PanelBufferResizeDecision decision =
                PanelBufferResizeDecision.Create(
                    HasFront,
                    workingPixelWidth, workingPixelHeight,
                    backPixelWidth, backPixelHeight,
                    nextPixelWidth, nextPixelHeight);

            if (!decision.KeepFront && front != null)
            {
                PanelBufferBackend.ReleaseTexture(front);
                front = null;
                frontPixelWidth = 0;
                frontPixelHeight = 0;
                frontLogicalWidth = 0f;
                frontLogicalHeight = 0f;
            }
            if (decision.ReplaceWorking)
            {
                PanelBufferBackend.ReleaseTexture(working);
                working = backend.CreateWorkingSurface(
                    nextPixelWidth, nextPixelHeight);
                workingPixelWidth = nextPixelWidth;
                workingPixelHeight = nextPixelHeight;
            }
            if (decision.ReplaceBack)
            {
                PanelBufferBackend.ReleaseTexture(back);
                back = backend.CreatePublishedTexture(
                    nextPixelWidth, nextPixelHeight);
                backPixelWidth = nextPixelWidth;
                backPixelHeight = nextPixelHeight;
                backLogicalWidth = 0f;
                backLogicalHeight = 0f;
            }
        }

        private static Rect Scale(Rect rect, float scale) =>
            new Rect(
                rect.x * scale,
                rect.y * scale,
                rect.width * scale,
                rect.height * scale);

        private static RectOffset Scale(RectOffset border, float scale) =>
            new RectOffset(
                Mathf.RoundToInt(border.left * scale),
                Mathf.RoundToInt(border.right * scale),
                Mathf.RoundToInt(border.top * scale),
                Mathf.RoundToInt(border.bottom * scale));
    }
}
