using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Cache contract for the shared work-spec catalog: reuse, dependency
/// invalidation, equal-rebuild identity, owner partition, and teardown.
/// The contract has no final recommendation output of its own, so the cache
/// mechanics are proven at this boundary.
public class RoleWorkSpecCacheTests
{
    private static RoleWorkSpec Spec(int roleId, int gateMin = 4) =>
        RoleWorkSpecBuilder.Build(roleId, [
            RecsTestBed.Capability("Crafting", 0,
                RecsTestBed.Giver("MakeDrugs", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Crafting", gateMin))),
        ], null);

    [Test]
    public async Task RepeatedReadsReuseTheCachedInstanceWithoutRebuilding()
    {
        var cache = new RoleWorkSpecCache();
        var owner = new object();
        var index = new object();
        int builds = 0;

        RoleWorkSpec first = cache.For(1, owner, 0, index, () => { builds++; return Spec(1); });
        RoleWorkSpec second = cache.For(1, owner, 0, index, () => { builds++; return Spec(1); });

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task RevisionBumpRebuildsAndAnEqualRebuildPreservesIdentity()
    {
        var cache = new RoleWorkSpecCache();
        var owner = new object();
        var index = new object();
        int builds = 0;

        RoleWorkSpec first = cache.For(1, owner, 0, index, () => { builds++; return Spec(1); });
        RoleWorkSpec second = cache.For(1, owner, 1, index, () => { builds++; return Spec(1); });

        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task ChangedContentPublishesANewInstance()
    {
        var cache = new RoleWorkSpecCache();
        var owner = new object();
        var index = new object();

        RoleWorkSpec first = cache.For(1, owner, 0, index, () => Spec(1, gateMin: 4));
        RoleWorkSpec second = cache.For(1, owner, 1, index, () => Spec(1, gateMin: 6));

        await Assert.That(ReferenceEquals(first, second)).IsFalse();
        await Assert.That(RoleWorkSpec.StructurallyEqual(first, second)).IsFalse();
    }

    [Test]
    public async Task IndexIdentityChangeRebuilds()
    {
        var cache = new RoleWorkSpecCache();
        var owner = new object();
        int builds = 0;

        cache.For(1, owner, 0, new object(), () => { builds++; return Spec(1); });
        cache.For(1, owner, 0, new object(), () => { builds++; return Spec(1); });

        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task OwnerChangeDropsThePreviousStoreEntirely()
    {
        var cache = new RoleWorkSpecCache();
        var index = new object();

        RoleWorkSpec first = cache.For(1, new object(), 0, index, () => Spec(1));
        // Same content under a new owner must not reuse the old instance:
        // identity never crosses stores.
        RoleWorkSpec second = cache.For(1, new object(), 0, index, () => Spec(1));

        await Assert.That(RoleWorkSpec.StructurallyEqual(first, second)).IsTrue();
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ResetTearsDownAndIsIdempotent()
    {
        var cache = new RoleWorkSpecCache();
        var owner = new object();
        var index = new object();
        RoleWorkSpec first = cache.For(1, owner, 0, index, () => Spec(1));

        cache.Reset();
        cache.Reset();
        RoleWorkSpec second = cache.For(1, owner, 0, index, () => Spec(1));

        await Assert.That(ReferenceEquals(first, second)).IsFalse();
        await Assert.That(RoleWorkSpec.StructurallyEqual(first, second)).IsTrue();
    }

    [Test]
    public async Task StructuralEqualityDetectsEachFactKindChange()
    {
        RoleWorkSpec baseline = Spec(1);

        await Assert.That(RoleWorkSpec.StructurallyEqual(baseline, Spec(1))).IsTrue();
        await Assert.That(RoleWorkSpec.StructurallyEqual(baseline, Spec(2))).IsFalse();
        await Assert.That(RoleWorkSpec.StructurallyEqual(baseline, Spec(1, gateMin: 5))).IsFalse();
        RoleWorkSpec otherGiver = RoleWorkSpecBuilder.Build(1, [
            RecsTestBed.Capability("Crafting", 0,
                RecsTestBed.Giver("MakeOther", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Crafting", 4))),
        ], null);
        await Assert.That(RoleWorkSpec.StructurallyEqual(baseline, otherGiver)).IsFalse();
        RoleWorkSpec gated = RoleWorkSpecBuilder.Build(1, [
            RecsTestBed.Capability("Crafting", 0,
                RecsTestBed.Giver("MakeDrugs", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Crafting", 4))),
        ], ["Medicine"]);
        await Assert.That(RoleWorkSpec.StructurallyEqual(baseline, gated)).IsFalse();
    }
}
