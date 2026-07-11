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
    public async Task LadderFallsThroughVowelAndLastLetterToNumbers()
    {
        // All start "Co": Cook takes "Co"; Cooler falls to vowel "Co"->taken,
        // then first+last "Cr"; Coder: "Co" taken, vowel "Co" taken, last "Cr"
        // taken -> numbered.
        var map = Build("Cook", "Cooler", "Coder");
        await Assert.That(map[0]).IsEqualTo("Co");
        await Assert.That(map[1]).IsEqualTo("Cr");   // first+last of "Cooler"
        await Assert.That(map[2]).IsEqualTo("C1");   // everything collided
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
