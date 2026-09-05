namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// Spec §4 revert triggers. The regression here is the finisher carrying the
/// unfinished item from storage to the bench: the item is unspawned for the
/// whole walk, which must never count as a lost item.
public class DispatchRevertPolicyTests
{
    [Test]
    public async Task FinisherCarryingTheItemToTheBenchKeepsTheDispatch()
    {
        bool revert = DispatchRevertPolicy.ShouldRevert(
            itemOnFinisherMap: true,
            finisherAvailable: true,
            finishBillAlive: true,
            finisherQualifies: true);

        await Assert.That(revert).IsFalse();
    }

    [Test]
    public async Task ItemLeavingTheFinisherMapReverts()
    {
        bool revert = DispatchRevertPolicy.ShouldRevert(
            itemOnFinisherMap: false,
            finisherAvailable: true,
            finishBillAlive: true,
            finisherQualifies: true);

        await Assert.That(revert).IsTrue();
    }

    [Test]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    public async Task LostFinisherDeletedBillOrFailedConditionReverts(
        bool finisherAvailable, bool finishBillAlive, bool finisherQualifies)
    {
        bool revert = DispatchRevertPolicy.ShouldRevert(
            itemOnFinisherMap: true,
            finisherAvailable, finishBillAlive, finisherQualifies);

        await Assert.That(revert).IsTrue();
    }
}
