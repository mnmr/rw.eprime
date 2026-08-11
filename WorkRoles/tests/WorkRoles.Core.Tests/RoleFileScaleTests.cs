using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Named scales are retired: exports no longer carry a <Scales> section or
/// Holders references, but legacy files keep parsing so imports can convert
/// them to colonyMin/coverage.
public class RoleFileScaleTests
{
    [Test]
    public async Task LegacyScalesAndRoleReferencesStillParse()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"10\">" +
            "<Roles><Role name=\"Doctor\"><Options>" +
            "<Holders scale=\"Gentle\"/>" +
            "</Options><Jobs><WorkType>Doctor</WorkType></Jobs></Role></Roles>" +
            "<Scales>" +
            "<Scale name=\"Gentle\" mode=\"0\"><Min>1,1,2,2,3,3,4,4,5,5,6,6</Min>" +
            "<Train>0,0,1,1,1,1,2,2,2,2,3,3</Train><Max>6</Max></Scale>" +
            "<Scale name=\"Never\" mode=\"2\" preset=\"true\"/>" +
            "<Scale name=\"Unskilled\" mode=\"1\" preset=\"true\">" +
            "<Min>0,0,0,1,1,1,1,1,1,1,1,1</Min><Train>0</Train></Scale>" +
            "</Scales></WorkRoles>");

        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.roles[0].holderScale).IsEqualTo("Gentle");
        RoleAssignmentStrategy gentle = parsed.scales.Single(s => s.Name == "Gentle");
        await Assert.That(gentle.Mode).IsEqualTo(ScaleMode.Skilled);
        await Assert.That(gentle.Scale.RequiredTotals[0]).IsEqualTo(1);
        await Assert.That(gentle.Scale.RequiredTotals[11]).IsEqualTo(6);
        RoleAssignmentStrategy never = parsed.scales.Single(s => s.Name == "Never");
        await Assert.That(never.Mode).IsEqualTo(ScaleMode.Never);
        await Assert.That(never.Scale == null).IsTrue();
        RoleAssignmentStrategy unskilled = parsed.scales.Single(s => s.Name == "Unskilled");
        await Assert.That(unskilled.Mode).IsEqualTo(ScaleMode.Unskilled);
        await Assert.That(unskilled.Scale.RequiredTotalAt(12)).IsEqualTo(1);
        await Assert.That(unskilled.Scale.RequiredTotalAt(9)).IsEqualTo(0);
    }

    [Test]
    public async Task ExportsCarryNoScalesSectionOrHolderReferences()
    {
        var doc = new RoleFileDocument();
        doc.scales.Add(RoleAssignmentStrategy.Never("Never"));
        doc.roles.Add(new FileRole
        {
            label = "Doctor",
            holderScale = "Gentle",
            entries = { new JobEntry(JobEntryKind.WorkType, "Doctor") },
        });

        string xml = RoleFile.Build(doc);

        // Element checks: the format-notes comment legitimately mentions the
        // legacy element names.
        System.Xml.Linq.XElement root = System.Xml.Linq.XElement.Parse(xml);
        await Assert.That(root.Element("Scales") == null).IsTrue();
        await Assert.That(root.Element("Roles")!.Element("Role")!
            .Element("Options")?.Element("Holders") == null).IsTrue();
        RoleFileDocument parsed = RoleFile.Parse(xml);
        await Assert.That(parsed.scales.Count).IsEqualTo(0);
        await Assert.That(parsed.roles[0].holderScale == null).IsTrue();
    }

    [Test]
    public async Task DocumentsWithoutScalesParseEmpty()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole { label = "Cook" });
        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.scales.Count).IsEqualTo(0);
        await Assert.That(parsed.roles[0].holderScale == null).IsTrue();
    }
}
