using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 7: up to InTrainingAllowance of a needed role's slots may be filled
/// by a lower-band partner from any path containing it.
public class RecsAllowanceTests
{
    // Doctor path: medic 5-15 trains toward doctor 15-21.
    private static (RoleView doctor, RoleView medic, ColonyView colony) DoctorColony(
        params PawnView[] pawns)
    {
        var doctor = RecsTestBed.Role(1, "Doctor");
        doctor.MinHolders = 2;
        doctor.InTrainingAllowance = 1;
        var medic = RecsTestBed.Role(2, "Doctor", "Tend");
        var colony = RecsTestBed.Colony(new List<RoleView> { doctor, medic }, pawns);
        colony.Paths.Add(RecsTestBed.Path(1, (2, 5, 15), (1, 15, 21)));
        return (doctor, medic, colony);
    }

    [Test]
    public async Task UnderCoveredNeedDraftsATraineeIntoTheLowerBandPartner()
    {
        // One real doctor (16), one medic-band pawn (8): the second Doctor
        // slot is filled by a Medic trainee under the allowance.
        var high = RecsTestBed.Pawn(); high.SkillLevels["Medicine"] = 16;
        var mid = RecsTestBed.Pawn(); mid.SkillLevels["Medicine"] = 8;
        var (_, _, colony) = DoctorColony(high, mid);
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);

        // Allowance reserves mid (8) as the Medic trainee for the below-band slot.
        new InTrainingAllowanceRule().Apply(context);
        await Assert.That(context.Candidates[1].ContainsKey(2)).IsTrue();
        await Assert.That(context.Candidates[1][2].Reason.RuleId).IsEqualTo("allowance");
        await Assert.That(context.Candidates[1][2].Reason.TowardRoleId).IsEqualTo(1);

        // Draft fills the in-band slot with high (16); mid stays the trainee.
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsFalse();
    }

    [Test]
    public async Task ExistingPartnerCandidatesCountBeforeNewDrafts()
    {
        var high = RecsTestBed.Pawn(); high.SkillLevels["Medicine"] = 16;
        var mid = RecsTestBed.Pawn(); mid.SkillLevels["Medicine"] = 8;
        mid.PassionScores["Medicine"] = 2; // signal already made mid a Medic candidate
        var other = RecsTestBed.Pawn(); other.SkillLevels["Medicine"] = 7;
        var (_, _, colony) = DoctorColony(high, mid, other);
        var context = new EngineContext(colony);
        new SignalCandidatesRule().Apply(context, 0);
        new SignalCandidatesRule().Apply(context, 1);
        new SignalCandidatesRule().Apply(context, 2);
        new BandGatingRule().Apply(context, 0);
        new BandGatingRule().Apply(context, 1);
        new BandGatingRule().Apply(context, 2);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new InTrainingAllowanceRule().Apply(context);
        new BestInColonyDraftRule().Apply(context);
        // Allowance 1 is satisfied by mid's existing Medic candidacy; the
        // third pawn is NOT drafted.
        await Assert.That(context.Candidates[2].ContainsKey(2)).IsFalse();
    }

    [Test]
    public async Task AllowanceNeverExceedsItsBudgetOrTheMissingSlots()
    {
        // Nobody passes Doctor's band; want 2, allowance 1 → exactly ONE trainee.
        var a = RecsTestBed.Pawn(); a.SkillLevels["Medicine"] = 9;
        var b = RecsTestBed.Pawn(); b.SkillLevels["Medicine"] = 8;
        var (_, _, colony) = DoctorColony(a, b);
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new InTrainingAllowanceRule().Apply(context);
        new BestInColonyDraftRule().Apply(context);
        // want 2, allowance 1, nobody in band → exactly ONE Medic trainee
        // (best level) plus one below-band direct Doctor for the other slot.
        int trainees = context.Candidates.Count(c => c.ContainsKey(2));
        await Assert.That(trainees).IsEqualTo(1);
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue(); // best level first
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsTrue(); // floor met directly
    }

    [Test]
    public async Task IrrelevantWithoutAllowancesOrPaths()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.MinHolders = 2;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook }, RecsTestBed.Pawn()));
        await Assert.That(new InTrainingAllowanceRule().Relevant(context)).IsFalse();
    }
}
