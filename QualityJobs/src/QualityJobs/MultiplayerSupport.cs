using Multiplayer.API;
using Verse;

namespace QualityJobs
{
    /// Registers [SyncMethod]s with RimWorld Multiplayer when present. The API
    /// dll ships with the mod; without the MP mod, MP.enabled is false and
    /// this is a no-op.
    [StaticConstructorOnStartup]
    public static class MultiplayerSupport
    {
        static MultiplayerSupport()
        {
            // MP.RegisterAll() scans this assembly for [SyncMethod] AND
            // [SyncWorker] members, so SyncSeedValues below is picked up here.
            if (MP.enabled) MP.RegisterAll();
        }

        /// SyncWorker for the enable payload (Fix 1). shouldConstruct = true lets
        /// MP allocate the SeedValues via its parameterless ctor before we bind;
        /// each field is a primitive, so sync.Bind is sufficient (mirrors the
        /// WorkRoles SyncWorker style). See SeedValues for why the 14 values are
        /// carried as one synced object instead of 14 [SyncMethod] parameters.
        [SyncWorker(shouldConstruct = true)]
        private static void SyncSeedValues(SyncWorker sync, ref SeedValues v)
        {
            sync.Bind(ref v.manageNewBills);
            sync.Bind(ref v.minSkill);
            sync.Bind(ref v.requireInspired);
            sync.Bind(ref v.requireSpecialist);
            sync.Bind(ref v.productCap);
            sync.Bind(ref v.share);
            sync.Bind(ref v.manageNewConstruction);
            sync.Bind(ref v.constructionMinSkill);
            sync.Bind(ref v.constructionRequireInspired);
            sync.Bind(ref v.constructionRequireSpecialist);
            sync.Bind(ref v.constructionTargetQuality);
            sync.Bind(ref v.autoBest);
            sync.Bind(ref v.constructionAutoBest);
            sync.Bind(ref v.targetQuality);
        }

        /// Field-by-field worker for the API bill-creation payload. Do not move
        /// these values back onto the SyncMethod signature: Harmony's generated
        /// invoker can exceed RimWorld Mono's ILGenerator buffer and fail with
        /// ILGenerator.make_room during Multiplayer registration (see AGENTS.md).
        [SyncWorker(shouldConstruct = true)]
        private static void SyncCreateQualityBillValues(
            SyncWorker sync, ref CreateQualityBillValues v)
        {
            sync.Bind(ref v.billGiverThingId);
            sync.Bind(ref v.mapUniqueId);
            sync.Bind(ref v.productDefName);
            sync.Bind(ref v.recipeDefName);
            sync.Bind(ref v.explicitOptions);
            sync.Bind(ref v.skillGate);
            sync.Bind(ref v.requireInspired);
            sync.Bind(ref v.requireSpecialist);
            sync.Bind(ref v.autoBest);
            sync.Bind(ref v.targetQuality);
        }
    }
}
