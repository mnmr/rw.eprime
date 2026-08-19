using System;

namespace EPrimeReadouts.Core
{
    public readonly struct PanelSurfaceSizing
    {
        private PanelSurfaceSizing(
            float headerWidth,
            int logicalWidth,
            int logicalHeight,
            int pixelWidth,
            int pixelHeight,
            float rasterScale)
        {
            HeaderWidth = headerWidth;
            LogicalWidth = logicalWidth;
            LogicalHeight = logicalHeight;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            RasterScale = rasterScale;
        }

        public float HeaderWidth { get; }
        public int LogicalWidth { get; }
        public int LogicalHeight { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public float RasterScale { get; }
        public float PresentationWidth => PixelWidth / RasterScale;
        public float PresentationHeight => PixelHeight / RasterScale;

        public static PanelSurfaceSizing Create(
            float headerWidth,
            float contentWidth,
            int logicalHeight,
            float uiScale)
        {
            float safeHeader = Math.Max(1f, headerWidth);
            int width = Math.Max(1,
                (int)Math.Ceiling(Math.Max(safeHeader, contentWidth)));
            int height = Math.Max(1, logicalHeight);
            float scale = uiScale > 0f ? uiScale : 1f;
            return new PanelSurfaceSizing(
                safeHeader,
                width,
                height,
                Math.Max(1, (int)Math.Ceiling(width * scale)),
                Math.Max(1, (int)Math.Ceiling(height * scale)),
                scale);
        }
    }
}
