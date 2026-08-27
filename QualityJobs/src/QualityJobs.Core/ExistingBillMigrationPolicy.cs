namespace QualityJobs.Core
{
    public readonly struct ExistingBillMigrationConfig
    {
        public readonly bool Managed;
        public readonly bool AutoBest;
        public readonly bool RequireInspired;
        public readonly bool RequireSpecialist;
        public readonly int TargetQuality;

        public ExistingBillMigrationConfig(bool managed, bool autoBest,
            bool requireInspired, bool requireSpecialist, int targetQuality)
        {
            Managed = managed;
            AutoBest = autoBest;
            RequireInspired = requireInspired;
            RequireSpecialist = requireSpecialist;
            TargetQuality = targetQuality;
        }
    }

    /// <summary>Deterministic first-install policy for bills that would inherit
    /// Quality Jobs enablement despite predating the mod.</summary>
    public static class ExistingBillMigrationPolicy
    {
        public static bool ShouldQuarantine(bool firstInitialization,
            bool supportsQualityJobs, bool hasExplicitManagedOverride,
            bool manageNewBillsByDefault, int targetQualityByDefault)
            => firstInitialization
               && supportsQualityJobs
               && !hasExplicitManagedOverride
               && (manageNewBillsByDefault || targetQualityByDefault > 0);

        public static bool ShouldShowDialog(int pendingBillCount)
            => pendingBillCount > 0;

        public static ExistingBillMigrationConfig ConfigurationFor(
            bool enableQualityJobs)
            => new ExistingBillMigrationConfig(
                managed: enableQualityJobs,
                autoBest: enableQualityJobs,
                requireInspired: false,
                requireSpecialist: false,
                targetQuality: 0);
    }
}
