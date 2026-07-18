using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public enum SignalType { Passive, Active }

    public enum SignalSourceKind { Passion, Expertise, Gene, Trait, Hediff }

    public enum SignalEffectKind
    {
        SkillLevel,
        Passion,
        LearningRate,
        SkillDecay,
        WorkSpeed,
        Accuracy,
        AimingDelay,
        Quality,
        Yield,
        Damage,
        HitChance,
        Dodge,
        Mood,
        WorkPreference,
        Capability,
        MentalBreak,
        StatModifier,
    }

    public enum SignalOperation { Add, Multiply, Set, Disable, Descriptive }

    public enum SignalValueUnit
    {
        None,
        Scalar,
        Factor,
        StatValue,
        SkillLevels,
        PassionLevels,
        MoodPoints,
        Days,
    }

    public enum SignalScaleKind { None, ExpertiseLevel }

    public sealed class SignalCondition : IEquatable<SignalCondition>
    {
        public string Key { get; }
        public string Description { get; }

        public SignalCondition(string key, string description)
        {
            Key = Required(key, nameof(key));
            Description = description;
        }

        public bool Equals(SignalCondition other) => other != null
            && StringComparer.Ordinal.Equals(Key, other.Key)
            && StringComparer.Ordinal.Equals(Description, other.Description);

        public override bool Equals(object obj) => Equals(obj as SignalCondition);

        public override int GetHashCode() => SignalHash.Of(Key, Description);

        internal static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be blank.", name);
            return value;
        }
    }

    public sealed class SignalSource : IEquatable<SignalSource>
    {
        public SignalSourceKind Kind { get; }
        public string DefName { get; }
        public string PackageId { get; }
        public string TemplateId { get; }
        public string EffectDiscriminator { get; }
        public IReadOnlyList<string> RequiredPackageIds { get; }

        public SignalSource(
            SignalSourceKind kind,
            string defName,
            string packageId,
            string templateId = null,
            string effectDiscriminator = null,
            IEnumerable<string> requiredPackageIds = null)
        {
            Kind = kind;
            DefName = SignalCondition.Required(defName, nameof(defName));
            PackageId = SignalCondition.Required(packageId, nameof(packageId));
            TemplateId = Optional(templateId, nameof(templateId));
            EffectDiscriminator = Optional(effectDiscriminator, nameof(effectDiscriminator));
            RequiredPackageIds = CopyDependencies(requiredPackageIds);
        }

        public bool Equals(SignalSource other) => other != null
            && Kind == other.Kind
            && StringComparer.Ordinal.Equals(DefName, other.DefName)
            && StringComparer.Ordinal.Equals(PackageId, other.PackageId)
            && StringComparer.Ordinal.Equals(TemplateId, other.TemplateId)
            && StringComparer.Ordinal.Equals(EffectDiscriminator, other.EffectDiscriminator)
            && SignalHash.SequenceEqual(RequiredPackageIds, other.RequiredPackageIds);

        public override bool Equals(object obj) => Equals(obj as SignalSource);

        public override int GetHashCode() => SignalHash.WithSequence(
            SignalHash.Of((int)Kind, DefName, PackageId, TemplateId, EffectDiscriminator),
            RequiredPackageIds);

        private static IReadOnlyList<string> CopyDependencies(IEnumerable<string> values)
        {
            var unique = new SortedSet<string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (var value in values)
                    unique.Add(SignalCondition.Required(value, nameof(values)));
            }
            return new ReadOnlyCollection<string>(new List<string>(unique));
        }

        private static string Optional(string value, string name)
        {
            if (value == null) return null;
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be blank when supplied.", name);
            return value;
        }
    }

    public sealed class SignalEffect : IEquatable<SignalEffect>
    {
        public SignalEffectKind Kind { get; }
        public SignalOperation Operation { get; }
        public float? Magnitude { get; }
        public SignalValueUnit Unit { get; }
        public string TargetDefName { get; }
        public IReadOnlyList<SignalCondition> Conditions { get; }
        public SignalScaleKind ScaleKind { get; }
        public float? CurrentScale { get; }
        public float ScaleMultiplier { get; }
        public float? ResolvedMagnitude { get; }
        public bool AlreadyReflected { get; }

        public SignalEffect(
            SignalEffectKind kind,
            SignalOperation operation,
            float? magnitude,
            SignalValueUnit unit,
            string targetDefName = null,
            IEnumerable<SignalCondition> conditions = null,
            SignalScaleKind scaleKind = SignalScaleKind.None,
            float? currentScale = null,
            float scaleMultiplier = 1f,
            bool alreadyReflected = false)
        {
            if (magnitude.HasValue && (float.IsNaN(magnitude.Value) || float.IsInfinity(magnitude.Value)))
                throw new ArgumentOutOfRangeException(nameof(magnitude));
            if (currentScale.HasValue && (float.IsNaN(currentScale.Value) || float.IsInfinity(currentScale.Value)))
                throw new ArgumentOutOfRangeException(nameof(currentScale));
            if (float.IsNaN(scaleMultiplier) || float.IsInfinity(scaleMultiplier))
                throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));
            if (operation == SignalOperation.Descriptive && magnitude.HasValue)
                throw new ArgumentException("Descriptive effects cannot carry a magnitude.", nameof(magnitude));
            if ((operation == SignalOperation.Add || operation == SignalOperation.Multiply || operation == SignalOperation.Set)
                && !magnitude.HasValue)
                throw new ArgumentException("Numeric operations require a magnitude.", nameof(magnitude));
            if (kind == SignalEffectKind.StatModifier && string.IsNullOrWhiteSpace(targetDefName))
                throw new ArgumentException("A stat modifier requires a target defName.", nameof(targetDefName));

            Kind = kind;
            Operation = operation;
            Magnitude = magnitude;
            Unit = unit;
            TargetDefName = targetDefName;
            Conditions = CopyConditions(conditions);
            ScaleKind = scaleKind;
            CurrentScale = currentScale;
            ScaleMultiplier = scaleMultiplier;
            ResolvedMagnitude = scaleKind == SignalScaleKind.None
                ? magnitude
                : magnitude.HasValue && currentScale.HasValue
                    ? magnitude.Value * currentScale.Value * scaleMultiplier
                    : null;
            AlreadyReflected = alreadyReflected;
        }

        public bool Equals(SignalEffect other) => other != null
            && Kind == other.Kind
            && Operation == other.Operation
            && Magnitude.Equals(other.Magnitude)
            && Unit == other.Unit
            && StringComparer.Ordinal.Equals(TargetDefName, other.TargetDefName)
            && SignalHash.SequenceEqual(Conditions, other.Conditions)
            && ScaleKind == other.ScaleKind
            && CurrentScale.Equals(other.CurrentScale)
            && ScaleMultiplier.Equals(other.ScaleMultiplier)
            && ResolvedMagnitude.Equals(other.ResolvedMagnitude)
            && AlreadyReflected == other.AlreadyReflected;

        public override bool Equals(object obj) => Equals(obj as SignalEffect);

        public override int GetHashCode()
        {
            int hash = SignalHash.Of((int)Kind, (int)Operation, Magnitude, (int)Unit, TargetDefName);
            hash = SignalHash.WithSequence(hash, Conditions);
            return SignalHash.Of(hash, (int)ScaleKind, CurrentScale, ScaleMultiplier,
                ResolvedMagnitude, AlreadyReflected);
        }

        private static IReadOnlyList<SignalCondition> CopyConditions(IEnumerable<SignalCondition> conditions)
        {
            var copy = new List<SignalCondition>();
            if (conditions != null)
            {
                foreach (var condition in conditions)
                {
                    if (condition == null) throw new ArgumentException("Conditions cannot contain null.", nameof(conditions));
                    copy.Add(condition);
                }
            }
            return new ReadOnlyCollection<SignalCondition>(copy);
        }
    }

    public sealed class SignalUi : IEquatable<SignalUi>
    {
        public string Label { get; }
        public string Description { get; }
        public string IconKey { get; }
        public string AuthorTier { get; }
        public string ColorKey { get; }
        public string SourceDisplayName { get; }

        public SignalUi(
            string label,
            string description,
            string iconKey,
            string authorTier,
            string colorKey,
            string sourceDisplayName)
        {
            Label = label;
            Description = description;
            IconKey = iconKey;
            AuthorTier = authorTier;
            ColorKey = colorKey;
            SourceDisplayName = SignalCondition.Required(sourceDisplayName, nameof(sourceDisplayName));
        }

        public bool Equals(SignalUi other) => other != null
            && StringComparer.Ordinal.Equals(Label, other.Label)
            && StringComparer.Ordinal.Equals(Description, other.Description)
            && StringComparer.Ordinal.Equals(IconKey, other.IconKey)
            && StringComparer.Ordinal.Equals(AuthorTier, other.AuthorTier)
            && StringComparer.Ordinal.Equals(ColorKey, other.ColorKey)
            && StringComparer.Ordinal.Equals(SourceDisplayName, other.SourceDisplayName);

        public override bool Equals(object obj) => Equals(obj as SignalUi);

        public override int GetHashCode() => SignalHash.Of(
            Label, Description, IconKey, AuthorTier, ColorKey, SourceDisplayName);
    }

    public sealed class Signal : IEquatable<Signal>
    {
        public SignalType Type { get; }
        public SignalSource Source { get; }
        public string SkillDefName { get; }
        public IReadOnlyList<SignalEffect> Effects { get; }
        public SignalUi Ui { get; }

        public Signal(
            SignalType type,
            SignalSource source,
            string skillDefName,
            IEnumerable<SignalEffect> effects,
            SignalUi ui)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (skillDefName != null && string.IsNullOrWhiteSpace(skillDefName))
                throw new ArgumentException("Skill defName cannot be blank when supplied.", nameof(skillDefName));
            if (ui == null) throw new ArgumentNullException(nameof(ui));

            Type = type;
            Source = source;
            SkillDefName = skillDefName;
            Effects = CopyEffects(effects);
            Ui = ui;
        }

        public bool Equals(Signal other) => other != null
            && Type == other.Type
            && Source.Equals(other.Source)
            && StringComparer.Ordinal.Equals(SkillDefName, other.SkillDefName)
            && SignalHash.SequenceEqual(Effects, other.Effects)
            && Ui.Equals(other.Ui);

        public override bool Equals(object obj) => Equals(obj as Signal);

        public override int GetHashCode()
        {
            int hash = SignalHash.Of((int)Type, Source, SkillDefName, Ui);
            return SignalHash.WithSequence(hash, Effects);
        }

        private static IReadOnlyList<SignalEffect> CopyEffects(IEnumerable<SignalEffect> effects)
        {
            var copy = new List<SignalEffect>();
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect == null) throw new ArgumentException("Effects cannot contain null.", nameof(effects));
                    copy.Add(effect);
                }
            }
            return new ReadOnlyCollection<SignalEffect>(copy);
        }
    }

    internal static class SignalHash
    {
        public static int Of(params object[] values)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < values.Length; i++)
                {
                    object value = values[i];
                    int part = value is string text
                        ? StringComparer.Ordinal.GetHashCode(text)
                        : value?.GetHashCode() ?? 0;
                    hash = hash * 31 + part;
                }
                return hash;
            }
        }

        public static int WithSequence<T>(int hash, IReadOnlyList<T> values)
        {
            unchecked
            {
                for (int i = 0; i < values.Count; i++)
                    hash = hash * 31 + (values[i]?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static bool SequenceEqual<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null || first.Count != second.Count) return false;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < first.Count; i++)
                if (!comparer.Equals(first[i], second[i])) return false;
            return true;
        }
    }
}
