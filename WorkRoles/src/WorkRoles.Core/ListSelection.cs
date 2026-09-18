using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Explorer-style selection over an ordered list: Click selects one item,
    /// Toggle (Ctrl-click) flips one, Range (Shift-click) spans from the
    /// anchor, additive Range (Ctrl+Shift-click) adds that span. Anchor is
    /// where the next range starts; Focus is the keyboard cursor (the last
    /// item acted on). Retain prunes departed items without touching the
    /// rest, so a pawn leaving the table never resets a multi-selection.
    public sealed class ListSelection<T> where T : notnull
    {
        private readonly HashSet<T> items = new HashSet<T>();
        private readonly List<T> scratch = new List<T>();
        private T? single;
        private IReadOnlyList<T>? retainedAgainst;

        public int Count => items.Count;
        public T? Anchor { get; private set; }
        public T? Focus { get; private set; }

        /// The selected item when exactly one is selected, else default.
        public T? Single => items.Count == 1 ? single : default;

        public bool Contains(T item) => items.Contains(item);

        public void Click(T item)
        {
            items.Clear();
            items.Add(item);
            single = item;
            Anchor = Focus = item;
            retainedAgainst = null;
        }

        public void Toggle(T item)
        {
            if (!items.Remove(item)) items.Add(item);
            Anchor = Focus = item;
            RefreshSingle();
            retainedAgainst = null;
        }

        /// Selects every item between the anchor and `item` in `order`. An
        /// anchor outside the order (or none) behaves like a click on `item`.
        public void Range(T item, IReadOnlyList<T> order, bool additive)
        {
            if (!additive) items.Clear();
            int to = IndexOf(order, item);
            int from = Anchor == null ? -1 : IndexOf(order, Anchor);
            if (to < 0)
            {
                items.Add(item);
            }
            else
            {
                if (from < 0) from = to;
                int low = from < to ? from : to;
                int high = from < to ? to : from;
                for (int i = low; i <= high; i++) items.Add(order[i]);
            }
            if (from < 0) Anchor = item;
            Focus = item;
            RefreshSingle();
            retainedAgainst = null;
        }

        public void SelectAll(IReadOnlyList<T> order)
        {
            for (int i = 0; i < order.Count; i++) items.Add(order[i]);
            RefreshSingle();
            retainedAgainst = null;
        }

        public void Clear()
        {
            items.Clear();
            single = default;
            Anchor = Focus = default;
            retainedAgainst = null;
        }

        /// Drops selected items no longer in `order`. Allocation-free and a
        /// reference check when called again with the same list instance.
        public void Retain(IReadOnlyList<T> order)
        {
            if (ReferenceEquals(order, retainedAgainst)) return;
            retainedAgainst = order;
            if (items.Count == 0) return;
            scratch.Clear();
            foreach (T item in items)
                if (IndexOf(order, item) < 0) scratch.Add(item);
            for (int i = 0; i < scratch.Count; i++) items.Remove(scratch[i]);
            scratch.Clear();
            if (Anchor != null && IndexOf(order, Anchor) < 0) Anchor = default;
            if (Focus != null && IndexOf(order, Focus) < 0) Focus = default;
            RefreshSingle();
        }

        /// Appends the selected items to `into` in `order` order.
        public void CopyOrdered(IReadOnlyList<T> order, List<T> into)
        {
            for (int i = 0; i < order.Count; i++)
                if (items.Contains(order[i])) into.Add(order[i]);
        }

        private void RefreshSingle()
        {
            if (items.Count != 1) { single = default; return; }
            foreach (T item in items) { single = item; return; }
        }

        private static int IndexOf(IReadOnlyList<T> order, T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < order.Count; i++)
                if (comparer.Equals(order[i], item)) return i;
            return -1;
        }
    }
}
