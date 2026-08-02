namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class BillConfigTests
{
    [Test]
    public async Task EqualityDetectsNoOpEdits()
    {
        var a = new BillConfig(true, new ResumeCondition(15, true, false));
        var b = new BillConfig(true, new ResumeCondition(15, true, false));
        var c = new BillConfig(true, new ResumeCondition(16, true, false));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.Equals(c)).IsFalse();
    }

    [Test]
    public async Task EqualityComparesManagedFlag()
    {
        var cond = new ResumeCondition(15, true, false);
        await Assert.That(new BillConfig(false, cond).Equals(new BillConfig(true, cond))).IsFalse();
    }
}
