using HarmonyLib;
using Verse;

namespace WorkRoles
{
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.PostCorpseDestroy))]
    public static class Patch_Corpse_PostCorpseDestroy
    {
        public static void Postfix(Pawn pawn) => DeathRoles.NotifyCorpseDestroyed(pawn);
    }
}
