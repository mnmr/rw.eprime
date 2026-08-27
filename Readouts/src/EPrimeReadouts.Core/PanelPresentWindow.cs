using System;

namespace EPrimeReadouts.Core
{
    /// Pixel-snapped presentation window into a full-content cached texture.
    /// Scrolling presents a different window of the same texture instead of
    /// rebuilding anything; the window origin snaps to the physical pixel
    /// grid so point-filtered texels keep their one-to-one screen mapping,
    /// and the destination extent is derived from the snapped pixel window
    /// (physical size divided by raster scale) so no resampling occurs.
    public readonly struct PanelPresentWindow
    {
        private PanelPresentWindow(
            bool visible,
            int topPixels,
            int heightPixels,
            float destWidth,
            float destHeight,
            float uvY,
            float uvHeight)
        {
            Visible = visible;
            TopPixels = topPixels;
            HeightPixels = heightPixels;
            DestWidth = destWidth;
            DestHeight = destHeight;
            UvY = uvY;
            UvHeight = uvHeight;
        }

        public bool Visible { get; }
        /// Snapped physical scroll offset from the texture top.
        public int TopPixels { get; }
        public int HeightPixels { get; }
        /// Logical destination extent (physical pixels / raster scale).
        public float DestWidth { get; }
        public float DestHeight { get; }
        /// Normalized source rect, bottom-left origin (GPU convention);
        /// X spans the full texture width.
        public float UvY { get; }
        public float UvHeight { get; }

        public static PanelPresentWindow Create(
            int texturePixelWidth,
            int texturePixelHeight,
            float rasterScale,
            float scrollY,
            float viewportLogicalHeight)
        {
            if (texturePixelWidth <= 0 || texturePixelHeight <= 0
                || rasterScale <= 0f || viewportLogicalHeight <= 0f)
                return default;

            int topPixels = (int)Math.Round(scrollY * rasterScale,
                MidpointRounding.AwayFromZero);
            if (topPixels < 0) topPixels = 0;
            if (topPixels > texturePixelHeight) topPixels = texturePixelHeight;

            int heightPixels = (int)Math.Round(
                viewportLogicalHeight * rasterScale,
                MidpointRounding.AwayFromZero);
            if (heightPixels > texturePixelHeight - topPixels)
                heightPixels = texturePixelHeight - topPixels;
            if (heightPixels <= 0) return default;

            return new PanelPresentWindow(
                visible: true,
                topPixels,
                heightPixels,
                texturePixelWidth / rasterScale,
                heightPixels / rasterScale,
                uvY: (texturePixelHeight - topPixels - heightPixels)
                    / (float)texturePixelHeight,
                uvHeight: heightPixels / (float)texturePixelHeight);
        }
    }
}
