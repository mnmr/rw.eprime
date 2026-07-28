using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RestoreCoveragePlannerTests
{
    private static (int, IReadOnlyCollection<string>) Candidate(int id, params string[] coverage)
        => (id, coverage);

    [Test]
    public async Task PicksTheCandidateRecoveringTheMostLostGivers()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(
            new[] { "FightFires", "PatientBedRest", "DoctorRescue" },
            new List<(int, IReadOnlyCollection<string>)>
            {
                Candidate(1, "FightFires"),
                Candidate(2, "FightFires", "PatientBedRest", "DoctorRescue"),
            });
        await Assert.That(string.Join(",", picks)).IsEqualTo("2");
    }

    [Test]
    public async Task CombinesMultipleCandidatesWhenNoSingleOneSuffices()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(
            new[] { "FightFires", "HaulGeneral" },
            new List<(int, IReadOnlyCollection<string>)>
            {
                Candidate(1, "FightFires", "DoctorRescue"),
                Candidate(2, "HaulGeneral", "CleanFilth"),
            });
        await Assert.That(string.Join(",", picks)).IsEqualTo("1,2");
    }

    [Test]
    public async Task ZeroGainCandidatesAreNeverPicked_UnrecoverableGiversAreLeft()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(
            new[] { "ModdedGiver" },
            new List<(int, IReadOnlyCollection<string>)>
            {
                Candidate(1, "FightFires"),
            });
        await Assert.That(picks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NothingLostPlansNothing()
    {
        var picks = RestoreCoveragePlanner.RecoveryRoles(
            new string[0],
            new List<(int, IReadOnlyCollection<string>)> { Candidate(1, "FightFires") });
        await Assert.That(picks.Count).IsEqualTo(0);
    }
}
