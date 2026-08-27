using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.ClearAllPlayData))]
    public static class Patch_PlayDataLoader_ClearAllPlayData
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            DefinitionReloadCoordinator.ReleaseBeforeReload();
        }
    }

    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.HotReloadDefs))]
    public static class Patch_PlayDataLoader_HotReloadDefs
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            DefinitionReloadCoordinator.ReleaseBeforeReload();
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            DefinitionReloadCoordinator.QueueHotReloadWarm();
        }
    }

    /// Definition generations replace Def and Worker instances. Reset every
    /// owner only after all implied post-resolve work and other patches finish;
    /// the vanilla lifecycle invokes this on the main thread for startup,
    /// hot reload, language reinjection and newly generated definitions.
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PostResolve), typeof(bool))]
    public static class Patch_DefGenerator_GenerateImpliedDefs_PostResolve
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(bool hotReload)
        {
            DefinitionReloadCoordinator.DefinitionsRegenerated();
        }
    }
}
