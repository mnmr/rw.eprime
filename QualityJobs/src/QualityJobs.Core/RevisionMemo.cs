using System.Collections.Generic;

namespace QualityJobs.Core
{
    /// <summary>
    /// Memoizes immutable answers for one exact dependency revision. Moving the
    /// revision in either direction drops all entries, preventing values from a
    /// prior owner generation or loaded save from leaking into the current one.
    /// </summary>
    public sealed class RevisionMemo<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> entries =
            new Dictionary<TKey, TValue>();
        private long revision;
        private bool stamped;

        public int Count => entries.Count;

        public bool TryGet(long currentRevision, TKey key, out TValue value)
        {
            Observe(currentRevision);
            return entries.TryGetValue(key, out value);
        }

        public void Store(long currentRevision, TKey key, TValue value)
        {
            Observe(currentRevision);
            entries[key] = value;
        }

        public void Clear()
        {
            entries.Clear();
            revision = 0;
            stamped = false;
        }

        private void Observe(long currentRevision)
        {
            if (stamped && revision == currentRevision) return;
            entries.Clear();
            revision = currentRevision;
            stamped = true;
        }
    }
}
