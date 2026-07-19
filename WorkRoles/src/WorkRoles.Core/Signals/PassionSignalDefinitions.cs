using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public static class PassionSignalDefinitions
    {
        private const string Core = "Ludeon.RimWorld";
        private const string Vse = "vanillaexpanded.skills";
        private const string Alpha = "sarg.alphaskills";

        private static readonly HashSet<string> ExcludedTransientIdentities =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AS_YouthPassion",
                "AS_SanguinePassion",
                "AS_SanguinePassion_Active",
                "AS_ToxicPassion",
                "AS_ToxicPassion_Active",
            };

        public static readonly IReadOnlyList<SignalDefinition> All = Build();

        public static bool IsExcludedTransientIdentity(string packageId, string defName) =>
            StringComparer.OrdinalIgnoreCase.Equals(packageId, Alpha)
            && defName != null
            && ExcludedTransientIdentities.Contains(defName);

        private static IReadOnlyList<SignalDefinition> Build()
        {
            var result = new List<SignalDefinition>
            {
                Passion("Minor", Core, "RimWorld", SignalType.Active, "interested", 1f,
                    tier: "Minor", icon: "UI/Icons/PassionMinor",
                    extras: Mood("work:passionate", "Mood from doing passionate work")),
                Passion("Major", Core, "RimWorld", SignalType.Active, "burning", 1.5f,
                    tier: "Major", icon: "UI/Icons/PassionMajor",
                    extras: Mood("work:passionate", "Mood from doing passionate work")),

                Passion("VSE_Apathy", Vse, "Vanilla Skills Expanded", SignalType.Active,
                    "apathy", 0.25f, forget: 1.25f, isBad: true, tier: "Disabled",
                    icon: "Passions/PassionApathy"),
                Passion("VSE_Natural", Vse, "Vanilla Skills Expanded", SignalType.Active,
                    "natural", 2f, forget: 3f, tier: "Minor", icon: "Passions/PassionNatural"),
                Passion("VSE_Critical", Vse, "Vanilla Skills Expanded", SignalType.Active,
                    "critical", 3f, forget: 0f, other: 0.25f, tier: "Major",
                    icon: "Passions/PassionCritical",
                    otherConditions: Conditions("setting:vse-critical-affects-other-skills")),

                // Alpha Skills: persistent identities (Active).
                Passion("AS_DedicatedPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "dedicated", 1.25f, tier: "Minor", icon: "Passions/AS_DedicatedPassion"),
                Passion("AS_DuncePassion", Alpha, "Alpha Skills", SignalType.Active,
                    "dunce", 0f, isBad: true, tier: "Minor", icon: "Passions/AS_DuncePassion",
                    extras: new[] { Preference("work:enjoyment", "Strong enjoyment from related work") }),
                Passion("AS_ForbiddenPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "forbidden", 1.75f, forget: 0.75f, tier: "Major",
                    icon: "Passions/AS_ForbiddenPassion",
                    conditions: Conditions("age:min:18", "hediff:AS_ForbiddenPassion")),
                Passion("AS_FrozenPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "frozen", 0f, forget: 0f, isBad: true, tier: "Disabled",
                    icon: "Passions/AS_FrozenPassion"),
                Passion("AS_LikeMindedPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "like-minded", 1f, tier: "Major", icon: "Passions/AS_LikeMindedPassion",
                    extras: new[] { Preference("social:same-passion-opinion", "Improves opinion of pawns sharing the passion") }),
                Passion("AS_ObsessivePassion", Alpha, "Alpha Skills", SignalType.Active,
                    "obsessive", 1.25f, other: 0.9f, tier: "Minor",
                    icon: "Passions/AS_ObsessivePassion"),
                Passion("AS_SynergisticPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "synergistic", 1f, forget: 0.9f, other: 1.1f, tier: "Major",
                    icon: "Passions/AS_SynergisticPassion"),
                Passion("AS_TraumaticPassion", Alpha, "Alpha Skills", SignalType.Active,
                    "traumatic", 1f, isBad: true, tier: "Minor",
                    icon: "Passions/AS_TraumaticPassion",
                    conditions: Conditions("hediff:AS_TraumaticPassion"),
                    extras: new[] { Preference("work:compelled-aversion", "Hates the skill but feels compelled to learn it") }),

                // Alpha Skills: condition-changing identities (always Passive).
                Passion("AS_DrunkenPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "drunken", 1f, tier: "Major", icon: "Passions/AS_DrunkenPassion_Inactive",
                    conditions: Conditions("state:not-drunk"), transient: true),
                Passion("AS_DrunkenPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "drunken (active)", 2.5f, tier: "Major", icon: "Passions/AS_DrunkenPassion",
                    conditions: Conditions("state:drunk"), transient: true),
                Passion("AS_StonedPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "stoned", 1f, tier: "Major", icon: "Passions/AS_StonedPassion_Inactive",
                    conditions: Conditions("state:not-smokeleaf-high"), transient: true),
                Passion("AS_StonedPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "stoned (active)", 1.8f, tier: "Major", icon: "Passions/AS_StonedPassion",
                    conditions: Conditions("state:smokeleaf-high"), transient: true),
                Passion("AS_NightPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "night", 1f, tier: "Major", icon: "Passions/AS_NightPassion_Inactive",
                    conditions: Conditions("time:day", "hediff:AS_NightPassion"), transient: true),
                Passion("AS_NightPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "night (active)", 2.5f, tier: "Major", icon: "Passions/AS_NightPassion",
                    conditions: Conditions("time:20-5"), transient: true),
                Passion("AS_VengefulPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "vengeful", 1f, tier: "Major", icon: "Passions/AS_VengefulPassion_Inactive",
                    conditions: Conditions("colony:no-recent-loss", "hediff:AS_VengefulPassion_Hediff"), transient: true),
                Passion("AS_VengefulPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "vengeful (active)", 3f, tier: "Major", icon: "Passions/AS_VengefulPassion",
                    conditions: Conditions("colony:loss-within-3-days"), transient: true),
                Passion("AS_NomadicPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "nomadic", 1f, tier: "Major", icon: "Passions/AS_NomadicPassion_Inactive",
                    conditions: Conditions("location:home", "hediff:AS_NomadicPassion"), transient: true),
                Passion("AS_NomadicPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "nomadic (active)", 2.25f, tier: "Major", icon: "Passions/AS_NomadicPassion",
                    conditions: Conditions("location:away-from-home"), transient: true),
                Passion("AS_MoodyPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "moody (interested)", 1f, tier: "Minor", icon: "Passions/AS_MoodyPassion",
                    conditions: Conditions("cycle:daily", "mood-tier:interested"), transient: true),
                Passion("AS_MoodyPassion_Apathy", Alpha, "Alpha Skills", SignalType.Passive,
                    "moody (apathetic)", 0.1f, isBad: true, tier: "Disabled",
                    icon: "Passions/AS_MoodyPassion_Apathy",
                    conditions: Conditions("cycle:daily", "mood-tier:apathetic"), transient: true),
                Passion("AS_MoodyPassion_NoPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "moody", 0.35f, tier: "Main", icon: "Passions/AS_MoodyPassion_NoPassion",
                    conditions: Conditions("cycle:daily", "mood-tier:none"), transient: true),
                Passion("AS_MoodyPassion_Major", Alpha, "Alpha Skills", SignalType.Passive,
                    "moody (very interested)", 1.5f, tier: "Major", icon: "Passions/AS_MoodyPassion_Major",
                    conditions: Conditions("cycle:daily", "mood-tier:major"), transient: true),
                Passion("AS_MoodyPassion_Greater", Alpha, "Alpha Skills", SignalType.Passive,
                    "moody (passionate)", 2f, tier: "Major", icon: "Passions/AS_MoodyPassion_Greater",
                    conditions: Conditions("cycle:daily", "mood-tier:greater"), transient: true),
                Passion("AS_RainyDayPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "rainy day", 1f, tier: "Major", icon: "Passions/AS_RainyDayPassion_Off",
                    conditions: Conditions("weather:not-raining", "hediff:AS_RainyDayPassion"), transient: true),
                Passion("AS_RainyDayPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "rainy day (raining)", 2f, tier: "Major", icon: "Passions/AS_RainyDayPassion",
                    conditions: Conditions("weather:raining"), transient: true),
                Passion("AS_CompetitivePassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "competitive", 0.25f, tier: "Major", icon: "Passions/AS_CompetitivePassion_Off",
                    conditions: Conditions("colony:best-at-skill"), transient: true),
                Passion("AS_CompetitivePassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "competitive (active)", 2.5f, tier: "Major", icon: "Passions/AS_CompetitivePassion",
                    conditions: Conditions("colony:not-best-at-skill"), transient: true),
                Passion("AS_NudistPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "nudist", 0.5f, tier: "Major", icon: "Passions/AS_NudistPassion",
                    conditions: Conditions("trait:Nudist", "apparel:not-nude"), transient: true),
                Passion("AS_NudistPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "nudist (active)", 2f, tier: "Major", icon: "Passions/AS_NudistPassion_Active",
                    conditions: Conditions("trait:Nudist", "apparel:nude"), transient: true),
                Passion("AS_IntimatePassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "intimate", 0.75f, tier: "Major", icon: "Passions/AS_IntimatePassion",
                    conditions: Conditions("state:no-recent-lovin", "hediff:AS_IntimatePassion_Hediff"), transient: true),
                Passion("AS_IntimatePassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "intimate (active)", 1.75f, tier: "Major", icon: "Passions/AS_IntimatePassion_Active",
                    conditions: Conditions("state:recent-lovin"), transient: true),

                Passion("AS_PainDrivenPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "pain-driven", 1f, tier: "Minor", icon: "Passions/AS_PainDrivenPassion_Inactive",
                    conditions: Conditions("precept:Pain_Idealized", "pain:max:0.3", "hediff:AS_PainDrivenPassion"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_PainDrivenPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "pain-driven", 2f, tier: "Major", icon: "Passions/AS_PainDrivenPassion",
                    conditions: Conditions("precept:Pain_Idealized", "pain:min:0.3"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_IdeologicalPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "ideological", 1f, tier: "Major", icon: "Passions/AS_IdeologicalPassion",
                    conditions: Conditions("ritual:no-recent-quality-outcome", "hediff:AS_IdeologicalPassion_Hediff"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_IdeologicalPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "ideological (active)", 1.75f, tier: "Major", icon: "Passions/AS_IdeologicalPassion_Active",
                    conditions: Conditions("ritual:recent-quality-outcome"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_BlindPassion_Elevated", Alpha, "Alpha Skills", SignalType.Passive,
                    "blind, elevated", 0.5f, tier: "Minor", icon: "Passions/AS_BlindPassion",
                    conditions: Conditions("precept:Blindness_Elevated", "sight:not-blind", "hediff:AS_BlindPassion"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_BlindPassion_Elevated_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "blind, elevated (active)", 1.5f, tier: "Major", icon: "Passions/AS_BlindPassion_Active",
                    conditions: Conditions("precept:Blindness_Elevated", "sight:blind"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_BlindPassion_Sublime", Alpha, "Alpha Skills", SignalType.Passive,
                    "blind, sublime", 1f, tier: "Major", icon: "Passions/AS_BlindPassion",
                    conditions: Conditions("precept:Blindness_Sublime", "sight:not-blind", "hediff:AS_BlindPassion"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_BlindPassion_Sublime_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "blind, sublime (active)", 2f, tier: "Major", icon: "Passions/AS_BlindPassion_Active",
                    conditions: Conditions("precept:Blindness_Sublime", "sight:blind"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_TranshumanistPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "transhumanist", 0.5f, tier: "Minor", icon: "Passions/AS_TranshumanistPassion",
                    conditions: Conditions("precept:BodyMod_Approved", "body:no-artificial-parts", "hediff:AS_TranshumanistPassion"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),
                Passion("AS_TranshumanistPassion_Active", Alpha, "Alpha Skills", SignalType.Passive,
                    "transhumanist (active)", 1.5f, tier: "Major", icon: "Passions/AS_TranshumanistPassion_Active",
                    conditions: Conditions("precept:BodyMod_Approved", "body:artificial-parts"),
                    dependencies: Dependencies("Ludeon.RimWorld.Ideology"), transient: true),

                Passion("AS_PsychicPassion_Minor", Alpha, "Alpha Skills", SignalType.Passive,
                    "psychic (minor)", 0.5f, tier: "Minor", icon: "Passions/AS_PsychicPassion_Minor",
                    conditions: Conditions("psychic-sensitivity:tier:minor"),
                    dependencies: Dependencies("Ludeon.RimWorld.Royalty"), transient: true),
                Passion("AS_PsychicPassion_Nullified", Alpha, "Alpha Skills", SignalType.Passive,
                    "psychic (nullified)", 0.1f, isBad: true, tier: "Minor",
                    icon: "Passions/AS_PsychicPassion_Nullified",
                    conditions: Conditions("psychic-sensitivity:tier:nullified"),
                    dependencies: Dependencies("Ludeon.RimWorld.Royalty"), transient: true),
                Passion("AS_PsychicPassion", Alpha, "Alpha Skills", SignalType.Passive,
                    "psychic", 1f, tier: "Minor", icon: "Passions/AS_PsychicPassion",
                    conditions: Conditions("psychic-sensitivity:tier:normal"),
                    dependencies: Dependencies("Ludeon.RimWorld.Royalty"), transient: true),
                Passion("AS_PsychicPassion_Major", Alpha, "Alpha Skills", SignalType.Passive,
                    "psychic (major)", 1.5f, tier: "Major", icon: "Passions/AS_PsychicPassion_Major",
                    conditions: Conditions("psychic-sensitivity:tier:major"),
                    dependencies: Dependencies("Ludeon.RimWorld.Royalty"), transient: true),
                Passion("AS_PsychicPassion_Critical", Alpha, "Alpha Skills", SignalType.Passive,
                    "psychic (critical)", 2f, tier: "Major", icon: "Passions/AS_PsychicPassion_Critical",
                    conditions: Conditions("psychic-sensitivity:tier:critical"),
                    dependencies: Dependencies("Ludeon.RimWorld.Royalty"), transient: true),
            };

            return new ReadOnlyCollection<SignalDefinition>(result);
        }

        private static SignalDefinition Passion(
            string defName,
            string packageId,
            string sourceDisplayName,
            SignalType type,
            string label,
            float learn,
            float forget = 1f,
            float other = 1f,
            bool isBad = false,
            string tier = null,
            string icon = null,
            IEnumerable<SignalCondition> conditions = null,
            IEnumerable<SignalCondition> otherConditions = null,
            IEnumerable<string> dependencies = null,
            bool transient = false,
            IEnumerable<SignalEffect> extras = null)
        {
            var effects = new List<SignalEffect>
            {
                new SignalEffect(
                    SignalEffectKind.LearningRate,
                    SignalOperation.Multiply,
                    learn,
                    SignalValueUnit.Factor,
                    "CurrentSkill",
                    conditions),
            };
            if (Math.Abs(forget - 1f) > 0.00001f)
            {
                effects.Add(new SignalEffect(
                    SignalEffectKind.SkillDecay,
                    SignalOperation.Multiply,
                    forget,
                    SignalValueUnit.Factor,
                    "CurrentSkill",
                    conditions));
            }
            if (Math.Abs(other - 1f) > 0.00001f)
            {
                effects.Add(new SignalEffect(
                    SignalEffectKind.LearningRate,
                    SignalOperation.Multiply,
                    other,
                    SignalValueUnit.Factor,
                    "OtherSkills",
                    otherConditions ?? conditions));
            }
            if (isBad)
                effects.Add(Preference("author:is-bad", "The source mod marks this passion as bad"));
            if (extras != null) effects.AddRange(extras);

            var required = new List<string> { Vse };
            if (packageId != Alpha) required.Clear();
            if (dependencies != null)
                foreach (var dependency in dependencies)
                    if (!required.Contains(dependency)) required.Add(dependency);

            return new SignalDefinition(
                type,
                new SignalSource(SignalSourceKind.Passion, defName, packageId,
                    requiredPackageIds: required),
                degree: null,
                skillDefName: null,
                derivesSkillFromSource: true,
                effects: effects,
                fallbackUi: new SignalUi(label, null, icon, tier, tier, sourceDisplayName),
                isTransient: transient);
        }

        private static SignalEffect[] Mood(string key, string description) => new[]
        {
            new SignalEffect(
                SignalEffectKind.Mood,
                SignalOperation.Descriptive,
                null,
                SignalValueUnit.None,
                conditions: Conditions(key, description)),
        };

        private static SignalEffect Preference(string key, string description) =>
            new SignalEffect(
                SignalEffectKind.WorkPreference,
                SignalOperation.Descriptive,
                null,
                SignalValueUnit.None,
                conditions: Conditions(key, description));

        private static IReadOnlyList<SignalCondition> Conditions(params string[] values)
        {
            var result = new List<SignalCondition>();
            for (int i = 0; i < values.Length; i += 2)
            {
                string key = values[i];
                string description = i + 1 < values.Length && values[i + 1].IndexOf(':') < 0
                    ? values[++i]
                    : key;
                result.Add(new SignalCondition(key, description));
            }
            return result;
        }

        private static IReadOnlyList<string> Dependencies(params string[] values) => values;
    }
}
