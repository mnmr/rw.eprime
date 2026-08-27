using System;
using System.Collections.Generic;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    // Reserved for catalogued non-aptitude hediff mechanics. Aptitude hediffs
    // belong exclusively to AptitudeSignalProvider to avoid duplicate signals.
    internal sealed class HediffSignalProvider : ISignalProvider
    {
        public IEnumerable<PawnSignal> Collect(Pawn pawn) => Array.Empty<PawnSignal>();
    }
}
