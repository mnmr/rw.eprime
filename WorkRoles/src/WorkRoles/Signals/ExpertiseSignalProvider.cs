using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal sealed class ExpertiseSignalProvider : ISignalProvider
    {
        private readonly SignalCatalog catalog;

        internal ExpertiseSignalProvider(SignalCatalog catalog = null)
        {
            this.catalog = catalog ?? SignalCatalog.Default;
        }

        public IEnumerable<Signal> Collect(Pawn pawn)
        {
            var result = new List<Signal>();
            foreach (VseSignalReflection.ExpertiseFact fact in VseSignalReflection.Expertises(pawn))
            {
                string packageId = SignalUiFactory.PackageId(fact.Def);
                SignalDefinition definition = catalog.Find(SignalSourceKind.Expertise, fact.Def.defName)
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Source.PackageId, packageId));
                if (definition == null || !StringComparer.Ordinal.Equals(definition.SkillDefName, fact.Skill.defName))
                    continue;
                result.Add(SignalFactory.Instantiate(
                    definition,
                    runtimeSkillDefName: fact.Skill.defName,
                    currentScale: fact.Level,
                    scaleMultiplier: fact.StatMultiplier,
                    ui: SignalUiFactory.ForDef(fact.Def, definition, description: fact.FullDescription)));
            }
            return result;
        }
    }
}
