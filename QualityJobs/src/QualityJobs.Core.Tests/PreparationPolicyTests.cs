namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class PreparationPolicyTests
{
    // The policy is a simulation admission contract, shared by bill scans,
    // resumed shared work and construction. Removing its restriction would
    // admit ordinary robot preparation while the player reserves XP for people;
    // applying it to finishing or unmanaged work would block unrelated jobs.
    [Test]
    public async Task ReservingPreparationStillAllowsAssignedFinishersAndUnmanagedWork()
    {
        await Assert.That(PreparationPolicy.Allows(
            allowRobotPreparation: true, mechanical: true,
            managedWork: true, dispatchedFinisher: false)).IsTrue();
        await Assert.That(PreparationPolicy.Allows(
            allowRobotPreparation: false, mechanical: true,
            managedWork: true, dispatchedFinisher: false)).IsFalse();
        await Assert.That(PreparationPolicy.Allows(
            allowRobotPreparation: false, mechanical: true,
            managedWork: true, dispatchedFinisher: true)).IsTrue();
        await Assert.That(PreparationPolicy.Allows(
            allowRobotPreparation: false, mechanical: true,
            managedWork: false, dispatchedFinisher: false)).IsTrue();
        await Assert.That(PreparationPolicy.Allows(
            allowRobotPreparation: false, mechanical: false,
            managedWork: true, dispatchedFinisher: false)).IsTrue();
    }
}
