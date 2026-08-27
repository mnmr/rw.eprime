using System;

namespace RimShared.Common
{
    /// <summary>
    /// Caches measured text heights by text, font and available width until the
    /// caller's text-metric revision changes. Cache contract: Owner = caller;
    /// Key = text/font/width; Value = measured height; Dependencies = key plus
    /// metric revision; Refresh policy = immediate on dependency change;
    /// Equality policy = matching dependencies reuse the value; Teardown = Reset.
    /// </summary>
    public sealed class TextHeightCache
    {
        private readonly struct Key : IEquatable<Key>
        {
            private readonly string text;
            private readonly int font;
            private readonly float width;

            public Key(string text, int font, float width)
            {
                this.text = text;
                this.font = font;
                this.width = width;
            }

            public bool Equals(Key other) =>
                string.Equals(text, other.text, StringComparison.Ordinal)
                && font == other.font
                && width.Equals(other.width);

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (text != null ? text.GetHashCode() : 0) * 397;
                    hash = (hash * 397) ^ font;
                    return (hash * 397) ^ width.GetHashCode();
                }
            }
        }

        private readonly RevisionedCache<Key, int, float> cache =
            new RevisionedCache<Key, int, float>();

        public float Get<TState>(
            string text,
            int font,
            float width,
            int revision,
            TState state,
            Func<TState, float> measure) =>
            cache.Get(new Key(text, font, width), revision, state, measure);

        public void Reset() => cache.Clear();
    }
}
