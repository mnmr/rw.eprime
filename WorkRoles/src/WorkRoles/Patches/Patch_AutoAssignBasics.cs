using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    /// Faction transitions: joiners (recruits, freed slaves, wanderers) get the
    /// auto-assign roles; pawns leaving the colony lose their role set.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Patch_Pawn_SetFaction
    {
        public static void Postfix(Pawn __instance)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            if (__instance.Faction != Faction.OfPlayer)
            {
                store.UnmanagePawn(__instance);
                return;
            }
            if (Current.ProgramState != ProgramState.Playing) return;
            Seeding.TryAutoAssignBasics(__instance);
        }
    }

    /// Covers pawns whose work settings initialize after spawn.
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
    public static class Patch_PawnWorkSettings_EnableAndInitialize
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            Seeding.TryAutoAssignBasics(___pawn);
            // A re-init on a managed pawn zeroed the dormant vanilla map and its
            // SetPriority rebuild was swallowed — restore the projection now.
            if (RoleStore.Current?.IsManaged(___pawn) == true)
            {
                CompiledJobOrders.Invalidate(___pawn);
                CompiledJobOrders.EnsureFresh(___pawn);
            }
        }
    }
}
