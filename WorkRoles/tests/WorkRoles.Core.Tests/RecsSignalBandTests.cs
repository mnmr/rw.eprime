using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 3 (Strong+ interest makes a skilled role a candidate) and rule 4
/// (path bands gate candidates on the measured skill).
public class RecsSignalBandTests
{
    [Test]
    public async Task StrongOrBetterInterestCreatesCandidatesWithTheBucketAsStrength()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        var crafter = RecsTestBed.Role(2, "Crafting");
        var doctor = RecsTestBed.Role(3, "Doctor");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        pawn.SkillLevels["Crafting"] = 12; pawn.SignalBuckets["Crafting"] = SignalBucket.Exceptional;
        pawn.SkillLevels["Medicine"] = 9; // level alone = Neutral, no candidate
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook, crafter, doctor }, pawn));
        new SignalCandidatesRule().Apply(context, 0);
        await Assert.That(context.Candidates[0][1].Strength).IsEqualTo(SignalBucket.Great);
        await Assert.That(context.Candidates[0][1].Reason.SkillDefName).IsEqualTo("Cooking");
        await Assert.That(context.Candidates[0][2].Strength).IsEqualTo(SignalBucket.Exceptional);
        await Assert.That(context.Candidates[0][2].Reason.Source).IsEqualTo(SignalSource.Aggregated);
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsFalse();
    }

    [Test]
    public async Task AutoUnskilledHuntingAndVetoedRolesAreNotSignalCandidates()
    {
        var auto = RecsTestBed.Role(1, "Cooking"); auto.AutoAssign = true;
        var grunt = RecsTestBed.Unskilled(2, "Hauling");
        var hunter = RecsTestBed.Role(3, "Hunting"); hunter.Hunting = true;
        var vetoed = RecsTestBed.Role(4, "Cooking", "CookVetoed");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        pawn.SkillLevels["Shooting"] = 8; pawn.SignalBuckets["Shooting"] = SignalBucket.Great;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { auto, grunt, hunter, vetoed }, pawn));
        context.Vetoed.Add(4);
        new SignalCandidatesRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].Count).IsEqualTo(0);
    }

    [Test]
    public async Task BandsRemoveOutOfBandCandidates_OverlapCoexists_DisjointSupersedes()
    {
        // Farmer path shape: cutter 0-12, grower 4-21, farmer 12-21.
        var cutter = RecsTestBed.Role(1, "Cooking", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");
        var farmer = RecsTestBed.Role(3, "Cooking", "Farm");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 14; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        var colony = RecsTestBed.Colony(new List<RoleView> { cutter, grower, farmer }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1, (1, 0, 12), (2, 4, 21), (3, 12, 21)));
        var context = new EngineContext(colony);
        new SignalCandidatesRule().Apply(context, 0);
        new BandGatingRule().Apply(context, 0);
        // 14: cutter superseded (past 12), grower and farmer coexist (overlap).
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsTrue();
    }

    [Test]
    public async Task BandsAreIrrelevantWithoutPaths_AndNonMembersPassUntouched()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 2; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        var context = new EngineContext(RecsTestBed.Colony(new List<RoleView> { cook }, pawn));
        await Assert.That(new BandGatingRule().Relevant(context)).IsFalse();
        new SignalCandidatesRule().Apply(context, 0);
        new BandGatingRule().Apply(context, 0); // harmless even when irrelevant
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
    }
}
