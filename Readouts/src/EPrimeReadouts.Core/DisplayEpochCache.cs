using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Physical display inputs that define one icon-measurement epoch.
    public readonly struct DisplayEpoch : IEquatable<DisplayEpoch>
    {
        public DisplayEpoch(int width, int height, float uiScale)
        {
            Width = width;
            Height = height;
            UiScale = uiScale;
        }

        public readonly int Width;
        public readonly int Height;
        public readonly float UiScale;

        public bool Equals(DisplayEpoch other) =>
            Width == other.Width
            && Height == other.Height
            && UiScale.Equals(other.UiScale);

        public override bool Equals(object obj) =>
            obj is DisplayEpoch other && Equals(other);

        public override int GetHashCode() =>
            (Width * 397) ^ Height ^ UiScale.GetHashCode();
    }

    /// Caches values for one display epoch and schedules each stale key once
    /// when resolution or UI scale changes. A stale value remains readable
    /// until its replacement is published, avoiding visual popping while a
    /// bounded measurement queue catches up.
    public sealed class DisplayEpochCache<TKey, TValue>
    {
        private readonly struct Entry
        {
            internal Entry(int epoch, TValue value)
            {
                Epoch = epoch;
                Value = value;
            }

            internal readonly int Epoch;
            internal readonly TValue Value;
        }

        private readonly Dictionary<TKey, Entry> entries =
            new Dictionary<TKey, Entry>();
        private readonly Queue<TKey> pending = new Queue<TKey>();
        private readonly HashSet<TKey> pendingSet = new HashSet<TKey>();
        private DisplayEpoch display;
        private bool hasDisplay;
        private int epoch;

        public int PendingCount => pending.Count;

        public void Observe(DisplayEpoch current)
        {
            if (hasDisplay && display.Equals(current)) return;
            display = current;
            hasDisplay = true;
            unchecked { epoch++; }

            foreach (TKey key in entries.Keys)
                if (pendingSet.Add(key)) pending.Enqueue(key);
        }

        /// Returns true only when this call added new work.
        public bool Request(TKey key)
        {
            if (entries.TryGetValue(key, out Entry entry)
                && entry.Epoch == epoch) return false;
            if (!pendingSet.Add(key)) return false;
            pending.Enqueue(key);
            return true;
        }

        public bool TryTake(out TKey key)
        {
            if (pending.Count == 0)
            {
                key = default!;
                return false;
            }
            key = pending.Dequeue();
            pendingSet.Remove(key);
            return true;
        }

        public void Publish(TKey key, TValue value)
        {
            entries[key] = new Entry(epoch, value);
        }

        /// Returns the last published value, including the previous epoch's
        /// value while its replacement is queued.
        public bool TryGet(TKey key, out TValue value)
        {
            if (entries.TryGetValue(key, out Entry entry))
            {
                value = entry.Value;
                return true;
            }
            value = default!;
            return false;
        }

        public bool IsCurrent(TKey key) =>
            entries.TryGetValue(key, out Entry entry)
            && entry.Epoch == epoch;
    }
}
