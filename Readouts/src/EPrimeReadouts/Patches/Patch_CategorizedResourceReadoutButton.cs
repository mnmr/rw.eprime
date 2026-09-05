using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Hides vanilla's categorized-resource toggle while the custom readout is
    /// active, unless the player keeps it: then the toggle shows or hides
    /// the readout's bands. Skipping WidgetRow.ToggleableIcon also avoids
    /// leaving an empty cell in the global-controls row.
    [HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
    public static class Patch_CategorizedResourceReadoutButton
    {
        public static bool Prefix(Texture2D tex)
        {
            ReadoutSettings settings = EPrimeReadoutsMod.Settings;
            return settings.useVanillaReadout
                || settings.keepReadoutToggle
                || !object.ReferenceEquals(tex, TexButton.CategorizedResourceReadout);
        }
    }
}
