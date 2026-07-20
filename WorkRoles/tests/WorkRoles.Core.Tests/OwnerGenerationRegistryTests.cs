using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class OwnerGenerationRegistryTests
{
    [Test]
    public async Task SameOwnerAndLocalKeyReplacementUsesNewestLookupAndValue()
    {
        var registry = Registry();

        registry.Begin("window");
        registry.Touch("tip", "old text", 1);
        registry.Touch("tip", "new text", 2);
        registry.End("window");

        await Assert.That(registry.TryGet("old text", out int oldValue)).IsTrue();
        await Assert.That(oldValue).IsEqualTo(1);
        await Assert.That(registry.TryGet("new text", out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
        await Assert.That(registry.Count).IsEqualTo(2);
        await Assert.That(registry.RetiredCount).IsEqualTo(1);

        await Assert.That(registry.FlushRetired()).IsEqualTo(1);
        await Assert.That(registry.TryGet("old text", out _)).IsFalse();
        await Assert.That(registry.TryGet("new text", out value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
        await Assert.That(registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OwnersCanUseTheSameLocalKeyWithoutEvictingEachOther()
    {
        var registry = Registry();

        registry.Begin("main");
        registry.Touch("shared", "main text", 11);
        registry.End("main");
        registry.Begin("preview");
        registry.Touch("shared", "preview text", 22);
        registry.End("preview");

        await Assert.That(registry.TryGet("main text", out int main)).IsTrue();
        await Assert.That(main).IsEqualTo(11);
        await Assert.That(registry.TryGet("preview text", out int preview)).IsTrue();
        await Assert.That(preview).IsEqualTo(22);
        await Assert.That(registry.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CurrentEntriesSurviveEndAndFlush()
    {
        var registry = Registry();

        registry.Begin("window");
        registry.Touch("tip", "text", 7);
        registry.End("window");

        await Assert.That(registry.RetiredCount).IsEqualTo(0);
        await Assert.That(registry.FlushRetired()).IsEqualTo(0);
        await Assert.That(registry.TryGet("text", out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(7);
    }

    [Test]
    public async Task UnseenPriorEntriesRemainResolvableUntilFlush()
    {
        var registry = Registry();
        registry.Begin("window");
        registry.Touch("gone", "gone text", 1);
        registry.Touch("kept", "kept text", 2);
        registry.End("window");

        registry.Begin("window");
        registry.Touch("kept", "kept text", 3);
        registry.End("window");

        await Assert.That(registry.RetiredCount).IsEqualTo(1);
        await Assert.That(registry.TryGet("gone text", out int beforeFlush)).IsTrue();
        await Assert.That(beforeFlush).IsEqualTo(1);
        await Assert.That(registry.FlushRetired()).IsEqualTo(1);
        await Assert.That(registry.TryGet("gone text", out _)).IsFalse();
        await Assert.That(registry.TryGet("kept text", out int kept)).IsTrue();
        await Assert.That(kept).IsEqualTo(3);
    }

    [Test]
    public async Task RetouchBeforeFlushCancelsRetirement()
    {
        var registry = Registry();
        registry.Begin("window");
        registry.Touch("tip", "text", 1);
        registry.End("window");
        registry.Begin("window");
        registry.End("window");

        await Assert.That(registry.RetiredCount).IsEqualTo(1);

        registry.Begin("window");
        registry.Touch("tip", "text", 2);
        registry.End("window");

        await Assert.That(registry.RetiredCount).IsEqualTo(0);
        await Assert.That(registry.FlushRetired()).IsEqualTo(0);
        await Assert.That(registry.TryGet("text", out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task SameLookupUsesLastWriterAndRestoresPreviousWriterOnRelease()
    {
        var registry = Registry();
        registry.Begin("main");
        registry.Touch("main-tip", "same text", 1);
        registry.End("main");
        registry.Begin("preview");
        registry.Touch("preview-tip", "same text", 2);
        registry.End("preview");

        await Assert.That(registry.TryGet("same text", out int latest)).IsTrue();
        await Assert.That(latest).IsEqualTo(2);

        await Assert.That(registry.Release("preview")).IsEqualTo(1);
        await Assert.That(registry.TryGet("same text", out int restored)).IsTrue();
        await Assert.That(restored).IsEqualTo(1);
    }

    [Test]
    public async Task RetouchingAnExistingCollisionEntryMovesItBackToTheHead()
    {
        var registry = Registry();
        registry.Begin("main");
        registry.Touch("main-tip", "same text", 1);
        registry.End("main");
        registry.Begin("preview");
        registry.Touch("preview-tip", "same text", 2);
        registry.End("preview");

        registry.Begin("main");
        registry.Touch("main-tip", "same text", 3);
        registry.End("main");

        await Assert.That(registry.TryGet("same text", out int retouched)).IsTrue();
        await Assert.That(retouched).IsEqualTo(3);
        await Assert.That(registry.Release("main")).IsEqualTo(1);
        await Assert.That(registry.TryGet("same text", out int restored)).IsTrue();
        await Assert.That(restored).IsEqualTo(2);
    }

    [Test]
    public async Task RemovingMiddleAndHeadCollisionsRestoresChainOrder()
    {
        var registry = Registry();
        foreach (var (owner, value) in new[] { ("oldest", 1), ("middle", 2), ("head", 3) })
        {
            registry.Begin(owner);
            registry.Touch("tip", "same text", value);
            registry.End(owner);
        }

        await Assert.That(registry.Release("middle")).IsEqualTo(1);
        await Assert.That(registry.TryGet("same text", out int head)).IsTrue();
        await Assert.That(head).IsEqualTo(3);
        await Assert.That(registry.Release("head")).IsEqualTo(1);
        await Assert.That(registry.TryGet("same text", out int restored)).IsTrue();
        await Assert.That(restored).IsEqualTo(1);
    }

    [Test]
    public async Task FlushingRetiredCollisionHeadRestoresOlderWriter()
    {
        var registry = Registry();
        registry.Begin("main");
        registry.Touch("main-tip", "same text", 1);
        registry.End("main");
        registry.Begin("preview");
        registry.Touch("preview-tip", "same text", 2);
        registry.End("preview");
        registry.Begin("preview");
        registry.End("preview");

        await Assert.That(registry.TryGet("same text", out int beforeFlush)).IsTrue();
        await Assert.That(beforeFlush).IsEqualTo(2);
        await Assert.That(registry.FlushRetired()).IsEqualTo(1);
        await Assert.That(registry.TryGet("same text", out int restored)).IsTrue();
        await Assert.That(restored).IsEqualTo(1);
    }

    [Test]
    public async Task WarmedRetirementFlushAllocatesNoPerGenerationSnapshot()
    {
        const int keyCount = 64;
        var registry = Registry();

        RetireGeneration();
        await Assert.That(registry.FlushRetired()).IsEqualTo(keyCount);

        RetireGeneration();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int removed = registry.FlushRetired();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(removed).IsEqualTo(keyCount);
        await Assert.That(allocated).IsEqualTo(0);

        void RetireGeneration()
        {
            registry.Begin("window");
            for (int i = 0; i < keyCount; i++)
                registry.Touch($"tip-{i}", $"text-{i}", i);
            registry.End("window");
            registry.Begin("window");
            registry.End("window");
        }
    }

    [Test]
    public async Task ReleaseRemovesOnlyTheRequestedOwner()
    {
        var registry = Registry();
        registry.Begin("main");
        registry.Touch("tip", "main text", 1);
        registry.End("main");
        registry.Begin("preview");
        registry.Touch("tip", "preview text", 2);
        registry.End("preview");

        await Assert.That(registry.Release("main")).IsEqualTo(1);
        await Assert.That(registry.TryGet("main text", out _)).IsFalse();
        await Assert.That(registry.TryGet("preview text", out int preview)).IsTrue();
        await Assert.That(preview).IsEqualTo(2);
    }

    [Test]
    public async Task ManyAffectedLookupKeysRebuildOnlyTheirCollisionBuckets()
    {
        const int keyCount = 64;
        var registry = new OwnerGenerationRegistry<string, string, CountingLookupKey, int>();
        registry.Begin("survivor");
        for (int i = 0; i < keyCount; i++)
            registry.Touch($"survivor-{i}", new CountingLookupKey(i), i);
        registry.End("survivor");
        registry.Begin("newer");
        for (int i = 0; i < keyCount; i++)
            registry.Touch($"newer-{i}", new CountingLookupKey(i), 1000 + i);
        registry.End("newer");

        CountingLookupKey.ResetComparisons();
        await Assert.That(registry.Release("newer")).IsEqualTo(keyCount);
        await Assert.That(CountingLookupKey.Comparisons).IsLessThan(keyCount * 8)
            .Because("collision restoration must not compare every remaining entry for every lookup key");

        for (int i = 0; i < keyCount; i++)
        {
            await Assert.That(registry.TryGet(new CountingLookupKey(i), out int restored)).IsTrue();
            await Assert.That(restored).IsEqualTo(i);
        }
    }

    [Test]
    public async Task ClearDropsActiveRetiredAndOwnerState()
    {
        var registry = Registry();
        registry.Begin("window");
        registry.Touch("tip", "text", 1);
        registry.End("window");
        registry.Begin("window");
        registry.End("window");
        registry.Begin("other");
        registry.Touch("other", "other text", 2);

        registry.Clear();

        await Assert.That(registry.Count).IsEqualTo(0);
        await Assert.That(registry.RetiredCount).IsEqualTo(0);
        await Assert.That(registry.TryGet("text", out _)).IsFalse();
        await Assert.That(registry.TryGet("other text", out _)).IsFalse();
        registry.Begin("fresh");
        registry.Touch("fresh tip", "text", 3);
        registry.End("fresh");
        await Assert.That(registry.Release("fresh")).IsEqualTo(1);
        await Assert.That(registry.TryGet("text", out _)).IsFalse();
    }

    [Test]
    public async Task InvalidGenerationNestingFailsDeterministically()
    {
        var registry = Registry();
        registry.Begin("main");

        await Assert.That(() => registry.Begin("preview"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => registry.End("preview"))
            .Throws<InvalidOperationException>();

        registry.Touch("tip", "text", 1);
        registry.End("main");

        await Assert.That(() => registry.Touch("tip", "text", 2))
            .Throws<InvalidOperationException>();
        await Assert.That(() => registry.End("main"))
            .Throws<InvalidOperationException>();
    }

    private static OwnerGenerationRegistry<string, string, string, int> Registry()
        => new OwnerGenerationRegistry<string, string, string, int>();

    private sealed class CountingLookupKey
    {
        internal CountingLookupKey(int id) => Id = id;

        private int Id { get; }
        internal static int Comparisons { get; private set; }

        internal static void ResetComparisons() => Comparisons = 0;

        public override bool Equals(object obj)
        {
            Comparisons++;
            return obj is CountingLookupKey other && Id == other.Id;
        }

        public override int GetHashCode() => Id;
    }
}
