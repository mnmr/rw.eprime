using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Straight-alpha cached copy of the header row (gear, search field or
    /// title). It is rebuilt only when its explicit PanelHeaderRevision
    /// changes, so typing in the search field re-renders this small strip and
    /// nothing else.
    internal sealed class PanelHeaderSurface
    {
        private const float ClearColumnWidth = 22f;

        private readonly PanelBufferBackend backend;
        private Texture2D? texture;
        private RenderTexture? working;
        private int workingPixelWidth;
        private int workingPixelHeight;
        private PanelHeaderRevision revision;
        private bool hasRevision;

        internal PanelHeaderSurface(PanelBufferBackend backend)
        {
            this.backend = backend;
        }

        internal bool Ensure(PanelHeaderRevision next, PanelGlyphProduct glyphs)
        {
            if (hasRevision && revision.Equals(next)) return true;
            if (!backend.IsAvailable) return false;
            PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
                next.HeaderWidth, next.HeaderWidth,
                next.HeaderHeight, next.RasterScale);
            if (next.HeaderHeight <= 0
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
                if (!Render(next, glyphs, working, sizing.RasterScale))
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

        internal bool Present(float screenX, float screenY)
        {
            if (texture == null || !hasRevision) return false;
            backend.Present(texture, new Rect(
                    screenX, screenY,
                    texture.width / revision.RasterScale,
                    texture.height / revision.RasterScale),
                new Rect(0f, 0f, 1f, 1f));
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
            using (new GuiStateScope())
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
