namespace WorkRoles.Core.Tests.Jobs;

/// MultiFloors' prioritized scanner splits a pawn's normal giver order into a
/// high lane (work-type priority at or below its threshold) and a low lane,
/// each keeping the original order. WorkRoles feeds it the compiled role
/// order with the vanilla priority projection; these scenarios pin the split.
public class MultiFloorsLanesTests
{
    private static string Flat(IEnumerable<string> givers) => string.Join(",", givers);

    [Test]
    public async Task SplitsByThresholdAndKeepsOrderInBothLanes()
    {
        // Compiled order with the vanilla projection of each giver's work type.
        string[] order = ["FightFires", "DoctorTendEmergency", "HaulGeneral", "GrowerSow", "Research", "CleanFilth"];
        int[] priorities = [1, 1, 2, 3, 3, 4];
        var high = new List<string>();
        var low = new List<string>();

        MultiFloorsLanes.Split(order, priorities, threshold: 2, high, low);

        await Assert.That(Flat(high)).IsEqualTo("FightFires,DoctorTendEmergency,HaulGeneral");
        await Assert.That(Flat(low)).IsEqualTo("GrowerSow,Research,CleanFilth");
    }

    [Test]
    public async Task ThresholdBelowEveryPriorityLeavesTheHighLaneEmpty()
    {
        string[] order = ["HaulGeneral", "CleanFilth"];
        int[] priorities = [3, 4];
        var high = new List<string>();
        var low = new List<string>();

        MultiFloorsLanes.Split(order, priorities, threshold: 1, high, low);

        await Assert.That(high.Count).IsEqualTo(0);
        await Assert.That(Flat(low)).IsEqualTo("HaulGeneral,CleanFilth");
    }

    [Test]
    public async Task ThresholdAtOrAboveEveryPriorityFillsOnlyTheHighLane()
    {
        string[] order = ["HaulGeneral", "CleanFilth"];
        int[] priorities = [3, 4];
        var high = new List<string>();
        var low = new List<string>();

        MultiFloorsLanes.Split(order, priorities, threshold: 4, high, low);

        await Assert.That(Flat(high)).IsEqualTo("HaulGeneral,CleanFilth");
        await Assert.That(low.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RebuildingIntoReusedLanesReplacesStaleContents()
    {
        var high = new List<string> { "Stale" };
        var low = new List<string> { "Stale" };
        string[] order = ["HaulGeneral"];
        int[] priorities = [2];

        MultiFloorsLanes.Split(order, priorities, threshold: 2, high, low);

        await Assert.That(Flat(high)).IsEqualTo("HaulGeneral");
        await Assert.That(low.Count).IsEqualTo(0);
    }
}
