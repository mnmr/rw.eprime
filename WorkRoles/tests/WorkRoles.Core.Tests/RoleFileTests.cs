using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The export file format is a contract with users' saved/shared files —
/// these tests pin serialization, parsing, leniency and the hex/hours codecs.
public class RoleFileTests
{
    [Test]
    public async Task WhitespaceOnlyRoleNamesAreRejectedAfterNormalization()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Roles>" +
            "<Role fileId=\"role-a\" id=\"template-a\" name=\"   \"><Jobs/></Role>" +
            "</Roles></WorkRoles>");

        await Assert.That(parsed.roles.Count).IsEqualTo(0);
        await Assert.That(parsed.error == null).IsFalse();
    }

    [Test]
    public async Task LegacyPublicCollectionFieldTypesRemainExact()
    {
        Type document = typeof(RoleFileDocument);
        Type path = typeof(FileTrainingPath);

        await Assert.That(document.GetField(nameof(RoleFileDocument.groups))!.FieldType)
            .IsEqualTo(typeof(List<string>));
        await Assert.That(document.GetField(nameof(RoleFileDocument.recommendationOrder))!.FieldType)
            .IsEqualTo(typeof(List<string>));
        await Assert.That(path.GetField(nameof(FileTrainingPath.entries))!.FieldType)
            .IsEqualTo(typeof(List<(string role, int min, int max)>));
    }

    [Test]
    public async Task StaleRichMetadataFallsBackToMutatedLegacyCollections()
    {
        var doc = new RoleFileDocument
        {
            groups = { "Renamed team" },
            groupsWithIds = { new FileGroup { fileId = "group-old", name = "Old team" } },
            roles = { new FileRole { fileId = "role-new", label = "Renamed role" } },
            trainingPaths =
            {
                new FileTrainingPath
                {
                    name = "Path",
                    anchorRole = "Renamed role",
                    anchorRoleId = "role-old",
                    anchorWithId = new FileRoleReference("role-old", "Old role"),
                    entries = { ("Renamed role", 0, 21) },
                    entriesWithIds =
                    {
                        new FileTrainingPathEntry("role-old", "Old role", 0, 21),
                    },
                },
            },
            recommendationOrder = { "Renamed role" },
            recommendationOrderWithIds =
            {
                new FileRoleReference("role-old", "Old role"),
            },
        };

        var root = System.Xml.Linq.XElement.Parse(RoleFile.Build(doc));
        var group = root.Element("Groups")!.Element("Group")!;
        var path = root.Element("TrainingPaths")!.Element("Path")!;
        var anchor = path.Element("Anchor")!;
        var pathRole = path.Element("Role")!;
        var orderRole = root.Element("RecommendationOrder")!.Element("Role")!;

        await Assert.That(group.Attribute("fileId") == null).IsTrue();
        await Assert.That(group.Attribute("name")!.Value).IsEqualTo("Renamed team");
        await Assert.That(anchor.Attribute("roleId") == null).IsTrue();
        await Assert.That(anchor.Value).IsEqualTo("Renamed role");
        await Assert.That(pathRole.Attribute("roleId") == null).IsTrue();
        await Assert.That(pathRole.Value).IsEqualTo("Renamed role");
        await Assert.That(orderRole.Attribute("roleId") == null).IsTrue();
        await Assert.That(orderRole.Value).IsEqualTo("Renamed role");
    }

    [Test]
    public async Task ParserPopulatesAlignedLegacyAndRichCollections()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Groups><Group fileId=\"group-a\" name=\"Team\"/></Groups>" +
            "<Roles><Role fileId=\"role-a\" name=\"Worker\"><Jobs/></Role></Roles>" +
            "<TrainingPaths><Path name=\"P\"><Anchor roleId=\"role-a\">Worker</Anchor>" +
            "<Role roleId=\"role-a\" min=\"0\" max=\"21\">Worker</Role></Path></TrainingPaths>" +
            "<RecommendationOrder><Role roleId=\"role-a\">Worker</Role></RecommendationOrder>" +
            "</WorkRoles>");

        await Assert.That(parsed.groups).IsEquivalentTo(new[] { "Team" });
        await Assert.That(parsed.groupsWithIds[0].fileId).IsEqualTo("group-a");
        await Assert.That(parsed.trainingPaths[0].entries[0].role).IsEqualTo("Worker");
        await Assert.That(parsed.trainingPaths[0].entriesWithIds[0].role.fileId)
            .IsEqualTo("role-a");
        await Assert.That(parsed.trainingPaths[0].anchorWithId.fileId).IsEqualTo("role-a");
        await Assert.That(parsed.recommendationOrder).IsEquivalentTo(new[] { "Worker" });
        await Assert.That(parsed.recommendationOrderWithIds[0].fileId).IsEqualTo("role-a");
    }

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
        holderMode = RoleHolderMode.Custom,
        minHolders = 2,
        maxHolders = 4,
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
        await Assert.That(role.minHolders).IsEqualTo(2);
        await Assert.That(role.maxHolders).IsEqualTo(4);
        await Assert.That(role.holderMode).IsEqualTo(RoleHolderMode.Custom);

        var plain = parsed.roles[1];
        await Assert.That(plain.templateDef == null).IsTrue();
        await Assert.That(plain.colorRef == null).IsTrue();
        await Assert.That(plain.enabled).IsTrue();
        await Assert.That(plain.activeHours).IsEqualTo(FileRole.AllHours);
        await Assert.That(plain.holderMode).IsEqualTo(RoleHolderMode.Auto);
        await Assert.That(plain.minHolders).IsEqualTo(0);
        await Assert.That(plain.maxHolders).IsEqualTo(RoleHolderRange.Uncapped);
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
        await Assert.That(parsed.groups)
            .IsEquivalentTo(new[] { "Zulu & \"Friends\"", "Älpha" });
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
        // second "Shooter" is a duplicate id — both drop WITH their bands, and
        // the duplicate keeps its FIRST band.
        var resolved = RoleFile.ResolvePathEntries(path,
            name => name == "Shooter" ? 1 : name == "Sniper" ? 2 : (int?)null);
        await Assert.That(string.Join(",", resolved.ids)).IsEqualTo("1,2");
        await Assert.That(string.Join(",", resolved.mins)).IsEqualTo("0,6");
        await Assert.That(string.Join(",", resolved.maxes)).IsEqualTo("8,21");
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
            .IsEqualTo("7");

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
    public async Task DuplicateGroupNamesDedupCaseInsensitively()
    {
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"1\"><Palette/>" +
            "<Groups><Group name=\"Kitchen\"/><Group name=\"kitchen\"/><Group name=\"Farm\"/></Groups>" +
            "<Roles><Role name=\"A\"><Jobs><WorkType>Mining</WorkType></Jobs></Role></Roles></WorkRoles>");
        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.groups)
            .IsEquivalentTo(new[] { "Kitchen", "Farm" });
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
        await Assert.That(RoleFile.Parse("<WorkRoles version=\"1\"/>").error)
            .IsEqualTo("document contains no valid roles");
    }

    [Test]
    [Arguments("<WorkRoles version=\"7\"><Palette><Color name=\"accent\">#123456</Color></Palette></WorkRoles>")]
    [Arguments("<WorkRoles version=\"7\"><TrainingPaths><Path name=\"Apprentices\"><Role roleId=\"role-novice\" min=\"0\" max=\"8\">Novice</Role></Path></TrainingPaths></WorkRoles>")]
    [Arguments("<WorkRoles version=\"7\"><RecommendationOrder><Role roleId=\"role-cook\">Cook</Role></RecommendationOrder></WorkRoles>")]
    [Arguments("<WorkRoles version=\"7\"><Groups><Group fileId=\"group-kitchen\" name=\"Kitchen\"/></Groups></WorkRoles>")]
    public async Task DocumentsWithoutValidRolesAreRejected(string xml)
    {
        RoleFileDocument parsed = RoleFile.Parse(xml);

        await Assert.That(parsed.error).IsEqualTo("document contains no valid roles");
    }

    [Test]
    public async Task PaletteWithOnlySkippedPrimaryContentIsStillEmpty()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"7\">" +
            "<Palette><Color name=\"accent\">#123456</Color></Palette>" +
            "<Groups><Group fileId=\"ignored\" name=\" \"/></Groups>" +
            "<Roles><Role><Jobs><WorkType>Mining</WorkType></Jobs></Role></Roles>" +
            "<TrainingPaths><Path><Role min=\"0\" max=\"21\">Missing</Role></Path></TrainingPaths>" +
            "<RecommendationOrder><Role roleId=\"missing\"> </Role></RecommendationOrder>" +
            "</WorkRoles>");

        await Assert.That(parsed.error).IsEqualTo("document contains no valid roles");
        await Assert.That(parsed.palette.Count).IsEqualTo(1);
        await Assert.That(parsed.groups.Count).IsEqualTo(0);
        await Assert.That(parsed.roles.Count).IsEqualTo(0);
        await Assert.That(parsed.trainingPaths.Count).IsEqualTo(0);
        await Assert.That(parsed.recommendationOrder.Count).IsEqualTo(0);
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

    [Test]
    public async Task StableIdsDisambiguateDuplicateLabelsAcrossReferences()
    {
        var first = new FileRole { fileId = "role-a", label = "Worker", groupId = "group-a", group = "Team" };
        var second = new FileRole { fileId = "role-b", label = "Worker", groupId = "group-b", group = "Team" };
        var doc = new RoleFileDocument
        {
            groups =
            {
                "Team", "Team",
            },
            groupsWithIds =
            {
                new FileGroup { fileId = "group-a", name = "Team" },
                new FileGroup { fileId = "group-b", name = "Team" },
            },
            roles = { first, second },
            trainingPaths =
            {
                new FileTrainingPath
                {
                    name = "Workers",
                    anchorRole = "Worker",
                    anchorRoleId = "role-b",
                    anchorWithId = new FileRoleReference("role-b", "Worker"),
                    entries =
                    {
                        ("Worker", 0, 8), ("Worker", 8, 21),
                    },
                    entriesWithIds =
                    {
                        new FileTrainingPathEntry("role-a", "Worker", 0, 8),
                        new FileTrainingPathEntry("role-b", "Worker", 8, 21),
                    },
                },
            },
            recommendationOrder =
            {
                "Worker", "Worker",
            },
            recommendationOrderWithIds =
            {
                new FileRoleReference("role-b", "Worker"),
                new FileRoleReference("role-a", "Worker"),
            },
        };

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        FileTrainingPath path = parsed.trainingPaths[0];
        var resolved = RoleFile.ResolvePathEntries(path, parsed,
            role => role.fileId == "role-a" ? 101 : role.fileId == "role-b" ? 102 : null);
        int[] resolvedOrder = parsed.recommendationOrderWithIds.Select(reference =>
        {
            FileRole role = RoleFile.ResolveRole(parsed, reference.fileId, reference.label);
            return role.fileId == "role-a" ? 101 : 102;
        }).ToArray();

        await Assert.That(RoleFile.FormatVersion).IsEqualTo("7");
        await Assert.That(parsed.groupsWithIds.Select(group => group.fileId))
            .IsEquivalentTo(new[] { "group-a", "group-b" });
        await Assert.That(RoleFile.ResolveGroup(parsed, second.groupId, second.group)?.fileId)
            .IsEqualTo("group-b");
        await Assert.That(RoleFile.ResolveRole(parsed, path.anchorRoleId, path.anchorRole)?.fileId)
            .IsEqualTo("role-b");
        await Assert.That(string.Join(",", resolved.ids)).IsEqualTo("101,102");
        await Assert.That(string.Join(",", resolvedOrder)).IsEqualTo("102,101");
        await Assert.That(parsed.recommendationOrderWithIds[0].fileId).IsEqualTo("role-b");
        await Assert.That(parsed.recommendationOrderWithIds[1].fileId).IsEqualTo("role-a");
    }

    [Test]
    public async Task MalformedAndMissingIdsFallBackToLabels()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Palette/><Groups>" +
            "<Group fileId=\"dup-group\" name=\"First\"/><Group fileId=\"dup-group\" name=\"Second\"/>" +
            "</Groups><Roles>" +
            "<Role fileId=\"dup-role\" name=\"Alpha\"><Jobs/></Role>" +
            "<Role fileId=\"dup-role\" name=\"Beta\" groupId=\"missing-group\" group=\"Second\"><Jobs/></Role>" +
            "</Roles><TrainingPaths><Path name=\"P\">" +
            "<Anchor roleId=\"missing-role\">Beta</Anchor>" +
            "<Role roleId=\"missing-role\" min=\"0\" max=\"8\">Beta</Role>" +
            "<Role min=\"8\" max=\"21\">Alpha</Role>" +
            "</Path></TrainingPaths></WorkRoles>");

        FileRole beta = RoleFile.ResolveRole(parsed, "dup-role", "Beta");
        FileGroup second = RoleFile.ResolveGroup(parsed, beta.groupId, beta.group);
        var resolved = RoleFile.ResolvePathEntries(parsed.trainingPaths[0], parsed,
            role => role.label == "Alpha" ? 1 : role.label == "Beta" ? 2 : null);

        await Assert.That(beta.label).IsEqualTo("Beta");
        await Assert.That(second.name).IsEqualTo("Second");
        await Assert.That(RoleFile.ResolveRole(parsed, "missing-role", "Beta")?.label)
            .IsEqualTo("Beta");
        await Assert.That(string.Join(",", resolved.ids)).IsEqualTo("2,1");
    }

    [Test]
    public async Task UniqueIdsRemainAuthoritativeWhenDisplayLabelsAreStale()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Groups>" +
            "<Group fileId=\"group-a\" name=\"First\"/><Group fileId=\"group-b\" name=\"Second\"/>" +
            "</Groups><Roles>" +
            "<Role fileId=\"role-a\" name=\"Alpha\"><Jobs/></Role>" +
            "<Role fileId=\"role-b\" name=\"Beta\"><Jobs/></Role>" +
            "</Roles></WorkRoles>");

        await Assert.That(RoleFile.ResolveRole(parsed, "role-a", "Beta")?.fileId)
            .IsEqualTo("role-a");
        await Assert.That(RoleFile.ResolveGroup(parsed, "group-a", "Second")?.fileId)
            .IsEqualTo("group-a");
    }

    [Test]
    public async Task VersionSixLabelOnlyReferencesRemainCompatible()
    {
        RoleFileDocument parsed = RoleFile.Parse(
            "<WorkRoles version=\"6\"><Palette/><Groups><Group name=\"Crew\"/></Groups>" +
            "<Roles><Role name=\"Cook\" group=\"Crew\"><Jobs><WorkType>Cooking</WorkType></Jobs></Role></Roles>" +
            "<TrainingPaths><Path name=\"P\"><Anchor>Cook</Anchor><Role min=\"0\" max=\"21\">Cook</Role></Path></TrainingPaths>" +
            "<RecommendationOrder><Role>Cook</Role></RecommendationOrder></WorkRoles>");

        var runtimeIds = new Dictionary<FileRole, int> { [parsed.roles[0]] = 73 };
        var resolved = RoleFile.ResolvePathEntries(parsed.trainingPaths[0], parsed,
            role => runtimeIds.TryGetValue(role, out int id) ? id : null);
        var orderRole = RoleFile.ResolveRole(parsed,
            parsed.recommendationOrderWithIds[0].fileId, parsed.recommendationOrder[0]);

        await Assert.That(parsed.roles[0].fileId == null).IsTrue();
        await Assert.That(parsed.groupsWithIds[0].fileId == null).IsTrue();
        await Assert.That(RoleFile.ResolveRole(parsed, null, "Cook")?.label).IsEqualTo("Cook");
        await Assert.That(RoleFile.ResolveGroup(parsed, null, "Crew")?.name).IsEqualTo("Crew");
        await Assert.That(parsed.trainingPaths[0].entriesWithIds[0].role.fileId == null).IsTrue();
        await Assert.That(parsed.recommendationOrder[0]).IsEqualTo("Cook");
        await Assert.That(resolved.ids).IsEquivalentTo(new[] { 73 });
        await Assert.That(orderRole).IsSameReferenceAs(parsed.roles[0]);
    }

    [Test]
    public async Task PublicResolversTolerateNullCollectionsAndElements()
    {
        var role = new FileRole { fileId = "role-a", label = "Cook" };
        var group = new FileGroup { fileId = "group-a", name = "Crew" };
        var document = new RoleFileDocument
        {
            roles = new List<FileRole> { null, role },
            groups = new List<string> { "Crew" },
            groupsWithIds = new List<FileGroup> { group },
        };

        await Assert.That(RoleFile.ResolveRole(document, "role-a", "Cook"))
            .IsSameReferenceAs(role);
        await Assert.That(RoleFile.ResolveRole(document, "missing", "cook"))
            .IsSameReferenceAs(role);
        await Assert.That(RoleFile.ResolveGroup(document, "group-a", "Crew"))
            .IsSameReferenceAs(group);
        await Assert.That(RoleFile.ResolveGroup(document, "missing", "crew"))
            .IsSameReferenceAs(group);

        document.roles = null;
        document.groups = null;
        await Assert.That(RoleFile.ResolveRole(document, "role-a", "Cook") == null).IsTrue();
        await Assert.That(RoleFile.ResolveGroup(document, "group-a", "Crew") == null).IsTrue();
    }

    [Test]
    public async Task CatalogNamesRejectCaseInsensitiveCollisionsButAllowSelfRename()
    {
        var first = new NamedItem("Kitchen");
        var second = new NamedItem("Farm");
        var items = new[] { first, second };

        await Assert.That(CatalogNameRules.IsAvailable("  KITCHEN  ", items, item => item.Name))
            .IsFalse();
        await Assert.That(CatalogNameRules.IsAvailable("kitchen", items, item => item.Name, first))
            .IsTrue();
        await Assert.That(CatalogNameRules.IsAvailable("FARM", items, item => item.Name, first))
            .IsFalse();
        await Assert.That(CatalogNameRules.IsAvailable("   ", items, item => item.Name, first))
            .IsFalse();
    }

    [Test]
    public async Task EngineOwnedNamesGetDeterministicCaseInsensitiveSuffixes()
    {
        var items = new[]
        {
            new NamedItem("Worker"),
            new NamedItem("worker (2)"),
            new NamedItem("WORKER (3)"),
        };

        await Assert.That(CatalogNameRules.Unique("  Worker  ", items, item => item.Name))
            .IsEqualTo("Worker (4)");
        await Assert.That(CatalogNameRules.Unique("New Role", items, item => item.Name))
            .IsEqualTo("New Role");
    }

    private sealed class NamedItem
    {
        internal NamedItem(string name) => Name = name;
        internal string Name { get; }
    }

}
