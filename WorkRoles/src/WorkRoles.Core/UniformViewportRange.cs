using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// A clamped half-open range of uniform items intersecting a viewport.
    /// </summary>
    public readonly struct UniformViewportRange
    {
        private UniformViewportRange(int start, int endExclusive)
        {
            Start = start;
            EndExclusive = endExclusive;
        }

        public int Start { get; }
        public int EndExclusive { get; }
        public int Count => EndExclusive - Start;
        public bool IsEmpty => Start == EndExclusive;

        /// <summary>
        /// Finds items whose half-open extents intersect the half-open viewport,
        /// then expands that range by <paramref name="overscan"/> items per side.
        /// </summary>
        public static UniformViewportRange Calculate(
            int itemCount,
            float itemExtent,
            float contentStart,
            float viewportStart,
            float viewportExtent,
            int overscan = 0)
        {
            if (itemCount < 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
            if (itemExtent <= 0f || float.IsNaN(itemExtent) || float.IsInfinity(itemExtent))
                throw new ArgumentOutOfRangeException(nameof(itemExtent));
            if (viewportExtent < 0f || float.IsNaN(viewportExtent) || float.IsInfinity(viewportExtent))
                throw new ArgumentOutOfRangeException(nameof(viewportExtent));
            if (float.IsNaN(contentStart) || float.IsInfinity(contentStart))
                throw new ArgumentOutOfRangeException(nameof(contentStart));
            if (float.IsNaN(viewportStart) || float.IsInfinity(viewportStart))
                throw new ArgumentOutOfRangeException(nameof(viewportStart));
            if (overscan < 0) throw new ArgumentOutOfRangeException(nameof(overscan));

            if (itemCount == 0) return new UniformViewportRange(0, 0);

            double normalizedStart = ((double)viewportStart - contentStart) / itemExtent;
            int start = FloorAndClamp(normalizedStart, itemCount);
            if (viewportExtent == 0f) return new UniformViewportRange(start, start);

            double viewportEnd = (double)viewportStart + viewportExtent;
            double normalizedEnd = (viewportEnd - contentStart) / itemExtent;
            int endExclusive = CeilingAndClamp(normalizedEnd, itemCount);

            if (endExclusive <= start)
                return new UniformViewportRange(start, start);

            start = start < overscan ? 0 : start - overscan;
            endExclusive = itemCount - endExclusive < overscan
                ? itemCount
                : endExclusive + overscan;

            return new UniformViewportRange(start, endExclusive);
        }

        private static int FloorAndClamp(double value, int itemCount)
        {
            if (value <= 0d) return 0;
            if (value >= itemCount) return itemCount;
            return (int)Math.Floor(value);
        }

        private static int CeilingAndClamp(double value, int itemCount)
        {
            if (value <= 0d) return 0;
            if (value >= itemCount) return itemCount;
            return (int)Math.Ceiling(value);
        }
    }
}
