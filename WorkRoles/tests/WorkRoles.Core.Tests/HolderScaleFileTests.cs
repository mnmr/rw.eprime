using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class HolderScaleFileTests
{
    [Test]
    public async Task BuildOmitsHoldersAndLegacyReferencesStillParse()
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
        XElement role = XElement.Parse(xml)
            .Element("Roles")!.Element("Role")!;

        await Assert.That(role.Element("Options")?.Element("Holders") == null)
            .IsTrue();

        // Legacy files still parse both the scale reference and the older
        // mode="never" attribute form.
        RoleFileDocument legacy = RoleFile.Parse(
            "<WorkRoles version=\"10\"><Roles>" +
            "<Role name=\"Doctor\"><Options><Holders scale=\"Doctoring\"/></Options>" +
            "<Jobs><WorkType>Doctor</WorkType></Jobs></Role>" +
            "<Role name=\"Idle\"><Options><Holders mode=\"never\"/></Options>" +
            "<Jobs><WorkType>Hauling</WorkType></Jobs></Role>" +
            "</Roles></WorkRoles>");
        await Assert.That(legacy.roles[0].holderScale).IsEqualTo("Doctoring");
        await Assert.That(legacy.roles[1].holderScale).IsEqualTo("Never");
    }
}
