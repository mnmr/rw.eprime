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
        public static void Prefix(Pawn_MutantTracker __instance, out bool __state) =>
            __state = __instance.HasTurned;

        public static void Postfix(Pawn_MutantTracker __instance, Pawn ___pawn, bool __state)
        {
            if (__state || !__instance.HasTurned) return;
            RoleStore.Current?.UnmanagePawn(___pawn);
            // The tracker is installed before Turn, so IsColonist is already
            // false in the prefix. Observe the successful native transition,
            // independently of whether this pawn had any assignments to remove.
            if (__instance.Def.consideredSubhuman
                && (___pawn.Faction?.IsPlayer == true || ___pawn.IsSlaveOfColony))
            {
                ExternalPawnFacts.Invalidate(___pawn);
                UiVersion.RosterChanged();
            }
        }
    }

    /// Ticked ageing and growth-vat ageing both land here once per year gained.
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Patch_PawnAgeTracker_BirthdayBiological
    {
        public static void Prefix(Pawn ___pawn, out int __state) =>
            __state = ExternalPawnFacts.FactRevisions.RevisionOf(___pawn);

        public static void Postfix(Pawn ___pawn, int birthdayAge, int __state)
        {
            Seeding.ApplyBirthdayRoles(___pawn, birthdayAge);
            // Native work unlocks already refresh full facts. Eighteen (and
            // leaving the last inclusive gate at nineteen) need an age refresh
            // even when the standing assignments and native work stay equal.
            if (WorkRoles.Core.BirthdayRoles.CanChangeRoles(birthdayAge)
                && ExternalPawnFacts.IsRelevant(___pawn)
                && ExternalPawnFacts.FactRevisions.RevisionOf(___pawn) == __state)
                ExternalPawnFacts.InvalidateContext(___pawn);
        }
    }

    /// A reverted mutant rejoins the colony like any other joiner. Reverts that
    /// are part of dying (shambler cleanup) must not assign roles to a corpse.
    [HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Revert))]
    public static class Patch_PawnMutantTracker_Revert
    {
        public static void Prefix(Pawn_MutantTracker __instance, out bool __state) =>
            __state = __instance.HasTurned;

        public static void Postfix(Pawn_MutantTracker __instance, Pawn ___pawn,
            bool beingKilled, bool __state)
        {
            if (!__state || __instance.HasTurned) return;
            if (!beingKilled)
                Seeding.TryAutoAssignBasics(___pawn);
            if (__instance.Def.consideredSubhuman && (___pawn.IsColonist || ___pawn.IsSlaveOfColony))
            {
                ExternalPawnFacts.Invalidate(___pawn);
                UiVersion.RosterChanged();
            }
        }
    }
}
