using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    /// Covers recruits, freed slaves, wanderers — any pawn entering the player faction mid-game.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Patch_Pawn_SetFaction
    {
        public static void Postfix(Pawn __instance)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            Seeding.TryAssignRolesFromVanillaPriorities(__instance);
        }
    }

    /// Covers pawns whose work settings initialize after spawn.
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
    public static class Patch_PawnWorkSettings_EnableAndInitialize
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            Seeding.TryAssignRolesFromVanillaPriorities(___pawn);
        }
    }
}
