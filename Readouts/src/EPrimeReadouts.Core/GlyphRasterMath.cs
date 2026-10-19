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
        // Generated offsets are already physical pixels. Some engines leave
        // a fractional alignment offset in them; snap one anchor for the
        // whole run and apply its translation to every vertex. Rounding each
        // vertex would distort glyph sizes and fractional character advances.
        // Zero anchors preserve the Unity generator's existing placement.
        public static GlyphRasterPoint Place(
            float logicalOriginX,
            float logicalOriginY,
            float generatedX,
            float generatedY,
            float rasterScale,
            float generatedRunX = 0f,
            float generatedRunY = 0f) =>
            new GlyphRasterPoint(
                Snap(logicalOriginX * rasterScale)
                    + (Snap(generatedRunX) - generatedRunX) + generatedX,
                Snap(logicalOriginY * rasterScale)
                    - (Snap(generatedRunY) - generatedRunY) - generatedY);

        private static float Snap(float value) =>
            (float)System.Math.Round(
                value, System.MidpointRounding.AwayFromZero);
    }
}
