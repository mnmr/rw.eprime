using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal sealed class TraitSignalProvider : ISignalProvider
    {
        private readonly SignalCatalog catalog;

        internal TraitSignalProvider(SignalCatalog catalog = null)
        {
            this.catalog = catalog ?? SignalCatalog.Default;
        }

        public IEnumerable<PawnSignal> Collect(Pawn pawn)
        {
            var result = new List<PawnSignal>();
            if (pawn?.story?.traits?.TraitsSorted == null) return result;
            foreach (Trait trait in pawn.story.traits.TraitsSorted)
            {
                if (trait == null || trait.Suppressed || trait.def == null) continue;
                foreach (SignalDefinition definition in catalog.Find(
                    SignalSourceKind.Trait, trait.def.defName, trait.Degree))
                {
                    if (IsCollectedAsAptitude(trait, definition)) continue;
                    result.Add(SignalFactory.Instantiate(definition,
                        ui: new SignalUiOverride(
                            label: trait.LabelCap,
                            description: trait.CurrentData.description,
                            sourceDisplayName: SignalUiFactory.SourceDisplayName(trait.def, definition))));
                }
            }
            return result;
        }

        private static bool IsCollectedAsAptitude(Trait trait, SignalDefinition definition)
        {
            if (trait.CurrentData?.aptitudes == null) return false;
            foreach (Aptitude aptitude in trait.CurrentData.aptitudes)
                if (aptitude?.skill != null
                    && definition.IsPassiveSkillLevelFor(aptitude.skill.defName))
                    return true;
            return false;
        }
    }
}
