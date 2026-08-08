namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ExistingBillMigrationPolicyTests
{
    [Test]
    [Arguments(true, true, false, true)]
    [Arguments(false, true, false, false)]
    [Arguments(true, false, false, false)]
    [Arguments(true, true, true, false)]
    public async Task OnlyImplicitlyManagedSupportedBillsAreQuarantined(
        bool firstInitialization, bool supportsQualityJobs,
        bool hasExplicitManagedOverride, bool expected)
    {
        bool actual = ExistingBillMigrationPolicy.ShouldQuarantine(
            firstInitialization,
            supportsQualityJobs,
            hasExplicitManagedOverride,
            manageNewBillsByDefault: true,
            targetQualityByDefault: 0);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task DisabledDefaultDoesNotAffectExistingBills()
    {
        bool actual = ExistingBillMigrationPolicy.ShouldQuarantine(
            firstInitialization: true,
            supportsQualityJobs: true,
            hasExplicitManagedOverride: false,
            manageNewBillsByDefault: false,
            targetQualityByDefault: 0);

        await Assert.That(actual).IsFalse();
    }

    [Test]
    public async Task InheritedTargetQualityRetryMakesExistingBillAffected()
    {
        bool actual = ExistingBillMigrationPolicy.ShouldQuarantine(
            firstInitialization: true,
            supportsQualityJobs: true,
            hasExplicitManagedOverride: false,
            manageNewBillsByDefault: false,
            targetQualityByDefault: 3);

        await Assert.That(actual).IsTrue();
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(1, true)]
    [Arguments(4, true)]
    public async Task PendingAffectedBillsAreTheSoleDialogTrigger(
        int pendingBillCount, bool expected)
    {
        bool actual = ExistingBillMigrationPolicy.ShouldShowDialog(pendingBillCount);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task AcceptingMigrationUsesSafeAutoBestConfiguration()
    {
        ExistingBillMigrationConfig config =
            ExistingBillMigrationPolicy.ConfigurationFor(enableQualityJobs: true);

        await Assert.That(config.Managed).IsTrue();
        await Assert.That(config.AutoBest).IsTrue();
        await Assert.That(config.RequireInspired).IsFalse();
        await Assert.That(config.RequireSpecialist).IsFalse();
        await Assert.That(config.TargetQuality).IsEqualTo(0);
    }

    [Test]
    public async Task DecliningMigrationLeavesBillExplicitlyDisabled()
    {
        ExistingBillMigrationConfig config =
            ExistingBillMigrationPolicy.ConfigurationFor(enableQualityJobs: false);

        await Assert.That(config.Managed).IsFalse();
        await Assert.That(config.AutoBest).IsFalse();
        await Assert.That(config.RequireInspired).IsFalse();
        await Assert.That(config.RequireSpecialist).IsFalse();
        await Assert.That(config.TargetQuality).IsEqualTo(0);
    }
}
