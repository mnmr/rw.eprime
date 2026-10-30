using HarmonyLib;
using RimWorld;

namespace QualityJobs.Patches
{
    [HarmonyPatch(typeof(Bill), nameof(Bill.Clone))]
    internal static class Patch_BillCopy
    {
        private static void Postfix(Bill __instance, Bill __result)
            => BillConfigTransfer.Clone(__instance, __result);
    }

    [HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
    internal static class Patch_BillCopySerialization
    {
        private static void Postfix(Bill __instance) => BillConfigTransfer.Expose(__instance);
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    internal static class Patch_BillCopyAttached
    {
        private static void Postfix(BillStack __instance, Bill bill)
        {
            if (bill.billStack == __instance && __instance.Bills.Contains(bill))
                BillConfigTransfer.Attach(bill);
        }
    }
}
