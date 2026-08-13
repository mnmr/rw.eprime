namespace WorkRoles.Core.Tests.Caching;

public class SelectiveSnapshotCacheTests
{
    [Test]
    public async Task TargetedInvalidationRebuildsOnlyTheSpecifiedOwner()
    {
        var first = new Owner("first");
        var second = new Owner("second");
        Dictionary<Owner, int> builds = [];
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, int>(owner =>
        {
            builds.TryGetValue(owner, out int count);
            builds[owner] = count + 1;
            return count + 1;
        });
        Owner[] owners = [first, second];

        cache.Refresh(owners, revisions);
        revisions.Invalidate(first);

        await Assert.That(cache.NeedsRefresh(revisions)).IsTrue();
        await Assert.That(cache.Refresh(owners, revisions)).IsTrue();
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
        Owner[] owners = [first, second];

        cache.Refresh(owners, revisions);
        revisions.InvalidateAll();
        cache.Refresh(owners, revisions);

        await Assert.That(builds).IsEqualTo(4);
        await Assert.That(cache.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RefreshRetiresDepartedOwnersWithoutRebuildingSurvivors()
    {
        var departed = new Owner("departed");
        var survivor = new Owner("survivor");
        var arrived = new Owner("arrived");
        Dictionary<Owner, int> builds = [];
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, int>(owner =>
        {
            builds.TryGetValue(owner, out int count);
            builds[owner] = count + 1;
            return count + 1;
        });
        Owner[] initialOwners = [departed, survivor];
        Owner[] refreshedOwners = [survivor, arrived];

        cache.Refresh(initialOwners, revisions);
        revisions.Invalidate(arrived);
        cache.Refresh(refreshedOwners, revisions);

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
        Owner[] owners = [owner];
        cache.Refresh(owners, revisions);

        await Assert.That(cache.Refresh(ThrowingOwners(), revisions)).IsFalse();
    }

    [Test]
    public async Task EqualTargetedRefreshPreservesPublishedSnapshotIdentity()
    {
        var owner = new Owner("owner");
        var revisions = new OwnerInvalidationRevisions<Owner>();
        var cache = new SelectiveSnapshotCache<Owner, Snapshot>(item => new Snapshot(item.Name));
        Owner[] owners = [owner];
        cache.Refresh(owners, revisions);
        Snapshot published = cache.Get(owner);

        revisions.Invalidate(owner);
        cache.Refresh(owners, revisions);

        await Assert.That(ReferenceEquals(cache.Get(owner), published)).IsTrue();
    }

    [Test]
    public async Task EqualFullRefreshRebuildsButPreservesPublishedSnapshotIdentity()
    {
        var owner = new Owner("owner");
        var revisions = new OwnerInvalidationRevisions<Owner>();
        int builds = 0;
        var cache = new SelectiveSnapshotCache<Owner, Snapshot>(item =>
        {
            builds++;
            return new Snapshot(item.Name);
        });
        Owner[] owners = [owner];
        cache.Refresh(owners, revisions);
        Snapshot published = cache.Get(owner);

        revisions.InvalidateAll();
        cache.Refresh(owners, revisions);

        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(ReferenceEquals(cache.Get(owner), published)).IsTrue();
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

    private sealed class Snapshot : IEquatable<Snapshot>
    {
        internal Snapshot(string name) => Name = name;

        private string Name { get; }

        public bool Equals(Snapshot other) => other != null && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as Snapshot);

        public override int GetHashCode() => Name.GetHashCode();
    }
}
