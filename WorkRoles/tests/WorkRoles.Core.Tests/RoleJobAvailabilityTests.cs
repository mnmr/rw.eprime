using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleJobAvailabilityTests
{
    [Test]
    public async Task NoUnavailableJobsNeedsNoDecoration()
    {
        RoleJobAvailability result = RoleJobAvailabilitySummary.FromCounts(
            totalJobs: 4, unavailableJobs: 0);

        await Assert.That(result).IsEqualTo(RoleJobAvailability.Available);
    }

    [Test]
    public async Task AnyButNotAllUnavailableJobsUsesPartialWarning()
    {
        RoleJobAvailability result = RoleJobAvailabilitySummary.FromCounts(
            totalJobs: 4, unavailableJobs: 1);

        await Assert.That(result).IsEqualTo(RoleJobAvailability.SomeUnavailable);
    }

    [Test]
    public async Task EveryUnavailableJobUsesFullWarning()
    {
        RoleJobAvailability result = RoleJobAvailabilitySummary.FromCounts(
            totalJobs: 4, unavailableJobs: 4);

        await Assert.That(result).IsEqualTo(RoleJobAvailability.AllUnavailable);
    }

    [Test]
    public async Task EmptyRolesNeedNoCapabilityWarning()
    {
        RoleJobAvailability result = RoleJobAvailabilitySummary.FromCounts(
            totalJobs: 0, unavailableJobs: 0);

        await Assert.That(result).IsEqualTo(RoleJobAvailability.Available);
    }

    [Test]
    public async Task VetoSignalUsesOneCriticalMarkerAndDominatesPartialCapability()
    {
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available, hasVetoSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.SomeUnavailable, hasVetoSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }

    [Test]
    public async Task DampenedSignalIsCautionUnlessCapabilityEscalates()
    {
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available, hasVetoSignal: false, hasDampenedSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Caution);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.SomeUnavailable, hasVetoSignal: false, hasDampenedSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Caution);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.AllUnavailable, hasVetoSignal: false, hasDampenedSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }

    [Test]
    public async Task CapabilityAvailabilityMapsToExistingMarkerSeverities()
    {
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available, hasVetoSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.None);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.SomeUnavailable, hasVetoSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.Caution);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.AllUnavailable, hasVetoSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }
}
