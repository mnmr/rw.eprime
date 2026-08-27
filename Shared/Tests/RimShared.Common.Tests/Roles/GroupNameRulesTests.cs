namespace RimShared.Common.Tests;

public class GroupNameRulesTests
{
    [Test]
    public async Task DefaultIsReservedWithoutAMaterializedCatalogEntry()
    {
        NamedGroup[] empty = [];

        await Assert.That(GroupNameRules.IsDefault("  dEfAuLt  ")).IsTrue();
        await Assert.That(GroupNameRules.IsAvailable("default", empty, group => group.Name)).IsFalse();
    }

    [Test]
    public async Task ReservedDefaultCannotBeKeptAsASelfRename()
    {
        var group = new NamedGroup("Default");

        await Assert.That(GroupNameRules.IsAvailable("DEFAULT", [group], item => item.Name, group)).IsFalse();
    }

    [Test]
    public async Task OrdinaryGroupNameRejectsCaseInsensitiveCollision()
    {
        var kitchen = new NamedGroup("Kitchen");
        var farm = new NamedGroup("Farm");
        NamedGroup[] groups = [kitchen, farm];

        await Assert.That(GroupNameRules.IsAvailable("  KITCHEN  ", groups, group => group.Name)).IsFalse();
    }

    [Test]
    public async Task OrdinaryGroupNameAllowsCaseInsensitiveSelfRename()
    {
        var kitchen = new NamedGroup("Kitchen");
        var farm = new NamedGroup("Farm");
        NamedGroup[] groups = [kitchen, farm];

        await Assert.That(GroupNameRules.IsAvailable("kitchen", groups, group => group.Name, kitchen)).IsTrue();
    }

    [Test]
    public async Task OrdinaryUniqueGroupNameIsAvailable()
    {
        var kitchen = new NamedGroup("Kitchen");
        var farm = new NamedGroup("Farm");
        NamedGroup[] groups = [kitchen, farm];

        await Assert.That(GroupNameRules.IsAvailable("Workshop", groups, group => group.Name)).IsTrue();
    }

    private sealed class NamedGroup
    {
        internal NamedGroup(string name) => Name = name;

        internal string Name { get; }
    }
}
