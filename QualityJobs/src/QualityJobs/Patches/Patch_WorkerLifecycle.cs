using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    // Membership changes are structural. Publish immediately, including while
    // paused; dynamic health/overseer facts retain the existing 250-tick fallback.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_WorkerSpawn
    {
        private static void Postfix(Pawn __instance)
        {
            if (WorkerCapabilities.IsColonyWorker(__instance))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    internal static class Patch_WorkerDespawn
    {
        private static void Postfix(Pawn __instance)
        {
            if (WorkerCapabilities.IsColonyWorker(__instance))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_WorkerDeath
    {
        // Kill marks the pawn dead before DeSpawn, so the despawn hook can no
        // longer recognize a worker. Capture membership before that transition.
        private static void Prefix(Pawn __instance, out bool __state)
            => __state = WorkerCapabilities.IsColonyWorker(__instance);

        private static void Postfix(Pawn __instance, bool __state)
        {
            if (__state && __instance.Dead)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class Patch_WorkerFaction
    {
        private static void Prefix(Pawn __instance, out Faction? __state)
            => __state = __instance.Faction;

        private static void Postfix(Pawn __instance, Faction? __state)
        {
            if (__instance.Faction != __state
                && (__state == Faction.OfPlayer || __instance.Faction == Faction.OfPlayer))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }
}
