namespace EPrimeReadouts.Core
{
    /// Physical target sizing for a compositor that truncates its float
    /// display-size/target-size round-trip when creating the viewport.
    public static class GuiTargetSizing
    {
        public static int MinimumExactExtent(int pixels, int displayPixels)
        {
            // GPU extents fit inside float's exact integer range. Search only
            // this range; reject unsupported dimensions instead of overflowing.
            if (pixels < 1 || pixels > 1 << 24 || displayPixels < 1)
                throw new System.ArgumentOutOfRangeException();
            while ((int)(displayPixels * (pixels / (float)displayPixels)) < pixels)
            {
                if (pixels == 1 << 24)
                    throw new System.ArgumentOutOfRangeException();
                pixels++;
            }
            return pixels;
        }
    }
}
