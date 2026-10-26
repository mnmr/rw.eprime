using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace EPrimeReadouts
{
    internal static class CompatibilityWarnings
    {
        // The README's established conflicts, checked against upstream package
        // metadata and readout patches. Toggleable Readouts Continued keeps
        // the original package ID. Build once at the welcome lifecycle boundary;
        // the shared notice owns detection, acknowledgment and presentation.
        internal static WelcomeConflictNotice Current() => WelcomeDialog.FindConflicts(new[]
        {
            new WelcomeModConflict("garwel.readoutsplus", "EPR.CompatibilityReadoutsPlus".Translate()),
            new WelcomeModConflict("customresourcereadout.1trickpwnyta", "EPR.CompatibilityCustomResourceReadout".Translate()),
            new WelcomeModConflict("owlchemist.toggleablereadouts", "EPR.CompatibilityToggleableReadouts".Translate())
        });
    }
}
