using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WorkRoles.Core;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalContribution
    {
        public Signal Signal { get; }
        public SignalBucket Bucket { get; }
        public bool IsClassified { get; }
        public bool IsHardVeto => IsClassified && Bucket == SignalBucket.Awful;

        internal SignalContribution(Signal signal, SignalBucket bucket, bool isClassified)
        {
            Signal = signal ?? throw new ArgumentNullException(nameof(signal));
            Bucket = bucket;
            IsClassified = isClassified;
        }
    }

    public sealed class SkillBucketSignal
    {
        public string SkillDefName { get; }
        public SignalBucket Bucket { get; }
        public IReadOnlyList<SignalContribution> Contributions { get; }

        internal SkillBucketSignal(
            string skillDefName,
            SignalBucket bucket,
            IReadOnlyList<SignalContribution> contributions)
        {
            SkillDefName = SignalCondition.Required(skillDefName, nameof(skillDefName));
            Bucket = bucket;
            Contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));
        }
    }

    public sealed class SkillBucketSnapshot
    {
        private readonly Dictionary<string, SkillBucketSignal> bySkill;

        public static readonly SkillBucketSnapshot Empty =
            new SkillBucketSnapshot(Array.Empty<SkillBucketSignal>());

        public IReadOnlyList<SkillBucketSignal> All { get; }

        internal SkillBucketSnapshot(IEnumerable<SkillBucketSignal> signals)
        {
            var all = new List<SkillBucketSignal>();
            bySkill = new Dictionary<string, SkillBucketSignal>(StringComparer.Ordinal);
            foreach (SkillBucketSignal signal in signals)
            {
                if (signal == null) throw new ArgumentException("Signals cannot contain null.", nameof(signals));
                if (bySkill.ContainsKey(signal.SkillDefName))
                    throw new ArgumentException("Duplicate skill bucket: " + signal.SkillDefName, nameof(signals));
                bySkill.Add(signal.SkillDefName, signal);
                all.Add(signal);
            }
            All = new ReadOnlyCollection<SkillBucketSignal>(all);
        }

        public SkillBucketSignal ForSkill(string skillDefName)
        {
            if (string.IsNullOrWhiteSpace(skillDefName)) return null;
            return bySkill.TryGetValue(skillDefName, out var signal) ? signal : null;
        }
    }

    public static class SkillSignalAggregator
    {
        public static SkillBucketSnapshot Aggregate(
            IEnumerable<string> enabledSkillDefNames,
            SignalSnapshot snapshot,
            SignalClassificationCatalog catalog = null)
        {
            if (enabledSkillDefNames == null)
                throw new ArgumentNullException(nameof(enabledSkillDefNames));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            catalog = catalog ?? SignalClassificationCatalog.Default;

            var enabled = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string skill in enabledSkillDefNames)
                enabled.Add(SignalCondition.Required(skill, nameof(enabledSkillDefNames)));

            var results = new List<SkillBucketSignal>(enabled.Count);
            foreach (string skill in enabled)
            {
                var contributions = new List<SignalContribution>();
                int score = 0;
                bool hardVeto = false;
                foreach (Signal signal in snapshot.ForSkill(skill))
                {
                    if (signal.Type != SignalType.Active) continue;
                    bool classified = catalog.TryClassify(signal, out SignalBucket bucket);
                    contributions.Add(new SignalContribution(signal, bucket, classified));
                    if (!classified) continue;
                    if (bucket == SignalBucket.Awful)
                    {
                        hardVeto = true;
                        continue;
                    }
                    score += Score(bucket);
                }

                IReadOnlyList<SignalContribution> readOnly = contributions.Count == 0
                    ? Array.Empty<SignalContribution>()
                    : new ReadOnlyCollection<SignalContribution>(contributions);
                results.Add(new SkillBucketSignal(skill,
                    hardVeto ? SignalBucket.Awful : FromScore(score), readOnly));
            }
            return new SkillBucketSnapshot(results);
        }

        private static int Score(SignalBucket bucket)
        {
            switch (bucket)
            {
                case SignalBucket.Exceptional: return 3;
                case SignalBucket.Great: return 2;
                case SignalBucket.Strong: return 1;
                case SignalBucket.Poor: return -1;
                default: return 0;
            }
        }

        private static SignalBucket FromScore(int score)
        {
            if (score >= 3) return SignalBucket.Exceptional;
            if (score == 2) return SignalBucket.Great;
            if (score == 1) return SignalBucket.Strong;
            if (score == 0) return SignalBucket.Neutral;
            if (score == -1) return SignalBucket.Poor;
            return SignalBucket.Awful;
        }
    }
}
