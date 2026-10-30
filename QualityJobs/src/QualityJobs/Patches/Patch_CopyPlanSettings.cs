using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.Patches
{
    /// Capture copy settings as local input, attached to the exact native tool.
    /// Each placement sends its own settings; automatic rebuilds and other
    /// players never observe an armed world-wide copy flag.
    public static class CopyPlanPending
    {
        internal static void ArmFromSelection()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || Find.Selector == null
                || Find.DesignatorManager.SelectedDesignator is not Designator_Build designator) return;
            System.Collections.Generic.List<object> selected =
                Find.Selector.SelectedObjects;
            for (int i = 0; i < selected.Count; i++)
            {
                if (!(selected[i] is Thing thing)
                    || !store.TryGetPlanPresentation(thing.thingIDNumber,
                        out PlanPresentationSnapshot? plan)
                    || plan == null)
                    continue;
                CopyPlacement.Stage(designator, store, plan);
                return;
            }
        }
    }

    /// Arms copy settings only when the user invokes the vanilla copy command.
    /// ProcessInput is an input-event path, so selection traversal happens once
    /// per action rather than during every GetGizmos/render pass.
    [HarmonyPatch(typeof(Command_Action), nameof(Command_Action.ProcessInput))]
    public static class Patch_CopyPlanPending_ProcessInput
    {
        public static void Postfix(Command_Action __instance, Event ev)
        {
            if (__instance.hotKey != KeyBindingDefOf.Misc11) return;
            // Selecting the copy designator first deselects the old one. Arm
            // only after that transition, while the source remains selected.
            if (Find.DesignatorManager.SelectedDesignator is not Designator_Build) return;
            CopyPlanPending.ArmFromSelection();
        }
    }

    /// Deselect releases only this client's pending input. It cannot clear
    /// another player's already-serialized placement settings.
    [HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
    public static class Patch_CopyPlanPending_Deselect
    {
        public static void Postfix() => CopyPlacement.ClearInput();
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.DesignateSingleCell))]
    internal static class Patch_CopyPlanPlacement_Single
    {
        // MP's native prefix otherwise serializes only its native designator
        // fields. Capture the complete command before it suppresses input.
        [HarmonyPriority(Priority.First), HarmonyBefore("multiplayer")]
        private static bool Prefix(Designator_Build __instance, IntVec3 c)
            => !CopyPlacement.TryIssue(__instance, new[] { c }, multiCell: false);
    }

    [HarmonyPatch(typeof(Designator), nameof(Designator.DesignateMultiCell))]
    internal static class Patch_CopyPlanPlacement_Multi
    {
        [HarmonyPriority(Priority.First), HarmonyBefore("multiplayer")]
        private static bool Prefix(Designator __instance,
            System.Collections.Generic.IEnumerable<IntVec3> cells)
            => __instance is not Designator_Build build
                || !CopyPlacement.TryIssue(build, cells, multiCell: true);
    }
}
