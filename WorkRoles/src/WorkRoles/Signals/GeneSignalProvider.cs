using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal sealed class GeneSignalProvider : ISignalProvider
    {
        private readonly SignalCatalog catalog;

        internal GeneSignalProvider(SignalCatalog catalog = null)
        {
            this.catalog = catalog ?? SignalCatalog.Default;
        }

        public IEnumerable<PawnSignal> Collect(Pawn pawn)
        {
            var result = new List<PawnSignal>();
            if (pawn?.genes?.GenesListForReading == null) return result;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene == null || !gene.Active || gene.def == null) continue;
                foreach (SignalDefinition definition in catalog.Find(
                    SignalSourceKind.Gene, gene.def.defName))
                {
                    if (definition.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
                        continue;
                    result.Add(SignalFactory.Instantiate(definition,
                        ui: SignalUiFactory.ForDef(gene.def, definition, gene.def.iconPath)));
                }
            }
            return result;
        }
    }
}
