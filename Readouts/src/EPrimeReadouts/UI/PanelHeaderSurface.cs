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
    internal sealed class PanelHeaderSurface
    {
        private const float ClearColumnWidth = 22f;

        private readonly PanelBufferBackend backend;
        private readonly PanelSurfaceChannel channel;
        private PanelHeaderRevision publishedRevision;
        private bool hasPublished;
        private PanelHeaderRevision pendingRevision;
        private bool hasPending;

        internal PanelHeaderSurface(PanelBufferBackend backend)
        {
            this.backend = backend;
            channel = new PanelSurfaceChannel(backend);
        }

        internal PanelSurfaceChannel Channel => channel;

        internal SurfaceEnsureResult Ensure(
            PanelHeaderRevision next, PanelGlyphProduct glyphs)
        {
            if (hasPending && pendingRevision.Equals(next))
                return SurfaceEnsureResult.InFlight;
            if (hasPublished && publishedRevision.Equals(next)
                && !channel.HasWorkInFlight)
                return SurfaceEnsureResult.Unchanged;
            if (!backend.IsAvailable) return SurfaceEnsureResult.Failed;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                next.HeaderWidth, next.HeaderWidth,
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
            pendingRevision = next;
            hasPending = true;
            return SurfaceEnsureResult.InFlight;
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

        internal bool Present(float screenX, float screenY)
        {
            Texture2D? front = channel.Front;
            if (front == null || !hasPublished) return false;
            backend.Present(front, new Rect(
                    screenX, screenY,
                    front.width / publishedRevision.RasterScale,
                    front.height / publishedRevision.RasterScale),
                new Rect(0f, 0f, 1f, 1f));
            return true;
        }

        internal void Release()
        {
            channel.Release();
            hasPublished = false;
            hasPending = false;
        }

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

                if (!header.ShowTitle) return true;
                return glyphs.DrawTextIntoActive(
                    header.Title,
                    new Rect(26f, 0f, header.TitleWidth, header.HeaderHeight),
                    GameFont.Small, TextAnchor.MiddleLeft,
                    EprStyle.HeaderText, rasterScale);
            }
            finally
            {
                GL.PopMatrix();
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
