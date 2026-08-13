using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Enum order is display order (sections read in order of application);
    /// persistence uses descriptor stable keys, so reordering is save-safe.
    public enum RecommendationTuningOption
    {
        CandidateMinimumSignal,
        ChampionSkillDivisor,
        ChampionMultiSkillMinimumCount,
        ChampionAwfulMultiplierQuarterUnits,
        ChampionPoorMultiplierQuarterUnits,
        ChampionNeutralMultiplierQuarterUnits,
        ChampionStrongMultiplierQuarterUnits,
        ChampionGreatMultiplierQuarterUnits,
        ChampionExceptionalMultiplierQuarterUnits,
        ChampionAwfulTieBreakPoints,
        ChampionPoorTieBreakPoints,
        ChampionNeutralTieBreakPoints,
        ChampionStrongTieBreakPoints,
        ChampionGreatTieBreakPoints,
        ChampionExceptionalTieBreakPoints,
        RankedCandidatePrioritySignal,
        SurplusMinimumSignal,

        PathMinimumSignal,
        OptionalTargetMinimumSkillCount,
        OptionalTargetMinimumSignal,
        OptionalTargetStrongLevel,
        OptionalTargetStrongPromotedSignal,
        OptionalTargetGreatLevel,
        OptionalTargetGreatPromotedSignal,
        OptionalTargetMinimumPoints,

        LeadMinimumConnectedTargets,
        LeadMinimumSignal,

        OrderingSkillDivisor,
        OrderingAwfulSignalPoints,
        OrderingPoorSignalPoints,
        OrderingNeutralSignalPoints,
        OrderingStrongSignalPoints,
        OrderingGreatSignalPoints,
        OrderingExceptionalSignalPoints,
        FirstMinimumPickBonus,
        SecondMinimumPickBonus,
        ThirdMinimumPickBonus,
        LaterMinimumPickBonus,
        OrderingImportantCategoryPoints,
        OrderingOptionalCategoryPoints,
        OrderingPartTimePoints,
        OrderingOpportunisticPoints,

        HunterFirstTierMaximum,
        HunterSecondTierMaximum,
        HunterThirdTierMaximum,
        HunterFourthTierMaximum,

        RepeatChampionOverlapPenalty,
        RepeatChampionDistinctPenalty,
        RepeatChampionOccasionalPenalty,
    }

    public enum RecommendationTuningValueKind
    {
        Integer,
        QuarterMultiplier,
        SignalBucket,
    }

    public sealed class RecommendationTuningDescriptor
    {
        internal RecommendationTuningDescriptor(
            RecommendationTuningOption option,
            string stableKey,
            string sectionLabelKey,
            string labelKey,
            string descriptionKey,
            int defaultValue,
            int minimumValue,
            int maximumValue,
            int step,
            RecommendationTuningValueKind valueKind,
            bool hidden = false)
        {
            Option = option;
            StableKey = stableKey;
            SectionLabelKey = sectionLabelKey;
            LabelKey = labelKey;
            DescriptionKey = descriptionKey;
            DefaultValue = defaultValue;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            Step = step;
            ValueKind = valueKind;
            Hidden = hidden;
        }

        public RecommendationTuningOption Option { get; }
        public string StableKey { get; }
        public string SectionLabelKey { get; }
        public string LabelKey { get; }
        public string DescriptionKey { get; }
        public int DefaultValue { get; }
        public int MinimumValue { get; }
        public int MaximumValue { get; }
        public int Step { get; }
        public RecommendationTuningValueKind ValueKind { get; }
        /// Persisted and editable through the options API, but not rendered
        /// in the tuning UI.
        public bool Hidden { get; }
    }

    /// <summary>
    /// Immutable, deterministic inputs to recommendation formulas. A changed
    /// value publishes a new snapshot; a normalized no-op preserves identity.
    /// Descriptor stable keys are the save-game compatibility contract.
    /// </summary>
    public sealed class RecommendationsTuningOptions
    {
        private const string ChampionSection = "WR_RecTuneChampionSection";
        private const string OrderingSection = "WR_RecTuneOrderingSection";
        private const string OptionalSection = "WR_RecTuneOptionalSection";
        private const string LeadSection = "WR_RecTuneLeadSection";
        private const string HunterSection = "WR_RecTuneHunterSection";

        private static readonly RecommendationTuningDescriptor[] descriptorArray =
        {
            Signal(RecommendationTuningOption.CandidateMinimumSignal,
                "candidateMinimumSignal", ChampionSection, "CandidateMinimumSignal", 1),
            Integer(RecommendationTuningOption.ChampionSkillDivisor,
                "championSkillDivisor", ChampionSection, "ChampionSkillDivisor", 2, 1, 20),
            Integer(RecommendationTuningOption.ChampionMultiSkillMinimumCount,
                "championMultiSkillMinimumCount", ChampionSection,
                "ChampionMultiSkillMinimumCount", 2, 1, 20),
            Quarter(RecommendationTuningOption.ChampionAwfulMultiplierQuarterUnits,
                "championAwfulMultiplier", ChampionSection, "ChampionAwfulMultiplier", 0),
            Quarter(RecommendationTuningOption.ChampionPoorMultiplierQuarterUnits,
                "championPoorMultiplier", ChampionSection, "ChampionPoorMultiplier", 2),
            Quarter(RecommendationTuningOption.ChampionNeutralMultiplierQuarterUnits,
                "championNeutralMultiplier", ChampionSection, "ChampionNeutralMultiplier", 4),
            Quarter(RecommendationTuningOption.ChampionStrongMultiplierQuarterUnits,
                "championStrongMultiplier", ChampionSection, "ChampionStrongMultiplier", 6),
            Quarter(RecommendationTuningOption.ChampionGreatMultiplierQuarterUnits,
                "championGreatMultiplier", ChampionSection, "ChampionGreatMultiplier", 8),
            Quarter(RecommendationTuningOption.ChampionExceptionalMultiplierQuarterUnits,
                "championExceptionalMultiplier", ChampionSection,
                "ChampionExceptionalMultiplier", 10),
            Points(RecommendationTuningOption.ChampionAwfulTieBreakPoints,
                "championAwfulTieBreakPoints", ChampionSection,
                "ChampionAwfulTieBreakPoints", -5),
            Points(RecommendationTuningOption.ChampionPoorTieBreakPoints,
                "championPoorTieBreakPoints", ChampionSection, "ChampionPoorTieBreakPoints", -3),
            Points(RecommendationTuningOption.ChampionNeutralTieBreakPoints,
                "championNeutralTieBreakPoints", ChampionSection,
                "ChampionNeutralTieBreakPoints", 0),
            Points(RecommendationTuningOption.ChampionStrongTieBreakPoints,
                "championStrongTieBreakPoints", ChampionSection,
                "ChampionStrongTieBreakPoints", 1),
            Points(RecommendationTuningOption.ChampionGreatTieBreakPoints,
                "championGreatTieBreakPoints", ChampionSection, "ChampionGreatTieBreakPoints", 3),
            Points(RecommendationTuningOption.ChampionExceptionalTieBreakPoints,
                "championExceptionalTieBreakPoints", ChampionSection,
                "ChampionExceptionalTieBreakPoints", 5),
            Signal(RecommendationTuningOption.RankedCandidatePrioritySignal,
                "rankedCandidatePrioritySignal", ChampionSection,
                "RankedCandidatePrioritySignal", 3),
            Signal(RecommendationTuningOption.SurplusMinimumSignal,
                "surplusMinimumSignal", ChampionSection, "SurplusMinimumSignal", 3),

            Signal(RecommendationTuningOption.PathMinimumSignal,
                "pathMinimumSignal", OptionalSection, "PathMinimumSignal", 1),
            Integer(RecommendationTuningOption.OptionalTargetMinimumSkillCount,
                "optionalTargetMinimumSkillCount", OptionalSection,
                "OptionalTargetMinimumSkillCount", 2, 1, 20),
            Signal(RecommendationTuningOption.OptionalTargetMinimumSignal,
                "optionalTargetMinimumSignal", OptionalSection,
                "OptionalTargetMinimumSignal", 2),
            Level(RecommendationTuningOption.OptionalTargetStrongLevel,
                "optionalTargetStrongLevel", OptionalSection, "OptionalTargetStrongLevel", 10),
            Signal(RecommendationTuningOption.OptionalTargetStrongPromotedSignal,
                "optionalTargetStrongPromotedSignal", OptionalSection,
                "OptionalTargetStrongPromotedSignal", 3),
            Level(RecommendationTuningOption.OptionalTargetGreatLevel,
                "optionalTargetGreatLevel", OptionalSection, "OptionalTargetGreatLevel", 15),
            Signal(RecommendationTuningOption.OptionalTargetGreatPromotedSignal,
                "optionalTargetGreatPromotedSignal", OptionalSection,
                "OptionalTargetGreatPromotedSignal", 4),
            Points(RecommendationTuningOption.OptionalTargetMinimumPoints,
                "optionalTargetMinimumPoints", OptionalSection,
                "OptionalTargetMinimumPoints", 2, 0, 100),

            Integer(RecommendationTuningOption.LeadMinimumConnectedTargets,
                "leadMinimumConnectedTargets", LeadSection,
                "LeadMinimumConnectedTargets", 3, 1, 20),
            Signal(RecommendationTuningOption.LeadMinimumSignal,
                "leadMinimumSignal", LeadSection, "LeadMinimumSignal", 3),

            Integer(RecommendationTuningOption.OrderingSkillDivisor,
                "orderingSkillDivisor", OrderingSection, "OrderingSkillDivisor", 5, 1, 20),
            Points(RecommendationTuningOption.OrderingAwfulSignalPoints,
                "orderingAwfulSignalPoints", OrderingSection, "OrderingAwfulSignalPoints", -5),
            Points(RecommendationTuningOption.OrderingPoorSignalPoints,
                "orderingPoorSignalPoints", OrderingSection, "OrderingPoorSignalPoints", -5),
            Points(RecommendationTuningOption.OrderingNeutralSignalPoints,
                "orderingNeutralSignalPoints", OrderingSection,
                "OrderingNeutralSignalPoints", -3),
            Points(RecommendationTuningOption.OrderingStrongSignalPoints,
                "orderingStrongSignalPoints", OrderingSection, "OrderingStrongSignalPoints", 1),
            Points(RecommendationTuningOption.OrderingGreatSignalPoints,
                "orderingGreatSignalPoints", OrderingSection, "OrderingGreatSignalPoints", 3),
            Points(RecommendationTuningOption.OrderingExceptionalSignalPoints,
                "orderingExceptionalSignalPoints", OrderingSection,
                "OrderingExceptionalSignalPoints", 5),
            Bonus(RecommendationTuningOption.FirstMinimumPickBonus,
                "firstMinimumPickBonus", OrderingSection, "FirstMinimumPickBonus", 10),
            Bonus(RecommendationTuningOption.SecondMinimumPickBonus,
                "secondMinimumPickBonus", OrderingSection, "SecondMinimumPickBonus", 5),
            Bonus(RecommendationTuningOption.ThirdMinimumPickBonus,
                "thirdMinimumPickBonus", OrderingSection, "ThirdMinimumPickBonus", 2),
            Bonus(RecommendationTuningOption.LaterMinimumPickBonus,
                "laterMinimumPickBonus", OrderingSection, "LaterMinimumPickBonus", 1),
            Points(RecommendationTuningOption.OrderingImportantCategoryPoints,
                "orderingImportantCategoryPoints", OrderingSection, "OrderingImportantCategoryPoints", 4),
            Points(RecommendationTuningOption.OrderingOptionalCategoryPoints,
                "orderingOptionalCategoryPoints", OrderingSection, "OrderingOptionalCategoryPoints", -4),
            Points(RecommendationTuningOption.OrderingPartTimePoints,
                "orderingPartTimePoints", OrderingSection, "OrderingPartTimePoints", 2),
            Points(RecommendationTuningOption.OrderingOpportunisticPoints,
                "orderingOpportunisticPoints", OrderingSection, "OrderingOpportunisticPoints", -2),

            Level(RecommendationTuningOption.HunterFirstTierMaximum,
                "hunterFirstTierMaximum", HunterSection, "HunterFirstTierMaximum", 4),
            Level(RecommendationTuningOption.HunterSecondTierMaximum,
                "hunterSecondTierMaximum", HunterSection, "HunterSecondTierMaximum", 8),
            Level(RecommendationTuningOption.HunterThirdTierMaximum,
                "hunterThirdTierMaximum", HunterSection, "HunterThirdTierMaximum", 12),
            Level(RecommendationTuningOption.HunterFourthTierMaximum,
                "hunterFourthTierMaximum", HunterSection, "HunterFourthTierMaximum", 16),

            Percent(RecommendationTuningOption.RepeatChampionOverlapPenalty,
                "repeatChampionOverlapPenalty", ChampionSection,
                "RepeatChampionOverlapPenalty", 60),
            Percent(RecommendationTuningOption.RepeatChampionDistinctPenalty,
                "repeatChampionDistinctPenalty", ChampionSection,
                "RepeatChampionDistinctPenalty", 40),
            Percent(RecommendationTuningOption.RepeatChampionOccasionalPenalty,
                "repeatChampionOccasionalPenalty", ChampionSection,
                "RepeatChampionOccasionalPenalty", 20),
        };

        private static readonly IReadOnlyList<RecommendationTuningDescriptor>
            descriptorList = Array.AsReadOnly(descriptorArray);

        private readonly int[] values;

        static RecommendationsTuningOptions()
        {
            int count = Enum.GetValues(typeof(RecommendationTuningOption)).Length;
            if (descriptorArray.Length != count)
                throw new InvalidOperationException(
                    "Every recommendation tuning option requires a descriptor.");
            for (int index = 0; index < descriptorArray.Length; index++)
                if ((int)descriptorArray[index].Option != index)
                    throw new InvalidOperationException(
                        "Recommendation tuning descriptors must follow enum order.");
        }

        private RecommendationsTuningOptions(int[] values)
        {
            this.values = values;
        }

        public static IReadOnlyList<RecommendationTuningDescriptor> Descriptors =>
            descriptorList;

        public static RecommendationsTuningOptions Default { get; } =
            new RecommendationsTuningOptions(CreateDefaults());

        public int Get(RecommendationTuningOption option)
        {
            int index = (int)option;
            if (index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(nameof(option));
            return values[index];
        }

        public SignalBucket PromoteSkillSignal(
            int skillLevel,
            SignalBucket signal)
        {
            if (signal < (SignalBucket)Get(
                    RecommendationTuningOption.OptionalTargetMinimumSignal))
                return signal;
            SignalBucket promoted = skillLevel >= Get(
                    RecommendationTuningOption.OptionalTargetGreatLevel)
                ? (SignalBucket)Get(
                    RecommendationTuningOption.OptionalTargetGreatPromotedSignal)
                : skillLevel >= Get(
                    RecommendationTuningOption.OptionalTargetStrongLevel)
                    ? (SignalBucket)Get(
                        RecommendationTuningOption.OptionalTargetStrongPromotedSignal)
                    : signal;
            return promoted > signal ? promoted : signal;
        }

        public RecommendationsTuningOptions With(
            RecommendationTuningOption option,
            int value)
        {
            int index = (int)option;
            if (index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(nameof(option));
            RecommendationTuningDescriptor descriptor = descriptorArray[index];
            int normalized = Math.Max(
                descriptor.MinimumValue,
                Math.Min(descriptor.MaximumValue, value));
            normalized = NormalizeRelated(option, normalized);
            if (values[index] == normalized) return this;
            var changed = (int[])values.Clone();
            changed[index] = normalized;
            return SameValues(changed, Default.values)
                ? Default
                : new RecommendationsTuningOptions(changed);
        }

        public static RecommendationsTuningOptions FromValues(
            IReadOnlyDictionary<RecommendationTuningOption, int> source)
        {
            int[] loaded = CreateDefaults();
            if (source != null)
                foreach (KeyValuePair<RecommendationTuningOption, int> pair in source)
                {
                    int index = (int)pair.Key;
                    if (index < 0 || index >= descriptorArray.Length) continue;
                    RecommendationTuningDescriptor descriptor =
                        descriptorArray[index];
                    loaded[index] = Math.Max(
                        descriptor.MinimumValue,
                        Math.Min(descriptor.MaximumValue, pair.Value));
                }
            NormalizeRelatedValues(loaded);
            return SameValues(loaded, Default.values)
                ? Default
                : new RecommendationsTuningOptions(loaded);
        }

        public static bool TryOption(
            string stableKey,
            out RecommendationTuningOption option)
        {
            for (int index = 0; index < descriptorArray.Length; index++)
                if (string.Equals(
                        descriptorArray[index].StableKey,
                        stableKey,
                        StringComparison.Ordinal))
                {
                    option = descriptorArray[index].Option;
                    return true;
                }
            option = default;
            return false;
        }

        private int NormalizeRelated(
            RecommendationTuningOption option,
            int value)
        {
            switch (option)
            {
                case RecommendationTuningOption.OptionalTargetStrongLevel:
                    return Math.Min(value, Get(
                        RecommendationTuningOption.OptionalTargetGreatLevel));
                case RecommendationTuningOption.OptionalTargetGreatLevel:
                    return Math.Max(value, Get(
                        RecommendationTuningOption.OptionalTargetStrongLevel));
                case RecommendationTuningOption.OptionalTargetStrongPromotedSignal:
                    return Math.Min(value, Get(
                        RecommendationTuningOption.OptionalTargetGreatPromotedSignal));
                case RecommendationTuningOption.OptionalTargetGreatPromotedSignal:
                    return Math.Max(value, Get(
                        RecommendationTuningOption.OptionalTargetStrongPromotedSignal));
                case RecommendationTuningOption.HunterFirstTierMaximum:
                    return Math.Min(value, Get(
                        RecommendationTuningOption.HunterSecondTierMaximum));
                case RecommendationTuningOption.HunterSecondTierMaximum:
                    return Math.Max(
                        Get(RecommendationTuningOption.HunterFirstTierMaximum),
                        Math.Min(value, Get(
                            RecommendationTuningOption.HunterThirdTierMaximum)));
                case RecommendationTuningOption.HunterThirdTierMaximum:
                    return Math.Max(
                        Get(RecommendationTuningOption.HunterSecondTierMaximum),
                        Math.Min(value, Get(
                            RecommendationTuningOption.HunterFourthTierMaximum)));
                case RecommendationTuningOption.HunterFourthTierMaximum:
                    return Math.Max(value, Get(
                        RecommendationTuningOption.HunterThirdTierMaximum));
                default:
                    return value;
            }
        }

        private static int[] CreateDefaults()
        {
            var defaults = new int[descriptorArray.Length];
            for (int index = 0; index < descriptorArray.Length; index++)
                defaults[index] = descriptorArray[index].DefaultValue;
            return defaults;
        }

        private static void NormalizeRelatedValues(int[] loaded)
        {
            int strongLevel = (int)RecommendationTuningOption
                .OptionalTargetStrongLevel;
            int greatLevel = (int)RecommendationTuningOption
                .OptionalTargetGreatLevel;
            if (loaded[strongLevel] > loaded[greatLevel])
                loaded[strongLevel] = loaded[greatLevel];

            int strongSignal = (int)RecommendationTuningOption
                .OptionalTargetStrongPromotedSignal;
            int greatSignal = (int)RecommendationTuningOption
                .OptionalTargetGreatPromotedSignal;
            if (loaded[strongSignal] > loaded[greatSignal])
                loaded[strongSignal] = loaded[greatSignal];

            int firstTier = (int)RecommendationTuningOption
                .HunterFirstTierMaximum;
            int secondTier = (int)RecommendationTuningOption
                .HunterSecondTierMaximum;
            int thirdTier = (int)RecommendationTuningOption
                .HunterThirdTierMaximum;
            int fourthTier = (int)RecommendationTuningOption
                .HunterFourthTierMaximum;
            if (loaded[firstTier] > loaded[secondTier])
                loaded[firstTier] = loaded[secondTier];
            if (loaded[secondTier] > loaded[thirdTier])
                loaded[thirdTier] = loaded[secondTier];
            if (loaded[thirdTier] > loaded[fourthTier])
                loaded[fourthTier] = loaded[thirdTier];
        }

        private static bool SameValues(int[] left, int[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }

        private static RecommendationTuningDescriptor Integer(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue,
            int minimum = 0,
            int maximum = 100) => Descriptor(
                option, stableKey, section, name, defaultValue,
                minimum, maximum, RecommendationTuningValueKind.Integer);

        private static RecommendationTuningDescriptor Level(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue) => Integer(
                option, stableKey, section, name, defaultValue, 0, 20);

        /// Multipliers persist in quarter-units (value 4 = 1×). Saved values
        /// predate the visible tuning UI only as defaults, which are never
        /// scribed, so the unit change needs no migration.
        private static RecommendationTuningDescriptor Quarter(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue) => Descriptor(
                option, stableKey, section, name, defaultValue,
                0, 40, RecommendationTuningValueKind.QuarterMultiplier);

        /// Percent of colonist count; hidden pending the automatic
        /// occasional-role derivation.
        private static RecommendationTuningDescriptor Percent(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue) => new RecommendationTuningDescriptor(
                option, stableKey, section,
                "WR_RecTune" + name, "WR_RecTune" + name + "Desc",
                defaultValue, 0, 100, 1,
                RecommendationTuningValueKind.Integer, hidden: true);

        private static RecommendationTuningDescriptor Signal(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue) => Descriptor(
                option, stableKey, section, name, defaultValue,
                (int)SignalBucket.Awful,
                (int)SignalBucket.Exceptional,
                RecommendationTuningValueKind.SignalBucket);

        private static RecommendationTuningDescriptor Points(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue,
            int minimum = -50,
            int maximum = 50) => Descriptor(
                option, stableKey, section, name, defaultValue,
                minimum, maximum, RecommendationTuningValueKind.Integer);

        private static RecommendationTuningDescriptor Bonus(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue) => Descriptor(
                option, stableKey, section, name, defaultValue,
                0, byte.MaxValue, RecommendationTuningValueKind.Integer);

        private static RecommendationTuningDescriptor Descriptor(
            RecommendationTuningOption option,
            string stableKey,
            string section,
            string name,
            int defaultValue,
            int minimum,
            int maximum,
            RecommendationTuningValueKind valueKind) =>
            new RecommendationTuningDescriptor(
                option,
                stableKey,
                section,
                "WR_RecTune" + name,
                "WR_RecTune" + name + "Desc",
                defaultValue,
                minimum,
                maximum,
                1,
                valueKind);
    }

    internal sealed class RecommendationFormulaEngine
    {
        private readonly RecommendationsTuningOptions options;

        internal RecommendationFormulaEngine(
            RecommendationsTuningOptions options)
        {
            this.options = options
                ?? throw new ArgumentNullException(nameof(options));
        }

        internal SignalBucket CandidateMinimumSignal => Signal(
            RecommendationTuningOption.CandidateMinimumSignal);
        internal int ChampionMultiSkillMinimumCount => Value(
            RecommendationTuningOption.ChampionMultiSkillMinimumCount);
        internal SignalBucket RankedCandidatePrioritySignal => Signal(
            RecommendationTuningOption.RankedCandidatePrioritySignal);
        internal SignalBucket SurplusMinimumSignal => Signal(
            RecommendationTuningOption.SurplusMinimumSignal);
        internal SignalBucket PathMinimumSignal => Signal(
            RecommendationTuningOption.PathMinimumSignal);
        internal int OptionalTargetMinimumSkillCount => Value(
            RecommendationTuningOption.OptionalTargetMinimumSkillCount);
        internal SignalBucket OptionalTargetMinimumSignal => Signal(
            RecommendationTuningOption.OptionalTargetMinimumSignal);
        internal int OptionalTargetStrongLevel => Value(
            RecommendationTuningOption.OptionalTargetStrongLevel);
        internal SignalBucket OptionalTargetStrongPromotedSignal => Signal(
            RecommendationTuningOption.OptionalTargetStrongPromotedSignal);
        internal int OptionalTargetGreatLevel => Value(
            RecommendationTuningOption.OptionalTargetGreatLevel);
        internal SignalBucket OptionalTargetGreatPromotedSignal => Signal(
            RecommendationTuningOption.OptionalTargetGreatPromotedSignal);
        internal int OptionalTargetMinimumPoints => Value(
            RecommendationTuningOption.OptionalTargetMinimumPoints);

        internal SignalBucket PromoteSkillSignal(
            int skillLevel,
            SignalBucket signal) =>
            options.PromoteSkillSignal(skillLevel, signal);
        internal int LeadMinimumConnectedTargets => Value(
            RecommendationTuningOption.LeadMinimumConnectedTargets);
        internal SignalBucket LeadMinimumSignal => Signal(
            RecommendationTuningOption.LeadMinimumSignal);
        internal byte MinimumBonus(int pickIndex)
        {
            RecommendationTuningOption option = pickIndex == 0
                ? RecommendationTuningOption.FirstMinimumPickBonus
                : pickIndex == 1
                    ? RecommendationTuningOption.SecondMinimumPickBonus
                    : pickIndex == 2
                        ? RecommendationTuningOption.ThirdMinimumPickBonus
                        : RecommendationTuningOption.LaterMinimumPickBonus;
            return (byte)Value(option);
        }

        internal int ChampionSkillScore(int level, SignalBucket verdict)
        {
            int divisor = Value(RecommendationTuningOption.ChampionSkillDivisor);
            int roundedSkill = (Math.Max(0, level) + divisor - 1) / divisor;
            RecommendationTuningOption multiplier;
            switch (verdict)
            {
                case SignalBucket.Exceptional:
                    multiplier = RecommendationTuningOption
                        .ChampionExceptionalMultiplierQuarterUnits;
                    break;
                case SignalBucket.Great:
                    multiplier = RecommendationTuningOption
                        .ChampionGreatMultiplierQuarterUnits;
                    break;
                case SignalBucket.Strong:
                    multiplier = RecommendationTuningOption
                        .ChampionStrongMultiplierQuarterUnits;
                    break;
                case SignalBucket.Neutral:
                    multiplier = RecommendationTuningOption
                        .ChampionNeutralMultiplierQuarterUnits;
                    break;
                case SignalBucket.Poor:
                    multiplier = RecommendationTuningOption
                        .ChampionPoorMultiplierQuarterUnits;
                    break;
                default:
                    multiplier = RecommendationTuningOption
                        .ChampionAwfulMultiplierQuarterUnits;
                    break;
            }
            // Multipliers are stored in quarter-units. Keeping the quadrupled
            // fixed-point score avoids rounding while preserving comparisons.
            return roundedSkill * Value(multiplier);
        }

        internal int ChampionSignalTieBreak(SignalBucket verdict)
        {
            switch (verdict)
            {
                case SignalBucket.Exceptional:
                    return Value(RecommendationTuningOption
                        .ChampionExceptionalTieBreakPoints);
                case SignalBucket.Great:
                    return Value(RecommendationTuningOption
                        .ChampionGreatTieBreakPoints);
                case SignalBucket.Strong:
                    return Value(RecommendationTuningOption
                        .ChampionStrongTieBreakPoints);
                case SignalBucket.Neutral:
                    return Value(RecommendationTuningOption
                        .ChampionNeutralTieBreakPoints);
                case SignalBucket.Poor:
                    return Value(RecommendationTuningOption
                        .ChampionPoorTieBreakPoints);
                default:
                    return Value(RecommendationTuningOption
                        .ChampionAwfulTieBreakPoints);
            }
        }

        /// Penalty for one prior championship, in the champion score's
        /// quadrupled fixed-point units: percent-of-colonist-count, so /25
        /// instead of /100.
        internal int RepeatChampionPenalty(
            bool priorUsesOccasionalRepeatChampionPenalty,
            bool skillsOverlap,
            int colonistCount)
        {
            RecommendationTuningOption option =
                priorUsesOccasionalRepeatChampionPenalty
                ? RecommendationTuningOption.RepeatChampionOccasionalPenalty
                : skillsOverlap
                    ? RecommendationTuningOption.RepeatChampionOverlapPenalty
                    : RecommendationTuningOption.RepeatChampionDistinctPenalty;
            return colonistCount * Value(option) / 25;
        }

        internal int OrderingScore(
            SignalBucket verdict,
            int skillLevel,
            byte minimumBonus,
            RoleCategory category,
            RoleTime time)
        {
            RecommendationTuningOption points;
            switch (verdict)
            {
                case SignalBucket.Exceptional:
                    points = RecommendationTuningOption
                        .OrderingExceptionalSignalPoints;
                    break;
                case SignalBucket.Great:
                    points = RecommendationTuningOption.OrderingGreatSignalPoints;
                    break;
                case SignalBucket.Strong:
                    points = RecommendationTuningOption.OrderingStrongSignalPoints;
                    break;
                case SignalBucket.Neutral:
                    points = RecommendationTuningOption.OrderingNeutralSignalPoints;
                    break;
                case SignalBucket.Poor:
                    points = RecommendationTuningOption.OrderingPoorSignalPoints;
                    break;
                default:
                    points = RecommendationTuningOption.OrderingAwfulSignalPoints;
                    break;
            }
            int divisor = Value(RecommendationTuningOption.OrderingSkillDivisor);
            int skill = (Math.Max(0, skillLevel) + divisor - 1) / divisor;
            // Unclassified category/time count as Normal/FullTime: no modifier.
            int categoryPoints = category == RoleCategory.Important
                ? Value(RecommendationTuningOption.OrderingImportantCategoryPoints)
                : category == RoleCategory.Optional
                    ? Value(RecommendationTuningOption.OrderingOptionalCategoryPoints)
                    : 0;
            int timePoints = time == RoleTime.PartTime
                ? Value(RecommendationTuningOption.OrderingPartTimePoints)
                : time == RoleTime.Opportunistic
                    ? Value(RecommendationTuningOption.OrderingOpportunisticPoints)
                    : 0;
            return Value(points) + skill + minimumBonus + categoryPoints + timePoints;
        }

        internal int HunterTier(int shootingLevel)
        {
            if (shootingLevel <= Value(
                    RecommendationTuningOption.HunterFirstTierMaximum))
                return 1;
            if (shootingLevel <= Value(
                    RecommendationTuningOption.HunterSecondTierMaximum))
                return 2;
            if (shootingLevel <= Value(
                    RecommendationTuningOption.HunterThirdTierMaximum))
                return 3;
            return shootingLevel <= Value(
                RecommendationTuningOption.HunterFourthTierMaximum) ? 4 : 5;
        }

        private int Value(RecommendationTuningOption option) =>
            options.Get(option);

        private SignalBucket Signal(RecommendationTuningOption option) =>
            (SignalBucket)Value(option);
    }
}
