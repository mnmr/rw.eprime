using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class InvariantDefNameTests
{
    [Test]
    [Arguments("WS_DrugMaker", "WS_", "Drug Maker")]
    [Arguments("WS_GroupDarkStudy", "WS_Group", "Dark Study")]
    [Arguments("PatientBedRest", null, "Patient Bed Rest")]
    [Arguments("VSE_AnimalCare", null, "VSE Animal Care")]
    public async Task HumanizeUsesOnlyInvariantDefName(
        string defName, string prefix, string expected)
    {
        await Assert.That(InvariantDefName.Humanize(defName, prefix))
            .IsEqualTo(expected);
    }
}
