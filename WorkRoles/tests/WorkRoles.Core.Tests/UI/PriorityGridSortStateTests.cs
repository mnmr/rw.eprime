namespace WorkRoles.Core.Tests.UI;

public class PriorityGridSortStateTests
{
    [Test]
    public async Task FirstClickSortsAscendingWithInactiveRowsLastAndStableTies()
    {
        var state = new PriorityGridSortState(rowCount: 5);
        int[] priorities = [3, 0, 1, 2, 1];

        state.Toggle(columnIndex: 2, priorities);

        await Assert.That(state.SortedColumnIndex).IsEqualTo(2);
        await Assert.That(string.Join(",", state.RowOrder)).IsEqualTo("2,4,3,0,1");
    }

    [Test]
    public async Task ClickingActiveColumnRestoresOriginalOrder()
    {
        var state = new PriorityGridSortState(rowCount: 4);
        int[] priorities = [2, 1, 0, 1];
        state.Toggle(columnIndex: 3, priorities);

        state.Toggle(columnIndex: 3, priorities);

        await Assert.That(state.SortedColumnIndex).IsNull();
        await Assert.That(string.Join(",", state.RowOrder)).IsEqualTo("0,1,2,3");
    }

    [Test]
    public async Task ClickingAnotherColumnReplacesTheActiveSort()
    {
        var state = new PriorityGridSortState(rowCount: 5);
        int[] firstPriorities = [3, 0, 1, 2, 1];
        int[] secondPriorities = [2, 1, 0, 1, 3];
        state.Toggle(columnIndex: 2, firstPriorities);

        state.Toggle(columnIndex: 4, secondPriorities);

        await Assert.That(state.SortedColumnIndex).IsEqualTo(4);
        await Assert.That(string.Join(",", state.RowOrder)).IsEqualTo("1,3,0,4,2");
    }

    [Test]
    public async Task RefreshReappliesActiveColumnWithoutTogglingItOff()
    {
        var state = new PriorityGridSortState(rowCount: 4);
        int[] initialPriorities = [3, 2, 1, 0];
        int[] refreshedPriorities = [1, 3, 0, 2];
        state.Toggle(columnIndex: 1, initialPriorities);

        state.Refresh(refreshedPriorities);

        await Assert.That(state.SortedColumnIndex).IsEqualTo(1);
        await Assert.That(string.Join(",", state.RowOrder)).IsEqualTo("0,3,1,2");
    }
}
