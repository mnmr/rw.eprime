namespace WorkRoles.Core.Tests.Roles;

/// Birthday role changes for a colony with split adult/child roles: an
/// auto-assign adult role (10+), an auto-assign child role (3-9), a manual
/// teen role (13-17) and a manual adult-only role (18+).
public class BirthdayRolesTests
{
    private const int Adult = 1;
    private const int Child = 2;
    private const int Teen = 3;
    private const int Grown = 4;
    private const int Youth = 5;

    private static readonly AgeGatedRole[] Roles =
    [
        new(Adult, autoAssign: true, minAge: 10, maxAge: 0),
        new(Child, autoAssign: true, minAge: 3, maxAge: 9),
        new(Teen, autoAssign: false, minAge: 13, maxAge: 17),
        new(Grown, autoAssign: false, minAge: 18, maxAge: 0),
        new(Youth, autoAssign: false, minAge: 0, maxAge: AgeBands.OldestGate),
    ];

    private static async Task Expect(int[] held, int birthdayAge, bool ageLimitsApply, int[] gained, int[] lost)
    {
        BirthdayRoleChanges changes = BirthdayRoles.Plan(Roles, held, birthdayAge, ageLimitsApply);
        await Assert.That(changes.Gained).IsEquivalentTo(gained);
        await Assert.That(changes.Lost).IsEquivalentTo(lost);
    }

    [Test]
    public async Task ChildTurningTenSwapsChildRoleForAdultRole() =>
        await Expect([Child], 10, true, gained: [Adult], lost: [Child]);

    [Test]
    public async Task ToddlerTurningThreeReceivesChildRole() =>
        await Expect([], 3, true, gained: [Child], lost: []);

    [Test]
    public async Task RoleRemovedByPlayerWhileItFitIsNotReadded() =>
        await Expect([], 11, true, gained: [], lost: []);

    [Test]
    public async Task ManualRoleHeldOutsideItsAgeGroupsIsKept() =>
        await Expect([Adult, Grown], 13, true, gained: [], lost: []);

    [Test]
    public async Task ManualRoleIsDroppedWhenThePawnAgesPastIt() =>
        await Expect([Adult, Teen], 18, true, gained: [], lost: [Teen]);

    /// The early exit for adults must still let the birthday after the
    /// oldest possible cap through.
    [Test]
    public async Task RoleCappedAtTheOldestGateIsDroppedTheYearAfter()
    {
        await Expect([Adult, Youth], AgeBands.OldestGate + 1, true, gained: [], lost: [Youth]);
        await Assert.That(BirthdayRoles.CanChangeRoles(AgeBands.OldestGate + 2)).IsFalse();
    }

    [Test]
    public async Task PawnWithoutAgeLimitsIsUntouched() =>
        await Expect([Child], 10, false, gained: [], lost: []);
}
