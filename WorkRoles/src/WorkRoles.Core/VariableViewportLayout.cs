using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// A clamped half-open range of variable-height items.
    /// </summary>
    public readonly struct VariableViewportRange
    {
        internal VariableViewportRange(int start, int endExclusive)
        {
            Start = start;
            EndExclusive = endExclusive;
        }

        public int Start { get; }
        public int EndExclusive { get; }
        public int Count => EndExclusive - Start;
        public bool IsEmpty => Start == EndExclusive;
    }

    /// <summary>
    /// Copy-owned prefix offsets for variable-height rows. Visible-range queries
    /// use binary searches and therefore do not rescan row heights per event.
    /// </summary>
    public sealed class VariableViewportLayout
    {
        private readonly float[] offsets;

        public VariableViewportLayout(IReadOnlyList<float> itemExtents)
        {
            if (itemExtents == null) throw new ArgumentNullException(nameof(itemExtents));
            offsets = new float[itemExtents.Count + 1];
            for (int i = 0; i < itemExtents.Count; i++)
            {
                float extent = itemExtents[i];
                if (extent <= 0f || float.IsNaN(extent) || float.IsInfinity(extent))
                    throw new ArgumentOutOfRangeException(nameof(itemExtents));
                float next = offsets[i] + extent;
                if (float.IsInfinity(next) || next <= offsets[i])
                    throw new ArgumentOutOfRangeException(nameof(itemExtents));
                offsets[i + 1] = next;
            }
        }

        public int Count => offsets.Length - 1;
        public float ContentExtent => offsets[offsets.Length - 1];

        public float OffsetOf(int index)
        {
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return offsets[index];
        }

        public float ExtentOf(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return offsets[index + 1] - offsets[index];
        }

        public VariableViewportRange Calculate(
            float viewportStart,
            float viewportExtent,
            int overscan = 0)
        {
            if (float.IsNaN(viewportStart) || float.IsInfinity(viewportStart))
                throw new ArgumentOutOfRangeException(nameof(viewportStart));
            if (viewportExtent < 0f || float.IsNaN(viewportExtent)
                || float.IsInfinity(viewportExtent))
                throw new ArgumentOutOfRangeException(nameof(viewportExtent));
            if (overscan < 0) throw new ArgumentOutOfRangeException(nameof(overscan));
            if (Count == 0) return new VariableViewportRange(0, 0);

            int start = ClampIndex(UpperBound(viewportStart) - 1);
            if (viewportExtent == 0f)
                return new VariableViewportRange(start, start);

            double viewportEnd = (double)viewportStart + viewportExtent;
            int endExclusive = ClampIndex(LowerBound(viewportEnd));
            if (endExclusive <= start)
                return new VariableViewportRange(start, start);

            start = start < overscan ? 0 : start - overscan;
            endExclusive = Count - endExclusive < overscan
                ? Count
                : endExclusive + overscan;
            return new VariableViewportRange(start, endExclusive);
        }

        private int ClampIndex(int index)
        {
            if (index <= 0) return 0;
            if (index >= Count) return Count;
            return index;
        }

        /// First prefix offset strictly greater than value.
        private int UpperBound(double value)
        {
            int low = 0;
            int high = offsets.Length;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (offsets[mid] <= value) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        /// First prefix offset greater than or equal to value.
        private int LowerBound(double value)
        {
            int low = 0;
            int high = offsets.Length;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (offsets[mid] < value) low = mid + 1;
                else high = mid;
            }
            return low;
        }
    }
}
