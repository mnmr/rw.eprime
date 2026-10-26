namespace RimShared.Common.Tests;

public class RoleAbbreviationsTests
{
    [Test]
    public async Task LocalizedAbbreviationsKeepWholeLettersAndSelectedLanguageCasing()
    {
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var map = RoleAbbreviations.Build(new (int, string)[]
        {
            (0, "inşaat işçisi"), (1, "ışık ustası"),
            (2, "e\u0301béniste"), (3, "\U00020000工"), (4, "か\u3099工")
        }, turkish);
        await Assert.That(map[0]).IsEqualTo("İİ");
        await Assert.That(map[1]).IsEqualTo("IU");
        await Assert.That(map[2]).IsEqualTo("E\u0301b");
        await Assert.That(map[3]).IsEqualTo("\U00020000工");
        await Assert.That(map[4]).IsEqualTo("か\u3099工");
    }

    private static Dictionary<int, string> Build(params string[] labels) => RoleAbbreviations.Build(labels.Select((label, i) => (i, label)).ToList());

    [Test]
    public async Task MultiWordRoleUsesInitials()
    {
        var abbreviations = Build("Plant Cutter");

        await Assert.That(abbreviations[0]).IsEqualTo("PC");
    }

    [Test]
    public async Task SingleWordRolesUseTheCharacterLadder()
    {
        var abbreviations = Build("Cook", "Crafter");

        await Assert.That(abbreviations[0]).IsEqualTo("Co");
        await Assert.That(abbreviations[1]).IsEqualTo("Cr");
    }

    [Test]
    public async Task LadderFallsThroughVowelLastLetterAndSingleToNumbers()
    {
        // All start "Co": Cook takes "Co"; Cooler falls to vowel "Co"->taken,
        // then first+last "Cr"; Coder: "Co", vowel "Co", last "Cr" all taken
        // -> bare "C"; Copper: everything including "C" taken -> numbered.
        var map = Build("Cook", "Cooler", "Coder", "Copper");
        await Assert.That(map[0]).IsEqualTo("Co");
        await Assert.That(map[1]).IsEqualTo("Cr"); // first+last of "Cooler"
        await Assert.That(map[2]).IsEqualTo("C"); // single letter before numbers
        await Assert.That(map[3]).IsEqualTo("C1"); // everything collided
    }

    [Test]
    public async Task MultiWordInitialsAreReservedBeforeSingleWordAbbreviations()
    {
        // "Haul urgently" owns HU even listed second; Hunter may not squat on
        // "Hu" (uniqueness stays case-insensitive) and falls to first+last.
        var abbreviations = Build("Hunter", "Haul urgently");

        await Assert.That(abbreviations[0]).IsEqualTo("Hr");
        await Assert.That(abbreviations[1]).IsEqualTo("HU");
    }

    [Test]
    public async Task SingleWordFallsToBareLetterWhenInitialsAndLastLetterAreReserved()
    {
        var abbreviations = Build("Hunter", "Haul urgently", "Harvest ripe");

        await Assert.That(abbreviations[0]).IsEqualTo("H");
        await Assert.That(abbreviations[2]).IsEqualTo("HR");
    }

    [Test]
    public async Task SpecialCharactersNeverAppearInAbbreviations()
    {
        var map = Build("Cook (Michelin)", "(Fancy) Chef", "!!!");
        await Assert.That(map[0]).IsEqualTo("CM"); // not "C("
        await Assert.That(map[1]).IsEqualTo("FC"); // parens stripped, word kept
        await Assert.That(map[2]).IsEqualTo("R1"); // nothing usable: R-numbered
    }

    [Test]
    public async Task AbbreviationsAreUniqueForTheShippedishCatalog()
    {
        var map = Build(
            "Basics",
            "Doctor",
            "Medic",
            "Cook",
            "Butcher",
            "Brewer",
            "Builder",
            "Handyman",
            "Farmer",
            "Grower",
            "Plant Cutter",
            "Hunter",
            "Handler",
            "Warden",
            "Childcare",
            "Miner",
            "Smith",
            "Tailor",
            "Crafter",
            "Fabricator",
            "Artist",
            "Researcher",
            "Hauler",
            "Cleaner",
            "Grunt",
            "Laborer",
            "Fisher",
            "Rescuer",
            "No Firefighting",
            "Odd Jobs"
        );
        await Assert.That(map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(map.Count);
    }

    [Test]
    public async Task EmptyAndWhitespaceLabelsStillGetAbbreviations()
    {
        var map = Build("", "   ", "Role");
        await Assert.That(map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(3);
    }
}
