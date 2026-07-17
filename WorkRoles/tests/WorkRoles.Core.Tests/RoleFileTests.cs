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

        var plain = parsed.roles[1];
        await Assert.That(plain.templateDef == null).IsTrue();
        await Assert.That(plain.colorRef == null).IsTrue();
        await Assert.That(plain.enabled).IsTrue();
        await Assert.That(plain.activeHours).IsEqualTo(FileRole.AllHours);
        await Assert.That(plain.trainSkill == null).IsTrue();
        await Assert.That(plain.minHolders).IsEqualTo(-1);
    }

    [Test]
    public async Task LegacyMaxZeroHoldersImportAsNeverDealt()
    {
        // v2 files from 1.1.x carried never-dealt as max="0".
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"2\"><Palette/><Roles>" +
            "<Role name=\"Statue\"><Options><Holders max=\"0\"/></Options>" +
            "<Jobs><WorkType>Art</WorkType></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.roles[0].minHolders).IsEqualTo(0);
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
    public async Task ResolvePathEntriesDropsUnknownAndDuplicateRolesKeepingBandsAligned()
    {
        var path = new FileTrainingPath
        {
            name = "Frontline",
            entries = { ("Shooter", 0, 8), ("Deleted", 5, 10), ("Sniper", 6, 21), ("Shooter", 2, 4) },
        };
        // "Deleted" resolves nowhere (import after the role was removed); the
        // second "Shooter" is a duplicate — both drop WITH their bands.
        var resolved = RoleFile.ResolvePathEntries(path,
            name => name == "Shooter" ? 1 : name == "Sniper" ? 2 : (int?)null);
        await Assert.That(resolved.ids).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(resolved.mins).IsEquivalentTo(new[] { 0, 6 });
        await Assert.That(resolved.maxes).IsEquivalentTo(new[] { 8, 21 });
    }

    [Test]
    public async Task TrainingPathsAndRecommendationOrderRoundTrip()
    {
        var doc = new RoleFileDocument
        {
            groups = { "Combat" },
            roles =
            {
                new FileRole { label = "Shooter", group = "Combat", entries = { new(JobEntryKind.WorkType, "Hunting") } },
                new FileRole { label = "Sniper", group = "Combat", entries = { new(JobEntryKind.WorkType, "Hunting") } },
                new FileRole { label = "Doctor", entries = { new(JobEntryKind.WorkType, "Doctor") } },
            },
            trainingPaths =
            {
                new FileTrainingPath
                {
                    name = "Frontline",
                    colorRef = "red-800",
                    anchorRole = "Doctor",
                    entries = { ("Shooter", 0, 8), ("Sniper", 6, 21) }, // 21 = open top
                },
                new FileTrainingPath
                {
                    name = "Medics & \"Friends\"",
                    anchorRole = "Doctor",
                    anchorBefore = false,
                    entries = { ("Doctor", 4, 21) },
                },
            },
            recommendationOrder = { "Doctor", "Shooter", "Sniper" },
        };
        string xml = RoleFile.Build(doc);
        await Assert.That(System.Xml.Linq.XElement.Parse(xml).Attribute("version")!.Value)
            .IsEqualTo("3");

        var parsed = RoleFile.Parse(xml);
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.roles[0].group).IsEqualTo("Combat");
        await Assert.That(parsed.trainingPaths.Count).IsEqualTo(2);
        var first = parsed.trainingPaths[0];
        await Assert.That(first.name).IsEqualTo("Frontline");
        await Assert.That(first.colorRef).IsEqualTo("red-800");
        await Assert.That(first.anchorRole).IsEqualTo("Doctor");
        await Assert.That(first.anchorBefore).IsTrue();
        await Assert.That(string.Join("|", first.entries.Select(e => $"{e.role}:{e.min}-{e.max}")))
            .IsEqualTo("Shooter:0-8|Sniper:6-21");
        var second = parsed.trainingPaths[1];
        await Assert.That(second.name).IsEqualTo("Medics & \"Friends\"");
        await Assert.That(second.colorRef == null).IsTrue(); // no override: attribute absent
        await Assert.That(second.anchorBefore).IsFalse();
        await Assert.That(string.Join("|", second.entries.Select(e => $"{e.role}:{e.min}-{e.max}")))
            .IsEqualTo("Doctor:4-21");
        await Assert.That(parsed.recommendationOrder)
            .IsEquivalentTo(new[] { "Doctor", "Shooter", "Sniper" }); // ORDER is the payload
    }

    [Test]
    public async Task AnchorlessPathAndEmptyOrderStayAbsent()
    {
        var doc = new RoleFileDocument
        {
            roles = { new FileRole { label = "A", entries = { new(JobEntryKind.WorkType, "Mining") } } },
            trainingPaths = { new FileTrainingPath { name = "Solo", entries = { ("A", 0, 21) } } },
        };
        string xml = RoleFile.Build(doc);
        // Element checks, not Contains: the format-notes comment names both.
        var root = System.Xml.Linq.XElement.Parse(xml);
        // Empty stored order: no section at all (absent != empty override).
        await Assert.That(root.Element("RecommendationOrder") == null).IsTrue();
        await Assert.That(root.Element("TrainingPaths")!.Element("Path")!.Element("Anchor") == null).IsTrue();
        await Assert.That(root.Element("TrainingPaths")!.Element("Path")!.Attribute("color") == null).IsTrue();
        var parsed = RoleFile.Parse(xml);
        await Assert.That(parsed.trainingPaths[0].anchorRole == null).IsTrue();
        await Assert.That(parsed.trainingPaths[0].anchorBefore).IsTrue();
        await Assert.That(parsed.recommendationOrder.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FilesWithoutVersionOrNewSectionsParseAsBefore()
    {
        // A v1 file: no version attribute, no v2/v3 sections.
        var parsed = RoleFile.Parse(
            "<WorkRoles><Palette/><Roles>" +
            "<Role name=\"Ok\"><Options><Color>red-800</Color></Options>" +
            "<Jobs><WorkType>Mining</WorkType></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.roles.Count).IsEqualTo(1);
        await Assert.That(parsed.roles[0].colorRef).IsEqualTo("red-800");
        await Assert.That(parsed.trainingPaths.Count).IsEqualTo(0);
        await Assert.That(parsed.recommendationOrder.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PathParsingSkipsNamelessPathsAndBadBands()
    {
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"3\"><Palette/>" +
            "<Roles><Role name=\"A\"><Jobs><WorkType>Mining</WorkType></Jobs></Role></Roles>" +
            "<TrainingPaths><Path><Role min=\"0\" max=\"21\">A</Role></Path>" +
            "<Path name=\"Ok\"><Role min=\"0\" max=\"3\">A</Role>" + // span < 4
            "<Role min=\"-1\" max=\"8\">A</Role><Role min=\"0\" max=\"22\">A</Role>" +
            "<Role min=\"4\" max=\"12\"></Role><Role min=\"4\" max=\"12\">B</Role></Path>" +
            "</TrainingPaths></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.trainingPaths.Count).IsEqualTo(1); // nameless skipped
        await Assert.That(string.Join("|", parsed.trainingPaths[0].entries.Select(e => e.role + ":" + e.min + "-" + e.max)))
            .IsEqualTo("B:4-12");
    }

    [Test]
    public async Task UnknownPathEntryNameDropsThatEntryAndKeepsTheRest()
    {
        var path = new FileTrainingPath
        {
            name = "P",
            entries = { ("Known", 0, 8), ("Unknown", 6, 14), ("Known", 8, 21), ("Other", 10, 21) },
        };
        var byName = new Dictionary<string, int> { ["Known"] = 7, ["Other"] = 9 };
        var (ids, mins, maxes) = RoleFile.ResolvePathEntries(path,
            n => byName.TryGetValue(n, out int id) ? id : (int?)null);
        // Unknown drops with its band; a duplicate id keeps its first band.
        await Assert.That(ids).IsEquivalentTo(new[] { 7, 9 });
        await Assert.That(mins).IsEquivalentTo(new[] { 0, 10 });
        await Assert.That(maxes).IsEquivalentTo(new[] { 8, 21 });
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
