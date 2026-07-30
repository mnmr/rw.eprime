using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PriorityGridSortStateTests
{
    [Test]
    public async Task FirstClickSortsAscendingWithInactiveRowsLastAndStableTies()
    {
        var state = new PriorityGridSortState(rowCount: 5);

        state.Toggle(columnIndex: 2, priorities: new[] { 3, 0, 1, 2, 1 });

        await Assert.That(state.SortedColumnIndex).IsEqualTo(2);
        await Assert.That(state.RowOrder.SequenceEqual(new[] { 2, 4, 3, 0, 1 })).IsTrue();
    }

    [Test]
    public async Task ClickingActiveColumnRestoresOriginalOrder()
    {
        var state = new PriorityGridSortState(rowCount: 4);
        state.Toggle(columnIndex: 3, priorities: new[] { 2, 1, 0, 1 });

        state.Toggle(columnIndex: 3, priorities: new[] { 2, 1, 0, 1 });

        await Assert.That(state.SortedColumnIndex).IsNull();
        await Assert.That(state.RowOrder.SequenceEqual(new[] { 0, 1, 2, 3 })).IsTrue();
    }

    [Test]
    public async Task ClickingAnotherColumnReplacesTheActiveSort()
    {
        var state = new PriorityGridSortState(rowCount: 5);
        state.Toggle(columnIndex: 2, priorities: new[] { 3, 0, 1, 2, 1 });

        state.Toggle(columnIndex: 4, priorities: new[] { 2, 1, 0, 1, 3 });

        await Assert.That(state.SortedColumnIndex).IsEqualTo(4);
        await Assert.That(state.RowOrder.SequenceEqual(new[] { 1, 3, 0, 4, 2 })).IsTrue();
    }

    [Test]
    public async Task RefreshReappliesActiveColumnWithoutTogglingItOff()
    {
        var state = new PriorityGridSortState(rowCount: 4);
        state.Toggle(columnIndex: 1, priorities: new[] { 3, 2, 1, 0 });

        state.Refresh(priorities: new[] { 1, 3, 0, 2 });

        await Assert.That(state.SortedColumnIndex).IsEqualTo(1);
        await Assert.That(state.RowOrder.SequenceEqual(new[] { 0, 3, 1, 2 })).IsTrue();
    }
}
