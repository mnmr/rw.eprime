using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Hides vanilla's categorized-resource toggle while the custom readout is
    /// active. Skipping WidgetRow.ToggleableIcon also avoids leaving an empty
    /// cell in the global-controls row.
    [HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
    public static class Patch_CategorizedResourceReadoutButton
    {
        public static bool Prefix(Texture2D tex)
        {
            return EPrimeReadoutsMod.Settings.useVanillaReadout
                || !object.ReferenceEquals(tex, TexButton.CategorizedResourceReadout);
        }
    }
}
