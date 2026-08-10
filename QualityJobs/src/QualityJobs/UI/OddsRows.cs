using QualityJobs.Core;

namespace QualityJobs.UI
{
    /// Pre-formatted odds rows for the dialog.
    ///
    /// Cache — Owner: dialog window (transient). Key: (minSkill, inspired,
    /// roleOffset). Value: immutable string array. Dependencies: the condition
    /// values only (the odds table is def- and language-independent digits;
    /// labels are drawn separately from cached translations). Refresh: rebuilt
    /// when Matches() fails on access. Equality: value match preserves the
    /// array. Teardown: dies with the window.
    public sealed class OddsRows
    {
        /// Display rows: Legendary, Masterwork, Excellent, Good, then Normal or
        /// worse (Normal + Poor + Awful collapsed into one row).
        public const int RowCount = 5;

        public readonly int MinSkill;
        public readonly bool Inspired;
        public readonly int RoleOffset;
        /// Percent per display row, formatted once ("12.3%"), index 0..4
        /// top-down: 0 = Legendary .. 3 = Good, 4 = Normal or worse.
        public readonly string[] Percents;

        private OddsRows(int minSkill, bool inspired, int roleOffset, string[] percents)
        {
            MinSkill = minSkill;
            Inspired = inspired;
            RoleOffset = roleOffset;
            Percents = percents;
        }

        public bool Matches(int minSkill, bool inspired, int roleOffset)
            => MinSkill == minSkill && Inspired == inspired && RoleOffset == roleOffset;

        public static OddsRows Build(int minSkill, bool inspired, int roleOffset)
        {
            double[] d = QualityOdds.Distribution(minSkill, inspired, roleOffset);
            var percents = new string[RowCount];
            for (int r = 0; r < 4; r++)
                percents[r] = Format(d[6 - r]);
            percents[4] = Format(d[0] + d[1] + d[2]);
            return new OddsRows(minSkill, inspired, roleOffset, percents);
        }

        private static string Format(double p) => (p * 100.0).ToString("0.0") + "%";
    }
}
