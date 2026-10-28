using System;
using HarmonyLib;
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

        public override bool Equals(object? obj) =>
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
        // Process-lifetime accessor to Verse's scalar GUI font state; owns no
        // game objects. Resolved once, never reflected over in a draw pass.
        private static readonly AccessTools.FieldRef<GameFont> FontState =
            AccessTools.StaticFieldRefAccess<GameFont>(
                AccessTools.Field(typeof(Text), "fontInt"));

        private static GameFont EffectiveFont => Text.TinyFontSupported
            ? GameFont.Tiny : GameFont.Small;

        public static GameFont ResolveFont(bool forceTiny) =>
            forceTiny ? GameFont.Tiny : EffectiveFont;

        /// Measures the actual selected style inside a caller's metric gate.
        /// Verse's startup line-height table can contain the Small fallback
        /// for Tiny if the preference was disabled during initialization.
        public static TinyTextMetrics MeasureMetrics(bool forceTiny)
            => MeasureMetrics(ResolveFont(forceTiny));

        /// Measures an explicit font choice, independent of the Tiny preference.
        /// Call only inside the consumer's UI metric/font invalidation gate.
        public static TinyTextMetrics MeasureMetrics(GameFont font)
        {
            using (UseFont(font))
                return new TinyTextMetrics(Text.Font,
                    Mathf.Ceil(Text.CalcHeight("W", 999f)), Prefs.UIScale);
        }

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
        /// Downward caption ink shift when Tiny resolves to Small, overlapping
        /// unused Small-font leading with the following control. Zero for
        /// native Tiny.
        /// </summary>
        public static float FallbackCaptionOffsetY =>
            EffectiveFont == GameFont.Small ? 2f : 0f;

        /// <summary>
        /// Selects the effective Tiny font until the returned scope is
        /// disposed. Use this to route measurements through a mod's existing
        /// current-font measurement cache.
        /// </summary>
        public static FontScope UseFont() => new FontScope(EffectiveFont);

        /// Local opt-in for numeric displays that explicitly require Tiny.
        /// Bypasses the font setter's fallback without touching preferences.
        public static FontScope UseFont(bool forceTiny) =>
            new FontScope(ResolveFont(forceTiny), forceTiny);

        /// An explicit font-size selector owns its Tiny/Small choice. Tiny
        /// bypasses the game fallback; Small remains Small even if Tiny is allowed.
        public static FontScope UseFont(GameFont font) =>
            new FontScope(font, font == GameFont.Tiny);

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

            internal FontScope(GameFont font, bool forceTiny = false)
            {
                previous = Text.Font;
                if (forceTiny) FontState() = font;
                else Text.Font = font;
            }

            // Restore the exact value, including an outer forced-Tiny scope.
            public void Dispose() => FontState() = previous;
        }
    }
}
