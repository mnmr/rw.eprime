using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    public static class PawnSignalCollector
    {
        private static readonly ISignalProvider[] Providers =
        {
            new PassionSignalProvider(),
            new ExpertiseSignalProvider(),
            new AptitudeSignalProvider(),
            new GeneSignalProvider(),
            new TraitSignalProvider(),
            new HediffSignalProvider(),
        };

        private static readonly IReadOnlyList<Func<Pawn, IEnumerable<PawnSignal>>> Steps = BuildSteps();
        private static readonly HashSet<int> warnedProviders = new HashSet<int>();

        public static IReadOnlyList<PawnSignal> Collect(Pawn pawn) =>
            SignalCollection.Collect(pawn, Steps, ProviderFailed);

        /// Every mutable value read by PawnSignalSnapshots.Build or one of the
        /// providers. Collection order is stable game state; hashing it avoids
        /// retaining duplicate lists just to compare snapshots.
        internal static MutableSignalSignature Signature(Pawn pawn)
        {
            MutableSignalSignatureBuilder builder = MutableSignalSignatureBuilder.Start();
            builder.AddProviderCondition("pawn:has-skills", pawn?.skills != null);

            if (pawn?.skills?.skills != null)
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    if (skill == null) continue;
                    builder.AddSkill(skill.def?.defName,
                        !skill.TotallyDisabled, (int)skill.passion);
                    VseSignalReflection.AppendPassionSignature(
                        skill.passion, ref builder);
                }

            VseSignalReflection.AppendExpertiseSignature(pawn, ref builder);

            if (pawn?.story?.traits?.allTraits != null)
                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    if (trait?.def == null) continue;
                    builder.AddTrait(SignalUiFactory.PackageId(trait.def),
                        trait.def.defName, trait.Degree, trait.Suppressed);
                }

            if (pawn?.genes?.GenesListForReading != null)
                foreach (Gene gene in pawn.genes.GenesListForReading)
                {
                    if (gene?.def == null) continue;
                    builder.AddGene(SignalUiFactory.PackageId(gene.def),
                        gene.def.defName, gene.Active);
                }

            if (pawn?.health?.hediffSet?.hediffs != null)
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff?.def == null) continue;
                    builder.AddHediff(SignalUiFactory.PackageId(hediff.def),
                        hediff.def.defName, hediff.Severity, hediff.CurStageIndex,
                        hediff.Part?.def?.defName);
                }

            return builder.Build();
        }

        private static IReadOnlyList<Func<Pawn, IEnumerable<PawnSignal>>> BuildSteps()
        {
            var result = new Func<Pawn, IEnumerable<PawnSignal>>[Providers.Length];
            for (int i = 0; i < Providers.Length; i++)
            {
                ISignalProvider provider = Providers[i];
                result[i] = provider.Collect;
            }
            return result;
        }

        private static void ProviderFailed(int index, Exception exception)
        {
            if (warnedProviders.Add(index))
                Log.Warning("[WorkRoles] pawn signal provider " + Providers[index].GetType().Name
                    + " failed; other signals remain available: " + exception.Message);
        }
    }
}
