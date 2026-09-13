namespace QualityJobs.Core
{
    /// <summary>Expected-quality ranking (auto-best spec §2.1, amended
    /// 2026-08-05). RankMilli is the expected quality of the pawn's roll
    /// in fixed-point milli-units, interpolated from a hard-coded table using
    /// integer arithmetic so every MP client ranks identically. The XpMilli tie-break
    /// lives in Outranks, not in the rank value.</summary>
    public static class FinisherRank
    {
        // Expected quality rounded to milli-units for skill 0..20 × shift 0..6.
        // Shifts above three cover guaranteed expertise upgrades and saturation.
        // Generated from
        // QualityOdds.Distribution; FinisherRankTests.TableMatchesAnalyticExpectedValue
        // keeps it in lockstep (its failure output prints the expected literals).
        private static readonly int[] EvMilli =
        {
            408, 1408, 2408, 3408, 4406, 5354, 6000,
            705, 1705, 2705, 3705, 4696, 5566, 6000,
            1095, 2095, 3095, 4094, 5064, 5798, 6000,
            1380, 2380, 3380, 4377, 5310, 5909, 6000,
            1564, 2564, 3564, 4558, 5452, 5952, 6000,
            1779, 2779, 3778, 4766, 5608, 5977, 6000,
            1987, 2987, 3987, 4964, 5738, 5990, 6000,
            2186, 3186, 4185, 5145, 5837, 5996, 6000,
            2375, 3375, 4373, 5308, 5907, 5999, 6000,
            2511, 3511, 4509, 5416, 5943, 5999, 6000,
            2662, 3662, 4657, 5531, 5966, 6000, 6000,
            2816, 3816, 4809, 5640, 5981, 6000, 6000,
            2965, 3965, 4953, 5735, 5990, 6000, 6000,
            3060, 4060, 5044, 5788, 5994, 6000, 6000,
            3151, 4151, 5130, 5834, 5996, 6000, 6000,
            3239, 4239, 5210, 5873, 5998, 6000, 6000,
            3323, 4323, 5286, 5904, 5999, 6000, 6000,
            3404, 4404, 5357, 5930, 5999, 6000, 6000,
            3483, 4483, 5423, 5949, 6000, 6000, 6000,
            3578, 4578, 5502, 5964, 6000, 6000, 6000,
            3672, 4672, 5577, 5975, 6000, 6000, 6000,
        };

        public static int EvMilliAt(int skill, int shift)
        {
            if (skill < 0) skill = 0; else if (skill > 20) skill = 20;
            if (shift < 0) shift = 0; else if (shift > 3) shift = 3;
            return ExpectedAt(skill, shift);
        }

        public static int RankMilliOf(in CandidateFacts f)
        {
            // Preserve the existing vanilla role/inspiration clamp. Bonus
            // levels are applied afterward and may reach the full quality cap.
            int shift = (f.Inspired ? 2 : 0) + f.RoleOffset;
            if (shift < 0) shift = 0; else if (shift > 3) shift = 3;
            shift += f.QualityBonusMilli / QualityBonus.Scale;
            int remainder = f.QualityBonusMilli % QualityBonus.Scale;
            int lower = ExpectedAt(f.Skill, shift);
            if (remainder == 0) return lower;
            int upper = ExpectedAt(f.Skill, shift + 1);
            return (lower * (QualityBonus.Scale - remainder) + upper * remainder
                + QualityBonus.Scale / 2) / QualityBonus.Scale;
        }

        private static int ExpectedAt(int skill, int shift)
        {
            if (skill < 0) skill = 0; else if (skill > 20) skill = 20;
            if (shift > 6) shift = 6;
            return EvMilli[skill * 7 + shift];
        }

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
