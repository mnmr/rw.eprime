using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class GroupNameRulesTests
{
    [Test]
    public async Task DefaultIsReservedWithoutAMaterializedCatalogEntry()
    {
        var empty = Array.Empty<NamedGroup>();

        await Assert.That(GroupNameRules.IsDefault("  dEfAuLt  ")).IsTrue();
        await Assert.That(GroupNameRules.IsAvailable(
            "default", empty, group => group.Name)).IsFalse();
    }

    [Test]
    public async Task ReservedDefaultCannotBeKeptAsASelfRename()
    {
        var group = new NamedGroup("Default");

        await Assert.That(GroupNameRules.IsAvailable(
            "DEFAULT", new[] { group }, item => item.Name, group)).IsFalse();
    }

    [Test]
    public async Task OrdinaryGroupNamesUseCaseInsensitiveCatalogUniqueness()
    {
        var kitchen = new NamedGroup("Kitchen");
        var farm = new NamedGroup("Farm");
        var groups = new[] { kitchen, farm };

        await Assert.That(GroupNameRules.IsAvailable(
            "  KITCHEN  ", groups, group => group.Name)).IsFalse();
        await Assert.That(GroupNameRules.IsAvailable(
            "kitchen", groups, group => group.Name, kitchen)).IsTrue();
        await Assert.That(GroupNameRules.IsAvailable(
            "Workshop", groups, group => group.Name)).IsTrue();
    }

    private sealed class NamedGroup
    {
        internal NamedGroup(string name) => Name = name;
        internal string Name { get; }
    }
}
