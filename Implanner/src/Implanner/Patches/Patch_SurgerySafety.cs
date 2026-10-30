using System;
using HarmonyLib;
using Verse.AI;

namespace Implanner.Patches
{
    /// A single check before anesthesia and again before the game splits or
    /// consumes ingredients. ApplyOnPawn is too late: vanilla consumes the
    /// implant first. One wrapper is owned by each newly constructed job
    /// toil, released with that job; no repeated-tick or render polling.
    internal sealed class SurgerySafetyAction
    {
        private readonly Toil toil;
        private readonly Action? original;
        private SurgerySafetyAction(Toil toil) { this.toil = toil; original = toil.initAction; }
        internal static void Wrap(Toil toil) => toil.initAction = new SurgerySafetyAction(toil).Invoke;
        private void Invoke()
        {
            var actor = toil.actor;
            var job = actor.CurJob;
            if (job != null && !ImplantSurgerySafety.CanOperate(actor, job))
            {
                actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }
            original?.Invoke();
        }
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))]
    internal static class Patch_SurgeryWorkSafety
    {
        static void Postfix(Toil __result) => SurgerySafetyAction.Wrap(__result);
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct))]
    internal static class Patch_SurgeryCompletionSafety
    {
        static void Postfix(Toil __result) => SurgerySafetyAction.Wrap(__result);
    }
}
