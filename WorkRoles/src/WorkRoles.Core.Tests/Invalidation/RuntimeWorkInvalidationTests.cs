using RimShared.Common;
using WorkRoles.Core.Tests.Planner;

namespace WorkRoles.Core.Tests.Invalidation;

// These exercise the independently meaningful cache/transition contract through
// actual compiled giver orders, including identity and builder counters.
public class RuntimeWorkInvalidationTests
{
    [Test]
    public async Task EquivalentActivationRetainsOrdersButRealRevocationChangesWork()
    {
        var pawn = new object();
        var catalog = new FakeCatalog().WithWorkType("Cook", "Stove").WithWorkType("Haul", "Carry");
        bool enabled = true;
        AssignmentState state = AssignmentState.ForceOn;
        int builds = 0, presentations = 0;
        var cache = new ExplicitProjectionCache<object, CompiledOrder>(_ =>
        {
            builds++;
            var roles = new List<IReadOnlyList<JobEntry>>();
            if (RoleActivation.IsActive(enabled, state)) roles.Add([new(JobEntryKind.WorkType, "Cook")]);
            roles.Add([new(JobEntryKind.WorkType, "Haul")]);
            return JobOrderCompiler.Compile(roles, catalog, _ => true);
        }, (_, _) => { });
        void Toggle(bool nextEnabled, AssignmentState nextState)
        {
            if (RoleActivation.ChangesExecution(enabled, state, nextEnabled, nextState)) cache.Remove(pawn);
            enabled = nextEnabled;
            state = nextState;
            presentations++;
        }
        CompiledOrder first = cache.GetOrBuild(pawn);
        Toggle(true, AssignmentState.Enabled);
        bool keptEquivalent = ReferenceEquals(first, cache.GetOrBuild(pawn));
        Toggle(true, AssignmentState.Disabled);
        CompiledOrder revoked = cache.GetOrBuild(pawn);
        Toggle(false, AssignmentState.Disabled);
        bool keptDisabled = ReferenceEquals(revoked, cache.GetOrBuild(pawn));
        Toggle(false, AssignmentState.ForceOn);
        CompiledOrder forced = cache.GetOrBuild(pawn);
        Toggle(true, AssignmentState.ForceOn);
        bool keptForced = ReferenceEquals(forced, cache.GetOrBuild(pawn));
        Console.WriteLine($"activation: builds={builds}, presentation changes={presentations}");
        await Assert.That(keptEquivalent && keptDisabled && keptForced).IsTrue();
        await Assert.That(string.Join(",", revoked.AllInOrder)).IsEqualTo("Carry");
        await Assert.That(string.Join(",", forced.AllInOrder)).IsEqualTo("Stove,Carry");
        await Assert.That(builds).IsEqualTo(3);
        await Assert.That(presentations).IsEqualTo(5);
        await Assert.That(RoleActivation.ChangesExecution(true, AssignmentState.Enabled,
            false, AssignmentState.Enabled, rulesPass: false)).IsFalse();
    }

    [Test]
    public async Task AFullDayRecompilesOnlyWhenTheScheduleChangesItsActiveContribution()
    {
        var pawn = new object();
        var catalog = new FakeCatalog().WithWorkType("Cook", "Stove");
        const int hours = (1 << 8) | (1 << 9) | (1 << 10);
        int hour = 0, observedHour = 0, builds = 0;
        var cache = new ExplicitProjectionCache<object, CompiledOrder>(_ =>
        {
            builds++;
            return JobOrderCompiler.Compile((hours & (1 << hour)) != 0
                ? new IReadOnlyList<JobEntry>[] { new[] { new JobEntry(JobEntryKind.WorkType, "Cook") } }
                : Array.Empty<IReadOnlyList<JobEntry>>(), catalog, _ => true);
        }, (_, _) => { });
        cache.GetOrBuild(pawn);
        var activeHours = new List<int>();
        for (int next = 1; next <= 24; next++)
        {
            hour = next % 24;
            var plan = TimedRoleInvalidationPlanner.Plan(
                [new TimedRoleInvalidationSource(1, hours)],
                [new TimedRoleHolderAssignment<object>(pawn, 1, 1, AssignmentState.Enabled, observedHour, hour)]);
            foreach (object target in plan.Pawns) cache.Remove(target);
            observedHour = hour;
            if (cache.GetOrBuild(pawn).AllInOrder.Count > 0) activeHours.Add(hour);
        }
        Console.WriteLine($"24 hour boundaries: builds={builds}, active hours={string.Join(",", activeHours)}");
        await Assert.That(activeHours).IsEquivalentTo([8, 9, 10]);
        await Assert.That(builds).IsEqualTo(3);
    }

