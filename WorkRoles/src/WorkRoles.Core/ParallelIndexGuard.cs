using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Keeps hidden values parallel to a publicly mutable list without ever
    /// returning a value after the public item at that index has changed.
    /// Identity is intentionally reference-based; state and text use their
    /// default exact equality.
    /// </summary>
    public sealed class ParallelIndexGuard<TIdentity, TState, TText, TValue>
        where TIdentity : class
    {
        private readonly struct Entry
        {
            internal readonly TIdentity Identity;
            internal readonly TState State;
            internal readonly TText Text;
            internal readonly TValue Value;

            internal Entry(TIdentity identity, TState state, TText text, TValue value)
            {
                Identity = identity;
                State = state;
                Text = text;
                Value = value;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();

        public void Add(TIdentity identity, TState state, TText text, TValue value)
        {
            entries.Add(new Entry(identity, state, text, value));
        }

        public void Insert(int index, TIdentity identity, TState state, TText text, TValue value)
        {
            entries.Insert(index, new Entry(identity, state, text, value));
        }

        public bool TryGet(int index, TIdentity identity, TState state, TText text,
            out TValue value)
        {
            value = default(TValue);
            if (index < 0 || index >= entries.Count) return false;
            Entry entry = entries[index];
            if (!ReferenceEquals(entry.Identity, identity)
                || !EqualityComparer<TState>.Default.Equals(entry.State, state)
                || !EqualityComparer<TText>.Default.Equals(entry.Text, text))
                return false;
            value = entry.Value;
            return true;
        }
    }
}
