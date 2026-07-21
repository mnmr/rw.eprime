using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class MutableSignalSnapshotCacheTests
{
    [Test]
    public async Task EveryMutableSignalCategoryChangesTheSnapshotSignature()
    {
        MutableSignalSignature baseline = Signature();

        await Assert.That(Signature(skillEnabled: false)).IsNotEqualTo(baseline);
        await Assert.That(Signature(passion: 4)).IsNotEqualTo(baseline);
        await Assert.That(Signature(passionLearnRate: 1.25f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(passionAuthorTier: "Critical")).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseLevel: 3)).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseXpSinceLastLevel: 25f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseXpSinceLastLevel: 20.1f)).IsEqualTo(baseline);
        await Assert.That(Signature(expertiseXpSinceLastLevel: 20.6f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseXpSinceLastLevel: 20.5f))
            .IsEqualTo(Signature(expertiseXpSinceLastLevel: 21f));
        await Assert.That(Signature(expertiseXpRequiredForLevelUp: 250f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseXpRequiredForLevelUp: 200.1f)).IsEqualTo(baseline);
        await Assert.That(Signature(expertiseXpRequiredForLevelUp: 200.6f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(expertiseXpRequiredForLevelUp: 200.5f))
            .IsEqualTo(Signature(expertiseXpRequiredForLevelUp: 201f));
        await Assert.That(Signature(expertiseMultiplier: 1.5f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(traitSuppressed: true)).IsNotEqualTo(baseline);
        await Assert.That(Signature(traitDegree: 2)).IsNotEqualTo(baseline);
        await Assert.That(Signature(geneActive: false)).IsNotEqualTo(baseline);
        await Assert.That(Signature(hediffSeverity: 0.75f)).IsNotEqualTo(baseline);
        await Assert.That(Signature(hediffStage: 2)).IsNotEqualTo(baseline);
        await Assert.That(Signature(providerCondition: false)).IsNotEqualTo(baseline);
        await Assert.That(Signature(disabledWorkTags: 8)).IsNotEqualTo(baseline);
    }

    [Test]
    public async Task BoundedGameTickEpochAvoidsPerFrameTraversalAndReusesSnapshot()
    {
        int traversals = 0, builds = 0;
        MutableSignalSignature signature = Signature();
        var cache = new MutableSignalSnapshotCache<PawnKey, Snapshot>(
            _ => { traversals++; return signature; },
            _ => new Snapshot(++builds));
        var pawn = new PawnKey();

        Snapshot first = null;
        for (int tick = 120; tick < 180; tick++)
        {
            Snapshot current = cache.Get(pawn,
                MutableSignalObservationEpoch.FromGameTick(tick));
            first ??= current;
            await Assert.That(ReferenceEquals(first, current)).IsTrue();
        }
        Snapshot nextEpoch = cache.Get(pawn,
            MutableSignalObservationEpoch.FromGameTick(180));

        await Assert.That(ReferenceEquals(first, nextEpoch)).IsTrue();
        await Assert.That(traversals).IsEqualTo(2);
        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task PausedEpochUsesWallClockAndPauseTransitionsAreBoundaries()
    {
        long running = MutableSignalObservationEpoch.FromClocks(
            gameTick: 120, realTimeSecond: 20, paused: false);
        long runningLaterWallClock = MutableSignalObservationEpoch.FromClocks(
            gameTick: 120, realTimeSecond: 21, paused: false);
        long paused = MutableSignalObservationEpoch.FromClocks(
            gameTick: 120, realTimeSecond: 20, paused: true);
        long pausedLater = MutableSignalObservationEpoch.FromClocks(
            gameTick: 120, realTimeSecond: 21, paused: true);

        await Assert.That(runningLaterWallClock).IsEqualTo(running);
        await Assert.That(paused).IsNotEqualTo(running);
        await Assert.That(pausedLater).IsNotEqualTo(paused);
    }

    [Test]
    public async Task ChangedSignatureRebuildsAndAdvancesRevision()
    {
        int builds = 0;
        MutableSignalSignature signature = Signature();
        var cache = new MutableSignalSnapshotCache<PawnKey, Snapshot>(
            _ => signature,
            _ => new Snapshot(++builds));
        var pawn = new PawnKey();

        Snapshot first = cache.Get(pawn, observationEpoch: 10);
        long firstRevision = cache.Revision;
        signature = Signature(traitDegree: 2);
        Snapshot changed = cache.Get(pawn, observationEpoch: 11);

        await Assert.That(ReferenceEquals(first, changed)).IsFalse();
        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(cache.Revision).IsGreaterThan(firstRevision);
    }

    [Test]
    public async Task ExplicitInvalidationRebuildsExactlyOnce()
    {
        int builds = 0;
        var cache = new MutableSignalSnapshotCache<PawnKey, Snapshot>(
            _ => Signature(),
            _ => new Snapshot(++builds));
        var pawn = new PawnKey();

        cache.Get(pawn, observationEpoch: 10);
        long beforeInvalidation = cache.Revision;
        cache.Invalidate(pawn);
        Snapshot rebuilt = cache.Get(pawn, observationEpoch: 10);
        Snapshot reused = cache.Get(pawn, observationEpoch: 10);

        await Assert.That(ReferenceEquals(rebuilt, reused)).IsTrue();
        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(cache.Revision).IsGreaterThan(beforeInvalidation);
    }

    [Test]
    public async Task ClearDropsEveryKeyAndKeepsRevisionMonotonic()
    {
        int builds = 0;
        var cache = new MutableSignalSnapshotCache<PawnKey, Snapshot>(
            _ => Signature(),
            _ => new Snapshot(++builds));
        var first = new PawnKey();
        var second = new PawnKey();

        cache.Get(first, 1);
        cache.Get(second, 1);
        long beforeClear = cache.Revision;
        cache.Clear();
        cache.Get(first, 1);
        cache.Get(second, 1);

        await Assert.That(builds).IsEqualTo(4);
        await Assert.That(cache.Revision).IsGreaterThan(beforeClear);
    }

    [Test]
    public async Task CohortGateEnumeratesOncePerEpochAndKey()
    {
        var gate = new ObservationEpochGate<string>();

        await Assert.That(gate.Enter(epoch: 10, key: "listed:4")).IsTrue();
        await Assert.That(gate.Enter(epoch: 10, key: "listed:4")).IsFalse();
        await Assert.That(gate.Enter(epoch: 10, key: "listed:5")).IsTrue();
        await Assert.That(gate.Enter(epoch: 11, key: "listed:5")).IsTrue();
    }

    private static MutableSignalSignature Signature(
        bool skillEnabled = true,
        int passion = 3,
        float passionLearnRate = 1f,
        string passionAuthorTier = "Major",
        int expertiseLevel = 2,
        float expertiseXpSinceLastLevel = 20f,
        float expertiseXpRequiredForLevelUp = 200f,
        float expertiseMultiplier = 1f,
        bool traitSuppressed = false,
        int traitDegree = 1,
        bool geneActive = true,
        float hediffSeverity = 0.5f,
        int hediffStage = 1,
        bool providerCondition = true,
        int disabledWorkTags = 4)
    {
        MutableSignalSignatureBuilder builder = MutableSignalSignatureBuilder.Start();
        builder.AddSkill("Shooting", skillEnabled, passion);
        builder.AddModdedPassion("example.passions", "Focused", false,
            passionLearnRate, 1f, 1f, "UI/Focused", passionAuthorTier);
        builder.AddExpertise("example.skills", "Marksman", "Shooting",
            expertiseLevel, expertiseXpSinceLastLevel,
            expertiseXpRequiredForLevelUp, expertiseMultiplier);
        builder.AddTrait("ludeon.rimworld", "FastLearner", traitDegree, traitSuppressed);
        builder.AddGene("ludeon.rimworld.biotech", "AptitudeStrong_Shooting", geneActive);
        builder.AddHediff("example.health", "NeuralBoost", hediffSeverity,
            hediffStage, "Brain");
        builder.AddProviderCondition("vse:cross-skill", providerCondition);
        builder.AddWorkAversionState(disabledWorkTags);
        return builder.Build();
    }

    private sealed class PawnKey { }
    private sealed record Snapshot(int Build);
}
