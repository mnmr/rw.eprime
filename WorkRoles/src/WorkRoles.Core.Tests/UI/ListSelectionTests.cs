namespace WorkRoles.Core.Tests.UI;

/// Explorer-style list selection: click, Shift range, Ctrl toggle, additive
/// range, select all, and pruning when items leave the list.
public class ListSelectionTests
{
    private static readonly string[] Order = ["a", "b", "c", "d", "e", "f"];

    private static string Joined(ListSelection<string> selection, IReadOnlyList<string> order)
    {
        var picked = new List<string>();
        selection.CopyOrdered(order, picked);
        return string.Join(",", picked);
    }

    [Test]
    public async Task ClickRangeToggleAndAdditiveRangeFollowExplorerSemantics()
    {
        var selection = new ListSelection<string>();

        selection.Click("b");
        await Assert.That(selection.Single).IsEqualTo("b");

        // Shift-click: range from the anchor, replacing the selection.
        selection.Range("d", Order, additive: false);
        await Assert.That(Joined(selection, Order)).IsEqualTo("b,c,d");
        await Assert.That(selection.Single).IsNull();
        await Assert.That(selection.Focus).IsEqualTo("d");

        // Ctrl-click: toggles one item and moves the anchor there.
        selection.Toggle("f");
        await Assert.That(Joined(selection, Order)).IsEqualTo("b,c,d,f");
        await Assert.That(selection.Anchor).IsEqualTo("f");

        // Ctrl+Shift-click: adds a range from the new anchor.
        selection.Range("e", Order, additive: true);
        await Assert.That(Joined(selection, Order)).IsEqualTo("b,c,d,e,f");

        // Shift-click without Ctrl: the range replaces everything else.
        selection.Range("e", Order, additive: false);
        await Assert.That(Joined(selection, Order)).IsEqualTo("e,f");

        // Toggling the last selected item empties the selection.
        selection.Click("c");
        selection.Toggle("c");
        await Assert.That(selection.Count).IsEqualTo(0);
        await Assert.That(selection.Single).IsNull();
    }

    [Test]
    public async Task SelectAllKeepsAnchorAndRetainDropsOnlyDepartedItems()
    {
        var selection = new ListSelection<string>();
        selection.Click("b");
        selection.SelectAll(Order);
        await Assert.That(selection.Count).IsEqualTo(6);
        await Assert.That(selection.Anchor).IsEqualTo("b");

        // "b" and "f" leave the list: the rest of the selection survives.
        string[] shrunk = ["a", "c", "d", "e"];
        selection.Retain(shrunk);
        await Assert.That(Joined(selection, shrunk)).IsEqualTo("a,c,d,e");
        await Assert.That(selection.Anchor).IsNull();

        // Down to one item: it becomes the single selection again.
        string[] one = ["d"];
        selection.Retain(one);
        await Assert.That(selection.Single).IsEqualTo("d");
    }

    [Test]
    public async Task RangeWithAnchorOutsideTheOrderSelectsTheClickedItemOnly()
    {
        var selection = new ListSelection<string>();
        selection.Click("zz"); // e.g. a pawn inside a collapsed group
        selection.Range("c", Order, additive: false);
        await Assert.That(Joined(selection, Order)).IsEqualTo("c");
        await Assert.That(selection.Single).IsEqualTo("c");
    }
}
