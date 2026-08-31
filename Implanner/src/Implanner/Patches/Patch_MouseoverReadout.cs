using HarmonyLib;
using Implanner.UI;
using Verse;

namespace Implanner.Patches
{
    /// Vanilla hides the bottom-left map mouseover readout only for open
    /// main tabs, so it keeps reporting cells underneath ordinary dialogs.
    /// The Implanner dialog is main-tab-sized, so it suppresses the readout
    /// the same way a main tab does. Prefix returning false skips the readout
    /// draw entirely while the dialog is open; everything else is untouched,
    /// and closing the dialog restores vanilla behavior (runtime-verified).
    /// The prefix runs every OnGUI frame for the whole session, so it reads
    /// the dialog's PreOpen/PostClose flag instead of scanning the window
    /// stack.
    [HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
    public static class Patch_MouseoverReadout
    {
        public static bool Prefix() => !Dialog_Implanner.AnyOpen;
    }
}
