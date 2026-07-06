using HarmonyLib;
using Verse;

namespace WorkRoles
{
    public class WorkRolesMod : Mod
    {
        public const string HarmonyId = "mnmr.workroles";

        public WorkRolesMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[WorkRoles] loaded");
        }
    }
}
