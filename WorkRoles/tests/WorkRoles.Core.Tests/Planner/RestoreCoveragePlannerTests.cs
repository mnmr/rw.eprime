namespace WorkRoles.Core.Tests.Planner;

public class RestoreCoveragePlannerTests
{
    private static (int, IReadOnlyCollection<string>) Candidate(int id, params string[] coverage) => (id, coverage);

    [Test]
    public async Task PicksTheCandidateRecoveringTheMostLostGivers()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(["FightFires", "PatientBedRest", "DoctorRescue"], [Candidate(1, "FightFires"), Candidate(2, "FightFires", "PatientBedRest", "DoctorRescue")]);
        await Assert.That(string.Join(",", picks)).IsEqualTo("2");
    }

    [Test]
    public async Task CombinesMultipleCandidatesWhenNoSingleOneSuffices()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(["FightFires", "HaulGeneral"], [Candidate(1, "FightFires", "DoctorRescue"), Candidate(2, "HaulGeneral", "CleanFilth")]);
        await Assert.That(string.Join(",", picks)).IsEqualTo("1,2");
    }

    [Test]
    public async Task ZeroGainCandidatesAreNeverPickedAndUnrecoverableGiversAreLeft()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(["ModdedGiver"], [Candidate(1, "FightFires")]);

        await Assert.That(picks).IsEmpty();
    }

    [Test]
    public async Task NothingLostPlansNothing()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles([], [Candidate(1, "FightFires")]);

        await Assert.That(picks).IsEmpty();
    }
}
