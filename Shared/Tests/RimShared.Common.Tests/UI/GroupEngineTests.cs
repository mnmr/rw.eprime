namespace RimShared.Common.Tests;

public class GroupEngineTests
{
    [Test]
    public async Task SectionsAreOrderedByKeyNotTitleAndMembersKeepInputOrder()
    {
        // Life-stage keys carry the stage index before the label, so key
        // order puts the youngest first; title order would read
        // Adult,Baby,Child.
        var sections = GroupEngine.Partition(
            [("anna", 4, "Adult"), ("bob", 0, "Baby"), ("carl", 4, "Adult"), ("dina", 1, "Child")],
            p => (key: "age|" + p.Item2.ToString("D2") + "|" + p.Item3, title: p.Item3));
        await Assert.That(string.Join(",", sections.Select(s => s.Title))).IsEqualTo("Baby,Child,Adult");
        await Assert.That(string.Join(",", sections[2].Members.Select(m => m.Item1))).IsEqualTo("anna,carl");
    }

    [Test]
    public async Task NullKeyCoalescesToEmptyKey()
    {
        var sections = GroupEngine.Partition([1, 2, 3], _ => (key: (string)null!, title: "All"));
        await Assert.That(sections).Count().IsEqualTo(1);
        await Assert.That(sections[0].Key).IsEqualTo("");
        await Assert.That(sections[0].Members).Count().IsEqualTo(3);
    }

    [Test]
    public async Task KeyOrderingIsCaseInsensitive()
    {
        // "apple" before "Banana" holds only case-insensitively; Ordinal
        // would sort "Banana" (66) ahead of "apple" (97). Equal titles keep
        // title order from deciding anything.
        var sections = GroupEngine.Partition(["cherry", "apple", "Banana"], s => (key: s, title: "All"));
        await Assert.That(string.Join(",", sections.Select(s => s.Key))).IsEqualTo("apple,Banana,cherry");
    }

    private static MembershipGroup<string> Members(string key, string title, params string[] members) =>
        new MembershipGroup<string>
        {
            Key = key,
            Title = title,
            Members = [.. members],
        };

    [Test]
    public async Task MembershipKeepsGroupOrderAndItemOrder()
    {
        var sections = GroupEngine.PartitionByMembership(["carl", "anna", "bob"], [Members("g2", "Zulu", "bob", "anna"), Members("g1", "Alpha", "carl")], "Ungrouped");
        await Assert.That(string.Join(",", sections.Select(s => s.Title))).IsEqualTo("Zulu,Alpha");
        await Assert.That(string.Join(",", sections[0].Members)).IsEqualTo("anna,bob");
    }

    [Test]
    public async Task MembershipDuplicatesItemsAcrossGroupsAndTailsUngrouped()
    {
        var sections = GroupEngine.PartitionByMembership(["anna", "bob", "carl"], [Members("g1", "Guards", "anna"), Members("g2", "Cooks", "anna", "bob")], "Ungrouped");
        await Assert.That(string.Join(",", sections.Select(s => s.Title))).IsEqualTo("Guards,Cooks,Ungrouped");
        await Assert.That(string.Join(",", sections[0].Members)).IsEqualTo("anna");
        await Assert.That(string.Join(",", sections[1].Members)).IsEqualTo("anna,bob");
        await Assert.That(string.Join(",", sections[2].Members)).IsEqualTo("carl");
        await Assert.That(sections[2].Key).IsEqualTo("ungrouped");
    }

    [Test]
    public async Task MembershipSkipsGroupsWithoutMatchingItems()
    {
        var sections = GroupEngine.PartitionByMembership(["anna"], [Members("g1", "Empty"), Members("g2", "Elsewhere", "zorro")], "Ungrouped");

        await Assert.That(sections.Select(s => s.Title)).IsEquivalentTo(["Ungrouped"]);
    }

    [Test]
    public async Task MembershipWithNoItemsProducesNoSections()
    {
        var sections = GroupEngine.PartitionByMembership([], [Members("g1", "Guards", "anna")], "Ungrouped");

        await Assert.That(sections).IsEmpty();
    }
}
