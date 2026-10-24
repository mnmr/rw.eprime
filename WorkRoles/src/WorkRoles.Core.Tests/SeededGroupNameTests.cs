using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class SeededGroupNameTests
{
    [Test]
    public async Task AuthoredTemplateRoleNameKeepsItsAuthorshipAcrossExport()
    {
        var parsed = RoleFile.Parse("<WorkRoles><Roles><Role name='Cook' id='WS_Cook' customName='true'/></Roles></WorkRoles>");
        var role = XElement.Parse(RoleFile.Build(parsed)).Element("Roles")!.Element("Role")!;
        await Assert.That(role.Attribute("name")!.Value).IsEqualTo("Cook");
        await Assert.That(role.Attribute("id")!.Value).IsEqualTo("WS_Cook");
        await Assert.That(role.Attribute("customName")?.Value).IsEqualTo("true");
    }

    [Test]
    public async Task ExportRoundTripRetainsExplicitSeedProvenanceWithoutInferringCustomNames()
    {
        const string xml = "<WorkRoles><Groups><Group name='Food' fileId='g1'/>"
            + "<Group name='Production' fileId='g2' nameSourceDef='WS_GroupProduction'/></Groups>"
            + "<Roles><Role name='Cook' groupId='g1'/><Role name='Crafter' groupId='g2'/></Roles></WorkRoles>";
        var parsed = RoleFile.Parse(xml);
        var exported = XElement.Parse(RoleFile.Build(parsed));
        var groups = exported.Element("Groups")!.Elements("Group").ToArray();
        await Assert.That(groups[0].Attribute("name")!.Value).IsEqualTo("Food");
        await Assert.That(groups[0].Attribute("nameSourceDef")).IsNull();
        await Assert.That(groups[1].Attribute("nameSourceDef")?.Value).IsEqualTo("WS_GroupProduction");
    }
}
