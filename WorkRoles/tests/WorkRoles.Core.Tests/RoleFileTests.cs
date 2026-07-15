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
        locations = { LocationRules.Caravans, "settlement:Bö & <Wood> \"Camp\"", "ship:The Wanderer" },
        trainSkill = "Medicine",
        trainMin = 5,
        trainMax = 15,
        trainTargets = { "Doctor" },
        minHolders = 1,
        maxHolders = 3,
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
        // Location names round-trip any characters a player can type (XLinq escapes).
        await Assert.That(string.Join("|", role.locations))
            .IsEqualTo("caravans|settlement:Bö & <Wood> \"Camp\"|ship:The Wanderer");
        await Assert.That(string.Join(",", role.entries.Select(e => e.Encode())))
            .IsEqualTo("WorkGiver:FightFires,WorkType:Hauling"); // ORDER preserved across kinds
        await Assert.That(role.trainSkill).IsEqualTo("Medicine");
        await Assert.That(role.trainMin).IsEqualTo(5);
        await Assert.That(role.trainMax).IsEqualTo(15);
        await Assert.That(string.Join(",", role.trainTargets)).IsEqualTo("Doctor");
        await Assert.That(role.minHolders).IsEqualTo(1);
        await Assert.That(role.maxHolders).IsEqualTo(3);

        var plain = parsed.roles[1];
        await Assert.That(plain.templateDef == null).IsTrue();
        await Assert.That(plain.colorRef == null).IsTrue();
        await Assert.That(plain.enabled).IsTrue();
        await Assert.That(plain.activeHours).IsEqualTo(FileRole.AllHours);
        await Assert.That(plain.trainSkill == null).IsTrue();
        await Assert.That(plain.maxHolders).IsEqualTo(-1);
    }

    [Test]
    public async Task DefaultsProduceMinimalOptions_AndScaffoldingIsAlwaysPresent()
    {
        var doc = new RoleFileDocument
        {
            roles = { new FileRole { label = "Plain", entries = { new(JobEntryKind.WorkType, "Mining") } } },
        };
        string xml = RoleFile.Build(doc);
        // No Options ELEMENT (the format-notes comment mentions the word).
        var role = System.Xml.Linq.XElement.Parse(xml).Element("Roles")!.Element("Role")!;
        await Assert.That(role.Element("Options") == null).IsTrue();
        // Scaffolding: WorkRoles root, format-notes comment, Palette (with a
        // commented syntax sample when empty) and Roles always present.
        await Assert.That(xml.StartsWith("<WorkRoles")).IsTrue();
        await Assert.That(xml.Contains("<Palette>")).IsTrue();
        await Assert.That(xml.Contains("Format notes")).IsTrue();
        await Assert.That(xml.Contains("<!-- <Color name=\"ocean\">#0e7490</Color> -->")).IsTrue();
        // The sample is a comment: importing our own export adds no palette slot.
        var parsed = RoleFile.Parse(xml);
        await Assert.That(parsed.palette.Count).IsEqualTo(0);
        await Assert.That(parsed.roles.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GroupsRoundTripInOrderWithSpecialCharacters_AndDefaultStaysImplicit()
    {
        var doc = new RoleFileDocument
        {
            groups = { "Zulu & \"Friends\"", "Älpha" },
            roles =
            {
                new FileRole { label = "A", group = "Älpha", entries = { new(JobEntryKind.WorkType, "Mining") } },
                new FileRole { label = "B", entries = { new(JobEntryKind.WorkType, "Hauling") } },
                new FileRole { label = "C", group = "Unlisted", entries = { new(JobEntryKind.WorkType, "Cooking") } },
            },
        };
        var parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.error == null).IsTrue();
        // Order preserved, names escaped; unlisted names survive on the role.
        await Assert.That(parsed.groups).IsEquivalentTo(new[] { "Zulu & \"Friends\"", "Älpha" });
        await Assert.That(parsed.roles[0].group).IsEqualTo("Älpha");
        await Assert.That(parsed.roles[1].group == null).IsTrue(); // Default stays implicit
        await Assert.That(parsed.roles[2].group).IsEqualTo("Unlisted");
    }

    [Test]
    public async Task DuplicateGroupNamesDedupCaseInsensitively()
    {
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"1\"><Palette/>" +
            "<Groups><Group name=\"Kitchen\"/><Group name=\"kitchen\"/><Group name=\"Farm\"/></Groups>" +
            "<Roles><Role name=\"A\"><Jobs><WorkType>Mining</WorkType></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.groups).IsEquivalentTo(new[] { "Kitchen", "Farm" });
    }

    [Test]
    public async Task CommentsAreIgnoredEverywhereOnImport()
    {
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"1\"><!-- header --><Palette><!-- sample --></Palette>" +
            "<Roles><!-- note --><Role name=\"Ok\"><Jobs><!-- inline --><WorkType>Mining</WorkType></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.palette.Count).IsEqualTo(0);
        await Assert.That(parsed.roles.Count).IsEqualTo(1);
        await Assert.That(parsed.roles[0].entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ParsingIsLenient()
    {
        // Nameless role skipped; bad palette hex skipped; unknown elements ignored.
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"1\"><Palette><Color name=\"x\">notahex</Color></Palette><Roles>" +
            "<Role><Jobs><WorkType>Mining</WorkType></Jobs></Role>" +
            "<Role name=\"Ok\"><Junk/><Jobs><WorkGiver>Smelt</WorkGiver><Mystery>z</Mystery></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.palette.Count).IsEqualTo(0);
        await Assert.That(parsed.roles.Count).IsEqualTo(1);
        await Assert.That(parsed.roles[0].entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GarbageAndEmptyDocumentsReportErrors()
    {
        await Assert.That(RoleFile.Parse("not xml at all").error != null).IsTrue();
        await Assert.That(RoleFile.Parse("<WorkRoles version=\"1\"/>").error).IsEqualTo("empty document");
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
