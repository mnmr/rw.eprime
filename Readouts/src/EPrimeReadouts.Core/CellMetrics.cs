namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Font-resolved cell geometry for the readout grid. RimWorld silently
    /// substitutes the Small font for Tiny when tiny text is unavailable
    /// (the "disable tiny text" preference, a language without tiny-font
    /// support, or Steam Deck); the wider and taller glyphs then need a
    /// bigger counter box or numbers get clipped. The game layer measures
    /// the resolved font behind its UI metric revision gate and passes the
    /// result here; the layout engine consumes these values everywhere it
    /// would otherwise use the LayoutMetrics constants.
    /// Values clamp up to the LayoutMetrics baselines on read, so
    /// default(CellMetrics) reproduces the classic tiny-font geometry
    /// exactly and cells never shrink below the icon-fitting minimum.
    /// The icon keeps its fixed size and centers inside CellW, so a wider
    /// cell adds symmetric padding on both sides of the icon.
    /// </summary>
    public readonly struct CellMetrics
    {
        private readonly float cellW;
        private readonly float counterRowH;

        public CellMetrics(float cellW, float counterRowH)
        {
            this.cellW = cellW;
            this.counterRowH = counterRowH;
        }

        /// One resource column: counter box width; the icon centers inside it.
        public float CellW =>
            cellW > LayoutMetrics.CellW ? cellW : LayoutMetrics.CellW;

        /// Height of the counter text box under the icon.
        public float CounterRowH =>
            counterRowH > LayoutMetrics.CounterRowH
                ? counterRowH : LayoutMetrics.CounterRowH;

        /// Label rows share the counter font and keep the baseline's extra
        /// breathing room (LabelRowH - CounterRowH) above and below.
        public float LabelRowH =>
            CounterRowH + (LayoutMetrics.LabelRowH - LayoutMetrics.CounterRowH);

        /// Combined height of one icon+counter row pair.
        public float RowPairH =>
            LayoutMetrics.IconRowH + CounterRowH - LayoutMetrics.CounterOverlap;
    }
}
