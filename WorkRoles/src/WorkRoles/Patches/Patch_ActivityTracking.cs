using HarmonyLib;
using Verse;
using Verse.AI;

namespace WorkRoles.Patches
{
    /// Job-transition signals for the current-activity display. StartJob and
    /// CleanupCurrentJob together cover every transition, including job-to-null
    /// and draft interrupts; the tracker ignores everything while no WorkRoles
    /// window is open. StartJob also stamps the issue-time rank baseline that
    /// reconciliation compares against when role state changes.
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_JobTracker_StartJob
    {
        public static void Postfix(Pawn ___pawn)
        {
            ActivityTracker.NotifyJobChanged(___pawn);
            JobRankBaseline.NotifyJobStarted(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    public static class Patch_JobTracker_CleanupCurrentJob
    {
        public static void Postfix(Pawn ___pawn) => ActivityTracker.NotifyJobChanged(___pawn);
    }
}
