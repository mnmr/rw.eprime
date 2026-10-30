using WorkRoles.Core.Recs;
using WorkRoles.Core.Tests.SampleColony;

namespace WorkRoles.Core.Tests.Planner;

// Identity checks protect a cache contract that equivalent colony outcomes
// cannot establish: unrelated edits/hits must not recompute the planner.
public class CompositeStateCacheRegressionTests
{
    [Test]
    public async Task StateChangesInExcludedCompositeDoNotInvalidateOrdinaryRecommendations()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), pawn = new(), catalog = new();
        RecommendationSnapshot Read(int activation, bool enabled) => cache.For(owner, [pawn], catalog,
            0, 0, 0, RecommendationsTuningOptions.Default, (_, _) =>
            {
                var first = RecsTestBed.Unskilled(1, "Hauling");
                var excludedMember = RecsTestBed.Unskilled(2, "Cleaning");
                first.AutoAssign = true;
                excludedMember.ExcludedFromRecommendations = true;
                ColonyView colony = RecsTestBed.Colony([first, excludedMember], new PawnView
                {
                    CapableWorkTypes = { "Hauling", "Cleaning" },
                    Existing = { new() { RoleId = 1, Enabled = enabled } },
                });
                RecommendationProtectionRegressionTests.Bundle(colony, 1, 2);
                return colony;
            }, assignmentActivation: activation);
        var first = Read(0, true);
        await Assert.That(ReferenceEquals(first, Read(1, false))).IsTrue();
    }

    [Test]
    public async Task NewCompositeObservesPreviouslyUnrelatedStateEvenWhenEqualPlanWasRetained()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = Enumerable.Range(0, 27).Select(_ => new object()).ToArray();
        RecommendationSnapshot Read(int stamp, bool addCraftingBundle)
            => cache.For(owner, pawns, catalog, 0, 0, 0, RecommendationsTuningOptions.Default, (_, previous) =>
            {
                var capture = new CapturedColony("1.7");
                if (previous != null) capture.Colony.Pawns = previous.ToList();
                else if (stamp > 0)
                {
                    var existing = capture.Colony.Pawns[0].Existing;
                    int index = existing.FindIndex(a => a.RoleId == 21);
                    AssignmentView assignment = existing[index];
                    assignment.Enabled = false;
                    existing[index] = assignment;
                }
                RecommendationProtectionRegressionTests.Bundle(capture.Colony, 43, 1);
                if (addCraftingBundle)
                {
                    RoleView bundle = RecommendationProtectionRegressionTests.Bundle(capture.Colony, 21, 20);
                    bundle.Id = 1000;
                }
                return capture.Colony;
            }, assignmentActivation: stamp);
        var first = Read(0, false);
        await Assert.That(ReferenceEquals(first, Read(1, false))).IsTrue();
        catalog = new object();
        var introduced = Read(1, true);
        await Assert.That(RecsProjection.Holds(introduced.Plan, 0, 1000)).IsFalse();
        await Assert.That(RecsProjection.Holds(introduced.Plan, 0, 21)).IsTrue();
    }

    [Test]
    public async Task AddingCompositeAfterStateEditRecapturesAssignmentInsteadOfReusingStateFreeProjection()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), pawnIdentity = new(), external = new(), ordinaryCatalog = new(), compositeCatalog = new();
        bool enabled = true;
        int pawnBuilds = 0;
        PawnView BuildPawn(bool _)
        {
            pawnBuilds++;
            return new PawnView { CapableWorkTypes = { "Hauling", "Cleaning" },
                Existing = { new() { RoleId = 1, Enabled = enabled }, new() { RoleId = 2, Enabled = true } } };
        }
        RecommendationSnapshot Read(bool composite) => cache.For(owner, [pawnIdentity],
            composite ? compositeCatalog : ordinaryCatalog, 7, 0, 0, RecommendationsTuningOptions.Default,
            (_, previous) =>
            {
                var first = RecsTestBed.Unskilled(1, "Hauling");
                var second = RecsTestBed.Unskilled(2, "Cleaning");
                first.AutoAssign = second.AutoAssign = true;
                PawnView facts = previous?[0] ?? cache.PawnFor(owner, pawnIdentity, 7, external, false, BuildPawn,
                    assignmentActivation: composite ? 7 : 0);
                ColonyView colony = RecsTestBed.Colony([first, second], facts);
                if (composite) RecommendationProtectionRegressionTests.Bundle(colony, 1, 2);
                return colony;
            }, assignmentActivation: composite ? 7 : 0);
        var initial = Read(false);
        enabled = false;
        await Assert.That(ReferenceEquals(initial, Read(false))).IsTrue();
        var introduced = Read(true);
        await Assert.That(RecsProjection.Holds(introduced.Plan, 0, 999)).IsFalse();
        await Assert.That(RecsProjection.Holds(introduced.Plan, 0, 1)).IsTrue();
        await Assert.That(pawnBuilds).IsEqualTo(2);
    }

    [Test]
    public async Task CompositeStateRefreshesOnlyConsumedPlanInputsAndSharesSuitability()
    {
        object owner = new(), catalog = new();
        object[] pawns = Enumerable.Range(0, 27).Select(_ => new object()).ToArray();
        var cache = new RecommendationSnapshotCache<object>();
        int builds = 0;
        RecommendationSnapshot Read(int stamp, int stateRole = 0, bool forceOn = false)
            => cache.For(owner, pawns, catalog, 0, 0, 0, RecommendationsTuningOptions.Default, (_, _) =>
            {
                builds++;
                var capture = new CapturedColony("1.7");
                RecommendationProtectionRegressionTests.Bundle(capture.Colony, 43, 1);
                var existing = capture.Colony.Pawns[0].Existing;
                int index = existing.FindIndex(a => a.RoleId == stateRole);
                if (index >= 0)
                {
                    AssignmentView changed = existing[index];
                    changed.Enabled = forceOn;
                    changed.ForceOn = forceOn;
                    existing[index] = changed;
                }
                return capture.Colony;
            }, assignmentActivation: stamp);

        var first = Read(0);
        var baselinePlan = first.Plan;
        var badges = first.Suitability;
        await Assert.That(RecsProjection.Holds(baselinePlan, 0, 999)).IsTrue();
        await Assert.That(ReferenceEquals(first, Read(0))).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
        await Assert.That(ReferenceEquals(first, Read(1))).IsTrue(); // equal refresh
        await Assert.That(ReferenceEquals(first, Read(2, 16))).IsTrue(); // Farmer outside the composite

        var disabled = Read(3, 43);
        await Assert.That(RecsProjection.Holds(disabled.Plan, 0, 999)).IsFalse();
        await Assert.That(RecsProjection.Holds(disabled.Plan, 0, 43)).IsTrue();
        await Assert.That(ReferenceEquals(badges, disabled.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(disabled, Read(4, 43))).IsTrue();
        var forced = Read(5, 43, forceOn: true);
        await Assert.That(RecsProjection.Holds(forced.Plan, 0, 999)).IsFalse();
        await Assert.That(ReferenceEquals(badges, forced.Suitability)).IsTrue();
        var restored = Read(6);
        await Assert.That(RecsProjection.Holds(restored.Plan, 0, 999)).IsTrue();
        await Assert.That(ReferenceEquals(badges, restored.Suitability)).IsTrue();
        cache.Reset();
        await Assert.That(ReferenceEquals(restored, Read(6))).IsFalse();
    }
}
