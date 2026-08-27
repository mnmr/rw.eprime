using HarmonyLib;
using Verse;

namespace WorkRoles.Patches
{
    /// SelectLanguage starts a worker-backed play-data reload. Invalidation is
    /// requested here and completed only after translated data is reinjected.
    [HarmonyPatch(typeof(LanguageDatabase), nameof(LanguageDatabase.SelectLanguage))]
    public static class Patch_LanguageDatabase_SelectLanguage
    {
        public static void Postfix()
        {
            LanguageChangeCoordinator.Request();
        }
    }

    /// Public late injection runs on the main thread after the selected
    /// language has replaced def labels and implied-def translations.
    [HarmonyPatch(typeof(LoadedLanguage), nameof(LoadedLanguage.InjectIntoData_AfterImpliedDefs))]
    public static class Patch_LoadedLanguage_InjectIntoData_AfterImpliedDefs
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            LanguageChangeCoordinator.Complete();
        }
    }
}
