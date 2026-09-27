namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// The API management commands (QualityJobsApi.ManageBill / UnmanageBill)
/// pin their keys so a later per-save default edit can never undo the
/// caller's request, write only when the pinned state differs, and report
/// an eligibility change only when the effective managed flag flips.
public class BillManagementTests
{
    [Test]
    public async Task ManagingABillThatTracksDefaultsPinsManagementAndTarget()
    {
        BillManagementChange change = BillManagement.Manage(
            managedOverride: null, managedDefault: false,
            targetOverride: null, requestedTarget: 4);

        await Assert.That(change.WritesManaged).IsTrue();
        await Assert.That(change.Managed).IsTrue();
        await Assert.That(change.WritesTarget).IsTrue();
        await Assert.That(change.TargetQuality).IsEqualTo(4);
        await Assert.That(change.EligibilityChanged).IsTrue();
    }

    [Test]
    public async Task ManagingPinsDefaultsThatAlreadyMatchWithoutChangingEligibility()
    {
        // manageNewBillsDefault = true: the bill is already managed, but only
        // by default. Pinning keeps it managed if the player turns the default
        // off later.
        BillManagementChange change = BillManagement.Manage(
            managedOverride: null, managedDefault: true,
            targetOverride: null, requestedTarget: 0);

        await Assert.That(change.WritesManaged).IsTrue();
        await Assert.That(change.WritesTarget).IsTrue();
        await Assert.That(change.IsNoOp).IsFalse();
        await Assert.That(change.EligibilityChanged).IsFalse();
    }

    [Test]
    public async Task RepeatingAManageRequestChangesNothing()
    {
        BillManagementChange change = BillManagement.Manage(
            managedOverride: true, managedDefault: false,
            targetOverride: 5, requestedTarget: 5);

        await Assert.That(change.IsNoOp).IsTrue();
        await Assert.That(change.EligibilityChanged).IsFalse();
    }

    [Test]
    public async Task RetargetingAManagedBillWritesOnlyTheTarget()
    {
        BillManagementChange change = BillManagement.Manage(
            managedOverride: true, managedDefault: false,
            targetOverride: 2, requestedTarget: 6);

        await Assert.That(change.WritesManaged).IsFalse();
        await Assert.That(change.WritesTarget).IsTrue();
        await Assert.That(change.TargetQuality).IsEqualTo(6);
        await Assert.That(change.EligibilityChanged).IsFalse();
    }

    [Test]
    public async Task UnmanagingOverridesTheManageNewBillsDefault()
    {
        BillManagementChange change = BillManagement.Unmanage(
            managedOverride: null, managedDefault: true);

        await Assert.That(change.WritesManaged).IsTrue();
        await Assert.That(change.Managed).IsFalse();
        await Assert.That(change.WritesTarget).IsFalse();
        await Assert.That(change.EligibilityChanged).IsTrue();
    }

    [Test]
    public async Task UnmanagingAnImplicitlyUnmanagedBillPinsItWithoutChangingEligibility()
    {
        // manageNewBillsDefault = false today; pinning keeps the bill vanilla
        // if the player turns the default on later.
        BillManagementChange change = BillManagement.Unmanage(
            managedOverride: null, managedDefault: false);

        await Assert.That(change.WritesManaged).IsTrue();
        await Assert.That(change.Managed).IsFalse();
        await Assert.That(change.EligibilityChanged).IsFalse();
    }

    [Test]
    public async Task RepeatingAnUnmanageRequestChangesNothing()
    {
        BillManagementChange change = BillManagement.Unmanage(
            managedOverride: false, managedDefault: true);

        await Assert.That(change.IsNoOp).IsTrue();
    }

    [Test]
    public async Task ManagingAnUnmanagedBillRestoresEligibility()
    {
        BillManagementChange change = BillManagement.Manage(
            managedOverride: false, managedDefault: true,
            targetOverride: 3, requestedTarget: 3);

        await Assert.That(change.WritesManaged).IsTrue();
        await Assert.That(change.WritesTarget).IsFalse();
        await Assert.That(change.EligibilityChanged).IsTrue();
    }

    [Test]
    [Arguments(-1, false)]
    [Arguments(0, true)]
    [Arguments(6, true)]
    [Arguments(7, false)]
    public async Task TargetQualityMustBeAQualityCategory(int target, bool expected)
    {
        await Assert.That(BillManagement.IsValidTargetQuality(target))
            .IsEqualTo(expected);
    }
}
