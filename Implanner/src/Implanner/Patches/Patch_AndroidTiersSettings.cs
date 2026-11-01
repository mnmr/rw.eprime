using HarmonyLib;
using Verse;

namespace Implanner.Patches
{
    /// Android Tiers' "androids can only be healed by crafters" setting only
    /// changes in its settings window, and the game writes a mod's settings
    /// through Mod.WriteSettings when that window closes (Dialog_ModSettings
    /// and the Options mod list). Android Tiers does not override it, so this
    /// postfix sees every change; Implanner then follows the setting.
    [HarmonyPatch(typeof(Mod), nameof(Mod.WriteSettings))]
    public static class Patch_AndroidTiersSettings
    {
        public static void Postfix(Mod __instance)
        {
            if (AndroidTiers.IsAndroidTiersMod(__instance))
                AndroidTiers.FollowSetting();
        }
    }
}
