using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class IdentityKeySweepPlannerTests
{
    [Test]
    public async Task ReferenceIdentityDictionaryKeepsValueEqualKeysSeparateAndAliasesStable()
    {
        var first = new IdentityKey(1);
        var equalButDifferentReference = new IdentityKey(1);
        var alias = first;
        var mapped = new Dictionary<IdentityKey, int>(
            ReferenceIdentityComparer<IdentityKey>.Instance)
        {
            [first] = 10,
            [equalButDifferentReference] = 20,
        };

        await Assert.That(mapped.Count).IsEqualTo(2);
        await Assert.That(mapped[alias]).IsEqualTo(10);
        await Assert.That(mapped[equalButDifferentReference]).IsEqualTo(20);
    }

    [Test]
    public async Task StaleKeysUseReferenceIdentityAndIgnoreNullDuplicateLiveRefs()
    {
        var live = new IdentityKey(1);
        var equalButDifferentReference = new IdentityKey(1);
        var stale = new IdentityKey(2);
        IdentityKey[] stored = { live, equalButDifferentReference, stale, null };
        IdentityKey[] liveKeys = { null, live, live };

        IReadOnlyList<IdentityKey> planned =
            IdentityKeySweepPlanner.StaleKeys(stored, liveKeys);

        await Assert.That(planned.Count).IsEqualTo(3);
        await Assert.That(ReferenceEquals(planned[0], equalButDifferentReference)).IsTrue();
        await Assert.That(ReferenceEquals(planned[1], stale)).IsTrue();
        await Assert.That(planned[2]).IsNull();
    }

    [Test]
    public async Task ApplyingThePlanMakesTheNextSweepIdempotent()
    {
        var live = new IdentityKey(1);
        var stale = new IdentityKey(2);
        var stored = new List<IdentityKey> { live, stale };

        IReadOnlyList<IdentityKey> first =
            IdentityKeySweepPlanner.StaleKeys(stored, new[] { live });
        stored.RemoveAll(candidate => first.Any(
            planned => ReferenceEquals(candidate, planned)));
        IReadOnlyList<IdentityKey> second =
            IdentityKeySweepPlanner.StaleKeys(stored, new[] { live, live });

        await Assert.That(first.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(first[0], stale)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HashSetOverloadDoesNotTrustValueEqualityComparer()
    {
        var live = new IdentityKey(1);
        var equalButDifferentReference = new IdentityKey(1);
        var valueEqualitySet = new HashSet<IdentityKey> { live };

        IReadOnlyList<IdentityKey> planned = IdentityKeySweepPlanner.StaleKeys(
            new[] { equalButDifferentReference }, valueEqualitySet);

        await Assert.That(planned.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(planned[0], equalButDifferentReference)).IsTrue();
    }

    private sealed class IdentityKey
    {
        private readonly int value;

        internal IdentityKey(int value) => this.value = value;

        public override bool Equals(object obj) =>
            obj is IdentityKey other && other.value == value;

        public override int GetHashCode() => value;
    }
}
