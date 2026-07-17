namespace WorkRoles.Core
{
    /// Unified pawn-signal tiers. Rung order is the contract: rules compare
    /// buckets numerically (interest = Strong+, drafts draw Neutral then Poor,
    /// Awful is never drafted).
    public enum SignalBucket
    {
        Awful = 0,       // apathy: counter-productive, never drafted
        Poor = 1,        // no interest, untouched skill
        Neutral = 2,     // no interest, some level
        Strong = 3,      // minor-tier passion or positive aptitude
        Great = 4,       // major-tier passion
        Exceptional = 5, // VSE expertise
    }

    public static class SignalBuckets
    {
        /// passionScore: 0/1/2 (VSE customs pre-mapped by tier); aptitude:
        /// sign only (VSE apathy negative); expertise: VSE specialization.
        public static SignalBucket Classify(int level, int passionScore, int aptitude, bool expertise)
        {
            if (aptitude < 0) return SignalBucket.Awful;
            if (expertise) return SignalBucket.Exceptional;
            if (passionScore >= 2) return SignalBucket.Great;
            if (passionScore == 1 || aptitude > 0) return SignalBucket.Strong;
            return level > 0 ? SignalBucket.Neutral : SignalBucket.Poor;
        }
    }
}
