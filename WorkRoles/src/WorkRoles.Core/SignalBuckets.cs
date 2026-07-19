namespace WorkRoles.Core
{
    /// Unified pawn-signal tiers. Rung order is the contract: rules compare
    /// buckets numerically (interest = Strong+, Awful is disqualifying).
    public enum SignalBucket
    {
        Awful = 0,       // hard veto or at least two net detrimental steps
        Poor = 1,        // one net detrimental step
        Neutral = 2,     // no net signal
        Strong = 3,      // one net beneficial step
        Great = 4,       // two net beneficial steps
        Exceptional = 5, // at least three net beneficial steps
    }

}
