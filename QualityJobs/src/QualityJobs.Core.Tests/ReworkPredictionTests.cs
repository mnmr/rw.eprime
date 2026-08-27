namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ReworkPredictionTests
{
    /// A finish bill is one run by construction: on a below-target roll the
    /// bill is deleted and the rework debt stays on the source bill's
    /// undecremented repeat count. Predicting rework for it double-counts.
    [Test]
    public async Task FinishBillsNeverPredictRework()
    {
        await Assert.That(ReworkPrediction.PredictsRework(
            isFinishBill: true, repeatCountMode: true,
            managed: true, targetQuality: 4)).IsFalse();
    }

    /// Retry marking is gated to repeat-count mode: Forever never stops and
    /// TargetCount already filters counted products by quality in vanilla, so
    /// Quality Jobs never reworks those modes.
    [Test]
    public async Task NonRepeatCountModesNeverPredictRework()
    {
        await Assert.That(ReworkPrediction.PredictsRework(
            isFinishBill: false, repeatCountMode: false,
            managed: true, targetQuality: 4)).IsFalse();
    }

    [Test]
    public async Task UnmanagedBillsNeverPredictRework()
    {
        await Assert.That(ReworkPrediction.PredictsRework(
            isFinishBill: false, repeatCountMode: true,
            managed: false, targetQuality: 4)).IsFalse();
    }

    [Test]
    public async Task AbsentTargetPredictsNoRework()
    {
        await Assert.That(ReworkPrediction.PredictsRework(
            isFinishBill: false, repeatCountMode: true,
            managed: true, targetQuality: 0)).IsFalse();
    }

    [Test]
    public async Task ManagedRepeatCountBillWithTargetPredictsRework()
    {
        await Assert.That(ReworkPrediction.PredictsRework(
            isFinishBill: false, repeatCountMode: true,
            managed: true, targetQuality: 4)).IsTrue();
    }
}
