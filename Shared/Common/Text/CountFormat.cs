using System;
using System.Globalization;

namespace RimShared.Common
{
    /// <summary>
    /// Compact display formatting for resource counters. At or below 10000 the
    /// exact invariant integer is shown. Above that, metric prefixes keep the
    /// text narrow: one decimal while the scaled value is below 100
    /// ("12786" -> "12.8k", trimming a trailing ".0"), integer digits from 100
    /// upward ("114000" -> "114k") so three-digit values never overflow the
    /// counter cell. Millions follow the same rule with "M".
    /// </summary>
    public static class CountFormat
    {
        public static string Compact(int value)
        {
            // 999500+ would integer-round to "1000k" — promote to "1M" instead.
            if (value >= 999_500) return Scaled(value, 1_000_000m, "M");
            if (value > 10_000) return Scaled(value, 1_000m, "k");
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Scaled(int value, decimal unit, string suffix)
        {
            decimal scaled = (decimal)value / unit;
            if (scaled >= 100m)
                return Math.Round(scaled, 0, MidpointRounding.AwayFromZero)
                    .ToString("0", CultureInfo.InvariantCulture) + suffix;
            string s = Math.Round(scaled, 1, MidpointRounding.AwayFromZero)
                .ToString("0.0", CultureInfo.InvariantCulture);
            if (s.EndsWith(".0")) s = s.Substring(0, s.Length - 2);
            return s + suffix;
        }
    }
}
