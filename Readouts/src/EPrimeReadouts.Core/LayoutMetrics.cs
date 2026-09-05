namespace EPrimeReadouts.Core
{
    /// All readout panel geometry, in UI-space pixels (RimWorld's UI scale
    /// multiplies the whole UI coordinate space, so these need no scaling).
    public static class LayoutMetrics
    {
        public const float MarkerColW = 11f;  // 2px pad + 7px triangle + 2px pad
        public const float TriW = 7f;
        public const float TriH = 9f;
        public const float TriGap = 1f;        // vertical gap
        public const float MarkerStackH =
            TierOps.MaxTiers * TriH + (TierOps.MaxTiers - 1) * TriGap;
        public const float CellW = 34f;       // one resource column
        public const float IconSize = 27f;    // vanilla resource icon size
        public const float IconRowH = 27f;    // flush with the icon
        public const float CounterRowH = 16f; // GameFont.Tiny line fits in 16
        // The counter box rises this far into the icon row: the Tiny font's
        // internal top padding (~2px before visible glyphs) provides the
        // visual gap, so reserving canvas for it would double the spacing.
        public const float CounterOverlap = 2f;
        // Combined height of one icon+counter row pair.
        public const float RowPairH = IconRowH + CounterRowH - CounterOverlap;
        // Vertical tier layout: empty space between one tier's counter row
        // and the next tier's icon row. A counter sits tight under its own
        // icon (it even overlaps it), so this larger gap is what makes each
        // counter read as belonging to the icon above rather than below.
        public const float TierRowGap = 3f;
        public const float GroupGap = 2f;
        public const float LabelRowH = 20f;
        public const float StripeW = 3f;      // colored band on a container's left edge
        public const float GroupPadX = 4f;    // content inset right of the stripe
        public const float GroupPadY = 0f;    // content inset top/bottom of a container
    }
}
