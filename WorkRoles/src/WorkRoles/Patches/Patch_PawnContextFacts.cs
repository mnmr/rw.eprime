using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    /// Health membership is observed only for definitions supplying captured
    /// skill aptitudes or mechanitor status. Ordinary injuries/severity updates
    /// retain the existing capability-only path. No periodic observation.
    internal readonly struct PawnHealthFactTransition
    {
        private readonly Pawn? pawn;
        private readonly HediffDef? definition;
        private readonly int count;
        private readonly int factRevision;
        private readonly int contextRevision;
        private readonly bool skills;

        internal PawnHealthFactTransition(Pawn owner, Hediff hediff)
        {
            pawn = null;
            definition = null;
            count = factRevision = contextRevision = 0;
            skills = hediff?.def?.aptitudes?.Count > 0;
            if ((!skills && !(hediff is Hediff_Mechlink))
                || !ExternalPawnFacts.IsRelevant(owner)) return;
            pawn = owner;
            definition = hediff!.def;
            count = Count(owner, definition);
            factRevision = ExternalPawnFacts.FactRevisions.RevisionOf(owner);
            contextRevision = ExternalPawnFacts.ContextRevisions.RevisionOf(owner);
        }

        internal void Publish()
        {
            if (pawn == null || definition == null || count == Count(pawn, definition)) return;
            // Nested callbacks may already have marked the same detached facts
            // dirty. Publication is deferred, so their refresh sees the final state.
            if (skills)
            {
                if (ExternalPawnFacts.FactRevisions.RevisionOf(pawn) == factRevision)
                    ExternalPawnFacts.Invalidate(pawn);
                PriorityGridFacts.Invalidate(pawn);
            }
            else if (ExternalPawnFacts.FactRevisions.RevisionOf(pawn) == factRevision
                && ExternalPawnFacts.ContextRevisions.RevisionOf(pawn) == contextRevision)
                ExternalPawnFacts.InvalidateContext(pawn);
        }

        private static int Count(Pawn owner, HediffDef def)
        {
            int result = 0;
            var hediffs = owner.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i].def == def) result++;
            return result;
        }
    }

    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    internal static class Patch_HediffSet_AddDirect_Facts
    {
        private static void Prefix(Pawn ___pawn, Hediff hediff,
            out PawnHealthFactTransition __state) =>
            __state = new PawnHealthFactTransition(___pawn, hediff);

        private static void Postfix(PawnHealthFactTransition __state) => __state.Publish();
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class Patch_HealthTracker_RemoveHediff_Facts
    {
        private static void Prefix(Pawn ___pawn, Hediff hediff,
            out PawnHealthFactTransition __state) =>
            __state = new PawnHealthFactTransition(___pawn, hediff);

        private static void Postfix(PawnHealthFactTransition __state) => __state.Publish();
    }

    /// Native ThingOwner notifications cover equip, drop, destruction and
    /// transfer. Primary ranged availability changes only on ranged-primary
    /// membership changes; unrelated equipment and melee swaps do no work.
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
    internal static class Patch_EquipmentAdded_Facts
    {
        internal static void Changed(Pawn pawn, ThingWithComps eq)
        {
            if (eq?.def?.equipmentType == EquipmentType.Primary
                && eq.def.IsRangedWeapon && ExternalPawnFacts.IsRelevant(pawn))
                ExternalPawnFacts.InvalidateContext(pawn);
        }

        private static void Postfix(Pawn ___pawn, ThingWithComps eq) => Changed(___pawn, eq);
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
    internal static class Patch_EquipmentRemoved_Facts
    {
        private static void Postfix(Pawn ___pawn, ThingWithComps eq) =>
            Patch_EquipmentAdded_Facts.Changed(___pawn, eq);
    }
}
