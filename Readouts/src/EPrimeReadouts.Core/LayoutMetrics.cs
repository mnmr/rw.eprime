namespace EPrimeReadouts.Core
{
    /// All readout panel geometry, in UI-space pixels (RimWorld's UI scale
    /// multiplies the whole UI coordinate space, so these need no scaling).
    public static class LayoutMetrics
    {
        public const float MarkerColW = 26f;  // fits 3 triangles + gaps
        public const float TriW = 7f;
        public const float TriH = 9f;
        public const float TriGap = 1f;
        public const float CellW = 34f;       // one resource column
        public const float IconSize = 27f;    // vanilla resource icon size
        public const float IconRowH = 29f;    // icon 27 + 2px gap to the counter text
        public const float CounterRowH = 16f; // GameFont.Tiny line fits in 16
        public const float GroupGap = 2f;
        public const float LabelRowH = 20f;
        public const float StripeW = 3f;      // colored band on a container's left edge
        public const float GroupPadX = 4f;    // content inset right of the stripe
        public const float GroupPadY = 1f;    // content inset top/bottom of a container
    }
}
