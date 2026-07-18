using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public static class VanillaSignalDefinitions
    {
        private const string Core = "Ludeon.RimWorld";
        private const string Biotech = "Ludeon.RimWorld.Biotech";
        private const string Anomaly = "Ludeon.RimWorld.Anomaly";

        public static readonly IReadOnlyList<SignalDefinition> All = Build();

        public static bool IsGeneratedAptitudeIdentity(
            string actualDefName,
            string templateDefName,
            string skillDefName) =>
            !string.IsNullOrWhiteSpace(actualDefName)
            && !string.IsNullOrWhiteSpace(templateDefName)
            && !string.IsNullOrWhiteSpace(skillDefName)
            && StringComparer.Ordinal.Equals(
                actualDefName, templateDefName + "_" + skillDefName);

        private static IReadOnlyList<SignalDefinition> Build()
        {
            var result = new List<SignalDefinition>
            {
                Aptitude("AptitudeTerrible", "awful aptitude", -8f,
                    new SignalEffect(SignalEffectKind.Passion, SignalOperation.Disable, null,
                        SignalValueUnit.None, "CurrentSkill", alreadyReflected: true)),
                Aptitude("AptitudePoor", "poor aptitude", -4f),
                Aptitude("AptitudeStrong", "strong aptitude", 4f),
                Aptitude("AptitudeRemarkable", "great aptitude", 8f,
                    new SignalEffect(SignalEffectKind.Passion, SignalOperation.Add, 1f,
                        SignalValueUnit.PassionLevels, "CurrentSkill", alreadyReflected: true)),

                Gene("MeleeDamage_Strong", "strong melee damage", "Melee",
                    Effect(SignalEffectKind.Damage, SignalOperation.Multiply, 1.5f,
                        SignalValueUnit.Factor, "MeleeDamageFactor")),
                Gene("MeleeDamage_Weak", "weak melee damage", "Melee",
                    Effect(SignalEffectKind.Damage, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "MeleeDamageFactor")),
                Gene("Nearsighted", "nearsighted", "Shooting",
                    Effect(SignalEffectKind.Accuracy, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "ShootingAccuracyFactor_Medium"),
                    Effect(SignalEffectKind.Accuracy, SignalOperation.Multiply, 0.25f,
                        SignalValueUnit.Factor, "ShootingAccuracyFactor_Long")),
                Gene("Learning_Fast", "quick study", null,
                    Effect(SignalEffectKind.LearningRate, SignalOperation.Add, 0.5f,
                        SignalValueUnit.Factor, "GlobalLearningFactor")),
                Gene("Learning_Slow", "slow study", null,
                    Effect(SignalEffectKind.LearningRate, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "GlobalLearningFactor")),
                Gene("ViolenceDisabled", "violence disabled", null,
                    Disabled(SignalEffectKind.Capability, "Violent", "work-tag:Violent")),
                Gene("FireTerror", "pyrophobia", null,
                    Effect(SignalEffectKind.Mood, SignalOperation.Add, -10f,
                        SignalValueUnit.MoodPoints, "NearFire", "environment:near-fire"),
                    Effect(SignalEffectKind.MentalBreak, SignalOperation.Set, 0.1f,
                        SignalValueUnit.Days, "FireTerror", "environment:near-fire"),
                    Disabled(SignalEffectKind.Capability, "Pyromaniac", "trait:suppressed")),

                Trait("Brawler", 0, "brawler", "Melee", "melee", SignalType.Active,
                    ReflectedSkill(4f, "Melee"),
                    Effect(SignalEffectKind.HitChance, SignalOperation.Add, 4f,
                        SignalValueUnit.StatValue, "MeleeHitChance")),
                Trait("Brawler", 0, "brawler", "Shooting", "shooting", SignalType.Active,
                    ReflectedSkill(-10f, "Shooting"),
                    Descriptive(SignalEffectKind.WorkPreference, "RangedWeapon", "equipment:ranged-weapon"),
                    Descriptive(SignalEffectKind.Passion, "Shooting", "passion:conflicting")),
                Trait("Nimble", 0, "nimble", "Melee", null, SignalType.Active,
                    Effect(SignalEffectKind.Dodge, SignalOperation.Add, 15f,
                        SignalValueUnit.StatValue, "MeleeDodgeChance"),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Multiply, 0.1f,
                        SignalValueUnit.Factor, "PawnTrapSpringChance")),
                Trait("ShootingAccuracy", 1, "careful shooter", "Shooting", null, SignalType.Active,
                    Effect(SignalEffectKind.AimingDelay, SignalOperation.Add, 0.25f,
                        SignalValueUnit.StatValue, "AimingDelayFactor"),
                    Effect(SignalEffectKind.Accuracy, SignalOperation.Add, 5f,
                        SignalValueUnit.StatValue, "ShootingAccuracyPawn")),
                Trait("ShootingAccuracy", -1, "trigger-happy", "Shooting", null, SignalType.Active,
                    Effect(SignalEffectKind.AimingDelay, SignalOperation.Add, -0.5f,
                        SignalValueUnit.StatValue, "AimingDelayFactor"),
                    Effect(SignalEffectKind.Accuracy, SignalOperation.Add, -5f,
                        SignalValueUnit.StatValue, "ShootingAccuracyPawn")),
                Trait("FastLearner", 0, "fast learner", null, null, SignalType.Active,
                    Effect(SignalEffectKind.LearningRate, SignalOperation.Add, 0.75f,
                        SignalValueUnit.Factor, "GlobalLearningFactor")),
                Trait("SlowLearner", 0, "slow learner", null, null, SignalType.Active,
                    Effect(SignalEffectKind.LearningRate, SignalOperation.Add, -0.75f,
                        SignalValueUnit.Factor, "GlobalLearningFactor"),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "CertaintyLossFactor", "package:Ludeon.RimWorld.Ideology")),
                Trait("TooSmart", 0, "too smart", null, null, SignalType.Active,
                    Effect(SignalEffectKind.LearningRate, SignalOperation.Add, 0.75f,
                        SignalValueUnit.Factor, "GlobalLearningFactor"),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Add, 0.12f,
                        SignalValueUnit.StatValue, "MentalBreakThreshold"),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "CertaintyLossFactor", "package:Ludeon.RimWorld.Ideology")),
                Trait("GreatMemory", 0, "great memory", null, null, SignalType.Active,
                    Effect(SignalEffectKind.SkillDecay, SignalOperation.Multiply, 0.5f,
                        SignalValueUnit.Factor, "AllSkills")),
                Trait("PerfectMemory", 0, "perfect memory", null, null, SignalType.Active,
                    Disabled(SignalEffectKind.SkillDecay, "AllSkills"), packageId: Anomaly,
                    sourceDisplayName: "Anomaly"),
                Trait("Industriousness", 2, "industrious", null, null, SignalType.Active,
                    WorkSpeed(0.35f)),
                Trait("Industriousness", 1, "hard worker", null, null, SignalType.Active,
                    WorkSpeed(0.20f)),
                Trait("Industriousness", -1, "lazy", null, null, SignalType.Active,
                    WorkSpeed(-0.20f)),
                Trait("Industriousness", -2, "slothful", null, null, SignalType.Active,
                    WorkSpeed(-0.35f)),
                Trait("Neurotic", 1, "neurotic", null, null, SignalType.Active,
                    WorkSpeed(0.20f),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Add, 0.08f,
                        SignalValueUnit.StatValue, "MentalBreakThreshold")),
                Trait("Neurotic", 2, "very neurotic", null, null, SignalType.Active,
                    WorkSpeed(0.40f),
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Add, 0.14f,
                        SignalValueUnit.StatValue, "MentalBreakThreshold")),
                Trait("Occultist", 0, "occultist", "Intellectual", null, SignalType.Active,
                    Effect(SignalEffectKind.StatModifier, SignalOperation.Add, 1f,
                        SignalValueUnit.StatValue, "StudyEfficiency"),
                    Effect(SignalEffectKind.WorkSpeed, SignalOperation.Add, 0.5f,
                        SignalValueUnit.StatValue, "EntityStudyRate"),
                    packageId: Anomaly, sourceDisplayName: "Anomaly"),
                Trait("Pyromaniac", 0, "pyromaniac", null, null, SignalType.Active,
                    Disabled(SignalEffectKind.Capability, "Firefighting"),
                    Descriptive(SignalEffectKind.WorkPreference, "Fire", "environment:fire"),
                    Descriptive(SignalEffectKind.MentalBreak, "FireStartingSpree", "mental-break:fire-starting")),
                Trait("Gourmand", 0, "gourmand", "Cooking", null, SignalType.Passive,
                    ReflectedSkill(4f, "Cooking")),
                Trait("Immunity", -1, "sickly", "Medicine", null, SignalType.Passive,
                    ReflectedSkill(4f, "Medicine")),
                Trait("TorturedArtist", 0, "tortured artist", "Artistic", null, SignalType.Active,
                    Effect(SignalEffectKind.Passion, SignalOperation.Set, 1f,
                        SignalValueUnit.PassionLevels, "Artistic", alreadyReflected: true),
                    Descriptive(SignalEffectKind.Mood, "TorturedArtist", "mood:constant-debuff"),
                    Effect(SignalEffectKind.MentalBreak, SignalOperation.Set, 0.5f,
                        SignalValueUnit.Scalar, "Inspired_Creativity", "after:mental-break")),

                HediffAptitude("Animals"),
                HediffAptitude("Social"),
                HediffAptitude("Artistic"),
            };

            return new ReadOnlyCollection<SignalDefinition>(result);
        }

        private static SignalDefinition Aptitude(
            string template, string label, float value, params SignalEffect[] extras)
        {
            var effects = new List<SignalEffect> { ReflectedSkill(value, "CurrentSkill") };
            effects.AddRange(extras);
            return Definition(
                SignalType.Passive,
                SignalSourceKind.Gene,
                template,
                Biotech,
                "Biotech",
                null,
                null,
                true,
                effects,
                label,
                "UI/Icons/Genes/Skills/{skill}/" + template.Substring("Aptitude".Length));
        }

        private static SignalDefinition Gene(
            string defName, string label, string skill, params SignalEffect[] effects) =>
            Definition(SignalType.Active, SignalSourceKind.Gene, defName, Biotech, "Biotech",
                null, skill, false, effects, label, "UI/Icons/Genes/" + defName);

        private static SignalDefinition Trait(
            string defName,
            int degree,
            string label,
            string skill,
            string discriminator,
            SignalType type,
            SignalEffect first,
            params SignalEffect[] rest) =>
            Trait(defName, degree, label, skill, discriminator, type,
                Combine(first, rest), Core, "RimWorld");

        private static SignalDefinition Trait(
            string defName,
            int degree,
            string label,
            string skill,
            string discriminator,
            SignalType type,
            SignalEffect first,
            SignalEffect second,
            string packageId,
            string sourceDisplayName) =>
            Trait(defName, degree, label, skill, discriminator, type,
                new[] { first, second }, packageId, sourceDisplayName);

        private static SignalDefinition Trait(
            string defName,
            int degree,
            string label,
            string skill,
            string discriminator,
            SignalType type,
            SignalEffect first,
            string packageId,
            string sourceDisplayName) =>
            Trait(defName, degree, label, skill, discriminator, type,
                new[] { first }, packageId, sourceDisplayName);

        private static SignalDefinition Trait(
            string defName,
            int degree,
            string label,
            string skill,
            string discriminator,
            SignalType type,
            IEnumerable<SignalEffect> effects,
            string packageId,
            string sourceDisplayName) =>
            Definition(type, SignalSourceKind.Trait, defName, packageId, sourceDisplayName,
                degree, skill, false, effects, label, null, discriminator);

        private static SignalDefinition HediffAptitude(string skill) =>
            Definition(SignalType.Passive, SignalSourceKind.Hediff, "Inhumanized", Anomaly,
                "Anomaly", null, skill, false,
                new[] { ReflectedSkill(-12f, skill) }, "inhumanization", null,
                skill.ToLowerInvariant());

        private static SignalDefinition Definition(
            SignalType type,
            SignalSourceKind kind,
            string defName,
            string packageId,
            string sourceDisplayName,
            int? degree,
            string skill,
            bool derivesSkill,
            IEnumerable<SignalEffect> effects,
            string label,
            string icon,
            string discriminator = null) =>
            new SignalDefinition(
                type,
                new SignalSource(kind, defName, packageId, effectDiscriminator: discriminator),
                degree,
                skill,
                derivesSkill,
                effects,
                new SignalUi(label, null, icon, null, null, sourceDisplayName));

        private static SignalEffect ReflectedSkill(float value, string skill) =>
            Effect(SignalEffectKind.SkillLevel, SignalOperation.Add, value,
                SignalValueUnit.SkillLevels, skill, alreadyReflected: true);

        private static SignalEffect WorkSpeed(float value) =>
            Effect(SignalEffectKind.WorkSpeed, SignalOperation.Add, value,
                SignalValueUnit.StatValue, "WorkSpeedGlobal");

        private static SignalEffect Disabled(
            SignalEffectKind kind, string target, string condition = null) =>
            new SignalEffect(kind, SignalOperation.Disable, null, SignalValueUnit.None,
                target, Conditions(condition));

        private static SignalEffect Descriptive(
            SignalEffectKind kind, string target, string condition) =>
            new SignalEffect(kind, SignalOperation.Descriptive, null, SignalValueUnit.None,
                target, Conditions(condition));

        private static SignalEffect Effect(
            SignalEffectKind kind,
            SignalOperation operation,
            float value,
            SignalValueUnit unit,
            string target,
            string condition = null,
            bool alreadyReflected = false) =>
            new SignalEffect(kind, operation, value, unit, target,
                Conditions(condition), alreadyReflected: alreadyReflected);

        private static IReadOnlyList<SignalCondition> Conditions(string condition) =>
            condition == null ? null : new[] { new SignalCondition(condition, condition) };

        private static SignalEffect[] Combine(SignalEffect first, SignalEffect[] rest)
        {
            var result = new SignalEffect[rest.Length + 1];
            result[0] = first;
            rest.CopyTo(result, 1);
            return result;
        }
    }
}
