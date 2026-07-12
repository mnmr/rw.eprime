using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleAbbreviationsTests
{
    private static Dictionary<int, string> Build(params string[] labels) =>
        RoleAbbreviations.Build(labels.Select((label, i) => (i, label)).ToList());

    [Test]
    public async Task MultiWordUsesInitials_SingleWordUsesLadder()
    {
        var map = Build("Plant Cutter", "Cook", "Crafter");
        await Assert.That(map[0]).IsEqualTo("PC");
        await Assert.That(map[1]).IsEqualTo("Co");   // first+second
        await Assert.That(map[2]).IsEqualTo("Cr");   // first+second, still free
    }

    [Test]
    public async Task LadderFallsThroughVowelLastLetterAndSingleToNumbers()
    {
        // All start "Co": Cook takes "Co"; Cooler falls to vowel "Co"->taken,
        // then first+last "Cr"; Coder: "Co", vowel "Co", last "Cr" all taken
        // -> bare "C"; Copper: everything including "C" taken -> numbered.
        var map = Build("Cook", "Cooler", "Coder", "Copper");
        await Assert.That(map[0]).IsEqualTo("Co");
        await Assert.That(map[1]).IsEqualTo("Cr");   // first+last of "Cooler"
        await Assert.That(map[2]).IsEqualTo("C");    // single letter before numbers
        await Assert.That(map[3]).IsEqualTo("C1");   // everything collided
    }

    [Test]
    public async Task MultiWordInitialsAreReserved_SingleWordsFallThrough()
    {
        // "Haul urgently" owns HU even listed second; Hunter may not squat on
        // "Hu" (uniqueness stays case-insensitive) and falls to first+last.
        var map = Build("Hunter", "Haul urgently");
        await Assert.That(map[0]).IsEqualTo("Hr");
        await Assert.That(map[1]).IsEqualTo("HU");

        // With Hr reserved too, Hunter drops all the way to a bare letter.
        var crowded = Build("Hunter", "Haul urgently", "Harvest ripe");
        await Assert.That(crowded[0]).IsEqualTo("H");
        await Assert.That(crowded[2]).IsEqualTo("HR");
    }

    [Test]
    public async Task SpecialCharactersNeverAppearInAbbreviations()
    {
        var map = Build("Cook (Michelin)", "(Fancy) Chef", "!!!");
        await Assert.That(map[0]).IsEqualTo("CM");  // not "C("
        await Assert.That(map[1]).IsEqualTo("FC");  // parens stripped, word kept
        await Assert.That(map[2]).IsEqualTo("R1");  // nothing usable: R-numbered
    }

    [Test]
    public async Task AbbreviationsAreUniqueForTheShippedishCatalog()
    {
        var map = Build("Basics", "Doctor", "Medic", "Cook", "Butcher", "Brewer",
            "Builder", "Handyman", "Farmer", "Grower", "Plant Cutter", "Hunter",
            "Handler", "Warden", "Childcare", "Miner", "Smith", "Tailor", "Crafter",
            "Fabricator", "Artist", "Researcher", "Hauler", "Cleaner", "Grunt",
            "Laborer", "Fisher", "Rescuer", "No Firefighting", "Odd Jobs");
        await Assert.That(map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .IsEqualTo(map.Count);
    }

    [Test]
    public async Task EmptyAndWhitespaceLabelsStillGetAbbreviations()
    {
        var map = Build("", "   ", "Role");
        await Assert.That(map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(3);
    }
}
