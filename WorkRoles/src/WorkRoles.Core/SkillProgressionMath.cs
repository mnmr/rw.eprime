using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Pure geometry of a skill progression: each role holds a [min, max)
    /// band in whole levels on the 0..21 axis (21 = open top). Bands may
    /// overlap; a band always spans at least MinSpan levels.
    public static class SkillProgressionMath
    {
        public const int MaxLevel = 21;
        public const int MinSpan = 4;
        public const int MaxInitialRoles = MaxLevel / MinSpan; // 5

        /// Adjacent even split — the pure-replacement starting layout.
        public static (List<int> mins, List<int> maxes) DefaultBands(int count)
        {
            var mins = new List<int>();
            var maxes = new List<int>();
            int prev = 0;
            for (int i = 1; i <= count; i++)
            {
                int end = (int)System.Math.Round(MaxLevel * (double)i / count,
                    System.MidpointRounding.AwayFromZero);
                mins.Add(prev);
                maxes.Add(end);
                prev = end;
            }
            return (mins, maxes);
        }

        /// One edge of a band dragged toward desired: stays on the axis and
        /// MinSpan from its own other edge. Other bands never constrain.
        public static int ClampEdge(int min, int max, bool movingMin, int desired)
        {
            int lo = movingMin ? 0 : min + MinSpan;
            int hi = movingMin ? max - MinSpan : MaxLevel;
            return desired < lo ? lo : desired > hi ? hi : desired;
        }

        /// A shared boundary between two touching bands moves both: it stays
        /// MinSpan inside each neighbour.
        public static int ClampSharedEdge(int leftMin, int rightMax, int desired)
        {
            int lo = leftMin + MinSpan;
            int hi = rightMax - MinSpan;
            return desired < lo ? lo : desired > hi ? hi : desired;
        }

        /// Whole-band translate: returns the clamped new min, width kept.
        public static int ClampSlide(int min, int max, int desiredMin)
        {
            int width = max - min;
            int hi = MaxLevel - width;
            return desiredMin < 0 ? 0 : desiredMin > hi ? hi : desiredMin;
        }

        /// Greedy first-fit display rows for (min, max)-sorted bands: a band
        /// joins the first row whose last band ends at or before its start.
        public static List<int> PackRows(IReadOnlyList<(int min, int max)> bands)
        {
            var rows = new List<int>(bands.Count);
            var rowEnds = new List<int>();
            foreach (var (min, max) in bands)
            {
                int row = rowEnds.FindIndex(end => end <= min);
                if (row < 0)
                {
                    row = rowEnds.Count;
                    rowEnds.Add(max);
                }
                else
                {
                    rowEnds[row] = max;
                }
                rows.Add(row);
            }
            return rows;
        }

        /// Equal counts and every band inside 0..21 with span >= MinSpan.
        public static bool Validate(int roleCount, IReadOnlyList<int> mins, IReadOnlyList<int> maxes)
        {
            if (roleCount < 1) return false;
            if (mins == null || maxes == null
                || mins.Count != roleCount || maxes.Count != roleCount) return false;
            for (int i = 0; i < roleCount; i++)
            {
                if (mins[i] < 0 || maxes[i] > MaxLevel) return false;
                if (maxes[i] - mins[i] < MinSpan) return false;
            }
            return true;
        }
    }
}
