using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// Spec §8 companion: pooling unbinds an idle unfinished item from its
    /// bill so any crafter can resume it, which also frees the bill to start
    /// a brand-new item. While the pooled item is being hauled (unspawned)
    /// or reserved, the resume match (Patch_ShareResume_Match) cannot see it
    /// and the next crafter started a duplicate, spending a second set of
    /// ingredients (observed in-game 2026-09-26: seven unfinished bionic arms
    /// for one bill). A bill therefore starts no new item while the pool
    /// still holds one it took from that bill; resume paths run earlier in
    /// StartOrResumeBillJob and are untouched. Same new-start seam as
    /// Patch_StockGate. Read-only (float-menu safe, MP-safe).
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
    public static class Patch_PooledWorkGate
    {
        // Cache contract — Owner: process. Key: active language identity.
        // Value: translated fail label. Dependencies: active language.
        // Refresh: lazy on language identity change. Equality: cache hits
        // retain the string identity. Teardown: none needed (one string).
        private static LoadedLanguage? labelLanguage;
        private static string waiting = string.Empty;

        private static string WaitingLabel
        {
            get
            {
                LoadedLanguage? language = LanguageDatabase.activeLanguage;
                if (!ReferenceEquals(language, labelLanguage))
                {
                    labelLanguage = language;
                    waiting = "QJ_UnfinishedWorkWaiting".Translate();
                }
                return waiting;
            }
        }

        public static bool Prefix(Bill bill, List<ThingCount> chosen, ref bool __result)
        {
            if (!(bill is Bill_ProductionWithUft)) return true;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !store.shareUnfinishedWork) return true;
            if (store.IsFinishBill(bill) || !store.HasPooledWorkFor(bill)) return true;
            chosen.Clear();
            JobFailReason.Is(WaitingLabel, bill.Label);
            __result = false;
            return false;
        }
    }
}
