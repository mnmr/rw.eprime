using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Retention (existing unskilled chores stay) and rule 10 (combo beats
/// parts; same-path specializations survive; order-compatible folds only).
public class RecsRedundancyRetentionTests
{
    [Test]
    public async Task ExistingUnskilledRolesAreRetained_SkilledOnesAreNot()
    {
        var hauler = RecsTestBed.Unskilled(1, "Hauling");
        var cook = RecsTestBed.Role(2, "Cooking");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = false });
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { hauler, cook }, pawn));
        new RetentionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("retention");
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsFalse();
    }

    [Test]
    public async Task ProtectedExistingRolesAreNotRetentionCandidates()
    {
        var pinned = RecsTestBed.Unskilled(1, "Hauling");
        var blocker = RecsTestBed.Unskilled(2, "Hauling", "Veto"); blocker.Blocker = true;
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true, Pinned = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { pinned, blocker }, pawn));
        new RetentionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].Count).IsEqualTo(0);
    }

    [Test]
    public async Task ComboBeatsParts()
    {
        var basics = RecsTestBed.Role(1, "Cooking", "Firefight", "Patient");
        var firefighter = RecsTestBed.Role(2, "Cooking", "Firefight");
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { basics, firefighter }, RecsTestBed.Pawn()));
        context.AddCandidate(0, 1, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        context.AddCandidate(0, 2, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        new RedundancySuppressionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsFalse();
    }

    [Test]
    public async Task SamePathSpecializationSurvivesItsCoverer()
    {
        // Fabricator (band min 8) under Smith (band min 4): the covered
        // specialization keeps its chip, the lower-band partner survives too.
        var smith = RecsTestBed.Role(1, "Crafting", "MakeWeapons", "Fabricate");
        var fabricator = RecsTestBed.Role(2, "Crafting", "Fabricate");
        var colony = RecsTestBed.Colony(
            new List<RoleView> { smith, fabricator }, RecsTestBed.Pawn());
        colony.Paths.Add(RecsTestBed.Path(1, (2, 8, 21), (1, 4, 21)));
        var context = new EngineContext(colony);
        context.AddCandidate(0, 1, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        context.AddCandidate(0, 2, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        new RedundancySuppressionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue();
    }

    [Test]
    public async Task OrderIncompatibleCovererIsDroppedInsteadOfFolding()
    {
        // Grunt's job order is Haul,Clean; the pawn holds Clean before Haul —
        // folding would reshuffle, so the NEW Grunt candidate drops.
        var grunt = RecsTestBed.Unskilled(1, "Hauling", "Haul", "Clean");
        grunt.OrderedCoverage = new List<string> { "Haul", "Clean" };
        var cleaner = RecsTestBed.Unskilled(2, "Hauling", "Clean");
        var hauler = RecsTestBed.Unskilled(3, "Hauling", "Haul");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 3, Enabled = true });
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { grunt, cleaner, hauler }, pawn));
        context.AddCandidate(0, 1, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        new RetentionRule().Apply(context, 0);
        new RedundancySuppressionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsTrue();

        // Held in the coverer's own order: the fold happens instead.
        var pawn2 = RecsTestBed.Pawn();
        pawn2.Existing.Add(new AssignmentView { RoleId = 3, Enabled = true });
        pawn2.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        var context2 = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { grunt, cleaner, hauler }, pawn2));
        context2.AddCandidate(0, 1, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        new RetentionRule().Apply(context2, 0);
        new RedundancySuppressionRule().Apply(context2, 0);
        await Assert.That(context2.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context2.Candidates[0].ContainsKey(2)).IsFalse();
        await Assert.That(context2.Candidates[0].ContainsKey(3)).IsFalse();
    }

    [Test]
    public async Task BlockersNeitherSuppressNorGetSuppressed()
    {
        var blocker = RecsTestBed.Role(1, "Cooking", "Firefight"); blocker.Blocker = true;
        var basics = RecsTestBed.Role(2, "Cooking", "Firefight", "Patient");
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { blocker, basics }, RecsTestBed.Pawn()));
        context.AddCandidate(0, 1, new Reason { RuleId = "fire", TowardRoleId = -1 },
            SignalBucket.Neutral, force: true);
        context.AddCandidate(0, 2, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        new RedundancySuppressionRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue();
    }
}
