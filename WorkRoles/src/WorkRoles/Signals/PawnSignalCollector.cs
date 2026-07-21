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
            new MoreThanCapableSignalProvider(),
        };

        private static readonly IReadOnlyList<Func<Pawn, IEnumerable<PawnSignal>>> Steps = BuildSteps();
        private static readonly HashSet<int> warnedProviders = new HashSet<int>();

        public static IReadOnlyList<PawnSignal> Collect(Pawn pawn) =>
            SignalCollection.Collect(pawn, Steps, ProviderFailed);

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
