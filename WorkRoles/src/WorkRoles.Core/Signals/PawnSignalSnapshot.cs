using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Signals
{
    /// A pawn's normalized signals and the per-skill recommendation buckets
    /// derived from those exact signals. Keeping both immutable results together
    /// lets callers cache one coherent classification pass.
    public sealed class PawnSignalSnapshot
    {
        public static readonly PawnSignalSnapshot Empty = new PawnSignalSnapshot(
            SignalSnapshot.Empty, SkillBucketSnapshot.Empty);

        public SignalSnapshot Signals { get; }
        public SkillBucketSnapshot SkillBuckets { get; }

        private PawnSignalSnapshot(
            SignalSnapshot signals,
            SkillBucketSnapshot skillBuckets)
        {
            Signals = signals ?? throw new ArgumentNullException(nameof(signals));
            SkillBuckets = skillBuckets
                ?? throw new ArgumentNullException(nameof(skillBuckets));
        }

        public static PawnSignalSnapshot Create(
            IEnumerable<string> enabledSkillDefNames,
            SignalSnapshot signals,
            SignalClassificationCatalog catalog = null)
        {
            if (enabledSkillDefNames == null)
                throw new ArgumentNullException(nameof(enabledSkillDefNames));
            if (signals == null) throw new ArgumentNullException(nameof(signals));

            return new PawnSignalSnapshot(signals,
                SkillSignalAggregator.Aggregate(
                    enabledSkillDefNames, signals, catalog));
        }
    }
}
