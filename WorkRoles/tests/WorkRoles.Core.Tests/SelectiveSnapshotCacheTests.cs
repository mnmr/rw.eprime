using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class SelectiveSnapshotCacheTests
{
    [Test]
    public async Task TargetedInvalidationRebuildsOnlyTheSpecifiedOwner()
    {
        var first = new Owner("first");
        var second = new Owner("second");
        var builds = new Dictionary<Owner, int>();
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, int>(owner =>
        {
            builds.TryGetValue(owner, out int count);
            builds[owner] = count + 1;
            return count + 1;
        });

        cache.Refresh(new[] { first, second }, revisions);
        revisions.Invalidate(first);

        await Assert.That(cache.NeedsRefresh(revisions)).IsTrue();
        await Assert.That(cache.Refresh(new[] { first, second }, revisions)).IsTrue();
        await Assert.That(cache.Get(first)).IsEqualTo(2);
        await Assert.That(cache.Get(second)).IsEqualTo(1);
        await Assert.That(builds[first]).IsEqualTo(2);
        await Assert.That(builds[second]).IsEqualTo(1);
        await Assert.That(cache.NeedsRefresh(revisions)).IsFalse();
    }

    [Test]
    public async Task FullInvalidationRebuildsEveryCurrentOwner()
    {
        var first = new Owner("first");
        var second = new Owner("second");
        int builds = 0;
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, int>(_ => ++builds);

        cache.Refresh(new[] { first, second }, revisions);
        revisions.InvalidateAll();
        cache.Refresh(new[] { first, second }, revisions);

        await Assert.That(builds).IsEqualTo(4);
        await Assert.That(cache.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RefreshRetiresDepartedOwnersWithoutRebuildingSurvivors()
    {
        var departed = new Owner("departed");
        var survivor = new Owner("survivor");
        var arrived = new Owner("arrived");
        var builds = new Dictionary<Owner, int>();
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, int>(owner =>
        {
            builds.TryGetValue(owner, out int count);
            builds[owner] = count + 1;
            return count + 1;
        });

        cache.Refresh(new[] { departed, survivor }, revisions);
        revisions.Invalidate(arrived);
        cache.Refresh(new[] { survivor, arrived }, revisions);

        await Assert.That(cache.Contains(departed)).IsFalse();
        await Assert.That(cache.Get(survivor)).IsEqualTo(1);
        await Assert.That(cache.Get(arrived)).IsEqualTo(1);
        await Assert.That(builds[survivor]).IsEqualTo(1);
        await Assert.That(cache.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UnchangedRevisionDoesNotEnumerateOrRefreshTheCohort()
    {
        var owner = new Owner("owner");
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, string>(item => item.Name);
        cache.Refresh(new[] { owner }, revisions);

        await Assert.That(cache.Refresh(ThrowingOwners(), revisions)).IsFalse();
    }

    private static IEnumerable<Owner> ThrowingOwners()
    {
        throw new Exception("unchanged snapshots must not enumerate owners");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class Owner
    {
        internal Owner(string name) => Name = name;
        internal string Name { get; }
    }
}
