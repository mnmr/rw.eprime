namespace WorkRoles.Core.Tests.UI;

public class RoleJobAvailabilityTests
{
    [Test]
    [Arguments(4, 0, RoleJobAvailability.Available)]
    [Arguments(4, 1, RoleJobAvailability.SomeUnavailable)]
    [Arguments(4, 4, RoleJobAvailability.AllUnavailable)]
    [Arguments(0, 0, RoleJobAvailability.Available)]
    public async Task JobCountsMapToAvailability(int totalJobs, int unavailableJobs, RoleJobAvailability expected)
    {
        RoleJobAvailability result = RoleJobAvailabilitySummary.FromCounts(totalJobs, unavailableJobs);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(RoleJobAvailability.Available)]
    [Arguments(RoleJobAvailability.SomeUnavailable)]
    public async Task VetoSignalUsesCriticalMarker(RoleJobAvailability availability)
    {
        RoleAssignmentWarningSeverity result = RoleAssignmentWarningSummary.From(availability, hasVetoSignal: true);

        await Assert.That(result).IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }

    [Test]
    [Arguments(RoleJobAvailability.Available, RoleAssignmentWarningSeverity.Caution)]
    [Arguments(RoleJobAvailability.SomeUnavailable, RoleAssignmentWarningSeverity.Caution)]
    [Arguments(RoleJobAvailability.AllUnavailable, RoleAssignmentWarningSeverity.Critical)]
    public async Task DampenedSignalUsesCapabilitySeverityFloor(RoleJobAvailability availability, RoleAssignmentWarningSeverity expected)
    {
        RoleAssignmentWarningSeverity result = RoleAssignmentWarningSummary.From(availability, hasVetoSignal: false, hasDampenedSignal: true);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(RoleJobAvailability.Available, RoleAssignmentWarningSeverity.None)]
    [Arguments(RoleJobAvailability.SomeUnavailable, RoleAssignmentWarningSeverity.Caution)]
    [Arguments(RoleJobAvailability.AllUnavailable, RoleAssignmentWarningSeverity.Critical)]
    public async Task CapabilityAvailabilityMapsToMarkerSeverity(RoleJobAvailability availability, RoleAssignmentWarningSeverity expected)
    {
        RoleAssignmentWarningSeverity result = RoleAssignmentWarningSummary.From(availability, hasVetoSignal: false);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task MissingExplicitRequiredSkillUsesCriticalMarker()
    {
        RoleAssignmentWarningSeverity result =
            RoleAssignmentWarningSummary.From(
                RoleJobAvailability.Available,
                hasVetoSignal: false,
                hasDampenedSignal: false,
                hasMissingRequiredSkill: true);

        await Assert.That(result)
            .IsEqualTo(RoleAssignmentWarningSeverity.Critical);
    }
}
