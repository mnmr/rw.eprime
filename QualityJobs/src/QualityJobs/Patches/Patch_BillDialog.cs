using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Replaces the bill dialog with our owned Dialog_QualityBillConfig for
    /// managed quality recipes via the protected virtual factory method
    /// Bill_Production.GetBillDialog() (Bill_Production.cs:261-264). Windows
    /// are transient: no save or MP footprint; bills stay vanilla.
    [HarmonyPatch(typeof(Bill_Production), "GetBillDialog")]
    public static class Patch_BillDialog
    {
        public static void Postfix(Bill_Production __instance, ref Window __result)
        {
            if (QualityJobsStore.Active == null) return;
            if (!(__instance is Bill_ProductionWithUft uftBill)) return;
            if (!ManagedRecipes.IsManagedRecipe(__instance.recipe)) return;
            Thing? giver = __instance.billStack?.billGiver as Thing;
            if (giver == null) return;
            __result = new Dialog_QualityBillConfig(uftBill, giver.Position);
        }
    }
}
