using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal sealed class PassionSignalProvider : ISignalProvider
    {
        private readonly SignalCatalog catalog;

        internal PassionSignalProvider(SignalCatalog catalog = null)
        {
            this.catalog = catalog ?? SignalCatalog.Default;
        }

        public IEnumerable<PawnSignal> Collect(Pawn pawn)
        {
            var result = new List<PawnSignal>();
            if (pawn?.skills?.skills == null) return result;
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill == null || skill.TotallyDisabled || skill.passion == Passion.None) continue;
                string vanilla = skill.passion == Passion.Minor ? "Minor"
                    : skill.passion == Passion.Major ? "Major" : null;
                if (vanilla != null)
                {
                    SignalDefinition definition = catalog.Find(SignalSourceKind.Passion, vanilla).FirstOrDefault();
                    if (definition != null)
                        result.Add(SignalFactory.Instantiate(definition, skill.def.defName));
                    continue;
                }

                VseSignalReflection.PassionFact fact = VseSignalReflection.Passion(skill.passion);
                if (fact == null) continue;
                string packageId = SignalUiFactory.PackageId(fact.Def);
                if (PassionSignalDefinitions.IsExcludedTransientIdentity(packageId, fact.Def.defName))
                    continue;
                SignalDefinition known = catalog.Find(SignalSourceKind.Passion, fact.Def.defName)
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Source.PackageId, packageId));
                if (known != null)
                {
                    result.Add(SignalFactory.Instantiate(
                        known,
                        skill.def.defName,
                        ui: SignalUiFactory.ForDef(fact.Def, known, fact.IconPath,
                            fact.AuthorTier, fact.AuthorTier)));
                }
                else
                {
                    result.Add(UnknownPassive(skill, fact));
                }
            }
            return result;
        }

        private static PawnSignal UnknownPassive(SkillRecord skill, VseSignalReflection.PassionFact fact)
        {
            var effects = new List<SignalEffect>
            {
                new SignalEffect(SignalEffectKind.LearningRate, SignalOperation.Multiply,
                    fact.LearnRate, SignalValueUnit.Factor, "CurrentSkill"),
            };
            if (Math.Abs(fact.ForgetRate - 1f) > 0.00001f)
                effects.Add(new SignalEffect(SignalEffectKind.SkillDecay, SignalOperation.Multiply,
                    fact.ForgetRate, SignalValueUnit.Factor, "CurrentSkill"));
            if (Math.Abs(fact.OtherLearnRate - 1f) > 0.00001f)
                effects.Add(new SignalEffect(SignalEffectKind.LearningRate, SignalOperation.Multiply,
                    fact.OtherLearnRate, SignalValueUnit.Factor, "OtherSkills"));
            if (fact.IsBad)
                effects.Add(new SignalEffect(SignalEffectKind.WorkPreference,
                    SignalOperation.Descriptive, null, SignalValueUnit.None, "CurrentSkill",
                    new[] { new SignalCondition("author:is-bad", "The source mod marks this passion as bad") }));

            string packageId = SignalUiFactory.PackageId(fact.Def) ?? "unknown";
            string displayName = fact.Def.modContentPack?.Name ?? "Unknown mod";
            return new PawnSignal(
                SignalType.Passive,
                new SignalSource(SignalSourceKind.Passion, fact.Def.defName, packageId),
                skill.def.defName,
                effects,
                new SignalUi(fact.Def.LabelCap, fact.Def.description, fact.IconPath,
                    fact.AuthorTier, fact.AuthorTier, displayName));
        }
    }
}
