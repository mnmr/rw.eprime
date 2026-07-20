using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class TimedRoleInvalidationPlannerTests
{
    [Test]
    public async Task PlanDeduplicatesAndStablyOrdersRolesAndPawns()
    {
        var pawn30 = new PawnToken("pawn-30");
        var pawn10 = new PawnToken("pawn-10");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[]
            {
                new TimedRoleInvalidationSource(7, hasTimeRule: true),
                new TimedRoleInvalidationSource(3, hasTimeRule: true),
                new TimedRoleInvalidationSource(7, hasTimeRule: true),
            },
            new[]
            {
                Assignment(pawn30, 30, 7),
                Assignment(pawn30, 30, 7),
                Assignment(pawn10, 10, 3),
                Assignment(pawn10, 10, 7),
                Assignment(pawn30, 30, 3),
            });

        await Assert.That(string.Join(",", plan.RoleIds)).IsEqualTo("3,7");
        await Assert.That(string.Join(",", plan.Pawns.Select(pawn => pawn.Name)))
            .IsEqualTo("pawn-10,pawn-30");
    }

    [Test]
    public async Task ApplyRunsUniqueEffectsInOrderAndCompletesOnce()
    {
        var pawn20 = new PawnToken("pawn-20");
        var pawn10 = new PawnToken("pawn-10");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[]
            {
                new TimedRoleInvalidationSource(4, hasTimeRule: true),
                new TimedRoleInvalidationSource(2, hasTimeRule: true),
                new TimedRoleInvalidationSource(4, hasTimeRule: true),
            },
            new[]
            {
                Assignment(pawn20, 20, 4),
                Assignment(pawn20, 20, 2),
                Assignment(pawn10, 10, 2),
                Assignment(pawn20, 20, 4),
            });
        var calls = new List<string>();

        plan.Apply(
            roleId => calls.Add("role:" + roleId),
            pawn => calls.Add("pawn:" + pawn.Name),
            () => calls.Add("complete"));

        await Assert.That(string.Join(",", calls))
            .IsEqualTo("role:2,role:4,pawn:pawn-10,pawn:pawn-20,complete");
        await Assert.That(calls.Count(call => call == "complete")).IsEqualTo(1);
    }

    [Test]
    public async Task NoTimedRolesProducesNoTargetsOrCompletion()
    {
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[] { new TimedRoleInvalidationSource(2, hasTimeRule: false) },
            ThrowingAssignments());
        int completions = 0;

        plan.Apply(_ => throw new Exception("unexpected role"),
            _ => throw new Exception("unexpected pawn"), () => completions++);

        await Assert.That(plan.RoleIds.Count).IsEqualTo(0);
        await Assert.That(plan.Pawns.Count).IsEqualTo(0);
        await Assert.That(completions).IsEqualTo(0);
    }

    [Test]
    public async Task DistinctPawnsWithOneStableOrderAreBothRetained()
    {
        var first = new PawnToken("first");
        var second = new PawnToken("second");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[] { new TimedRoleInvalidationSource(2, hasTimeRule: true) },
            new[] { Assignment(first, 10, 2), Assignment(second, 10, 2) });

        await Assert.That(plan.Pawns.Count).IsEqualTo(2);
        await Assert.That(plan.Pawns.Any(pawn => ReferenceEquals(pawn, first))).IsTrue();
        await Assert.That(plan.Pawns.Any(pawn => ReferenceEquals(pawn, second))).IsTrue();
    }

    [Test]
    public async Task EqualStableOrdersUseExplicitFirstSeenOrder()
    {
        var first = new PawnToken("first");
        var second = new PawnToken("second");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[] { new TimedRoleInvalidationSource(2, hasTimeRule: true) },
            new[]
            {
                Assignment(first, 10, 2),
                Assignment(second, 10, 2),
                Assignment(first, 20, 2),
            });

        await Assert.That(string.Join(",", plan.Pawns.Select(pawn => pawn.Name)))
            .IsEqualTo("first,second");
    }

    [Test]
    public async Task UniqueStableKeysAreInputOrderInvariantAndUnrelatedPawnsStayExcluded()
    {
        var first = new PawnToken("first");
        var second = new PawnToken("second");
        var unrelated = new PawnToken("unrelated");
        var roles = new[]
        {
            new TimedRoleInvalidationSource(7, hasTimeRule: true),
            new TimedRoleInvalidationSource(3, hasTimeRule: true),
            new TimedRoleInvalidationSource(99, hasTimeRule: false),
        };
        var assignments = new[]
        {
            Assignment(first, 30, 7),
            Assignment(unrelated, 5, 99),
            Assignment(second, 20, 3),
            Assignment(first, 10, 3),
        };

        var forward = TimedRoleInvalidationPlanner.Plan(roles, assignments);
        var reverse = TimedRoleInvalidationPlanner.Plan(roles.Reverse(), assignments.Reverse());

        await Assert.That(string.Join(",", forward.RoleIds)).IsEqualTo("3,7");
        await Assert.That(string.Join(",", reverse.RoleIds)).IsEqualTo("3,7");
        await Assert.That(string.Join(",", forward.Pawns.Select(pawn => pawn.Name)))
            .IsEqualTo("first,second");
        await Assert.That(string.Join(",", reverse.Pawns.Select(pawn => pawn.Name)))
            .IsEqualTo("first,second");
        await Assert.That(forward.Pawns.Contains(unrelated)).IsFalse();
        await Assert.That(reverse.Pawns.Contains(unrelated)).IsFalse();
    }

    [Test]
    public async Task TimedRoleWithoutHoldersStillCompletesOneBatch()
    {
        var calls = new List<string>();
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[] { new TimedRoleInvalidationSource(9, hasTimeRule: true) },
            Array.Empty<TimedRoleHolderAssignment<PawnToken>>());

        plan.Apply(roleId => calls.Add("role:" + roleId),
            _ => calls.Add("pawn"), () => calls.Add("complete"));

        await Assert.That(string.Join(",", calls)).IsEqualTo("role:9,complete");
    }

    [Test]
    public async Task DisabledBlockerAutoAndPinnedSourcesRemainTimeRuled()
    {
        var pawn = new PawnToken("pawn");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[]
            {
                new TimedRoleInvalidationSource(5, hasTimeRule: true,
                    enabled: false, blocker: true, autoAssign: true),
            },
            new[]
            {
                new TimedRoleHolderAssignment<PawnToken>(pawn, 1, 5,
                    enabled: false, pinned: true),
            });

        await Assert.That(plan.RoleIds.Single()).IsEqualTo(5);
        await Assert.That(ReferenceEquals(plan.Pawns.Single(), pawn)).IsTrue();
    }

    [Test]
    public async Task PlanCopiesInputsAndPublishesReadOnlyCollections()
    {
        var pawn = new PawnToken("pawn");
        var roles = new List<TimedRoleInvalidationSource>
        {
            new TimedRoleInvalidationSource(5, hasTimeRule: true),
        };
        var assignments = new List<TimedRoleHolderAssignment<PawnToken>>
        {
            new TimedRoleHolderAssignment<PawnToken>(pawn, 1, 5),
        };
        var plan = TimedRoleInvalidationPlanner.Plan(roles, assignments);

        roles.Clear();
        assignments.Clear();

        await Assert.That(plan.RoleIds.Single()).IsEqualTo(5);
        await Assert.That(ReferenceEquals(plan.Pawns.Single(), pawn)).IsTrue();
        await Assert.That(() => ((IList<int>)plan.RoleIds).Add(8))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<PawnToken>)plan.Pawns).Clear())
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task PawnDedupUsesReferenceIdentityInsteadOfValueEquality()
    {
        var first = new ValueEqualPawn("same");
        var second = new ValueEqualPawn("same");
        var plan = TimedRoleInvalidationPlanner.Plan(
            new[] { new TimedRoleInvalidationSource(1, hasTimeRule: true) },
            new[]
            {
                new TimedRoleHolderAssignment<ValueEqualPawn>(first, 10, 1),
                new TimedRoleHolderAssignment<ValueEqualPawn>(second, 20, 1),
            });

        await Assert.That(plan.Pawns.Count).IsEqualTo(2);
        await Assert.That(ReferenceEquals(plan.Pawns[0], first)).IsTrue();
        await Assert.That(ReferenceEquals(plan.Pawns[1], second)).IsTrue();
    }

    private static TimedRoleHolderAssignment<TPawn> Assignment<TPawn>(
        TPawn pawn, int stableOrder, int roleId) where TPawn : class =>
        new TimedRoleHolderAssignment<TPawn>(pawn, stableOrder, roleId);

    private static IEnumerable<TimedRoleHolderAssignment<PawnToken>> ThrowingAssignments()
    {
        throw new Exception("holders must not be enumerated without timed work");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class PawnToken
    {
        public PawnToken(string name) => Name = name;
        public string Name { get; }
    }

    private sealed class ValueEqualPawn
    {
        private readonly string value;
        public ValueEqualPawn(string value) => this.value = value;
        public override bool Equals(object obj) => obj is ValueEqualPawn other && other.value == value;
        public override int GetHashCode() => value.GetHashCode();
    }
}
