using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 11 (template positions; anchored path blocks band-min-descending;
/// same-anchor tie-breaks by bucket then position), rule 12 (protected
/// re-entry) and the assembled default pipeline.
public class RecsOrderingTests
{
    private static void Candidate(EngineContext context, int pawn, int roleId,
        SignalBucket strength = SignalBucket.Neutral)
        => context.AddCandidate(pawn, roleId,
            new Reason { RuleId = "test", Bucket = strength, TowardRoleId = -1 }, strength);

    [Test]
    public async Task CandidatesSortByTemplatePosition_TogglesComeFromExisting()
    {
        var a = RecsTestBed.Role(1, "Cooking");
        var b = RecsTestBed.Role(2, "Crafting");
        var c = RecsTestBed.Role(3, "Doctor");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 3, Enabled = false });
        var colony = RecsTestBed.Colony(new List<RoleView> { a, b, c }, pawn);
        colony.OrderTemplate = new List<int> { 3, 1, 2 };
        var context = new EngineContext(colony);
        Candidate(context, 0, 1);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3);
        new OrderingRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("3,1,2");
        await Assert.That(context.Results[0].Assignments[0].Enabled).IsFalse();
        await Assert.That(context.Results[0].Reasons.ContainsKey(1)).IsTrue();
    }

    [Test]
    public async Task AnchoredPathBlocksPlaceBandMinDescendingAtTheirAnchor()
    {
        var anchor = RecsTestBed.Role(1, "Cooking");
        var crafter = RecsTestBed.Role(2, "Crafting", "Craft");
        var smith = RecsTestBed.Role(3, "Crafting", "Smith");
        var fab = RecsTestBed.Role(4, "Crafting", "Fab");
        var pawn = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(new List<RoleView> { anchor, crafter, smith, fab }, pawn);
        colony.OrderTemplate = new List<int> { 1 };
        // Stored path order is display packing; assignment order is min DESC.
        var path = RecsTestBed.Path(1, (2, 0, 21), (3, 4, 21), (4, 8, 21));
        path.AnchorRoleId = 1;
        path.AnchorBefore = false;
        colony.Paths.Add(path);
        var context = new EngineContext(colony);
        Candidate(context, 0, 1);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3);
        Candidate(context, 0, 4);
        new OrderingRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,4,3,2");
    }

    [Test]
    public async Task SameAnchorBlocksTieBreakByBucketThenPositionThenPathId()
    {
        var anchor = RecsTestBed.Role(1, "Cooking");
        var artly = RecsTestBed.Role(2, "Crafting", "Art");
        var smithy = RecsTestBed.Role(3, "Doctor", "Smithy");
        var pawn = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(new List<RoleView> { anchor, artly, smithy }, pawn);
        colony.OrderTemplate = new List<int> { 1 };
        var pathA = RecsTestBed.Path(1, (2, 0, 21)); pathA.AnchorRoleId = 1; pathA.AnchorBefore = false;
        var pathB = RecsTestBed.Path(2, (3, 0, 21)); pathB.AnchorRoleId = 1; pathB.AnchorBefore = false;
        colony.Paths.Add(pathA);
        colony.Paths.Add(pathB);
        var context = new EngineContext(colony);
        Candidate(context, 0, 1);
        Candidate(context, 0, 2, SignalBucket.Strong);
        Candidate(context, 0, 3, SignalBucket.Great);   // stronger block leads
        new OrderingRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,3,2");

        // Equal buckets: lower base position wins; then path id.
        var tie = new EngineContext(colony);
        Candidate(tie, 0, 1);
        Candidate(tie, 0, 2, SignalBucket.Strong);
        Candidate(tie, 0, 3, SignalBucket.Strong);
        new OrderingRule().Apply(tie, 0);
        await Assert.That(RecsTestBed.Ids(tie.Results[0])).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task HunterTiersAndFireBlockerOverrideTheTemplate()
    {
        var blocker = RecsTestBed.Role(1, "Cooking", "Firefight"); blocker.Blocker = true;
        var cook = RecsTestBed.Role(2, "Cooking");
        var hunter = RecsTestBed.Role(3, "Hunting"); hunter.Hunting = true;
        var pawn = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(new List<RoleView> { blocker, cook, hunter }, pawn);
        colony.OrderTemplate = new List<int> { 2, 3 };
        colony.HunterRoleId = 3;
        colony.FireBlockerRoleId = 1;
        var context = new EngineContext(colony);
        Candidate(context, 0, 1);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3);
        context.HunterTiers[0] = 0;
        new OrderingRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,3,2");
        context.HunterTiers[0] = 2;
        new OrderingRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task ProtectedAssignmentsReenterAtTheirOriginalIndexWithToggles()
    {
        var rules = RecsTestBed.Role(1, "Cooking", "Night"); rules.HasRules = true;
        var cook = RecsTestBed.Role(2, "Cooking");
        var pinnedRole = RecsTestBed.Role(3, "Crafting");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = false });
        pawn.Existing.Add(new AssignmentView { RoleId = 3, Enabled = true, Pinned = true });
        var colony = RecsTestBed.Colony(new List<RoleView> { rules, cook, pinnedRole }, pawn);
        var context = new EngineContext(colony);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3); // pinned candidate: sorts normally, pin kept
        new OrderingRule().Apply(context, 0);
        new ProtectedReentryRule().Apply(context, 0);
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3");
        await Assert.That(context.Results[0].Assignments[0].Enabled).IsFalse();
        await Assert.That(context.Results[0].Assignments[2].Pinned).IsTrue();
    }

    [Test]
    public async Task DefaultPipelineRunsEndToEnd()
    {
        var basics = RecsTestBed.Role(1, "Cooking", "Fire", "Patient");
        basics.AutoAssign = true; basics.NaturalPriority = 100f;
        var cook = RecsTestBed.Role(2, "Cooking");
        cook.MinHolders = 1;
        var doctor = RecsTestBed.Role(3, "Doctor");
        var chef = RecsTestBed.Pawn();
        chef.SkillLevels["Cooking"] = 9; chef.PassionScores["Cooking"] = 2;
        var idler = RecsTestBed.Pawn();
        idler.SkillLevels["Cooking"] = 2; idler.SkillLevels["Medicine"] = 6;
        var colony = RecsTestBed.Colony(new List<RoleView> { basics, cook, doctor }, chef, idler);
        var results = RecsEngine.Run(colony);
        // Chef: auto + passion cook; idler: auto only (Neutral medicine is no signal).
        await Assert.That(RecsTestBed.Ids(results[0])).IsEqualTo("1,2");
        await Assert.That(results[0].Reasons[2].RuleId).IsEqualTo("signals");
        await Assert.That(RecsTestBed.Ids(results[1])).IsEqualTo("1");
        await Assert.That(results[1].HunterTier).IsEqualTo(-1);
    }
}
