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
        private readonly struct TransitionState
        {
            internal TransitionState(bool wasColonyMember, int uiRevision)
            {
                WasColonyMember = wasColonyMember;
                UiRevision = uiRevision;
            }

            internal bool WasColonyMember { get; }
            internal int UiRevision { get; }
        }

        private static bool IsColonyMember(Pawn pawn) =>
            pawn?.IsColonist == true || pawn?.IsSlaveOfColony == true;

        private static void Prefix(Pawn __instance, out TransitionState __state) =>
            __state = new TransitionState(
                IsColonyMember(__instance), UiVersion.Current);

        private static void Postfix(Pawn __instance, TransitionState __state)
        {
            var store = RoleStore.Current;
            if (store != null)
            {
                if (__instance.Faction != Faction.OfPlayer)
                    store.UnmanagePawn(__instance);
                else if (Current.ProgramState == ProgramState.Playing)
                    Seeding.TryAutoAssignBasics(__instance);
            }

            // Role removal/auto-assignment normally invalidates the UI itself.
            // A roleless joiner or leaver changes roster membership without
            // touching role state, so provide exactly one fallback bump.
            if (__state.WasColonyMember != IsColonyMember(__instance)
                && UiVersion.Current == __state.UiRevision)
                UiVersion.Bump();
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
