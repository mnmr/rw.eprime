using EPrimeReadouts.UI;
using HarmonyLib;
using RimWorld;
using UnityEngine;

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
            // ResourceReadoutOnGUI is invoked for both Layout and Repaint.
            // We replace vanilla on Layout too, but have no layout work of our
            // own, so keep that call out of the panel pipeline entirely.
            if (Event.current.type != EventType.Layout)
                ReadoutPanel.OnGUI();
            return false;
        }
    }
}
