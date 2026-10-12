namespace WorkRoles.Core.Tests.UI;

/// Published output cannot prove whether unchanged presentation work ran again.
/// These assertions protect the independent identity/rebuild/lifecycle contract.
public class PawnPresentationCacheTests
{
    private sealed class PawnFacts
    {
        internal string SkillText = "Crafting 8";
        internal object External = new();
    }

    private sealed record Presentation(string Text);

    [Test]
    public async Task RosterReadsRetainEachPawnAndRefreshOnlyConsumedDependencies()
    {
        int builds = 0;
        var cache = new PawnPresentationCache<PawnFacts, Presentation>(
            (pawn, _) => { builds++; return new Presentation(pawn.SkillText); },
            (left, right) => left == right,
            ReferenceEqualityComparer.Instance);
        var store = new object();
        var firstPawn = new PawnFacts();
        var secondPawn = new PawnFacts { SkillText = "Crafting 12" };
        PawnPresentationStamp Stamp(PawnFacts pawn, int tuning = 0,
            int language = 0, int definition = 0, int metrics = 0) =>
            new(store, pawn.External, tuning, language, definition, metrics);

        Presentation first = cache.Get(firstPawn, Stamp(firstPawn));
        Presentation second = cache.Get(secondPawn, Stamp(secondPawn));
        // Drawing another row, selecting a pawn, and editing assignments do not
        // change any skill dependency. Alternating reads must remain true hits.
        await Assert.That(ReferenceEquals(first,
            cache.Get(firstPawn, Stamp(firstPawn)))).IsTrue();
        await Assert.That(ReferenceEquals(second,
            cache.Get(secondPawn, Stamp(secondPawn)))).IsTrue();
        await Assert.That(builds).IsEqualTo(2);

        firstPawn.SkillText = "Crafting 9";
        firstPawn.External = new object();
        Presentation changed = cache.Get(firstPawn, Stamp(firstPawn));
        await Assert.That(changed.Text).IsEqualTo("Crafting 9");
        await Assert.That(ReferenceEquals(first, changed)).IsFalse();
        await Assert.That(ReferenceEquals(second,
            cache.Get(secondPawn, Stamp(secondPawn)))).IsTrue();
        await Assert.That(builds).IsEqualTo(3);

        // Each consumed global dependency is observed immediately, even when
        // there is no game tick. Equal content retains published identity.
        await Assert.That(ReferenceEquals(changed,
            cache.Get(firstPawn, Stamp(firstPawn, tuning: 1)))).IsTrue();
        await Assert.That(ReferenceEquals(changed,
            cache.Get(firstPawn, Stamp(firstPawn, tuning: 1, language: 1)))).IsTrue();
        await Assert.That(ReferenceEquals(changed,
            cache.Get(firstPawn, Stamp(firstPawn, tuning: 1, language: 1, definition: 1)))).IsTrue();
        var finalStamp = Stamp(firstPawn, tuning: 1, language: 1, definition: 1, metrics: 1);
        await Assert.That(ReferenceEquals(changed,
            cache.Get(firstPawn, finalStamp))).IsTrue();
        await Assert.That(builds).IsEqualTo(7);
        cache.Get(firstPawn, finalStamp);
        await Assert.That(builds).IsEqualTo(7);
    }

    [Test]
    public async Task StoreReplacementEvictionAndResetReleasePriorPresentations()
    {
        var cache = new PawnPresentationCache<PawnFacts, Presentation>(
            (pawn, _) => new Presentation(pawn.SkillText), (left, right) => left == right,
            ReferenceEqualityComparer.Instance);
        var pawn = new PawnFacts();
        var store = new object();
        var stamp = new PawnPresentationStamp(store, pawn.External, 0, 0, 0, 0);
        Presentation first = cache.Get(pawn, stamp);
        var replacementStoreStamp = new PawnPresentationStamp(
            new object(), pawn.External, 0, 0, 0, 0);
        Presentation replacement = cache.Get(pawn, replacementStoreStamp);
        await Assert.That(ReferenceEquals(first, replacement)).IsFalse();
        cache.Remove(pawn);
        Presentation readmitted = cache.Get(pawn, replacementStoreStamp);
        await Assert.That(ReferenceEquals(replacement, readmitted)).IsFalse();
        cache.Clear();
        cache.Clear();
        await Assert.That(ReferenceEquals(readmitted,
            cache.Get(pawn, replacementStoreStamp))).IsFalse();
    }
}
