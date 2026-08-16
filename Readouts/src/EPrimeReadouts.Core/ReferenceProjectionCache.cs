using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Projects an externally owned immutable snapshot only when its published
    /// reference changes, while preserving the prior projection when the newly
    /// projected contents are equal.
    ///
    /// Cache contract:
    /// Owner: caller-provided lifecycle.
    /// Key: source snapshot reference identity.
    /// Value: caller-owned immutable projection.
    /// Dependencies: the complete source snapshot represented by its identity.
    /// Refresh policy: immediate on a new source reference; cache hits only
    /// compare references and return the existing projection.
    /// Equality policy: an equal rebuilt projection preserves value identity.
    /// Teardown: Clear releases both the source and projected references.
    public sealed class ReferenceProjectionCache<TSource, TValue>
        where TSource : class
        where TValue : class
    {
        private readonly Func<TSource, TValue> build;
        private readonly IEqualityComparer<TValue> comparer;
        private TSource source;
        private TValue value;
        private bool populated;

        public ReferenceProjectionCache(
            Func<TSource, TValue> build,
            IEqualityComparer<TValue> comparer = null)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.comparer = comparer ?? EqualityComparer<TValue>.Default;
        }

        public TValue Get(TSource current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (populated && ReferenceEquals(source, current)) return value;

            TValue candidate = build(current);
            source = current;
            if (populated && comparer.Equals(value, candidate)) return value;

            value = candidate;
            populated = true;
            return value;
        }

        public void Clear()
        {
            source = null;
            value = null;
            populated = false;
        }
    }
}
