using EPrimeReadouts.Core;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha cached copy of the header row (gear, search field or
    /// title). It is re-rendered only when its explicit PanelHeaderRevision
    /// changes, so typing in the search field re-renders this small strip and
    /// nothing else; the publish flows through the channel's asynchronous
    /// readback and the previous front keeps presenting until promotion.
    ///
    /// The title travels on a second, coverage-from-red channel: like the
    /// content counters it is drawn through the font material, because the
    /// sprite material multiplies by the atlas RGB and renders glyphs black.
    /// Both channels publish from one Ensure and promote together, so the
    /// presented strip never mixes a gear from one revision with a title from
    /// another.
    internal sealed class PanelHeaderSurface
    {
        private const float ClearColumnWidth = 22f;

        private readonly PanelBufferBackend backend;
        private readonly PanelSurfaceChannel channel;
        private readonly PanelSurfaceChannel titleChannel;
        private PanelHeaderRevision publishedRevision;
        private bool hasPublished;
        private PanelHeaderRevision pendingRevision;
        private bool hasPending;

        internal PanelHeaderSurface(PanelBufferBackend backend)
        {
            this.backend = backend;
            channel = new PanelSurfaceChannel(backend);
            titleChannel = new PanelSurfaceChannel(
                backend, coverageFromRed: true);
        }

        internal SurfaceEnsureResult Ensure(
            PanelHeaderRevision next, PanelGlyphProduct glyphs)
        {
            if (hasPending && pendingRevision.Equals(next))
                return SurfaceEnsureResult.InFlight;
            if (hasPublished && publishedRevision.Equals(next)
                && !channel.HasWorkInFlight
                && !titleChannel.HasWorkInFlight)
                return SurfaceEnsureResult.Unchanged;
            if (!backend.IsAvailable) return SurfaceEnsureResult.Failed;
            // The strip is as wide as its content needs: a title longer
            // than the panel extends past the panel edge instead of losing
            // its last letters to the texture bounds.
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                next.SurfaceWidth, next.SurfaceWidth,
                next.HeaderHeight, next.RasterScale);
            if (next.HeaderHeight <= 0
                || sizing.PixelWidth > SystemInfo.maxTextureSize
                || sizing.PixelHeight > SystemInfo.maxTextureSize)
                return SurfaceEnsureResult.Failed;

            RenderTexture? working = channel.EnsureWorking(
                sizing.PixelWidth, sizing.PixelHeight);
            if (working == null) return SurfaceEnsureResult.Failed;
            if (!Render(next, glyphs, working, sizing.RasterScale))
                return SurfaceEnsureResult.Failed;
            channel.RequestPublish();
            if (ShowsTitle(next))
            {
                RenderTexture? titleWorking = titleChannel.EnsureWorking(
                    sizing.PixelWidth, sizing.PixelHeight);
                if (titleWorking == null) return SurfaceEnsureResult.Failed;
                if (!RenderTitle(
                        next, glyphs, titleWorking, sizing.RasterScale))
                    return SurfaceEnsureResult.Failed;
                titleChannel.RequestPublish();
            }
            pendingRevision = next;
            hasPending = true;
            return SurfaceEnsureResult.InFlight;
        }

        /// Polls both channels; the worse state wins so a build waits for,
        /// or fails with, either publish.
        internal SurfacePublishState Pump()
        {
            SurfacePublishState strip = channel.Pump();
            SurfacePublishState title = titleChannel.Pump();
            if (strip == SurfacePublishState.Failed
                || title == SurfacePublishState.Failed)
                return SurfacePublishState.Failed;
            if (strip == SurfacePublishState.Pending
                || title == SurfacePublishState.Pending)
                return SurfacePublishState.Pending;
            if (strip == SurfacePublishState.Ready
                || title == SurfacePublishState.Ready)
                return SurfacePublishState.Ready;
            return SurfacePublishState.Idle;
        }

        internal void OnPromoted()
        {
            bool promoted = channel.Promote();
            titleChannel.Promote();
            if (!promoted) return;
            publishedRevision = pendingRevision;
            hasPublished = true;
            hasPending = false;
        }

        internal void OnAborted()
        {
            channel.Abandon();
            titleChannel.Abandon();
            hasPending = false;
        }

        internal bool Present(float screenX, float screenY)
        {
            Texture2D? front = channel.Front;
            if (front == null || !hasPublished) return false;
            var rect = new Rect(
                screenX, screenY,
                front.width / publishedRevision.RasterScale,
                front.height / publishedRevision.RasterScale);
            var uv = new Rect(0f, 0f, 1f, 1f);
            backend.Present(front, rect, uv);
            Texture2D? title = titleChannel.Front;
            if (title != null && ShowsTitle(publishedRevision))
                backend.Present(title, rect, uv);
            return true;
        }

        internal void Release()
        {
            channel.Release();
            titleChannel.Release();
            hasPublished = false;
            hasPending = false;
        }

        private static bool ShowsTitle(PanelHeaderRevision header) =>
            !header.ShowSearch && header.ShowTitle;

        private bool Render(
            PanelHeaderRevision header,
            PanelGlyphProduct glyphs,
            RenderTexture target,
            float rasterScale)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0f, target.width, target.height, 0f);
                GL.Clear(clearDepth: true, clearColor: true, Color.clear);

                backend.DrawToActive(
                    Scale(new Rect(0f, 2f, 22f, 22f), rasterScale),
                    ReadoutTextures.Gear, Color.white);

                if (header.ShowSearch)
                    return RenderSearchField(header, glyphs, rasterScale);
                return true;
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
            }
        }

        private bool RenderTitle(
            PanelHeaderRevision header,
            PanelGlyphProduct glyphs,
            RenderTexture target,
            float rasterScale)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                return glyphs.DrawFontTextIntoActive(
                    header.Title,
                    new Rect(PanelHeaderRevision.TitleX, 0f,
                        header.TitleWidth, header.HeaderHeight),
                    GameFont.Small, TextAnchor.MiddleLeft,
                    EprStyle.PanelTitleText, rasterScale);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private bool RenderSearchField(
            PanelHeaderRevision header,
            PanelGlyphProduct glyphs,
            float rasterScale)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                GUIStyle style = Text.CurTextFieldStyle;
                var fieldRect = new Rect(
                    26f, 1f,
                    header.HeaderWidth - 26f - ClearColumnWidth, 22f);
                Texture2D? background = style.normal.background;
                if (background == null) return false;
                backend.DrawNineSliceToActive(
                    Scale(fieldRect, rasterScale),
                    background, Scale(style.border, rasterScale),
                    Color.white);
                if (!glyphs.DrawTextIntoActive(
                    header.SearchText, fieldRect,
                    GameFont.Small, style.alignment,
                    style.normal.textColor,
                    rasterScale, style))
                    return false;
                if (header.SearchText.Length != 0)
                    backend.DrawToActive(
                        Scale(new Rect(
                                header.HeaderWidth - 20f,
                                5f, 16f, 16f),
                            rasterScale),
                        TexButton.CloseXSmall, Color.white);
            }
            return true;
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
