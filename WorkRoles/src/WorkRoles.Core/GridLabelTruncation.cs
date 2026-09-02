#nullable enable
using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// How much of a full role name a grid chip's column is sized for.
    /// Persisted by name, so the order is free to change.
    public enum GridNamePreference
    {
        Automatic,
        Short,
        Medium,
        Long,
        Full,
    }

    /// <summary>
    /// Sizes full-name chips for the equal-width grid layout. A chip's
    /// column room is derived from a prefix of its label, chosen from the
    /// labels alone: the player's named size, or for Automatic the shortest
    /// count of whole characters that keeps every label distinct, with a
    /// floor. The chip then draws its whole name clipped to that room, so
    /// no measurement is needed here; callers measure the sizing prefix in
    /// their own cache builders.
    /// </summary>
    public static class GridLabelTruncation
    {
        /// Automatic floor: short catalogs would otherwise collapse to
        /// near-initials, which the initials display already covers better.
        public const int DefaultMinimumPrefixLength = 8;
        /// The named sizes step by four letters so the jumps read as equal.
        public const int ShortPrefixLength = 0;
        public const int MediumPrefixLength = 4;
        public const int LongPrefixLength = 8;

        /// <summary>
        /// Per label (same order as the input), the shortest prefix length
        /// (at least one character) at which it stays distinct from every
        /// other label. Any label shares its longest common prefix with a
        /// neighbour in ordinal order, so one sort and one linear scan over
        /// adjacent pairs are enough. A label that is a whole prefix of its
        /// neighbour needs no extra character: it shows in full while the
        /// neighbour is cut. Duplicate labels cannot be told apart and only
        /// require their own length. Requirements are per label so one
        /// near-duplicate pair never lengthens the others.
        /// </summary>
        public static int[] RequiredPrefixLengths(IReadOnlyList<string> labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            int count = labels.Count;
            var texts = new string[count];
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                texts[i] = labels[i] ?? string.Empty;
                order[i] = i;
            }
            Array.Sort(texts, order, StringComparer.Ordinal);

            var required = new int[count];
            for (int i = 0; i < count; i++) required[i] = 1;
            for (int i = 1; i < count; i++)
            {
                string previous = texts[i - 1];
                int shared = CommonPrefixLength(previous, texts[i]);
                int needed = shared == previous.Length ? shared : shared + 1;
                if (needed > required[order[i - 1]]) required[order[i - 1]] = needed;
                if (needed > required[order[i]]) required[order[i]] = needed;
            }
            return required;
        }

        /// <summary>
        /// The prefix length a chip's column room is sized for. Automatic
        /// keeps names distinct: the default floor raised to the label's own
        /// requirement. A named size is exact, collisions included, since
        /// the player chose that width knowingly and the chip tooltip still
        /// names the role. Full is effectively unbounded.
        /// </summary>
        public static int PrefixLengthFor(int requiredLength,
            GridNamePreference preference)
        {
            if (requiredLength < 1)
                throw new ArgumentOutOfRangeException(nameof(requiredLength));
            switch (preference)
            {
                case GridNamePreference.Automatic:
                    return Math.Max(requiredLength, DefaultMinimumPrefixLength);
                case GridNamePreference.Short: return ShortPrefixLength;
                case GridNamePreference.Medium: return MediumPrefixLength;
                case GridNamePreference.Long: return LongPrefixLength;
                case GridNamePreference.Full: return int.MaxValue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preference));
            }
        }

        /// <summary>
        /// Whether the label overruns its column room and fades out. A
        /// label one character over the prefix shows whole: the fade region
        /// is about a character wide anyway.
        /// </summary>
        public static bool IsCut(string label, int prefixLength)
        {
            if (prefixLength < 0)
                throw new ArgumentOutOfRangeException(nameof(prefixLength));
            return label != null && prefixLength != int.MaxValue
                && label.Length > prefixLength + 1;
        }

        /// <summary>
        /// The text the column room is measured from: the whole label when
        /// it is not cut, otherwise its first prefixLength characters with
        /// trailing spaces dropped (possibly empty).
        /// </summary>
        public static string SizingPrefix(string label, int prefixLength)
        {
            if (label == null) return string.Empty;
            if (!IsCut(label, prefixLength)) return label;
            int end = prefixLength;
            while (end > 0 && char.IsWhiteSpace(label[end - 1])) end--;
            return label.Substring(0, end);
        }

        private static int CommonPrefixLength(string left, string right)
        {
            int limit = Math.Min(left.Length, right.Length);
            int i = 0;
            while (i < limit && left[i] == right[i]) i++;
            return i;
        }
    }
}
