using System.Collections.Generic;
using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// Spec §9: block NEW item starts at cap; resume paths run earlier in
    /// StartOrResumeBillJob and are never touched. Read-only (float-menu safe);
    /// vanilla `suspended` flag never used.
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
    public static class Patch_StockGate
    {
        /// Client-local presentation marker: set during a JobOnThing scan when
        /// the stock gate blocked a bill; read only to re-assert the float-menu
        /// fail reason; never influences sim decisions (MP-safe).
        private static bool blockedThisScan;

        internal static void ResetMarker() => blockedThisScan = false;

        internal static bool IsBlocked => blockedThisScan;

        public static bool Prefix(Bill bill, Pawn pawn, Thing billGiver,
            List<ThingCount> chosen, List<IngredientCount> missingIngredients,
            ref bool __result)
        {
            if (!(bill is Bill_ProductionWithUft)) return true;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return true;
            if (!ManagedRecipes.IsManagedRecipe(bill.recipe)) return true;
            if (!store.ConfigFor(bill).Managed) return true;

            string? product = ManagedRecipes.ProductDefName(bill.recipe);
            int count = store.SpawnedUftCount(billGiver.Map, product);
            int cap = store.CapFor(product);
            if (StockPolicy.CanStartNewItem(count, cap, store.IsFinishBill(bill)))
                return true;

            blockedThisScan = true;
            JobFailReason.Is("QJ_AtStockCap".Translate(), bill.Label);
            __result = false;
            return false;
        }
    }

    /// Re-asserts the stock-cap fail reason after vanilla's StartOrResumeBillJob
    /// overwrites JobFailReason with "MissingMaterials" when our prefix returns
    /// false (WorkGiver_DoBill.cs:261-281).
    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class Patch_StockGate_FailReason
    {
        // Unconditional: clear the marker before each JobOnThing scan so a
        // non-blocked scan never inherits a stale marker from a previous call.
        public static void Prefix()
        {
            Patch_StockGate.ResetMarker();
        }

        public static void Postfix(Job? __result)
        {
            if (__result != null) return;
            if (!Patch_StockGate.IsBlocked) return;
            // Re-assert after vanilla's "MissingMaterials" overwrite.
            JobFailReason.Is("QJ_AtStockCap".Translate());
            Patch_StockGate.ResetMarker();
        }
    }
}
