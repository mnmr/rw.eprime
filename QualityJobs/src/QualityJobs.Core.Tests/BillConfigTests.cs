namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class BillConfigTests
{
    [Test]
    public async Task EqualityDetectsNoOpEdits()
    {
        var a = new BillConfig(true, false, new ResumeCondition(15, true, false));
        var b = new BillConfig(true, false, new ResumeCondition(15, true, false));
        var c = new BillConfig(true, false, new ResumeCondition(16, true, false));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.Equals(c)).IsFalse();
    }

    [Test]
    public async Task AutoBestDifferenceBreaksEquality()
    {
        var a = new BillConfig(true, false, new ResumeCondition(10, false, false));
        var b = new BillConfig(true, true, new ResumeCondition(10, false, false));
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task EqualityComparesManagedFlag()
    {
        var cond = new ResumeCondition(15, true, false);
        await Assert.That(new BillConfig(false, false, cond).Equals(new BillConfig(true, false, cond))).IsFalse();
    }
}
