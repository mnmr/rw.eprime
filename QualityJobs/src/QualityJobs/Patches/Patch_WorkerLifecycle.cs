using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace QualityJobs.Patches
{
    internal static class MechanicalCaravanAvailability
    {
        internal static bool Affected(Pawn pawn)
            => pawn.RaceProps.IsMechanoid && !MiscRobotSupport.IsRobot(pawn);

        internal static bool AnyAvailable(IReadOnlyList<Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
                if (Affected(pawns[i]) && WorkerCapabilities.Available(pawns[i])) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.StartFormingCaravan))]
    internal static class Patch_WorkerCaravanStarted
    {
        private static void Prefix(List<Pawn> pawns, out bool __state)
            => __state = MechanicalCaravanAvailability.AnyAvailable(pawns);

        private static void Postfix(List<Pawn> pawns, bool __state)
        {
            if (__state != MechanicalCaravanAvailability.AnyAvailable(pawns))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.StopFormingCaravan))]
    internal static class Patch_WorkerCaravanStopped
    {
        private static void Prefix(Lord lord, out Pawn[]? __state)
        {
            __state = null;
            // Native formation makes every mech in this lord unavailable.
            // Retain its members across RemoveLord, which releases ownership.
            if (MechanicalCaravanAvailability.AnyAvailable(lord.ownedPawns)) return;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
                if (MechanicalCaravanAvailability.Affected(lord.ownedPawns[i]))
                { __state = lord.ownedPawns.ToArray(); return; }
        }

        private static void Postfix(Pawn[]? __state)
        {
            if (__state != null && MechanicalCaravanAvailability.AnyAvailable(__state))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    internal static class Patch_WorkerDrafted
    {
        private static void Prefix(Pawn ___pawn, out bool __state)
            => __state = WorkerCapabilities.IsMechanical(___pawn) && WorkerCapabilities.Available(___pawn);

        private static void Postfix(Pawn ___pawn, bool __state)
        {
            if (WorkerCapabilities.IsMechanical(___pawn) && __state != WorkerCapabilities.Available(___pawn))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(MechanitorControlGroup), nameof(MechanitorControlGroup.SetWorkMode),
        new[] { typeof(MechWorkModeDef), typeof(GlobalTargetInfo) })]
    internal static class Patch_WorkerWorkMode
    {
        private static bool HasAvailableWorker(MechanitorControlGroup group)
        {
            var mechs = group.AssignedMechs;
            for (int i = 0; i < mechs.Count; i++)
                if (WorkerCapabilities.Available(mechs[i].pawn)) return true;
            return false;
        }

        private static void Prefix(MechanitorControlGroup __instance, out bool __state)
            => __state = HasAvailableWorker(__instance);

        private static void Postfix(MechanitorControlGroup __instance, bool __state)
        {
            if (__state != HasAvailableWorker(__instance))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(MechanitorControlGroup), nameof(MechanitorControlGroup.Assign))]
    internal static class Patch_WorkerControlGroup
    {
        private static void Prefix(Pawn pawn, out bool __state)
            => __state = WorkerCapabilities.Available(pawn);

        private static void Postfix(Pawn pawn, bool __state)
        {
            if (__state != WorkerCapabilities.Available(pawn))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

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
