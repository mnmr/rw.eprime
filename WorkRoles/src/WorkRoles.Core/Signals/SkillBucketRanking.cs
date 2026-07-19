using System;
using System.Collections.Generic;
using WorkRoles.Core;

namespace WorkRoles.Core.Signals
{
    public readonly struct SkillBucketCandidate
    {
        public string SkillDefName { get; }
        public int SkillLevel { get; }

        public SkillBucketCandidate(string skillDefName, int skillLevel)
        {
            SkillDefName = SignalCondition.Required(skillDefName, nameof(skillDefName));
            SkillLevel = skillLevel;
        }
    }

    public sealed class SkillBucketChoice
    {
        public string SkillDefName { get; }
        public SignalBucket Bucket { get; }
        public int SkillLevel { get; }

        internal SkillBucketChoice(string skillDefName, SignalBucket bucket, int skillLevel)
        {
            SkillDefName = skillDefName;
            Bucket = bucket;
            SkillLevel = skillLevel;
        }
    }

    /// Picks the strongest aggregated skill verdict, using current skill level
    /// only to break equal-verdict ties. Input order is the final stable tie.
    public static class SkillBucketRanking
    {
        public static SkillBucketChoice Best(
            SkillBucketSnapshot snapshot,
            IEnumerable<SkillBucketCandidate> candidates)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            SkillBucketChoice best = null;
            foreach (SkillBucketCandidate candidate in candidates)
            {
                SignalBucket bucket = snapshot.ForSkill(candidate.SkillDefName)?.Bucket
                    ?? SignalBucket.Neutral;
                if (best == null || bucket > best.Bucket
                    || (bucket == best.Bucket && candidate.SkillLevel > best.SkillLevel))
                {
                    best = new SkillBucketChoice(
                        candidate.SkillDefName, bucket, candidate.SkillLevel);
                }
            }
            return best;
        }
    }
}
