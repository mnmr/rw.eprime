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

        // Equal buckets: the block whose member sits earlier in the template
        // wins on base position, beating the lower path id.
        var positioned = RecsTestBed.Colony(new List<RoleView> { anchor, artly, smithy }, pawn);
        positioned.OrderTemplate = new List<int> { 1, 3 };
        positioned.Paths.Add(pathA);
        positioned.Paths.Add(pathB);
        var byPosition = new EngineContext(positioned);
        Candidate(byPosition, 0, 1);
        Candidate(byPosition, 0, 2, SignalBucket.Strong);
        Candidate(byPosition, 0, 3, SignalBucket.Strong);
        new OrderingRule().Apply(byPosition, 0);
        await Assert.That(RecsTestBed.Ids(byPosition.Results[0])).IsEqualTo("1,3,2");

        // Equal buckets and equal (unlisted) positions: path id decides.
        var tie = new EngineContext(colony);
        Candidate(tie, 0, 1);
        Candidate(tie, 0, 2, SignalBucket.Strong);
        Candidate(tie, 0, 3, SignalBucket.Strong);
        new OrderingRule().Apply(tie, 0);
        await Assert.That(RecsTestBed.Ids(tie.Results[0])).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task DirectRoleSharedBySeveralPathsKeepsConfiguredPosition()
    {
        var firstAnchor = RecsTestBed.Role(1, "Cooking", "First");
        var shared = RecsTestBed.Role(2, "Crafting", "Shared");
        var middle = RecsTestBed.Role(3, "Doctor", "Middle");
        var lastAnchor = RecsTestBed.Role(4, "Hunting", "Last");
        var roles = new List<RoleView> { firstAnchor, shared, middle, lastAnchor };
        var colony = RecsTestBed.Colony(roles, RecsTestBed.Pawn());
        colony.OrderTemplate = new List<int> { 1, 2, 3, 4 };
        var firstPath = RecsTestBed.Path(1, (shared.Id, 0, 21));
        firstPath.AnchorRoleId = firstAnchor.Id;
        firstPath.AnchorBefore = false;
        var secondPath = RecsTestBed.Path(2, (shared.Id, 0, 21));
        secondPath.AnchorRoleId = lastAnchor.Id;
        secondPath.AnchorBefore = false;
        colony.Paths.Add(firstPath);
        colony.Paths.Add(secondPath);
        var context = new EngineContext(colony);
        foreach (var role in roles) Candidate(context, 0, role.Id);

        new OrderingRule().Apply(context, 0);

        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3,4");
    }

    [Test]
    public async Task ExplicitHunterTemplatePositionOverridesSkillTier()
    {
        var blocker = RecsTestBed.Role(1, "Cooking", "Firefight"); blocker.Blocker = true;
        var cook = RecsTestBed.Role(2, "Cooking");
        var hunter = RecsTestBed.Role(3, "Hunting"); hunter.Hunting = true;
        var pawn = RecsTestBed.Pawn();

        // Tier 0 would naturally lead the template; the template keeps it last.
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
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3");

        // Tier 3 would naturally sort last; the template keeps it first.
        var leading = RecsTestBed.Colony(new List<RoleView> { blocker, cook, hunter }, pawn);
        leading.OrderTemplate = new List<int> { 3, 2 };
        leading.HunterRoleId = 3;
        leading.FireBlockerRoleId = 1;
        var tier3 = new EngineContext(leading);
        Candidate(tier3, 0, 1);
        Candidate(tier3, 0, 2);
        Candidate(tier3, 0, 3);
        tier3.HunterTiers[0] = 3;
        new OrderingRule().Apply(tier3, 0);
        await Assert.That(RecsTestBed.Ids(tier3.Results[0])).IsEqualTo("1,3,2");
    }

    [Test]
    public async Task UnlistedHunterUsesBasicsAndWorkRoleAnchorsForItsSkillTier()
    {
        var core = RecsTestBed.Role(1, "Cooking"); core.AutoAssign = true;
        var basics = RecsTestBed.Role(2, "BasicWorker"); // auto flag intentionally false
        var childcare = RecsTestBed.Role(3, "Childcare"); childcare.PrimarySkill = "Social";
        var warden = RecsTestBed.Role(4, "Warden"); warden.PrimarySkill = "Social";
        var doctor = RecsTestBed.Role(5, "Doctor");
        var handler = RecsTestBed.Role(6, "Handling"); handler.PrimarySkill = "Animals";
        var cook = RecsTestBed.Role(7, "Cooking");
        var grunt = RecsTestBed.Role(8, "Hauling");
        var hunter = RecsTestBed.Role(9, "Hunting"); hunter.Hunting = true;
        var roles = new List<RoleView>
            { core, basics, childcare, warden, doctor, handler, cook, grunt, hunter };
        var pawn = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(roles, pawn);
        colony.OrderTemplate = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        colony.HunterRoleId = hunter.Id;

        string AtTier(int tier)
        {
            var context = new EngineContext(colony);
            foreach (var role in roles) Candidate(context, 0, role.Id);
            context.HunterTiers[0] = tier;
            new OrderingRule().Apply(context, 0);
            return RecsTestBed.Ids(context.Results[0]);
        }

        await Assert.That(AtTier(0)).IsEqualTo("1,2,3,4,9,5,6,7,8");
        await Assert.That(AtTier(1)).IsEqualTo("1,2,3,4,5,6,9,7,8");
        await Assert.That(AtTier(2)).IsEqualTo("1,2,3,4,5,6,7,9,8");
        await Assert.That(AtTier(3)).IsEqualTo("1,2,3,4,5,6,7,8,9");
    }

    [Test]
    public async Task UnlistedLowHunterFallsAfterLeadingAutosWhenBasicsIsMissing()
    {
        var autoA = RecsTestBed.Role(1, "Cooking"); autoA.AutoAssign = true;
        var autoB = RecsTestBed.Role(2, "Doctor"); autoB.AutoAssign = true;
        var ordinary = RecsTestBed.Role(3, "Crafting");
        var hunter = RecsTestBed.Role(4, "Hunting"); hunter.Hunting = true;
        var roles = new List<RoleView> { autoA, autoB, ordinary, hunter };
        var colony = RecsTestBed.Colony(roles, RecsTestBed.Pawn());
        colony.OrderTemplate = new List<int> { 1, 2, 3 };
        colony.HunterRoleId = hunter.Id;
        var context = new EngineContext(colony);
        foreach (var role in roles) Candidate(context, 0, role.Id);
        context.HunterTiers[0] = 0;

        new OrderingRule().Apply(context, 0);

        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,4,3");
    }

    [Test]
    public async Task HunterUsesLowTierAnchorWhenTheTemplateHasNoWorkRole()
    {
        var basics = RecsTestBed.Role(1, "BasicWorker");
        var childcare = RecsTestBed.Role(2, "Childcare"); childcare.PrimarySkill = "Social";
        var grunt = RecsTestBed.Role(3, "Hauling");
        var hunter = RecsTestBed.Role(4, "Hunting"); hunter.Hunting = true;
        var roles = new List<RoleView> { basics, childcare, grunt, hunter };
        var colony = RecsTestBed.Colony(roles, RecsTestBed.Pawn());
        colony.OrderTemplate = new List<int> { 1, 2, 3 };
        colony.HunterRoleId = hunter.Id;

        string AtTier(int tier)
        {
            var context = new EngineContext(colony);
            foreach (var role in roles) Candidate(context, 0, role.Id);
            context.HunterTiers[0] = tier;
            new OrderingRule().Apply(context, 0);
            return RecsTestBed.Ids(context.Results[0]);
        }

        await Assert.That(AtTier(1)).IsEqualTo("1,2,4,3");
        await Assert.That(AtTier(2)).IsEqualTo("1,2,4,3");
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
    public async Task PinnedAssignmentsStayAnchoredWhileAutoAssignedRolesFollowTheTemplate()
    {
        var a = RecsTestBed.Role(1, "A");
        var pinned = RecsTestBed.Role(2, "Pinned");
        var b = RecsTestBed.Role(3, "B");
        var c = RecsTestBed.Role(4, "C");
        var auto = RecsTestBed.Role(5, "Auto"); auto.AutoAssign = true;
        var d = RecsTestBed.Role(6, "D");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1 });
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Pinned = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 3 });
        pawn.Existing.Add(new AssignmentView { RoleId = 4 });
        pawn.Existing.Add(new AssignmentView { RoleId = 5 });
        pawn.Existing.Add(new AssignmentView { RoleId = 6 });
        var colony = RecsTestBed.Colony(
            new List<RoleView> { a, pinned, b, c, auto, d }, pawn);
        colony.OrderTemplate = new List<int> { 4, 5, 1, 3, 6, 2 };
        var context = new EngineContext(colony);
        for (int roleId = 1; roleId <= 6; roleId++) Candidate(context, 0, roleId);

        new OrderingRule().Apply(context, 0);
        new ProtectedReentryRule().Apply(context, 0);
        new AnchorPreservationRule().Apply(context, 0);

        await Assert.That(RecsTestBed.Ids(context.Results[0]))
            .IsEqualTo("4,5,1,2,3,6");
        await Assert.That(context.Results[0].Assignments[3].Pinned).IsTrue();
    }

    [Test]
    public async Task AnchoredAssignmentFallsBackToNextNeighborThenOldIndex()
    {
        var removedA = RecsTestBed.Role(1, "RemovedA");
        var pinned = RecsTestBed.Role(2, "Pinned");
        var next = RecsTestBed.Role(3, "Next");
        var added = RecsTestBed.Role(4, "Added");
        var removedB = RecsTestBed.Role(5, "RemovedB");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1 });
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Pinned = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 3 });
        var colony = RecsTestBed.Colony(
            new List<RoleView> { removedA, pinned, next, added, removedB }, pawn);
        colony.OrderTemplate = new List<int> { 3, 2, 4 };
        var context = new EngineContext(colony);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3);
        Candidate(context, 0, 4);

        new OrderingRule().Apply(context, 0);
        new ProtectedReentryRule().Apply(context, 0);
        new AnchorPreservationRule().Apply(context, 0);

        // The preceding role disappeared, so the pin stays before its next
        // surviving neighbor even though the recommendation sorted it later.
        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("2,3,4");

        pawn.Existing.Clear();
        pawn.Existing.Add(new AssignmentView { RoleId = 1 });
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Pinned = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 5 });
        var noAnchors = new EngineContext(colony);
        Candidate(noAnchors, 0, 2);
        Candidate(noAnchors, 0, 4);
        new OrderingRule().Apply(noAnchors, 0);
        new ProtectedReentryRule().Apply(noAnchors, 0);
        new AnchorPreservationRule().Apply(noAnchors, 0);

        await Assert.That(RecsTestBed.Ids(noAnchors.Results[0])).IsEqualTo("4,2");
    }

    [Test]
    public async Task NewlyAddedAutoAssignmentKeepsItsRecommendedPosition()
    {
        var a = RecsTestBed.Role(1, "A");
        var b = RecsTestBed.Role(2, "B");
        var auto = RecsTestBed.Role(3, "Auto"); auto.AutoAssign = true;
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = 1 });
        pawn.Existing.Add(new AssignmentView { RoleId = 2 });
        var colony = RecsTestBed.Colony(new List<RoleView> { a, b, auto }, pawn);
        colony.OrderTemplate = new List<int> { 3, 2, 1 };
        var context = new EngineContext(colony);
        Candidate(context, 0, 1);
        Candidate(context, 0, 2);
        Candidate(context, 0, 3);

        new OrderingRule().Apply(context, 0);
        new ProtectedReentryRule().Apply(context, 0);
        new AnchorPreservationRule().Apply(context, 0);

        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("3,2,1");
    }

    [Test]
    public async Task ConfiguredOrderPositionsCoreAndBasicsAroundOptionalMedic()
    {
        var core = RecsTestBed.Role(1, "Core"); core.AutoAssign = true;
        var medic = RecsTestBed.Role(2, "Doctor");
        var basics = RecsTestBed.Role(3, "BasicWorker"); basics.AutoAssign = true;
        var skilled = RecsTestBed.Role(4, "Crafting");
        var withMedic = RecsTestBed.Pawn();
        withMedic.Existing.Add(new AssignmentView { RoleId = core.Id });
        withMedic.Existing.Add(new AssignmentView { RoleId = skilled.Id });
        withMedic.Existing.Add(new AssignmentView { RoleId = basics.Id });
        var withoutMedic = RecsTestBed.Pawn();
        withoutMedic.Existing.Add(new AssignmentView { RoleId = core.Id });
        withoutMedic.Existing.Add(new AssignmentView { RoleId = skilled.Id });
        withoutMedic.Existing.Add(new AssignmentView { RoleId = basics.Id });
        var colony = RecsTestBed.Colony(
            new List<RoleView> { core, medic, basics, skilled },
            withMedic, withoutMedic);
        colony.OrderTemplate = new List<int> { core.Id, medic.Id, basics.Id, skilled.Id };
        var context = new EngineContext(colony);
        Candidate(context, 0, core.Id);
        Candidate(context, 0, medic.Id);
        Candidate(context, 0, basics.Id);
        Candidate(context, 0, skilled.Id);
        Candidate(context, 1, core.Id);
        Candidate(context, 1, basics.Id);
        Candidate(context, 1, skilled.Id);

        for (int pawnIndex = 0; pawnIndex < 2; pawnIndex++)
        {
            new OrderingRule().Apply(context, pawnIndex);
            new ProtectedReentryRule().Apply(context, pawnIndex);
            new AnchorPreservationRule().Apply(context, pawnIndex);
        }

        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3,4");
        await Assert.That(RecsTestBed.Ids(context.Results[1])).IsEqualTo("1,3,4");
    }

    [Test]
    public async Task TrainingPathPositionsDoctorAfterExistingCoreForEverySelectionReason()
    {
        var core = RecsTestBed.Role(1, "Core"); core.AutoAssign = true;
        var doctor = RecsTestBed.Role(2, "Doctor");
        var basics = RecsTestBed.Role(3, "BasicWorker"); basics.AutoAssign = true;
        var skilled = RecsTestBed.Role(4, "Crafting");
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = core.Id });
        pawn.Existing.Add(new AssignmentView { RoleId = skilled.Id });
        pawn.Existing.Add(new AssignmentView { RoleId = basics.Id });
        var colony = RecsTestBed.Colony(
            new List<RoleView> { core, doctor, basics, skilled }, pawn);
        colony.OrderTemplate = new List<int> { core.Id, basics.Id, skilled.Id };
        var path = RecsTestBed.Path(1, (doctor.Id, 15, 21));
        path.AnchorRoleId = core.Id;
        path.AnchorBefore = false;
        colony.Paths.Add(path);
        var context = new EngineContext(colony);
        Candidate(context, 0, core.Id);
        Candidate(context, 0, doctor.Id);
        Candidate(context, 0, basics.Id);
        Candidate(context, 0, skilled.Id);

        new OrderingRule().Apply(context, 0);
        new ProtectedReentryRule().Apply(context, 0);
        new AnchorPreservationRule().Apply(context, 0);

        await Assert.That(RecsTestBed.Ids(context.Results[0])).IsEqualTo("1,2,3,4");
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
        chef.SkillLevels["Cooking"] = 9; chef.SignalBuckets["Cooking"] = SignalBucket.Great;
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
