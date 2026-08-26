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
                if (__instance.Faction?.IsPlayer != true)
                    store.UnmanagePawn(__instance);
                else
                    Seeding.TryAutoAssignBasics(__instance);
            }

            // Role removal/auto-assignment normally invalidates the UI itself.
            // A roleless joiner or leaver changes roster membership without
            // touching role state, so provide exactly one fallback bump.
            if (__state.WasColonyMember != IsColonyMember(__instance))
            {
                ExternalPawnFacts.Invalidate(__instance);
                if (UiVersion.Current == __state.UiRevision)
                    UiVersion.Bump();
            }
        }
    }

    /// Covers pawns whose work settings initialize after spawn, and joiners
    /// generated mid-game (PawnGenerator initializes work settings for
    /// player-faction requests before the pawn spawns or boards anything).
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
    public static class Patch_PawnWorkSettings_EnableAndInitialize
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || ___pawn == null) return;
            Seeding.TryAutoAssignBasics(___pawn);
            // A re-init on a managed pawn zeroed the dormant vanilla map and its
            // SetPriority rebuild was swallowed — restore the projection now.
            // Deliberately not gated on ProgramState: a re-init during map
            // finalization must also be repaired, and a freshly generated pawn
            // can never be managed here.
            if (RoleStore.Current?.IsManaged(___pawn) == true)
            {
                CompiledJobOrders.Invalidate(___pawn);
                CompiledJobOrders.MirrorFreshVanillaFallback(___pawn);
            }
        }
    }

    /// Subhumans (ghouls) cannot hold work priorities, so a turned colonist is
    /// evicted at the mutation boundary: UnmanagePawn mirrors the current
    /// projection into the vanilla fallback before removing tracking, keeping
    /// memory and the next save in agreement (IsColonist is false for
    /// subhumans, so the persistence filter would drop the entry anyway).
    /// Turn/Revert run in deterministic simulation code on every peer.
    [HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Turn))]
    public static class Patch_PawnMutantTracker_Turn
    {
        public static void Postfix(Pawn ___pawn) =>
            RoleStore.Current?.UnmanagePawn(___pawn);
    }

    /// A reverted mutant rejoins the colony like any other joiner. Reverts that
    /// are part of dying (shambler cleanup) must not assign roles to a corpse.
    [HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Revert))]
    public static class Patch_PawnMutantTracker_Revert
    {
        public static void Postfix(Pawn ___pawn, bool beingKilled)
        {
            if (!beingKilled)
                Seeding.TryAutoAssignBasics(___pawn);
        }
    }
}
