namespace QualityJobs.Core
{
    /// <summary>Identity of a bill's style configuration. Product style is read from the
    /// COMPLETING bill at completion time (Toils_Recipe.cs:194-197), so
    /// cross-bill sharing must only match style-compatible bills (spec §8).</summary>
    public readonly struct StyleKey
    {
        public readonly string? PreceptId;     // Precept_ThingStyle unique id, null if none
        public readonly string? StyleDefName;  // ThingStyleDef defName, null if none
        public readonly bool GlobalStyle;
        public readonly int? GraphicIndex;

        /// <summary>True when this key was built from a live bill via the four-argument
        /// constructor. False only for default/Unknown instances (the sole constructor
        /// always sets it true).</summary>
        public readonly bool Known;

        public StyleKey(string? preceptId, string? styleDefName, bool globalStyle, int? graphicIndex)
        {
            PreceptId = preceptId;
            StyleDefName = styleDefName;
            GlobalStyle = globalStyle;
            GraphicIndex = graphicIndex;
            Known = true;
        }

        /// <summary>Snapshot unavailable (UFT pooled while already unbound).</summary>
        public static StyleKey Unknown => default;
    }

    public static class ShareMatch
    {
        /// <summary>Returns true when <paramref name="bill"/> is style-compatible with
        /// <paramref name="entrySnapshot"/>. The <paramref name="bill"/> argument must
        /// always be a Known key built from a live bill; <see cref="StyleKey.Unknown"/>
        /// is only valid as the entry snapshot.</summary>
        public static bool StyleCompatible(in StyleKey entrySnapshot, in StyleKey bill)
        {
            if (!bill.Known) return false;
            if (!entrySnapshot.Known)
                return bill.GlobalStyle && bill.StyleDefName == null && bill.PreceptId == null
                       && bill.GraphicIndex == null;
            return entrySnapshot.PreceptId == bill.PreceptId
                   && entrySnapshot.StyleDefName == bill.StyleDefName
                   && entrySnapshot.GlobalStyle == bill.GlobalStyle
                   && entrySnapshot.GraphicIndex == bill.GraphicIndex;
        }
    }
}
