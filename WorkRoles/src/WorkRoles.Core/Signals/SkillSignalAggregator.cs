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

    public sealed class WorkTypeBucketSignal
    {
        public string WorkTypeDefName { get; }
        public SignalBucket Bucket { get; }
        public IReadOnlyList<SignalContribution> Contributions { get; }

        internal WorkTypeBucketSignal(
            string workTypeDefName,
            SignalBucket bucket,
            IReadOnlyList<SignalContribution> contributions)
        {
            WorkTypeDefName = SignalCondition.Required(
                workTypeDefName, nameof(workTypeDefName));
            Bucket = bucket;
            Contributions = contributions
                ?? throw new ArgumentNullException(nameof(contributions));
        }
    }

    public sealed class WorkTypeBucketSnapshot
    {
        private readonly Dictionary<string, WorkTypeBucketSignal> byWorkType;

        public static readonly WorkTypeBucketSnapshot Empty =
            new WorkTypeBucketSnapshot(Array.Empty<WorkTypeBucketSignal>());

        public IReadOnlyList<WorkTypeBucketSignal> All { get; }

        internal WorkTypeBucketSnapshot(IEnumerable<WorkTypeBucketSignal> signals)
        {
            var all = new List<WorkTypeBucketSignal>();
            byWorkType = new Dictionary<string, WorkTypeBucketSignal>(StringComparer.Ordinal);
            foreach (WorkTypeBucketSignal signal in signals)
            {
                if (signal == null)
                    throw new ArgumentException("Signals cannot contain null.", nameof(signals));
                if (byWorkType.ContainsKey(signal.WorkTypeDefName))
                    throw new ArgumentException(
                        "Duplicate work type bucket: " + signal.WorkTypeDefName,
                        nameof(signals));
                byWorkType.Add(signal.WorkTypeDefName, signal);
                all.Add(signal);
            }
            All = new ReadOnlyCollection<WorkTypeBucketSignal>(all);
        }

        public WorkTypeBucketSignal ForWorkType(string workTypeDefName)
        {
            if (string.IsNullOrWhiteSpace(workTypeDefName)) return null;
            return byWorkType.TryGetValue(workTypeDefName, out var signal)
                ? signal
                : null;
        }
    }

    public static class WorkTypeSignalAggregator
    {
        public static WorkTypeBucketSnapshot Aggregate(
            SignalSnapshot snapshot,
            SignalClassificationCatalog catalog = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            catalog = catalog ?? SignalClassificationCatalog.Default;

            if (!snapshot.HasWorkTypeSignals)
                return WorkTypeBucketSnapshot.Empty;

            var workTypes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Signal signal in snapshot.All)
                if (signal.WorkTypeDefName != null)
                    workTypes.Add(signal.WorkTypeDefName);

            var results = new List<WorkTypeBucketSignal>(workTypes.Count);
            foreach (string workType in workTypes)
            {
                AggregatedBucket aggregate = SignalBucketAggregation.Aggregate(
                    snapshot.ForWorkType(workType), catalog);
                results.Add(new WorkTypeBucketSignal(
                    workType, aggregate.Bucket, aggregate.Contributions));
            }
            return results.Count == 0
                ? WorkTypeBucketSnapshot.Empty
                : new WorkTypeBucketSnapshot(results);
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
                AggregatedBucket aggregate = SignalBucketAggregation.Aggregate(
                    snapshot.ForSkill(skill), catalog);
                results.Add(new SkillBucketSignal(
                    skill, aggregate.Bucket, aggregate.Contributions));
            }
            return new SkillBucketSnapshot(results);
        }

        internal static int Score(SignalBucket bucket)
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

        internal static SignalBucket FromScore(int score)
        {
            if (score >= 3) return SignalBucket.Exceptional;
            if (score == 2) return SignalBucket.Great;
            if (score == 1) return SignalBucket.Strong;
            if (score == 0) return SignalBucket.Neutral;
            if (score == -1) return SignalBucket.Poor;
            return SignalBucket.Awful;
        }
    }

    internal readonly struct AggregatedBucket
    {
        internal AggregatedBucket(
            SignalBucket bucket,
            IReadOnlyList<SignalContribution> contributions)
        {
            Bucket = bucket;
            Contributions = contributions;
        }

        internal SignalBucket Bucket { get; }
        internal IReadOnlyList<SignalContribution> Contributions { get; }
    }

    internal static class SignalBucketAggregation
    {
        internal static AggregatedBucket Aggregate(
            IReadOnlyList<Signal> signals,
            SignalClassificationCatalog catalog)
        {
            var contributions = new List<SignalContribution>();
            int score = 0;
            bool hardVeto = false;
            foreach (Signal signal in signals)
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
                score += SkillSignalAggregator.Score(bucket);
            }

            IReadOnlyList<SignalContribution> readOnly = contributions.Count == 0
                ? Array.Empty<SignalContribution>()
                : new ReadOnlyCollection<SignalContribution>(contributions);
            return new AggregatedBucket(
                hardVeto ? SignalBucket.Awful
                    : SkillSignalAggregator.FromScore(score),
                readOnly);
        }
    }
}
