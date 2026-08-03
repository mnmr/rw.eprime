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
        /// WorkRoles SyncWorker style). See SeedValues for why the 12 values are
        /// carried as one synced object instead of 12 [SyncMethod] parameters.
        [SyncWorker(shouldConstruct = true)]
        private static void SyncSeedValues(SyncWorker sync, ref SeedValues v)
        {
            sync.Bind(ref v.manageNewBills);
            sync.Bind(ref v.minSkill);
            sync.Bind(ref v.requireInspired);
            sync.Bind(ref v.requireSpecialist);
            sync.Bind(ref v.productCap);
            sync.Bind(ref v.share);
            sync.Bind(ref v.dispatchLetter);
            sync.Bind(ref v.manageNewConstruction);
            sync.Bind(ref v.constructionMinSkill);
            sync.Bind(ref v.constructionRequireInspired);
            sync.Bind(ref v.constructionRequireSpecialist);
            sync.Bind(ref v.constructionTargetQuality);
        }
    }
}
