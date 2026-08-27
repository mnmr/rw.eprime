namespace RimShared.Common.Tests;

public class CatalogNameRulesTests
{
    [Test]
    public async Task CaseInsensitiveCollisionIsRejected()
    {
        var first = new NamedItem("Kitchen");
        var second = new NamedItem("Farm");
        NamedItem[] items = [first, second];

        await Assert.That(CatalogNameRules.IsAvailable("  KITCHEN  ", items, item => item.Name)).IsFalse();
    }

    [Test]
    public async Task CaseInsensitiveSelfRenameIsAllowed()
    {
        var first = new NamedItem("Kitchen");
        var second = new NamedItem("Farm");
        NamedItem[] items = [first, second];

        await Assert.That(CatalogNameRules.IsAvailable("kitchen", items, item => item.Name, first)).IsTrue();
    }

    [Test]
    public async Task RenameCollisionWithAnotherItemIsRejected()
    {
        var first = new NamedItem("Kitchen");
        var second = new NamedItem("Farm");
        NamedItem[] items = [first, second];

        await Assert.That(CatalogNameRules.IsAvailable("FARM", items, item => item.Name, first)).IsFalse();
    }

    [Test]
    public async Task WhitespaceIsRejected()
    {
        var first = new NamedItem("Kitchen");
        var second = new NamedItem("Farm");
        NamedItem[] items = [first, second];

        await Assert.That(CatalogNameRules.IsAvailable("   ", items, item => item.Name, first)).IsFalse();
    }

    [Test]
    public async Task EngineOwnedNameGetsDeterministicCaseInsensitiveSuffix()
    {
        NamedItem[] items = [new NamedItem("Worker"), new NamedItem("worker (2)"), new NamedItem("WORKER (3)")];

        await Assert.That(CatalogNameRules.Unique("  Worker  ", items, item => item.Name)).IsEqualTo("Worker (4)");
    }

    [Test]
    public async Task EngineOwnedUniqueNameRemainsUnchanged()
    {
        NamedItem[] items = [new NamedItem("Worker"), new NamedItem("worker (2)"), new NamedItem("WORKER (3)")];

        await Assert.That(CatalogNameRules.Unique("New Role", items, item => item.Name)).IsEqualTo("New Role");
    }

    private sealed class NamedItem
    {
        internal NamedItem(string name) => Name = name;

        internal string Name { get; }
    }
}
