using System.Collections.Generic;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal interface ISignalProvider
    {
        IEnumerable<Signal> Collect(Pawn pawn);
    }
}
