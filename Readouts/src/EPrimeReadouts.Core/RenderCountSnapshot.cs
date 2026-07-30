using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EPrimeReadouts.Core
{
    public sealed class RenderCountSnapshot : IEquatable<RenderCountSnapshot>
    {
        private readonly IReadOnlyDictionary<string, int> counts;

        public RenderCountSnapshot(
            IReadOnlyDictionary<string, int> counts,
            long fingerprint)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var copy = new Dictionary<string, int>(counts.Count);
            foreach (var pair in counts) copy[pair.Key] = pair.Value;
            this.counts = new ReadOnlyDictionary<string, int>(copy);
            Fingerprint = fingerprint;
        }

        public IReadOnlyDictionary<string, int> Counts => counts;
        public long Fingerprint { get; }

        public bool Equals(RenderCountSnapshot other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || counts.Count != other.counts.Count)
                return false;
            foreach (var pair in counts)
                if (!other.counts.TryGetValue(pair.Key, out int value)
                    || value != pair.Value)
                    return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as RenderCountSnapshot);
        public override int GetHashCode() => counts.Count;
    }
}
