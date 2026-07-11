using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The export file format is a contract with users' saved/shared files —
/// these tests pin serialization, parsing, leniency and the hex/hours codecs.
public class RoleFileTests
{
    private static FileRole Full() => new()
    {
        label = "Night Guard",
        templateDef = "WS_Custom",
        colorRef = "fire",
        autoAssign = true,
        blocker = true,
        enabled = false,
        activeHours = RoleFile.BitsToHours("111111000000000000000000"),
        location = "AwayOnly",
        entries = new List<JobEntry>
        {
            new(JobEntryKind.WorkGiver, "FightFires"),
            new(JobEntryKind.WorkType, "Hauling"),
        },
    };

    [Test]
    public async Task FullDocumentRoundTrips()
    {
        var doc = new RoleFileDocument
        {
            palette = { ("fire", new ColorRgb(0.6f, 0.33f, 0.1f)) },
            roles = { Full(), new FileRole { label = "Plain", entries = { new(JobEntryKind.WorkType, "Mining") } } },
        };
        var parsed = RoleFile.Parse(RoleFile.Build(doc));

        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.palette.Count).IsEqualTo(1);
        await Assert.That(parsed.palette[0].name).IsEqualTo("fire");
        await Assert.That(parsed.palette[0].color.Hex()).IsEqualTo(doc.palette[0].color.Hex());

        var role = parsed.roles[0];
        await Assert.That(role.label).IsEqualTo("Night Guard");
        await Assert.That(role.templateDef).IsEqualTo("WS_Custom");
        await Assert.That(role.colorRef).IsEqualTo("fire");
        await Assert.That(role.autoAssign).IsTrue();
        await Assert.That(role.blocker).IsTrue();
        await Assert.That(role.enabled).IsFalse();
        await Assert.That(RoleFile.HoursToBits(role.activeHours)).IsEqualTo("111111000000000000000000");
        await Assert.That(role.location).IsEqualTo("AwayOnly");
        await Assert.That(string.Join(",", role.entries.Select(e => e.Encode())))
            .IsEqualTo("WorkGiver:FightFires,WorkType:Hauling"); // ORDER preserved across kinds

        var plain = parsed.roles[1];
        await Assert.That(plain.templateDef == null).IsTrue();
        await Assert.That(plain.colorRef == null).IsTrue();
        await Assert.That(plain.enabled).IsTrue();
        await Assert.That(plain.activeHours).IsEqualTo(FileRole.AllHours);
    }

    [Test]
    public async Task DefaultsProduceMinimalXmlWithoutOptions()
    {
        var doc = new RoleFileDocument
        {
            roles = { new FileRole { label = "Plain", entries = { new(JobEntryKind.WorkType, "Mining") } } },
        };
        string xml = RoleFile.Build(doc);
        await Assert.That(xml.Contains("<Options>")).IsFalse();
        await Assert.That(xml.Contains("<Palette>")).IsFalse();
    }

    [Test]
    public async Task ParsingIsLenient()
    {
        // Nameless role skipped; bad palette hex skipped; unknown elements ignored.
        var parsed = RoleFile.Parse(
            "<Roles version=\"1\"><Palette><Color name=\"x\">notahex</Color></Palette>" +
            "<Role><Jobs><WorkType>Mining</WorkType></Jobs></Role>" +
            "<Role name=\"Ok\"><Junk/><Jobs><WorkGiver>Smelt</WorkGiver><Mystery>z</Mystery></Jobs></Role></Roles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.palette.Count).IsEqualTo(0);
        await Assert.That(parsed.roles.Count).IsEqualTo(1);
        await Assert.That(parsed.roles[0].entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GarbageAndEmptyDocumentsReportErrors()
    {
        await Assert.That(RoleFile.Parse("not xml at all").error != null).IsTrue();
        await Assert.That(RoleFile.Parse("<Roles version=\"1\"/>").error).IsEqualTo("empty document");
    }

    [Test]
    public async Task HexCodecRoundTripsAndRejectsGarbage()
    {
        await Assert.That(ColorRgb.TryParseHex("#99551b", out var c)).IsTrue();
        await Assert.That(c.Hex()).IsEqualTo("#99551b");
        await Assert.That(ColorRgb.TryParseHex("99551b", out _)).IsFalse();
        await Assert.That(ColorRgb.TryParseHex("#99551", out _)).IsFalse();
        await Assert.That(ColorRgb.TryParseHex("#zzzzzz", out _)).IsFalse();
    }
}
