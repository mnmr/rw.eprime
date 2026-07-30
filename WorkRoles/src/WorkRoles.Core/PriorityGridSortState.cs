using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core
{
    /// <summary>
    /// Toggleable, stable row order for one ascending priority-grid column.
    /// Positive priorities sort first; inactive zeroes remain at the end.
    /// </summary>
    public sealed class PriorityGridSortState
    {
        private readonly int[] rowOrder;
        private readonly ReadOnlyCollection<int> rowOrderView;

        public PriorityGridSortState(int rowCount)
        {
            if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
            rowOrder = new int[rowCount];
            rowOrderView = Array.AsReadOnly(rowOrder);
            Reset();
        }

        public int? SortedColumnIndex { get; private set; }
        public IReadOnlyList<int> RowOrder => rowOrderView;

        public void Toggle(int columnIndex, IReadOnlyList<int> priorities)
        {
            if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
            if (SortedColumnIndex == columnIndex)
            {
                Reset();
                return;
            }

            Apply(columnIndex, priorities);
        }

        public void Refresh(IReadOnlyList<int> priorities)
        {
            if (!SortedColumnIndex.HasValue) return;
            Apply(SortedColumnIndex.Value, priorities);
        }

        public void Reset()
        {
            for (int i = 0; i < rowOrder.Length; i++)
                rowOrder[i] = i;
            SortedColumnIndex = null;
        }

        private void Apply(int columnIndex, IReadOnlyList<int> priorities)
        {
            if (priorities == null) throw new ArgumentNullException(nameof(priorities));
            if (priorities.Count != rowOrder.Length)
                throw new ArgumentException("Priority count must match row count.",
                    nameof(priorities));
            for (int i = 0; i < priorities.Count; i++)
                if (priorities[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(priorities));

            for (int i = 0; i < rowOrder.Length; i++)
                rowOrder[i] = i;
            Array.Sort(rowOrder, (left, right) =>
            {
                int leftPriority = priorities[left];
                int rightPriority = priorities[right];
                int leftKey = leftPriority == 0 ? int.MaxValue : leftPriority;
                int rightKey = rightPriority == 0 ? int.MaxValue : rightPriority;
                int byPriority = leftKey.CompareTo(rightKey);
                return byPriority != 0 ? byPriority : left.CompareTo(right);
            });
            SortedColumnIndex = columnIndex;
        }
    }
}
