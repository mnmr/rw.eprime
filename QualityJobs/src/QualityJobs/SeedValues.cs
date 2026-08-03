namespace QualityJobs
{
    /// MP-synced payload for the enable command (spec §12). All 12 seed values
    /// travel as ONE synced object rather than as 12 primitive parameters:
    /// RimWorld-Multiplayer's MethodInvoker cannot register a [SyncMethod] with
    /// that many parameters (ILGenerator.make_room NRE at 12 params), so the
    /// payload is bound field-by-field by a [SyncWorker] instead. Every client
    /// reconstructs identical values from the same synced object and seeds the
    /// store deterministically.
    ///
    /// Plain data holder — no game/Verse/Unity references — so it is safe to
    /// construct and bind on any client during synced replay.
    public class SeedValues
    {
        // WARNING: the [SyncWorker] SyncSeedValues in MultiplayerSupport binds
        // these fields positionally. Reordering or inserting fields here WITHOUT
        // updating that worker silently corrupts synced values. Keep the two in
        // lockstep.

        // Bill defaults (7).
        public bool manageNewBills;
        public int minSkill;
        public bool requireInspired;
        public bool requireSpecialist;
        public int productCap;
        public bool share;
        public bool dispatchLetter;

        // Construction defaults (5).
        public bool manageNewConstruction;
        public int constructionMinSkill;
        public bool constructionRequireInspired;
        public bool constructionRequireSpecialist;
        public int constructionTargetQuality;

        /// Parameterless ctor required by [SyncWorker(shouldConstruct = true)].
        public SeedValues() { }

        /// Convenience factory: captures the issuing client's global defaults for
        /// the synced enable call.
        public static SeedValues FromSettings(QualityJobsSettings s)
            => new SeedValues
            {
                manageNewBills = s.defaultManageNewBills,
                minSkill = s.defaultMinSkill,
                requireInspired = s.defaultRequireInspired,
                requireSpecialist = s.defaultRequireSpecialist,
                productCap = s.defaultProductCap,
                share = s.defaultShareUnfinishedWork,
                dispatchLetter = s.dispatchLetter,
                manageNewConstruction = s.defaultManageNewConstruction,
                constructionMinSkill = s.defaultConstructionMinSkill,
                constructionRequireInspired = s.defaultConstructionRequireInspired,
                constructionRequireSpecialist = s.defaultConstructionRequireSpecialist,
                constructionTargetQuality = s.defaultConstructionTargetQuality,
            };
    }
}
