using HarmonyLib;
using Verse;

namespace WorkRoles.Patches
{
    /// A language switch reloads play data in-session; every cache holding
    /// translated text (UI snapshots, composed tips, entry labels) must drop.
    [HarmonyPatch(typeof(LanguageDatabase), nameof(LanguageDatabase.SelectLanguage))]
    public static class Patch_LanguageDatabase_SelectLanguage
    {
        public static void Postfix()
        {
            UiVersion.Bump();
            JobSkillProfiles.ClearCaches();
            Patch_ActiveTip_TipRect.Clear();
            UI.RolesTabView.ClearEntryLabelCache();
        }
    }
}
