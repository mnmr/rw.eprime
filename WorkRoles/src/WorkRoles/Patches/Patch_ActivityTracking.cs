using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkRoles.Patches
{
    public readonly struct ActivityTransitionState
    {
        internal ActivityTransitionState(Pawn pawn, bool flag)
        {
            Pawn = pawn;
            Flag = flag;
            Revision = ActivityTracker.RevisionOf(pawn);
        }

        internal Pawn Pawn { get; }
        internal bool Flag { get; }
        internal int Revision { get; }
    }

    /// Event-backed signals for every live Pawn field consumed by the
    /// current-activity snapshot. StartJob and CleanupCurrentJob cover job
    /// identity; the draft and mental-state patches cover the higher-priority
    /// labels even when a transition does not replace the job. The tracker
    /// ignores everything while no WorkRoles window is open. StartJob also
    /// stamps the issue-time rank baseline that reconciliation compares against
    /// when role state changes.
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

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted),
        MethodType.Setter)]
    public static class Patch_DraftController_SetDrafted_Activity
    {
        public static void Prefix(Pawn_DraftController __instance,
            ref ActivityTransitionState __state)
            => __state = new ActivityTransitionState(__instance.pawn,
                __instance.Drafted);

        public static void Postfix(Pawn_DraftController __instance,
            ActivityTransitionState __state)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || __state.Flag == __instance.Drafted
                || ActivityTracker.RevisionOf(pawn) != __state.Revision)
                return;
            ActivityTracker.NotifyActivityChanged(pawn);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler),
        nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_MentalStateHandler_TryStart_Activity
    {
        public static void Prefix(Pawn ___pawn,
            ref ActivityTransitionState __state)
            => __state = new ActivityTransitionState(___pawn,
                ___pawn?.InMentalState == true);

        public static void Postfix(bool __result,
            ActivityTransitionState __state)
        {
            if (!__result || __state.Flag || __state.Pawn == null
                || ActivityTracker.RevisionOf(__state.Pawn)
                    != __state.Revision)
                return;
            ActivityTracker.NotifyActivityChanged(__state.Pawn);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), "ClearMentalStateDirect")]
    public static class Patch_MentalStateHandler_Clear_Activity
    {
        public static void Prefix(Pawn ___pawn,
            ref ActivityTransitionState __state)
            => __state = new ActivityTransitionState(___pawn,
                ___pawn?.InMentalState == true);

        public static void Postfix(ActivityTransitionState __state)
        {
            if (__state.Pawn == null || !__state.Flag
                || __state.Pawn.InMentalState
                || ActivityTracker.RevisionOf(__state.Pawn)
                    != __state.Revision)
                return;
            ActivityTracker.NotifyActivityChanged(__state.Pawn);
        }
    }
}
