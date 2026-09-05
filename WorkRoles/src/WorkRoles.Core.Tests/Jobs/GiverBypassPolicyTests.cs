namespace WorkRoles.Core.Tests.Jobs;

public class GiverBypassPolicyTests
{
    [Test]
    public async Task QualityJobsFinishGiversAreExemptFromBypassDetection()
    {
        await Assert.That(GiverBypassPolicy.IsExemptGiver("QJ_FinishQualityWork_Tailoring")).IsTrue();
        await Assert.That(GiverBypassPolicy.IsExemptGiver("QJ_FinishQualityWork_Construction")).IsTrue();
    }

    [Test]
    public async Task OrdinaryGiversStillCountAsBypasses()
    {
        await Assert.That(GiverBypassPolicy.IsExemptGiver("HaulGeneral")).IsFalse();
        await Assert.That(GiverBypassPolicy.IsExemptGiver("DoBillsMakeApparel")).IsFalse();
        await Assert.That(GiverBypassPolicy.IsExemptGiver(null)).IsFalse();
    }
}
