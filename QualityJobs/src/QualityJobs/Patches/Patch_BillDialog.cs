using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Attach to the factory's existing result, including windows supplied by
    /// other mods. WindowStack.Add also covers directly constructed bill dialogs.
    [HarmonyPatch(typeof(Bill_Production), "GetBillDialog")]
    public static class Patch_BillDialog
    {
        public static void Postfix(Bill_Production __instance, Window __result)
        {
            BillWindowIntegration.Attach(__result, __instance);
        }
    }
}
