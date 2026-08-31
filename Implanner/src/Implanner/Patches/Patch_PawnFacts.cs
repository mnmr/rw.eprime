using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Implanner.Patches
{
    /// The reliable event seams for Implanner evaluation and gear-display
    /// inputs: apparel and equipment tracker changes, hediff add/remove, and
    /// roster membership. Each bumps the facts revision only for pawns that
    /// could be enlisted, keeping the boundary work minimal.
    internal static class PawnFactsTransitions
    {
        internal static void BumpFor(Pawn? pawn)
        {
            if (pawn?.Faction?.IsPlayer == true)
                ExternalPawnFacts.Bump();
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelAdded))]
    public static class Patch_ApparelAdded_PawnFacts
    {
        public static void Postfix(Pawn_ApparelTracker __instance) =>
            PawnFactsTransitions.BumpFor(__instance.pawn);
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelRemoved))]
    public static class Patch_ApparelRemoved_PawnFacts
    {
        public static void Postfix(Pawn_ApparelTracker __instance) =>
            PawnFactsTransitions.BumpFor(__instance.pawn);
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
    public static class Patch_EquipmentAdded_PawnFacts
    {
        public static void Postfix(Pawn_EquipmentTracker __instance) =>
            PawnFactsTransitions.BumpFor(__instance.pawn);
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
    public static class Patch_EquipmentRemoved_PawnFacts
    {
        public static void Postfix(Pawn_EquipmentTracker __instance) =>
            PawnFactsTransitions.BumpFor(__instance.pawn);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult))]
    public static class Patch_HediffAdded_PawnFacts
    {
        public static void Postfix(Pawn ___pawn) =>
            PawnFactsTransitions.BumpFor(___pawn);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    public static class Patch_HediffRemoved_PawnFacts
    {
        public static void Postfix(Pawn ___pawn) =>
            PawnFactsTransitions.BumpFor(___pawn);
    }

    /// Roster membership: spawn, despawn, faction change, caravan moves.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_PawnSpawned_PawnFacts
    {
        public static void Postfix(Pawn __instance) =>
            PawnFactsTransitions.BumpFor(__instance);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public static class Patch_PawnDeSpawned_PawnFacts
    {
        public static void Prefix(Pawn __instance) =>
            PawnFactsTransitions.BumpFor(__instance);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Patch_PawnFaction_PawnFacts
    {
        public static void Prefix(Pawn __instance) =>
            PawnFactsTransitions.BumpFor(__instance);

        public static void Postfix(Pawn __instance) =>
            PawnFactsTransitions.BumpFor(__instance);
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.Notify_PawnAdded))]
    public static class Patch_CaravanPawnAdded_PawnFacts
    {
        public static void Postfix(Pawn p) =>
            PawnFactsTransitions.BumpFor(p);
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.Notify_PawnRemoved))]
    public static class Patch_CaravanPawnRemoved_PawnFacts
    {
        public static void Postfix(Pawn p) =>
            PawnFactsTransitions.BumpFor(p);
    }
}
