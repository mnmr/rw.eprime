using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkRoles.Patches
{
    /// Native 1.6/1.7 and supported Misc. Robots work nodes assign scanner
    /// provenance but omit it for NonScanJob. Keep the actual known caller at
    /// these issue seams; never infer authority from a JobDef or the UI.
    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_WorkJobOrigin
    {
        private static readonly MethodInfo NonScan = AccessTools.Method(
            typeof(WorkGiver), nameof(WorkGiver.NonScanJob), new[] { typeof(Pawn) });
        private static readonly MethodInfo WithOrigin = AccessTools.Method(
            typeof(Patch_WorkJobOrigin), nameof(NonScanJobWithOrigin));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage));
            yield return AccessTools.Method(typeof(JobGiver_Work), "GiverTryGiveJobPrioritized");
            var robot = AccessTools.TypeByName("AIRobot.X2_JobGiver_Work");
            var robotIssue = robot == null ? null : AccessTools.Method(robot, "TryIssueJobPackage");
            if (robotIssue != null) yield return robotIssue;
            // QualityJobs' high-priority finish path walks native care givers
            // before its finish job. Its audited helper has the same single
            // NonScanJob seam and can issue patient/rest work for a managed pawn.
            var care = AccessTools.TypeByName("QualityJobs.Patches.CareWork");
            var careIssue = care == null ? null : AccessTools.Method(care, "TryGiver");
            if (careIssue != null) yield return careIssue;
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            int count = 0;
            for (int i = 0; i < code.Count; i++)
                if (code[i].Calls(NonScan)) count++;
            // Audited nodes have exactly one call each. A changed provider or
            // another transpiler can replace the seam: retain its original IL
            // and warn once during installation rather than guessing a stack.
            if (count != 1)
            {
                Log.Warning("[WorkRoles] Non-scan work provenance seam changed in "
                    + __originalMethod.DeclaringType?.FullName + "." + __originalMethod.Name
                    + "; leaving the provider unchanged.");
                return code;
            }
            for (int i = 0; i < code.Count; i++)
                if (code[i].Calls(NonScan))
                {
                    // Same stack inputs: instance + pawn become two explicit
                    // arguments. Retain labels/exception blocks on the call.
                    code[i].opcode = OpCodes.Call;
                    code[i].operand = WithOrigin;
                }
            return code;
        }

        internal static Job? NonScanJobWithOrigin(WorkGiver giver, Pawn pawn)
        {
            var job = giver.NonScanJob(pawn);
            if (job != null && job.workGiverDef == null
                && RoleStore.Current?.IsManaged(pawn) == true)
                job.workGiverDef = giver.def;
            return job;
        }
    }
}
