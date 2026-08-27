using System;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// <summary>
    /// The Tiny-font and UI-geometry identity for text requested as Tiny.
    /// RimWorld silently substitutes Small when Tiny is disabled or
    /// unsupported, so callers must use this effective font and its line
    /// height for layout. Language and font-asset revisions remain separate
    /// dependencies for caches whose contents or measurements observe them.
    /// </summary>
    public readonly struct TinyTextMetrics : IEquatable<TinyTextMetrics>
    {
        internal TinyTextMetrics(GameFont font, float lineHeight, float uiScale)
        {
            Font = font;
            LineHeight = lineHeight;
            UiScale = uiScale;
        }

        public GameFont Font { get; }
        public float LineHeight { get; }
        public float UiScale { get; }

        public bool Equals(TinyTextMetrics other) =>
            Font == other.Font && LineHeight == other.LineHeight
            && UiScale == other.UiScale;

        public override bool Equals(object obj) =>
            obj is TinyTextMetrics other && Equals(other);

        public override int GetHashCode() =>
            ((int)Font * 397) ^ LineHeight.GetHashCode()
                ^ UiScale.GetHashCode();

        public static bool operator ==(
            TinyTextMetrics left, TinyTextMetrics right) => left.Equals(right);

        public static bool operator !=(
            TinyTextMetrics left, TinyTextMetrics right) => !left.Equals(right);
    }

    /// <summary>
    /// Tiny-font drawing and measurement with RimWorld's Small-font fallback.
    /// Layout caches should key Tiny geometry on <see cref="Metrics"/> and
    /// reserve at least <see cref="TinyTextMetrics.LineHeight"/> vertically.
    /// </summary>
    public static class TinyText
    {
        private static GameFont EffectiveFont => Text.TinyFontSupported
            ? GameFont.Tiny : GameFont.Small;

        public static TinyTextMetrics Metrics
        {
            get
            {
                GameFont font = EffectiveFont;
                return new TinyTextMetrics(font,
                    Mathf.Ceil(Text.LineHeightOf(font)), Prefs.UIScale);
            }
        }

        public static float LineHeight => Metrics.LineHeight;

        /// <summary>
        /// Selects the effective Tiny font until the returned scope is
        /// disposed. Use this to route measurements through a mod's existing
        /// current-font measurement cache.
        /// </summary>
        public static FontScope UseFont() => new FontScope(EffectiveFont);

        /// <summary>
        /// Cached-layout measurement only; never call from a steady draw path.
        /// Rounds up to a whole logical UI unit so the measured rect does not
        /// clip glyphs at fractional UI scales.
        /// </summary>
        public static float CalcHeight(string text, float width)
        {
            using (UseFont())
                return Mathf.Ceil(Text.CalcHeight(text, width));
        }

        public static void Label(Rect rect, string text)
        {
            using (UseFont())
                Widgets.Label(rect, text);
        }

        /// <summary>
        /// A caption above a control. The two extra visual pixels protect
        /// descenders without changing the caller's logical caption advance;
        /// the downward ink offset keeps the caption close to its control at
        /// every UI scale and with either effective font.
        /// </summary>
        public static void Caption(Rect rect, string text)
        {
            rect.y += 2f;
            rect.height += 2f;
            Label(rect, text);
        }

        /// <summary>
        /// A dense, top-aligned caption whose line box intentionally overlaps
        /// the control below it. Small fallback glyphs carry more top leading,
        /// so lift only that effective font while preserving the caller's row
        /// pitch. Native Tiny placement remains unchanged.
        /// </summary>
        public static void CompactCaption(Rect rect, string text)
        {
            if (EffectiveFont == GameFont.Small)
                rect.y -= 2f;
            Label(rect, text);
        }

        public readonly struct FontScope : IDisposable
        {
            private readonly GameFont previous;

            internal FontScope(GameFont font)
            {
                previous = Text.Font;
                Text.Font = font;
            }

            public void Dispose() => Text.Font = previous;
        }
    }
}
