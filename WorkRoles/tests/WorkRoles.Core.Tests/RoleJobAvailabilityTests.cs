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
    public async Task AwfulSignalUsesOneCriticalMarkerAndDominatesPartialCapability()
    {
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available, hasAwfulSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.SomeUnavailable, hasAwfulSignal: true))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }

    [Test]
    public async Task CapabilityAvailabilityMapsToExistingMarkerSeverities()
    {
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available, hasAwfulSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.None);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.SomeUnavailable, hasAwfulSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.Caution);
        await Assert.That(RoleAssignmentWarningSummary.From(
                RoleJobAvailability.AllUnavailable, hasAwfulSignal: false))
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }
}
