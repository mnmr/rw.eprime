using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Caches one value per key until a game-tick interval elapses. Callers
    /// explicitly remove entries when a non-tick dependency changes.
    public sealed class TickValueCache<TKey, TValue>
    {
        private readonly struct Entry
        {
            internal Entry(int tick, TValue value)
            {
                Tick = tick;
                Value = value;
            }

            internal readonly int Tick;
            internal readonly TValue Value;
        }

        private readonly int refreshIntervalTicks;
        private readonly Dictionary<TKey, Entry> entries;

        public TickValueCache(
            int refreshIntervalTicks,
            IEqualityComparer<TKey>? comparer = null)
        {
            if (refreshIntervalTicks <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(refreshIntervalTicks));
            this.refreshIntervalTicks = refreshIntervalTicks;
            entries = new Dictionary<TKey, Entry>(comparer);
        }

        public TValue Get<TState>(
            TKey key,
            int tick,
            TState state,
            Func<TState, TValue> build)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (build == null) throw new ArgumentNullException(nameof(build));

            if (entries.TryGetValue(key, out Entry entry))
            {
                long elapsed = (long)tick - entry.Tick;
                if (elapsed >= 0 && elapsed < refreshIntervalTicks)
                    return entry.Value;
            }

            TValue value = build(state);
            entries[key] = new Entry(tick, value);
            return value;
        }

        public bool Remove(TKey key) => key != null && entries.Remove(key);

        public void Clear() => entries.Clear();

        public int Count => entries.Count;
    }
}
