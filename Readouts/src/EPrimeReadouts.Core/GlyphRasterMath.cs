namespace EPrimeReadouts.Core
{
    public readonly struct GlyphRasterPoint
    {
        public GlyphRasterPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public static class GlyphRasterMath
    {
        public static GlyphRasterPoint Place(
            float logicalOriginX,
            float logicalOriginY,
            float generatedX,
            float generatedY,
            float rasterScale) =>
            new GlyphRasterPoint(
                Snap(logicalOriginX * rasterScale) + generatedX,
                Snap(logicalOriginY * rasterScale) - generatedY);

        private static float Snap(float value) =>
            (float)System.Math.Round(
                value, System.MidpointRounding.AwayFromZero);
    }
}
