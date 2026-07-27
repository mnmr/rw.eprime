using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class CameraPatchArchitectureTests
{
    [Test]
    public async Task EdgeDollyIsSuppressedViaTheMouseCoveredFieldOverThePanel()
    {
        string source = Source("Patches", "Patch_CameraDriver.cs");
        await Assert.That(source).Contains("\"CalculateCurInputDollyVect\"");
        await Assert.That(source).Contains("ref bool ___mouseCoveredByUI");
        await Assert.That(source).Contains("ReadoutPanel.IsOverPoint(");
        // Must not fight other camera logic: only ever sets the flag true.
        await Assert.That(source).DoesNotContain("___mouseCoveredByUI = false");
    }
}
