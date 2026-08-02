namespace QualityJobs.Core.Tests;

public class SmokeTests
{
    [Test]
    public async Task CoreAssemblyLoads()
    {
        await Assert.That(typeof(QualityJobs.Core.QualityLevel).Assembly).IsNotNull();
    }
}
