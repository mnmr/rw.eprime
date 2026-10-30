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
        private static bool IsColonyMember(Pawn pawn) =>
            pawn?.IsColonist == true || pawn?.IsSlaveOfColony == true
            || pawn != null && RoleStore.Current?.mechanicalWorkersEnabled == true
                && MechanicalWorkers.IsColonyWorker(pawn);

        private static void Prefix(Pawn __instance, out bool __state) =>
            __state = IsColonyMember(__instance);

        private static void Postfix(Pawn __instance, bool __state)
        {
            var store = RoleStore.Current;
            if (store != null)
            {
                if (__instance.Faction?.IsPlayer != true)
                    store.UnmanagePawn(__instance);
                else
                    Seeding.TryAutoAssignBasics(__instance);
            }

            // Roster membership is independent of assignment changes, including
            // roleless joiners/leavers and newly auto-assigned colonists.
            if (__state != IsColonyMember(__instance))
            {
                ExternalPawnFacts.Invalidate(__instance);
                UiVersion.RosterChanged();
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

    /// Ticked ageing and growth-vat ageing both land here once per year gained.
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Patch_PawnAgeTracker_BirthdayBiological
    {
        public static void Postfix(Pawn ___pawn, int birthdayAge) =>
            Seeding.ApplyBirthdayRoles(___pawn, birthdayAge);
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
