using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal sealed class AptitudeSignalProvider : ISignalProvider
    {
        private static readonly IReadOnlyDictionary<string, int> Templates =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["AptitudeTerrible"] = -8,
                ["AptitudePoor"] = -4,
                ["AptitudeStrong"] = 4,
                ["AptitudeRemarkable"] = 8,
            };

        private static readonly HashSet<string> warnedMismatches =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly SignalCatalog catalog;

        internal AptitudeSignalProvider(SignalCatalog catalog = null)
        {
            this.catalog = catalog ?? SignalCatalog.Default;
        }

        public IEnumerable<PawnSignal> Collect(Pawn pawn)
        {
            var result = new List<PawnSignal>();
            CollectGenes(pawn, result);
            CollectTraits(pawn, result);
            CollectHediffs(pawn, result);
            return result;
        }

        private void CollectGenes(Pawn pawn, List<PawnSignal> result)
        {
            if (pawn?.genes?.GenesListForReading == null) return;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene == null || !gene.Active || gene.def == null) continue;
                string template = TemplateOf(gene.def.defName);
                if (template == null) continue;
                if (!StringComparer.OrdinalIgnoreCase.Equals(
                        gene.def.modContentPack?.PackageId, "ludeon.rimworld.biotech"))
                    continue;

                if (gene.def.aptitudes == null || gene.def.aptitudes.Count != 1
                    || gene.def.aptitudes[0].skill == null
                    || gene.def.aptitudes[0].level != Templates[template]
                    || !VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(
                        gene.def.defName, template, gene.def.aptitudes[0].skill.defName))
                {
                    WarnMismatch(gene.def.defName);
                    continue;
                }

                Aptitude aptitude = gene.def.aptitudes[0];
                foreach (SignalDefinition definition in catalog.Find(SignalSourceKind.Gene, template))
                {
                    result.Add(SignalFactory.Instantiate(
                        definition,
                        aptitude.skill.defName,
                        gene.def.defName,
                        ui: SignalUiFactory.ForDef(gene.def, definition, gene.def.iconPath)));
                }
            }
        }

        private void CollectTraits(Pawn pawn, List<PawnSignal> result)
        {
            if (pawn?.story?.traits?.TraitsSorted == null) return;
            foreach (Trait trait in pawn.story.traits.TraitsSorted)
            {
                if (trait == null || trait.Suppressed || trait.CurrentData?.aptitudes == null) continue;
                foreach (Aptitude aptitude in trait.CurrentData.aptitudes)
                {
                    if (aptitude?.skill == null) continue;
                    foreach (SignalDefinition definition in catalog.Find(
                        SignalSourceKind.Trait, trait.def.defName, trait.Degree))
                    {
                        if (definition.Type != SignalType.Passive
                            || !StringComparer.Ordinal.Equals(definition.SkillDefName, aptitude.skill.defName)
                            || !definition.Effects.Any(x => x.Kind == SignalEffectKind.SkillLevel))
                            continue;
                        result.Add(SignalFactory.Instantiate(definition,
                            ui: TraitUi(trait, definition)));
                    }
                }
            }
        }

        private void CollectHediffs(Pawn pawn, List<PawnSignal> result)
        {
            if (pawn?.health?.hediffSet?.hediffs == null) return;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def?.aptitudes == null) continue;
                foreach (Aptitude aptitude in hediff.def.aptitudes)
                {
                    if (aptitude?.skill == null) continue;
                    foreach (SignalDefinition definition in catalog.Find(
                        SignalSourceKind.Hediff, hediff.def.defName))
                    {
                        if (!StringComparer.Ordinal.Equals(definition.SkillDefName, aptitude.skill.defName))
                            continue;
                        result.Add(SignalFactory.Instantiate(definition,
                            ui: SignalUiFactory.ForDef(hediff.def, definition)));
                    }
                }
            }
        }

        private static string TemplateOf(string defName)
        {
            if (defName == null) return null;
            foreach (string template in Templates.Keys)
                if (defName.StartsWith(template + "_", StringComparison.Ordinal)) return template;
            return null;
        }

        private static SignalUiOverride TraitUi(Trait trait, SignalDefinition definition) =>
            new SignalUiOverride(
                label: trait.LabelCap,
                description: trait.CurrentData.description,
                sourceDisplayName: SignalUiFactory.SourceDisplayName(trait.def, definition));

        private static void WarnMismatch(string defName)
        {
            if (warnedMismatches.Add(defName))
                Log.Warning("[WorkRoles] skipped generated aptitude gene with unexpected data: " + defName);
        }
    }
}
