using EPrimeReadouts.UI;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Both native entry points use the same window as the readout's gear.
    /// Replace before WindowStack performs its normal open/focus lifecycle;
    /// every other mod and every other window retain vanilla behavior.
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class Patch_ModSettings
    {
        private static readonly AccessTools.FieldRef<Dialog_ModSettings, Mod> SelectedMod =
            AccessTools.FieldRefAccess<Dialog_ModSettings, Mod>("mod");

        private static void Prefix(ref Window window)
        {
            bool readouts = window is Dialog_ModSettings settings && SelectedMod(settings) is EPrimeReadoutsMod
                || window is Dialog_Options options && options.selectedMod is EPrimeReadoutsMod;
            if (readouts)
                window = new Dialog_ReadoutConfig(openOptions: true)
                {
                    forcePause = true,
                    absorbInputAroundWindow = true
                };
        }
    }
}
