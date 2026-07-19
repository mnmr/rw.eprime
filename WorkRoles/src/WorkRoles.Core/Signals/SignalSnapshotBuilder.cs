using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Signals
{
    public static class SignalSnapshotBuilder
    {
        private const string OtherSkills = "OtherSkills";
        private const string AuthorBad = "author:is-bad";

        public static SignalSnapshot Build(
            IEnumerable<Signal> signals,
            IEnumerable<string> enabledSkillDefNames,
            bool crossSkillEffectsEnabled,
            IEnumerable<string> persistentlyBadSkillDefNames = null)
        {
            if (signals == null) throw new ArgumentNullException(nameof(signals));
            if (enabledSkillDefNames == null)
                throw new ArgumentNullException(nameof(enabledSkillDefNames));

            var enabled = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string skill in enabledSkillDefNames)
                enabled.Add(SignalCondition.Required(skill, nameof(enabledSkillDefNames)));

            var collected = new List<Signal>();
            foreach (Signal signal in signals)
            {
                if (signal == null)
                    throw new ArgumentException("Signals cannot contain null.", nameof(signals));
                collected.Add(signal);
            }

            var persistentBadSkills = new HashSet<string>(StringComparer.Ordinal);
            if (persistentlyBadSkillDefNames != null)
                foreach (string skill in persistentlyBadSkillDefNames)
                    persistentBadSkills.Add(SignalCondition.Required(
                        skill, nameof(persistentlyBadSkillDefNames)));
            foreach (Signal signal in collected)
            {
                if (signal.Type == SignalType.Active
                    && signal.Relation == SignalRelation.Primary
                    && signal.Source.Kind == SignalSourceKind.Passion
                    && signal.SkillDefName != null
                    && HasCondition(signal, AuthorBad))
                    persistentBadSkills.Add(signal.SkillDefName);
            }

            var expanded = new List<Signal>(collected.Count);
            foreach (Signal signal in collected)
            {
                if (signal.Type != SignalType.Active
                    || signal.Relation != SignalRelation.Primary
                    || signal.SkillDefName == null)
                {
                    expanded.Add(signal);
                    continue;
                }

                var spilloverEffects = new List<SignalEffect>();
                var primaryEffects = new List<SignalEffect>();
                foreach (SignalEffect effect in signal.Effects)
                {
                    if (StringComparer.Ordinal.Equals(effect.TargetDefName, OtherSkills))
                        spilloverEffects.Add(effect);
                    else
                        primaryEffects.Add(effect);
                }
                if (spilloverEffects.Count == 0)
                {
                    expanded.Add(signal);
                    continue;
                }

                expanded.Add(new Signal(
                    signal.Type,
                    signal.Source,
                    signal.SkillDefName,
                    primaryEffects,
                    signal.Ui));

                foreach (string targetSkill in enabled)
                {
                    if (StringComparer.Ordinal.Equals(targetSkill, signal.SkillDefName)) continue;
                    if (!crossSkillEffectsEnabled && !persistentBadSkills.Contains(targetSkill)) continue;

                    var targetedEffects = new List<SignalEffect>(spilloverEffects.Count);
                    foreach (SignalEffect effect in spilloverEffects)
                        targetedEffects.Add(Target(effect, targetSkill));
                    expanded.Add(new Signal(
                        signal.Type,
                        signal.Source,
                        targetSkill,
                        targetedEffects,
                        signal.Ui,
                        signal.SkillDefName,
                        SignalRelation.Spillover));
                }
            }

            return new SignalSnapshot(expanded);
        }

        private static bool HasCondition(Signal signal, string key)
        {
            foreach (SignalEffect effect in signal.Effects)
                foreach (SignalCondition condition in effect.Conditions)
                    if (StringComparer.Ordinal.Equals(condition.Key, key)) return true;
            return false;
        }

        private static SignalEffect Target(SignalEffect effect, string skillDefName) =>
            new SignalEffect(
                effect.Kind,
                effect.Operation,
                effect.Magnitude,
                effect.Unit,
                skillDefName,
                scaleKind: effect.ScaleKind,
                currentScale: effect.CurrentScale,
                scaleMultiplier: effect.ScaleMultiplier,
                alreadyReflected: effect.AlreadyReflected);
    }
}
