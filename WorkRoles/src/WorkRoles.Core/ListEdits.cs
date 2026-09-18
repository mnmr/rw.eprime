using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Batch edits over a selected set of list indices (role entries, member
    /// roles). Indices may arrive unsorted, duplicated or out of range; the
    /// synced command validates them here rather than trusting the UI.
    public static class ListEdits
    {
        /// Moves the selected items one step toward the front (delta -1) or the
        /// back (+1), keeping their relative order. Items against the edge, or
        /// packed behind selected items that are, stay put. True when any moved.
        public static bool MoveSelected<T>(IList<T> list, IReadOnlyList<int> indices, int delta)
        {
            if (delta != -1 && delta != 1) return false;
            List<int> sorted = Distinct(list.Count, indices);
            if (sorted.Count == 0) return false;
            bool moved = false;
            if (delta < 0)
            {
                int floor = 0;
                for (int i = 0; i < sorted.Count; i++)
                {
                    int at = sorted[i];
                    if (at - 1 >= floor)
                    {
                        Swap(list, at, at - 1);
                        moved = true;
                        floor = at;
                    }
                    else floor = at + 1;
                }
            }
            else
            {
                int ceiling = list.Count - 1;
                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    int at = sorted[i];
                    if (at + 1 <= ceiling)
                    {
                        Swap(list, at, at + 1);
                        moved = true;
                        ceiling = at;
                    }
                    else ceiling = at - 1;
                }
            }
            return moved;
        }

        /// Removes the items at the given indices. True when any was removed.
        public static bool RemoveAt<T>(IList<T> list, IReadOnlyList<int> indices)
        {
            List<int> sorted = Distinct(list.Count, indices);
            for (int i = sorted.Count - 1; i >= 0; i--) list.RemoveAt(sorted[i]);
            return sorted.Count > 0;
        }

        private static List<int> Distinct(int count, IReadOnlyList<int> indices)
        {
            var result = new List<int>(indices.Count);
            for (int i = 0; i < indices.Count; i++)
            {
                int at = indices[i];
                if (at >= 0 && at < count && !result.Contains(at)) result.Add(at);
            }
            result.Sort();
            return result;
        }

        private static void Swap<T>(IList<T> list, int a, int b)
        {
            T item = list[a];
            list[a] = list[b];
            list[b] = item;
        }
    }
}
