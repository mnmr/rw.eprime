namespace QualityJobs.Core
{
    /// <summary>Expected-quality ranking (auto-best spec §2.1, amended
    /// 2026-08-05). RankMilli is the exact expected quality of the pawn's roll
    /// in fixed-point milli-units, read from a hard-coded table — pure integer
    /// lookups so every MP client ranks identically. The XpMilli tie-break
    /// lives in Outranks, not in the rank value.</summary>
    public static class FinisherRank
    {
        // Exact expected quality in milli-units for skill 0..20 × shift 0..3,
        // shift = 2×inspired + roleOffset (clamped). Generated from
        // QualityOdds.Distribution; FinisherRankTests.TableMatchesAnalyticExpectedValue
        // keeps it in lockstep (its failure output prints the expected literals).
        private static readonly int[] EvMilli =
        {
            408, 1408, 2408, 3408,
            705, 1705, 2705, 3705,
            1095, 2095, 3095, 4094,
            1380, 2380, 3380, 4377,
            1564, 2564, 3564, 4558,
            1779, 2779, 3778, 4766,
            1987, 2987, 3987, 4964,
            2186, 3186, 4185, 5145,
            2375, 3375, 4373, 5308,
            2511, 3511, 4509, 5416,
            2662, 3662, 4657, 5531,
            2816, 3816, 4809, 5640,
            2965, 3965, 4953, 5735,
            3060, 4060, 5044, 5788,
            3151, 4151, 5130, 5834,
            3239, 4239, 5210, 5873,
            3323, 4323, 5286, 5904,
            3404, 4404, 5357, 5930,
            3483, 4483, 5423, 5949,
            3578, 4578, 5502, 5964,
            3672, 4672, 5577, 5975,
        };

        public static int EvMilliAt(int skill, int shift)
        {
            if (skill < 0) skill = 0; else if (skill > 20) skill = 20;
            if (shift < 0) shift = 0; else if (shift > 3) shift = 3;
            return EvMilli[skill * 4 + shift];
        }

        public static int RankMilliOf(in CandidateFacts f)
            => EvMilliAt(f.Skill, (f.Inspired ? 2 : 0) + f.RoleOffset);

        /// <summary>True when a strictly outranks b on (RankMilli, XpMilli).</summary>
        public static bool Outranks(in CandidateFacts a, in CandidateFacts b)
        {
            int ra = RankMilliOf(a);
            int rb = RankMilliOf(b);
            if (ra != rb) return ra > rb;
            return a.XpMilli > b.XpMilli;
        }
    }
}
