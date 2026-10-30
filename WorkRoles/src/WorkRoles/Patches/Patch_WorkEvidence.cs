using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Capabilities;

namespace WorkRoles.Patches
{
    [HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.SetIdeo))]
    internal static class Patch_SetIdeo_WorkEvidence
    {
        private static void Prefix(Pawn ___pawn, out Ideo? __state) => __state = ___pawn.Ideo;
        private static void Postfix(Pawn ___pawn, Ideo? __state)
        {
            if (ReferenceEquals(__state, ___pawn.Ideo)) return;
            int revision = ExternalPawnFacts.Revisions.RevisionOf(___pawn);
            PawnWorkFacts.Refresh(___pawn);
            // Grouping consumes ideology even when both ideologies permit the
            // same work. Reuse the existing presentation refresh without
            // rebuilding skill facts or adding a polling dependency.
            if (ExternalPawnFacts.IsRelevant(___pawn)
                && ExternalPawnFacts.Revisions.RevisionOf(___pawn) == revision)
                ExternalPawnFacts.InvalidatePresentation(___pawn);
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.RecachePrecepts))]
    internal static class Patch_RecachePrecepts_WorkEvidence
    {
        private static void Postfix(Ideo __instance) => PawnWorkFacts.IdeologyChanged(__instance);
    }

    [HarmonyPatch(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty))]
    internal static class Patch_Capacities_WorkEvidence
    {
        private static void Postfix(Pawn ___pawn) => PawnWorkFacts.HealthChanged(___pawn);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class Patch_Faction_WorkEvidence
    {
        private static void Postfix(Pawn __instance) => PawnWorkFacts.Refresh(__instance);
    }
}
