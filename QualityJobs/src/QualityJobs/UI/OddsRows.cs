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
        public readonly int MinSkill;
        public readonly bool Inspired;
        public readonly int RoleOffset;
        /// Percent per QualityCategory, formatted once ("12.3%"), index 0..6.
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
            var percents = new string[7];
            for (int i = 0; i < 7; i++)
                percents[i] = (d[i] * 100.0).ToString("0.0") + "%";
            return new OddsRows(minSkill, inspired, roleOffset, percents);
        }
    }
}
