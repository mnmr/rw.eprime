using System;
using System.Collections.Generic;
using System.Linq;
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
        /// The pawn's best skills at or above the minimum verdict, ordered by
        /// the engine's champion skill score (level and verdict weighed
        /// together), then verdict, then level; input order is the stable
        /// tie. Capped at max entries.
        public static List<SkillBucketChoice> Top(
            SkillBucketSnapshot snapshot,
            IEnumerable<SkillBucketCandidate> candidates,
            SignalBucket minimum,
            int max,
            Recs.RecommendationsTuningOptions tuning)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));

            var qualified = new List<SkillBucketChoice>();
            foreach (SkillBucketCandidate candidate in candidates)
            {
                SignalBucket bucket = snapshot.ForSkill(candidate.SkillDefName)?.Bucket
                    ?? SignalBucket.Neutral;
                if (bucket < minimum) continue;
                qualified.Add(new SkillBucketChoice(
                    candidate.SkillDefName, bucket, candidate.SkillLevel));
            }
            var engine = new Recs.RecommendationFormulaEngine(tuning);
            // Stable sort keeps candidate order for fully tied entries.
            var ordered = qualified
                .OrderByDescending(choice =>
                    engine.ChampionSkillScore(choice.SkillLevel, choice.Bucket))
                .ThenByDescending(choice => choice.Bucket)
                .ThenByDescending(choice => choice.SkillLevel)
                .ToList();
            if (ordered.Count > max) ordered.RemoveRange(max, ordered.Count - max);
            return ordered;
        }

        public static SkillBucketChoice? Best(
            SkillBucketSnapshot snapshot,
            IEnumerable<SkillBucketCandidate> candidates)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            SkillBucketChoice? best = null;
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
