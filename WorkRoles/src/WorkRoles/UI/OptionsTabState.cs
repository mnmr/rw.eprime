using Verse;

namespace WorkRoles.UI
{
    /// Owns the Options tab's translated tips. The recommendation order,
    /// tuning parameters, and training path projections moved with their
    /// sections to RecommendationsTabState.
    internal sealed class OptionsTabState
    {
        // Owner: Options tab. Key: UiVersion (language rides UiVersion via
        // explicit invalidation). Value: immutable StructuredTip models.
        // Dependencies: language. Refresh: lazy on first read after a revision
        // change. Equality: matching stamp preserves tip identity. Teardown:
        // Reset/InvalidateLanguageCaches releases the tips.
        private int tipsStamp = -1;

        internal StructuredTip NumericTip { get; private set; }
        internal StructuredTip RangeTip { get; private set; }

        internal void Reset() => InvalidateLanguageCaches();

        internal void InvalidateLanguageCaches()
        {
            tipsStamp = -1;
            NumericTip = null;
            RangeTip = null;
        }

        internal void EnsureTips()
        {
            if (tipsStamp == UiVersion.Current) return;
            tipsStamp = UiVersion.Current;

            var numeric = new TipModel { Title = "WR_OptNumeric".Translate() };
            numeric.AddSection().Text("WR_OptNumericTipWhat".Translate());
            numeric.AddSection()
                .Fact("WR_TipOff".Translate(), "WR_OptNumericTipOff".Translate())
                .Fact("WR_TipOn".Translate(), "WR_OptNumericTipOn".Translate());
            numeric.AddSection().Text("WR_OptNumericTipWhy".Translate(), dim: true);
            NumericTip = new StructuredTip("options:numeric", numeric);

            var range = new TipModel { Title = "WR_OptVanillaRange".Translate() };
            range.AddSection().Text("WR_OptVanillaRangeTipWhat".Translate());
            range.AddSection()
                .Fact("WR_TipOff".Translate(), "WR_OptVanillaRangeTipOff".Translate())
                .Fact("WR_TipOn".Translate(), "WR_OptVanillaRangeTipOn".Translate());
            RangeTip = new StructuredTip("options:vanilla-range", range);
        }
    }
}
