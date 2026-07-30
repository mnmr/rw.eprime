using System;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Caches measured text heights by text and available width until the
    /// caller's text-metric revision changes.
    /// </summary>
    public sealed class TextHeightCache
    {
        private readonly struct Key : IEquatable<Key>
        {
            private readonly string text;
            private readonly float width;

            public Key(string text, float width)
            {
                this.text = text;
                this.width = width;
            }

            public bool Equals(Key other) =>
                string.Equals(text, other.text, StringComparison.Ordinal)
                && width.Equals(other.width);

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((text != null ? text.GetHashCode() : 0) * 397)
                        ^ width.GetHashCode();
                }
            }
        }

        private readonly RevisionedCache<Key, int, float> cache =
            new RevisionedCache<Key, int, float>();

        public float Get<TState>(
            string text,
            float width,
            int revision,
            TState state,
            Func<TState, float> measure) =>
            cache.Get(new Key(text, width), revision, state, measure);
    }
}
