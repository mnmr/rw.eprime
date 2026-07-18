using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalDefinition
    {
        public SignalType Type { get; }
        public SignalSource Source { get; }
        public int? Degree { get; }
        public string SkillDefName { get; }
        public bool DerivesSkillFromSource { get; }
        public IReadOnlyList<SignalEffect> Effects { get; }
        public SignalUi FallbackUi { get; }
        public bool IsTransient { get; }
        public int StableOrder { get; }

        public SignalDefinition(
            SignalType type,
            SignalSource source,
            int? degree,
            string skillDefName,
            bool derivesSkillFromSource,
            IEnumerable<SignalEffect> effects,
            SignalUi fallbackUi,
            bool isTransient = false,
            int stableOrder = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (fallbackUi == null) throw new ArgumentNullException(nameof(fallbackUi));
            if (skillDefName != null && string.IsNullOrWhiteSpace(skillDefName))
                throw new ArgumentException("Skill defName cannot be blank when supplied.", nameof(skillDefName));
            if (derivesSkillFromSource && skillDefName != null)
                throw new ArgumentException("A derived skill definition cannot also have a fixed skill.", nameof(skillDefName));

            var effectCopy = new List<SignalEffect>();
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect == null) throw new ArgumentException("Effects cannot contain null.", nameof(effects));
                    effectCopy.Add(effect);
                }
            }
            if (type == SignalType.Active && effectCopy.Count == 0)
                throw new ArgumentException("Active definitions require at least one structured effect.", nameof(effects));

            Type = type;
            Source = source;
            Degree = degree;
            SkillDefName = skillDefName;
            DerivesSkillFromSource = derivesSkillFromSource;
            Effects = new ReadOnlyCollection<SignalEffect>(effectCopy);
            FallbackUi = fallbackUi;
            IsTransient = isTransient;
            StableOrder = stableOrder;
        }

        public bool IsPassiveSkillLevelFor(string skillDefName)
        {
            if (Type != SignalType.Passive
                || !StringComparer.Ordinal.Equals(SkillDefName, skillDefName))
                return false;
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i].Kind == SignalEffectKind.SkillLevel) return true;
            return false;
        }

        internal string Identity => string.Join("\u001f", new[]
        {
            ((int)Source.Kind).ToString(),
            Source.PackageId,
            Source.DefName,
            Degree?.ToString() ?? "",
            Source.EffectDiscriminator ?? "",
        });
    }

    public sealed class SignalUiOverride
    {
        public string Label { get; }
        public string Description { get; }
        public string IconKey { get; }
        public string AuthorTier { get; }
        public string ColorKey { get; }
        public string SourceDisplayName { get; }

        public SignalUiOverride(
            string label = null,
            string description = null,
            string iconKey = null,
            string authorTier = null,
            string colorKey = null,
            string sourceDisplayName = null)
        {
            Label = label;
            Description = description;
            IconKey = iconKey;
            AuthorTier = authorTier;
            ColorKey = colorKey;
            SourceDisplayName = sourceDisplayName;
        }
    }
}
