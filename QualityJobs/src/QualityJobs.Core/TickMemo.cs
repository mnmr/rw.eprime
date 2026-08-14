using System.Collections.Generic;

namespace QualityJobs.Core
{
    /// <summary>
    /// Answers that are expensive to compute and constant for the duration of a
    /// single game tick. A caller that asks the same question about many
    /// subjects in one pass — "how good is the best eligible crafter for this
    /// recipe" across every bill on a map — pays for each distinct question
    /// once instead of once per subject.
    ///
    /// Cache contract — Owner: the declaring caller. Key: TKey, which must
    /// name every input the answer depends on. Value: TValue, treated as
    /// immutable. Dependencies: the game tick only; entries are dropped
    /// wholesale whenever it moves in either direction, so a save load that
    /// rewinds the clock cannot serve a stale answer. Refresh: lazy, on the
    /// first miss of a new tick. Equality: n/a (one value per key).
    /// Teardown: <see cref="Clear"/>; callers holding keys that reference world
    /// objects must call it when the world changes.
    ///
    /// Usage is TryGet-then-Store: <see cref="Store"/> writes into whichever
    /// tick the preceding <see cref="TryGet"/> established, so it must always
    /// follow a TryGet for the same key.
    /// </summary>
    public sealed class TickMemo<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> entries =
            new Dictionary<TKey, TValue>();
        private int tick;
        private bool stamped;

        public int Count => entries.Count;

        /// <summary>
        /// Drops every entry if the tick has moved, then reports whether this
        /// key is already answered for the current tick.
        /// </summary>
        public bool TryGet(int currentTick, TKey key, out TValue value)
        {
            if (!stamped || tick != currentTick)
            {
                entries.Clear();
                tick = currentTick;
                stamped = true;
                // Unconstrained TValue: a miss hands back the type's default,
                // which callers must not read — the bool result is the contract.
                value = default!;
                return false;
            }
            return entries.TryGetValue(key, out value);
        }

        /// <summary>Records an answer for the tick the last TryGet established.</summary>
        public void Store(TKey key, TValue value) => entries[key] = value;

        public void Clear()
        {
            entries.Clear();
            stamped = false;
        }
    }
}
