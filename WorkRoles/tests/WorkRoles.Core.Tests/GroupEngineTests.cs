using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class GroupEngineTests
{
    private static List<GroupSection<string>> Partition(params string[] items) =>
        GroupEngine.Partition(items, s => (key: "k:" + s[0], title: s[0].ToString().ToUpperInvariant()));

    [Test]
    public async Task SectionsOrderedByTitle_MembersKeepInputOrder()
    {
        var sections = Partition("banana", "apple", "blueberry", "avocado");
        await Assert.That(sections.Select(s => s.Title)).IsEquivalentTo(new[] { "A", "B" });
        await Assert.That(sections[0].Members).IsEquivalentTo(new[] { "apple", "avocado" });
        await Assert.That(sections[1].Members).IsEquivalentTo(new[] { "banana", "blueberry" });
    }

    [Test]
    public async Task KeysAreStable_AndNullKeyIsTolerated()
    {
        var sections = GroupEngine.Partition(new[] { 1, 2, 3 }, _ => (key: (string)null!, title: "All"));
        await Assert.That(sections).Count().IsEqualTo(1);
        await Assert.That(sections[0].Key).IsEqualTo("");
        await Assert.That(sections[0].Members).Count().IsEqualTo(3);
    }

    [Test]
    public async Task TitleOrderingIsCaseInsensitive()
    {
        var sections = GroupEngine.Partition(new[] { "zeta", "Alpha", "beta" },
            s => (key: s, title: s));
        await Assert.That(sections.Select(s => s.Title))
            .IsEquivalentTo(new[] { "Alpha", "beta", "zeta" });
    }

    private static MembershipGroup<string> Members(string key, string title, params string[] members) =>
        new MembershipGroup<string> { Key = key, Title = title, Members = new HashSet<string>(members) };

    [Test]
    public async Task MembershipKeepsGroupOrderAndItemOrder()
    {
        var sections = GroupEngine.PartitionByMembership(
            new[] { "carl", "anna", "bob" },
            new[] { Members("g2", "Zulu", "bob", "anna"), Members("g1", "Alpha", "carl") },
            "Ungrouped");
        await Assert.That(sections.Select(s => s.Title)).IsEquivalentTo(new[] { "Zulu", "Alpha" });
        await Assert.That(sections[0].Members).IsEquivalentTo(new[] { "anna", "bob" });
    }

    [Test]
    public async Task MembershipDuplicatesItemsAcrossGroups_AndTailsUngrouped()
    {
        var sections = GroupEngine.PartitionByMembership(
            new[] { "anna", "bob", "carl" },
            new[] { Members("g1", "Guards", "anna"), Members("g2", "Cooks", "anna", "bob") },
            "Ungrouped");
        await Assert.That(sections.Select(s => s.Title))
            .IsEquivalentTo(new[] { "Guards", "Cooks", "Ungrouped" });
        await Assert.That(sections[0].Members).IsEquivalentTo(new[] { "anna" });
        await Assert.That(sections[1].Members).IsEquivalentTo(new[] { "anna", "bob" });
        await Assert.That(sections[2].Members).IsEquivalentTo(new[] { "carl" });
        await Assert.That(sections[2].Key).IsEqualTo("ungrouped");
    }

    [Test]
    public async Task MembershipSkipsEmptyGroups_AndHandlesNoGroups()
    {
        var sections = GroupEngine.PartitionByMembership(
            new[] { "anna" },
            new[] { Members("g1", "Empty"), Members("g2", "Elsewhere", "zorro") },
            "Ungrouped");
        await Assert.That(sections.Select(s => s.Title)).IsEquivalentTo(new[] { "Ungrouped" });

        var none = GroupEngine.PartitionByMembership(
            new string[0], new[] { Members("g1", "Guards", "anna") }, "Ungrouped");
        await Assert.That(none).Count().IsEqualTo(0);
    }
}
