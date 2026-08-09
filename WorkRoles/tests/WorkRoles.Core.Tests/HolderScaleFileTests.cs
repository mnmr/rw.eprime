using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class HolderScaleFileTests
{
    [Test]
    public async Task RoleScaleRoundTripsWithoutLegacyHolderAttributes()
    {
        var document = new RoleFileDocument
        {
            roles =
            {
                new FileRole
                {
                    label = "Doctor",
                    holderScale = "Doctoring",
                    entries =
                    {
                        new JobEntry(JobEntryKind.WorkType, "Doctor"),
                    },
                },
            },
        };

        string xml = RoleFile.Build(document);
        XElement holders = XElement.Parse(xml)
            .Element("Roles")!.Element("Role")!
            .Element("Options")!.Element("Holders")!;

        await Assert.That(holders.Attribute("scale")!.Value)
            .IsEqualTo("Doctoring");
        await Assert.That(holders.Attribute("mode") == null).IsTrue();
        await Assert.That(holders.Attribute("min") == null).IsTrue();
        await Assert.That(holders.Attribute("max") == null).IsTrue();
        await Assert.That(holders.Attribute("train") == null).IsTrue();
        await Assert.That(RoleFile.Parse(xml).roles[0].holderScale)
            .IsEqualTo("Doctoring");
    }
}
