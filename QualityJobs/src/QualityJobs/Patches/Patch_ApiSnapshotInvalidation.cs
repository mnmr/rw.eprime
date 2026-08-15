using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    [HarmonyPatch]
    internal static class Patch_ApiBillStackInvalidation
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.AddBill));
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.Delete));
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.Clear));
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.Reorder));
        }

        private static void Postfix()
            => QualityJobsStore.Active?.InvalidateManagedJobs();
    }

    /// <summary>
    /// Suspension is edited directly by vanilla's bill-row UI. Compare one
    /// scalar around the row and enqueue an API rebuild only for a real edit;
    /// the snapshot builder never runs inside OnGUI.
    /// </summary>
    [HarmonyPatch(typeof(Bill), nameof(Bill.DoInterface))]
    internal static class Patch_ApiBillSuspensionInvalidation
    {
        private static void Prefix(Bill __instance, out bool __state)
            => __state = __instance.suspended;

        private static void Postfix(Bill __instance, bool __state)
        {
            if (__state != __instance.suspended)
                QualityJobsStore.Active?.InvalidateManagedJobs();
        }
    }

    /// <summary>
    /// Vanilla owns repeat-mode/count editing inside Dialog_BillConfig. The
    /// input pass only compares captured scalars and marks the API feed dirty;
    /// GameComponentUpdate performs publication outside the render path.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    internal static class Patch_ApiBillCounterInvalidation
    {
        internal readonly struct State
        {
            internal readonly BillRepeatModeDef RepeatMode;
            internal readonly int RepeatCount;
            internal readonly int TargetCount;
            internal readonly bool Paused;
            internal readonly bool IncludeEquipped;
            internal readonly bool IncludeTainted;
            internal readonly FloatRange HpRange;
            internal readonly QualityRange QualityRange;
            internal readonly bool LimitToAllowedStuff;
            internal readonly ISlotGroup? IncludeGroup;

            internal State(Bill_Production bill)
            {
                RepeatMode = bill.repeatMode;
                RepeatCount = bill.repeatCount;
                TargetCount = bill.targetCount;
                Paused = bill.paused;
                IncludeEquipped = bill.includeEquipped;
                IncludeTainted = bill.includeTainted;
                HpRange = bill.hpRange;
                QualityRange = bill.qualityRange;
                LimitToAllowedStuff = bill.limitToAllowedStuff;
                IncludeGroup = bill.GetIncludeSlotGroup();
            }

            internal bool Matches(Bill_Production bill)
                => ReferenceEquals(RepeatMode, bill.repeatMode)
                   && RepeatCount == bill.repeatCount
                   && TargetCount == bill.targetCount
                   && Paused == bill.paused
                   && IncludeEquipped == bill.includeEquipped
                   && IncludeTainted == bill.includeTainted
                   && HpRange.Equals(bill.hpRange)
                   && QualityRange.Equals(bill.qualityRange)
                   && LimitToAllowedStuff == bill.limitToAllowedStuff
                   && ReferenceEquals(IncludeGroup, bill.GetIncludeSlotGroup());
        }

        private static void Prefix(Bill_Production ___bill, out State __state)
            => __state = new State(___bill);

        private static void Postfix(Bill_Production ___bill, State __state)
        {
            if (!__state.Matches(___bill))
                QualityJobsStore.Active?.InvalidateManagedJobs();
        }
    }

    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ShouldDoNow))]
    internal static class Patch_ApiBillAutoPauseInvalidation
    {
        private static void Prefix(Bill_Production __instance, out bool __state)
            => __state = __instance.paused;

        private static void Postfix(Bill_Production __instance, bool __state)
        {
            if (__state != __instance.paused)
                QualityJobsStore.Active?.InvalidateManagedJobs();
        }
    }

    [HarmonyPatch(typeof(CompForbiddable), nameof(CompForbiddable.Forbidden),
        MethodType.Setter)]
    internal static class Patch_ApiConstructionForbiddenInvalidation
    {
        private static void Prefix(CompForbiddable __instance, out bool __state)
            => __state = __instance.Forbidden;

        private static void Postfix(CompForbiddable __instance, bool __state)
        {
            if (__state == __instance.Forbidden) return;
            Thing target = __instance.parent;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (target != null && store?.FindPlan(target) != null)
                store.InvalidateManagedJobs();
        }
    }
}