    [Test]
    public async Task HourJumpsRespectForcedAssignmentsSuppressionUnknownClocksAndPresentation()
    {
        object disabled = new(), forced = new(), wrongLocation = new(), unchanged = new(), midnight = new(), unknown = new();
        const int morning = (1 << 8) | (1 << 9);
        var plan = TimedRoleInvalidationPlanner.Plan(
            [new TimedRoleInvalidationSource(1, morning, enabled: false),
             new TimedRoleInvalidationSource(2, morning),
             new TimedRoleInvalidationSource(3, 1 << 23)],
            [new TimedRoleHolderAssignment<object>(disabled, 1, 1, AssignmentState.Enabled, 9, 12),
             new TimedRoleHolderAssignment<object>(forced, 2, 1, AssignmentState.ForceOn, 9, 12),
             new TimedRoleHolderAssignment<object>(wrongLocation, 3, 2, AssignmentState.Enabled, 9, 12, locationMatches: false),
             new TimedRoleHolderAssignment<object>(unchanged, 4, 2, AssignmentState.Enabled, 8, 9),
             new TimedRoleHolderAssignment<object>(midnight, 5, 3, AssignmentState.Enabled, 23, 0),
             new TimedRoleHolderAssignment<object>(unknown, 6, 2, AssignmentState.Enabled, -1, 12)]);
        await Assert.That(plan.Pawns).IsEquivalentTo([forced, midnight, unknown]);
        await Assert.That(plan.PresentationPawns).IsEquivalentTo([disabled, forced, wrongLocation, midnight, unknown]);
        var repeated = TimedRoleInvalidationPlanner.Plan(
            [new TimedRoleInvalidationSource(2, morning)],
            [new TimedRoleHolderAssignment<object>(wrongLocation, 3, 2, AssignmentState.Enabled, 12, 13, locationMatches: false),
             new TimedRoleHolderAssignment<object>(unknown, 6, 2, AssignmentState.Enabled, -1, -1)]);
        await Assert.That(repeated.Pawns).IsEmpty();
        await Assert.That(repeated.PresentationPawns).IsEmpty();
        var revisions = new ModelRevisions();
        repeated.ApplyRuntime(_ => revisions.Changed(ModelDomain.RuntimeRules),
            _ => throw new Exception("unchanged rules cannot evict compiled work"));
        await Assert.That(revisions.Current).IsEqualTo(0);
    }

    [Test]
    public async Task NonbuildingObservationPreservesTheArtifactAndContextDrainLeavesOtherMapsQueued()
    {
        object mapA = new(), mapB = new();
        int builds = 0;
        var cache = new ExplicitProjectionCache<object, object>(_ => { builds++; return new object(); }, (_, _) => { });
        await Assert.That(cache.TryGetCached(mapA, out _)).IsFalse();
        object first = cache.GetOrBuild(mapA);
        await Assert.That(cache.TryGetCached(mapA, out var observed)).IsTrue();
        await Assert.That(ReferenceEquals(first, observed)).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
        var pending = new ContextualDrainQueue<object>();
        pending.Enqueue(mapA);
        pending.Enqueue(mapA);
        pending.Enqueue(mapB);
        var firstDrain = pending.Drain(mapA, ReferenceEquals);
        await Assert.That(firstDrain).IsEquivalentTo([mapA]);
        await Assert.That(pending.Drain(mapB, ReferenceEquals)).IsEquivalentTo([mapB]);
        await Assert.That(pending.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OneBoundaryCombinesQueuedEditsAndClockChangesIntoOneCompilation()
    {
        var pawn = new object();
        var catalog = new FakeCatalog().WithWorkType("Cook", "Stove").WithWorkType("Haul", "Carry");
        int builds = 0;
        var queue = new ContextualDrainQueue<object>();
        var cache = new ExplicitProjectionCache<object, CompiledOrder>(_ =>
        {
            builds++;
            return JobOrderCompiler.Compile(new IReadOnlyList<JobEntry>[]
                { new[] { new JobEntry(JobEntryKind.WorkType, "Haul") } }, catalog, _ => true);
        }, (_, _) => { });
        queue.Enqueue(pawn); // a preceding authoritative command left it cold
        RuntimeWorkBoundary.Process(true,
            () => { cache.Remove(pawn); queue.Enqueue(pawn); },
            () => { foreach (object target in queue.Drain(_ => true)) cache.GetOrBuild(target); });
        var final = cache.GetOrBuild(pawn);
        Console.WriteLine($"combined command/hour boundary: builds={builds}");
        await Assert.That(string.Join(",", final.AllInOrder)).IsEqualTo("Carry");
        await Assert.That(builds).IsEqualTo(1);
        await Assert.That(queue.Count).IsEqualTo(0);
    }
}
