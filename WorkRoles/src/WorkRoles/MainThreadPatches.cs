using System;
using HarmonyLib;
using Verse;

namespace WorkRoles
{
    /// Harmony may initialize a target's static fields while patching it.
    /// Optional providers that own Unity assets must be patched during CallAll,
    /// never the loading-thread Mod constructor.
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MainThreadPatchAttribute : Attribute { }

    [StaticConstructorOnStartup]
    internal static class MainThreadPatches
    {
        static MainThreadPatches()
        {
            var harmony = new Harmony(WorkRolesMod.HarmonyId);
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(MainThreadPatches).Assembly))
                if (type.IsDefined(typeof(MainThreadPatchAttribute), false))
                    harmony.CreateClassProcessor(type).Patch();
        }
    }
}
