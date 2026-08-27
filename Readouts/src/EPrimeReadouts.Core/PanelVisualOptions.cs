using System;

namespace EPrimeReadouts.Core
{
    /// Visual inputs kept separate from persisted settings so they can be
    /// promoted to user-facing options without changing renderer internals.
    public readonly struct PanelVisualOptions : IEquatable<PanelVisualOptions>
    {
        public const float DefaultBandOpacity = 0.35f;

        public readonly float BandOpacity;

        public PanelVisualOptions(float bandOpacity)
        {
            BandOpacity = bandOpacity < 0f
                ? 0f
                : bandOpacity > 1f ? 1f : bandOpacity;
        }

        public static PanelVisualOptions Default
            => new PanelVisualOptions(DefaultBandOpacity);

        public bool Equals(PanelVisualOptions other)
            => BandOpacity.Equals(other.BandOpacity);

        public override bool Equals(object obj)
            => obj is PanelVisualOptions other && Equals(other);

        public override int GetHashCode() => BandOpacity.GetHashCode();
    }
}
