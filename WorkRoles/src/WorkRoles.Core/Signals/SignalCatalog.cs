using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalCatalog
    {
        private static readonly IReadOnlyList<SignalDefinition> Empty =
            new ReadOnlyCollection<SignalDefinition>(new List<SignalDefinition>());

        private readonly Dictionary<LookupKey, IReadOnlyList<SignalDefinition>> bySource;

        public static readonly SignalCatalog Default = new SignalCatalog(DefaultDefinitions());

        public IReadOnlyList<SignalDefinition> All { get; }

        public SignalCatalog(IEnumerable<SignalDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var all = new List<SignalDefinition>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var grouped = new Dictionary<LookupKey, List<SignalDefinition>>();
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Definitions cannot contain null.", nameof(definitions));
                if (!identities.Add(definition.Identity))
                    throw new ArgumentException("Duplicate signal definition identity: " + definition.Identity, nameof(definitions));

                all.Add(definition);
                var key = new LookupKey(definition.Source.Kind, definition.Source.DefName, definition.Degree);
                if (!grouped.TryGetValue(key, out var values))
                    grouped[key] = values = new List<SignalDefinition>();
                values.Add(definition);
            }

            all.Sort(CompareDefinitions);
            All = new ReadOnlyCollection<SignalDefinition>(all);
            bySource = new Dictionary<LookupKey, IReadOnlyList<SignalDefinition>>();
            foreach (var pair in grouped)
            {
                pair.Value.Sort(CompareDefinitions);
                bySource[pair.Key] = new ReadOnlyCollection<SignalDefinition>(pair.Value);
            }
        }

        public IReadOnlyList<SignalDefinition> Find(
            SignalSourceKind kind,
            string defName,
            int? degree = null)
        {
            if (string.IsNullOrWhiteSpace(defName)) return Empty;
            return bySource.TryGetValue(new LookupKey(kind, defName, degree), out var values)
                ? values
                : Empty;
        }

        private static IEnumerable<SignalDefinition> DefaultDefinitions()
        {
            foreach (var definition in PassionSignalDefinitions.All) yield return definition;
            foreach (var definition in ExpertiseSignalDefinitions.All) yield return definition;
            foreach (var definition in VanillaSignalDefinitions.All) yield return definition;
        }

        private static int CompareDefinitions(SignalDefinition x, SignalDefinition y)
        {
            int compare = x.StableOrder.CompareTo(y.StableOrder);
            if (compare != 0) return compare;
            compare = StringComparer.Ordinal.Compare(x.Source.EffectDiscriminator, y.Source.EffectDiscriminator);
            if (compare != 0) return compare;
            return StringComparer.Ordinal.Compare(x.SkillDefName, y.SkillDefName);
        }

        private readonly struct LookupKey : IEquatable<LookupKey>
        {
            private readonly SignalSourceKind kind;
            private readonly string defName;
            private readonly int? degree;

            public LookupKey(SignalSourceKind kind, string defName, int? degree)
            {
                this.kind = kind;
                this.defName = defName;
                this.degree = degree;
            }

            public bool Equals(LookupKey other) => kind == other.kind
                && StringComparer.Ordinal.Equals(defName, other.defName)
                && degree == other.degree;

            public override bool Equals(object obj) => obj is LookupKey other && Equals(other);

            // Hand-rolled: SignalHash.Of boxes, and this runs on every Find().
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (int)kind;
                    hash = hash * 31 + (defName == null
                        ? 0 : StringComparer.Ordinal.GetHashCode(defName));
                    hash = hash * 31 + (degree ?? int.MinValue);
                    return hash;
                }
            }
        }
    }
}
