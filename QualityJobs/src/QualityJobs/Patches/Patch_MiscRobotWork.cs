using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// AIRobot has a separate work node, not a JobGiver_Work subclass. Hook
    /// that node so its existing recharge, fire, queued-order and timetable
    /// decisions remain ahead of QJ. Normal priority uses generated givers
    /// already included by AIRobot.GetWorkGivers and needs no replacement.
    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotWork
    {
        private static bool Prepare()
            => AccessTools.TypeByName("AIRobot.X2_JobGiver_Work") != null;

        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_JobGiver_Work"),
                "TryIssueJobPackage");

        private static bool Prefix(ThinkNode __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (!MiscRobotSupport.IsRobot(pawn) || !pawn.Spawned || pawn.Destroyed
                || pawn.HasAttachment(ThingDefOf.Fire)) return true;
            return Patch_FinisherPriority.TryIssue(__instance, pawn, ref __result);
        }
    }
}
