using EPrimeReadouts.UI;
using HarmonyLib;
using RimWorld;

namespace EPrimeReadouts.Patches
{
    /// Replaces the vanilla resource readout wholesale. The mod setting is an
    /// instant escape hatch back to vanilla behavior.
    [HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
    public static class Patch_ResourceReadout
    {
        public static bool Prefix()
        {
            if (EPrimeReadoutsMod.Settings.useVanillaReadout) return true;
            ReadoutPanel.OnGUI();
            return false;
        }
    }
}
