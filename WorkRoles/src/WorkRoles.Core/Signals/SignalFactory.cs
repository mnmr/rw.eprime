using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Signals
{
    public static class SignalFactory
    {
        public static Signal Instantiate(
            SignalDefinition definition,
            string runtimeSkillDefName = null,
            string actualSourceDefName = null,
            float? currentScale = null,
            float scaleMultiplier = 1f,
            SignalUiOverride ui = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (float.IsNaN(scaleMultiplier) || float.IsInfinity(scaleMultiplier))
                throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));

            string skill = ResolveSkill(definition, runtimeSkillDefName);
            string actualDefName = string.IsNullOrWhiteSpace(actualSourceDefName)
                ? definition.Source.DefName
                : actualSourceDefName;
            string templateId = definition.Source.TemplateId;
            if (!StringComparer.Ordinal.Equals(actualDefName, definition.Source.DefName) && templateId == null)
                templateId = definition.Source.DefName;

            var source = new SignalSource(
                definition.Source.Kind,
                actualDefName,
                definition.Source.PackageId,
                templateId,
                definition.Source.EffectDiscriminator,
                definition.Source.RequiredPackageIds,
                definition.Degree);

            var effects = new List<SignalEffect>(definition.Effects.Count);
            foreach (var effect in definition.Effects)
            {
                float? instanceScale = effect.ScaleKind == SignalScaleKind.None
                    ? null
                    : currentScale;
                effects.Add(new SignalEffect(
                    effect.Kind,
                    effect.Operation,
                    effect.Magnitude,
                    effect.Unit,
                    effect.TargetDefName,
                    effect.Conditions,
                    effect.ScaleKind,
                    instanceScale,
                    scaleMultiplier,
                    effect.AlreadyReflected));
            }

            return new Signal(definition.Type, source, skill, effects, MergeUi(definition.FallbackUi, ui));
        }

        private static string ResolveSkill(SignalDefinition definition, string runtimeSkillDefName)
        {
            if (definition.DerivesSkillFromSource)
            {
                if (string.IsNullOrWhiteSpace(runtimeSkillDefName))
                    throw new ArgumentException("This definition requires a runtime skill defName.", nameof(runtimeSkillDefName));
                return runtimeSkillDefName;
            }

            if (definition.SkillDefName != null)
            {
                if (runtimeSkillDefName != null
                    && !StringComparer.Ordinal.Equals(definition.SkillDefName, runtimeSkillDefName))
                    throw new ArgumentException("Runtime skill conflicts with the fixed catalogue skill.", nameof(runtimeSkillDefName));
                return definition.SkillDefName;
            }

            if (runtimeSkillDefName != null && string.IsNullOrWhiteSpace(runtimeSkillDefName))
                throw new ArgumentException("Runtime skill defName cannot be blank.", nameof(runtimeSkillDefName));
            return runtimeSkillDefName;
        }

        private static SignalUi MergeUi(SignalUi fallback, SignalUiOverride value) => new SignalUi(
            Prefer(value?.Label, fallback.Label),
            Prefer(value?.Description, fallback.Description),
            Prefer(value?.IconKey, fallback.IconKey),
            Prefer(value?.AuthorTier, fallback.AuthorTier),
            Prefer(value?.ColorKey, fallback.ColorKey),
            Prefer(value?.SourceDisplayName, fallback.SourceDisplayName));

        private static string Prefer(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
