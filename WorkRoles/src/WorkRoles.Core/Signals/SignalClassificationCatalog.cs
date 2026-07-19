using System;
using System.Collections.Generic;
using WorkRoles.Core;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalClassificationCatalog
    {
        private const string Core = "Ludeon.RimWorld";
        private const string Biotech = "Ludeon.RimWorld.Biotech";
        private const string Anomaly = "Ludeon.RimWorld.Anomaly";
        private const string Vse = "vanillaexpanded.skills";
        private const string Alpha = "sarg.alphaskills";

        private readonly Dictionary<PolicyKey, SignalBucket> policies;

        public static readonly SignalClassificationCatalog Default = BuildDefault();

        private SignalClassificationCatalog(Dictionary<PolicyKey, SignalBucket> policies)
        {
            this.policies = policies;
        }

        public bool TryClassify(Signal signal, out SignalBucket bucket)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            if (signal.Type != SignalType.Active)
            {
                bucket = SignalBucket.Neutral;
                return false;
            }

            if (signal.Relation == SignalRelation.Primary
                && signal.Source.Kind == SignalSourceKind.Expertise)
            {
                bucket = SignalBucket.Exceptional;
                return true;
            }

            if (policies.TryGetValue(new PolicyKey(
                    signal.Relation,
                    signal.Source.Kind,
                    signal.Source.PackageId,
                    signal.Source.TemplateId ?? signal.Source.DefName,
                    signal.Source.Degree,
                    signal.Source.EffectDiscriminator),
                out bucket))
                return true;

            bucket = SignalBucket.Neutral;
            return false;
        }

        private static SignalClassificationCatalog BuildDefault()
        {
            var values = new Dictionary<PolicyKey, SignalBucket>();

            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Core, "Minor", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Core, "Major", SignalBucket.Great);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Vse, "VSE_Apathy", SignalBucket.Awful);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Vse, "VSE_Natural", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Vse, "VSE_Critical", SignalBucket.Exceptional);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_DedicatedPassion", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_DuncePassion", SignalBucket.Poor);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_ForbiddenPassion", SignalBucket.Great);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_FrozenPassion", SignalBucket.Poor);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_LikeMindedPassion", SignalBucket.Neutral);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_ObsessivePassion", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_SynergisticPassion", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Passion, Alpha, "AS_TraumaticPassion", SignalBucket.Poor);

            Add(values, SignalRelation.Spillover, SignalSourceKind.Passion, Vse, "VSE_Critical", SignalBucket.Poor);
            Add(values, SignalRelation.Spillover, SignalSourceKind.Passion, Alpha, "AS_ObsessivePassion", SignalBucket.Poor);
            Add(values, SignalRelation.Spillover, SignalSourceKind.Passion, Alpha, "AS_SynergisticPassion", SignalBucket.Strong);

            Add(values, SignalRelation.Primary, SignalSourceKind.Gene, Biotech, "MeleeDamage_Strong", SignalBucket.Strong);
            Add(values, SignalRelation.Primary, SignalSourceKind.Gene, Biotech, "MeleeDamage_Weak", SignalBucket.Poor);
            Add(values, SignalRelation.Primary, SignalSourceKind.Gene, Biotech, "Nearsighted", SignalBucket.Poor);

            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "Brawler", SignalBucket.Strong,
                0, "melee");
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "Brawler", SignalBucket.Poor,
                0, "shooting");
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "Nimble", SignalBucket.Strong, 0);
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "ShootingAccuracy", SignalBucket.Neutral, 1);
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "ShootingAccuracy", SignalBucket.Neutral, -1);
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Anomaly, "Occultist", SignalBucket.Strong, 0);
            Add(values, SignalRelation.Primary, SignalSourceKind.Trait, Core, "TorturedArtist", SignalBucket.Neutral, 0);

            return new SignalClassificationCatalog(values);
        }

        private static void Add(
            IDictionary<PolicyKey, SignalBucket> values,
            SignalRelation relation,
            SignalSourceKind kind,
            string packageId,
            string defName,
            SignalBucket bucket,
            int? degree = null,
            string discriminator = null)
        {
            var key = new PolicyKey(relation, kind, packageId, defName, degree, discriminator);
            if (values.ContainsKey(key))
                throw new InvalidOperationException("Duplicate signal classification policy: " + key);
            values.Add(key, bucket);
        }

        /// Allocation-free policy key: package ids compare case-insensitively,
        /// the rest ordinal — hot classification lookups never build strings.
        private readonly struct PolicyKey : IEquatable<PolicyKey>
        {
            private readonly SignalRelation relation;
            private readonly SignalSourceKind kind;
            private readonly string packageId;
            private readonly string defName;
            private readonly int? degree;
            private readonly string discriminator;

            public PolicyKey(
                SignalRelation relation,
                SignalSourceKind kind,
                string packageId,
                string defName,
                int? degree,
                string discriminator)
            {
                this.relation = relation;
                this.kind = kind;
                this.packageId = packageId ?? "";
                this.defName = defName ?? "";
                this.degree = degree;
                this.discriminator = discriminator ?? "";
            }

            public bool Equals(PolicyKey other) => relation == other.relation
                && kind == other.kind
                && StringComparer.OrdinalIgnoreCase.Equals(packageId, other.packageId)
                && StringComparer.Ordinal.Equals(defName, other.defName)
                && degree == other.degree
                && StringComparer.Ordinal.Equals(discriminator, other.discriminator);

            public override bool Equals(object obj) => obj is PolicyKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (int)relation;
                    hash = hash * 31 + (int)kind;
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(packageId);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(defName);
                    hash = hash * 31 + (degree ?? int.MinValue);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(discriminator);
                    return hash;
                }
            }

            public override string ToString() =>
                relation + "/" + kind + "/" + packageId + "/" + defName + "/" + degree + "/" + discriminator;
        }
    }
}
