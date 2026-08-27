namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class CompletionRetrySignalTests
{
    [Test]
    public async Task MatchingBillAndTickConsumesSignalOnce()
    {
        var signal = new CompletionRetrySignal();
        signal.Mark("Bill_A", 100);

        await Assert.That(signal.Consume("Bill_A", 100)).IsTrue();
        await Assert.That(signal.Consume("Bill_A", 100)).IsFalse();
    }

    [Test]
    public async Task RepeatedMarkRemainsOneSignal()
    {
        var signal = new CompletionRetrySignal();
        signal.Mark("Bill_A", 100);
        signal.Mark("Bill_A", 100);

        await Assert.That(signal.Consume("Bill_A", 100)).IsTrue();
        await Assert.That(signal.Consume("Bill_A", 100)).IsFalse();
    }

    [Test]
    public async Task DifferentBillDoesNotMatchAndClearsSignal()
    {
        var signal = new CompletionRetrySignal();
        signal.Mark("Bill_A", 100);

        await Assert.That(signal.Consume("Bill_B", 100)).IsFalse();
        await Assert.That(signal.Consume("Bill_A", 100)).IsFalse();
    }

    [Test]
    public async Task DifferentTickDoesNotMatchAndClearsSignal()
    {
        var signal = new CompletionRetrySignal();
        signal.Mark("Bill_A", 100);

        await Assert.That(signal.Consume("Bill_A", 101)).IsFalse();
        await Assert.That(signal.Consume("Bill_A", 100)).IsFalse();
    }
}
