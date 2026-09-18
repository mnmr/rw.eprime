namespace WorkRoles.Core.Tests.Roles;

/// Batch moves and removals over a selected set of list indices, as used by
/// the role editor's Selected Jobs and Member Roles panels.
public class ListEditsTests
{
    [Test]
    public async Task MovingUpKeepsRelativeOrderAndStopsAtTheTop()
    {
        List<string> list = ["a", "b", "c", "d", "e"];
        // a and b are already against the top; only d can move.
        bool changed = ListEdits.MoveSelected(list, [3, 1, 0], -1);
        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join(",", list)).IsEqualTo("a,b,d,c,e");
    }

    [Test]
    public async Task MovingDownABlockShiftsItAsOne()
    {
        List<string> list = ["a", "b", "c", "d", "e"];
        ListEdits.MoveSelected(list, [1, 2], +1);
        await Assert.That(string.Join(",", list)).IsEqualTo("a,d,b,c,e");
    }

    [Test]
    public async Task BlockedMoveIsANoOp()
    {
        List<string> list = ["a", "b", "c"];
        bool changed = ListEdits.MoveSelected(list, [1, 2], +1);
        await Assert.That(changed).IsFalse();
        await Assert.That(string.Join(",", list)).IsEqualTo("a,b,c");
    }

    [Test]
    public async Task RemoveIgnoresDuplicatesAndOutOfRangeIndices()
    {
        List<string> list = ["a", "b", "c", "d", "e"];
        bool changed = ListEdits.RemoveAt(list, [4, 1, 1, 9, -1]);
        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join(",", list)).IsEqualTo("a,c,d");
        await Assert.That(ListEdits.RemoveAt(list, [7])).IsFalse();
    }
}
