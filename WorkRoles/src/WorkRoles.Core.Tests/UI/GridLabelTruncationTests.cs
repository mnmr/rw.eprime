using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class GridLabelTruncationTests
{
    private static readonly string[] ShippedCatalog =
    {
        "Basics", "Doctor", "Medic", "Cook", "Butcher", "Brewer",
        "Builder", "Handyman", "Farmer", "Grower", "Plant Cutter",
        "Hunter", "Handler", "Warden", "Childcare", "Miner", "Smith",
        "Tailor", "Crafter", "Fabricator", "Artist", "Researcher",
        "Hauler", "Cleaner", "Grunt", "Laborer", "Fisher", "Rescuer",
        "No Firefighting", "Odd Jobs",
    };

    private static string[] Labels(params string[] labels) => labels;

    /// The text each label's column room is sized from, for a preference;
    /// a trailing "~" marks a label that fades out.
    private static List<string> Sizing(GridNamePreference preference,
        params string[] labels)
    {
        int[] required = GridLabelTruncation.RequiredPrefixLengths(labels);
        var sizing = new List<string>(labels.Length);
        for (int i = 0; i < labels.Length; i++)
        {
            int prefix = GridLabelTruncation.PrefixLengthFor(required[i],
                preference);
            sizing.Add(GridLabelTruncation.SizingPrefix(labels[i], prefix)
                + (GridLabelTruncation.IsCut(labels[i], prefix) ? "~" : ""));
        }
        return sizing;
    }

    [Test]
    public async Task ShippedCatalogSizesEightLettersAutomatically()
    {
        // Handler/Handyman need five letters, everything else fewer; the
        // automatic floor of eight then cuts only the long names.
        int[] required = GridLabelTruncation.RequiredPrefixLengths(ShippedCatalog);
        await Assert.That(required[Array.IndexOf(ShippedCatalog, "Handler")])
            .IsEqualTo(5);
        await Assert.That(required[Array.IndexOf(ShippedCatalog, "Handyman")])
            .IsEqualTo(5);
        await Assert.That(required[Array.IndexOf(ShippedCatalog, "Doctor")])
            .IsEqualTo(1);

        await Assert.That(Sizing(GridNamePreference.Automatic, ShippedCatalog))
            .IsEquivalentTo(new[]
            {
                "Basics", "Doctor", "Medic", "Cook", "Butcher", "Brewer",
                "Builder", "Handyman", "Farmer", "Grower", "Plant Cu~",
                "Hunter", "Handler", "Warden", "Childcare", "Miner", "Smith",
                "Tailor", "Crafter", "Fabricat~", "Artist", "Research~",
                "Hauler", "Cleaner", "Grunt", "Laborer", "Fisher", "Rescuer",
                "No Firef~", "Odd Jobs",
            });
    }

    [Test]
    public async Task AutomaticKeepsOnlyTheNearDuplicatePairLong()
    {
        // The owner's catalog: "Drug Maker (2)" must keep all of "Drug
        // Maker" to stay distinct, but that requirement is its own; the
        // other names still cut at the automatic eight-letter floor.
        await Assert.That(Sizing(GridNamePreference.Automatic,
                "Drug Maker", "Drug Maker (2)", "Smith", "Smith Mech",
                "Plant Cutter", "Researcher", "Cook"))
            .IsEquivalentTo(new[]
            {
                "Drug Maker", "Drug Maker~", "Smith", "Smith Me~", "Plant Cu~",
                "Research~", "Cook",
            });
    }

    [Test]
    public async Task NamedSizesAreExactAndStepByFourLetters()
    {
        var labels = Labels("Handler", "Handyman", "Doctor", "Cook");

        await Assert.That(Sizing(GridNamePreference.Short, labels))
            .IsEquivalentTo(new[] { "~", "~", "~", "~" });
        await Assert.That(Sizing(GridNamePreference.Medium, labels))
            .IsEquivalentTo(new[] { "Hand~", "Hand~", "Doct~", "Cook" });
        await Assert.That(Sizing(GridNamePreference.Long, labels))
            .IsEquivalentTo(new[] { "Handler", "Handyman", "Doctor", "Cook" });
        await Assert.That(Sizing(GridNamePreference.Full,
                "No Firefighting", "Plant Cutter"))
            .IsEquivalentTo(new[] { "No Firefighting", "Plant Cutter" });
    }

    [Test]
    public async Task ALabelThatPrefixesAnotherNeedsNoExtraCharacter()
    {
        // "Cook" shows whole while "Cookware" fades, so four characters
        // already tell them apart.
        await Assert.That(GridLabelTruncation.RequiredPrefixLengths(
            Labels("Cookware", "Cook"))).IsEquivalentTo(new[] { 4, 4 });
    }

    [Test]
    public async Task SiblingsSharingAPrefixNeedOneMoreCharacter()
    {
        await Assert.That(GridLabelTruncation.RequiredPrefixLengths(
            Labels("Cook", "Cooking", "Cookware", "Doctor")))
            .IsEquivalentTo(new[] { 4, 5, 5, 1 });
    }

    [Test]
    public async Task ALabelOneCharacterOverThePrefixShowsWhole()
    {
        // The fade region is about a character wide anyway.
        await Assert.That(GridLabelTruncation.IsCut("Childcare", 8)).IsFalse();
        await Assert.That(GridLabelTruncation.IsCut("Researcher", 8)).IsTrue();
        await Assert.That(GridLabelTruncation.SizingPrefix("Researcher", 8))
            .IsEqualTo("Research");
    }

    [Test]
    public async Task SizingPrefixDropsTrailingSpaces()
    {
        await Assert.That(GridLabelTruncation.SizingPrefix("Plant Cutter", 6))
            .IsEqualTo("Plant");
    }

    [Test]
    public async Task DuplicateAndSingleLabelsStayBounded()
    {
        await Assert.That(GridLabelTruncation.RequiredPrefixLengths(
            Labels("Cook", "Cook"))).IsEquivalentTo(new[] { 4, 4 });
        await Assert.That(GridLabelTruncation.RequiredPrefixLengths(
            Labels("Cook"))).IsEquivalentTo(new[] { 1 });
        await Assert.That(GridLabelTruncation.RequiredPrefixLengths(
            Labels())).IsEmpty();
        await Assert.That(GridLabelTruncation.PrefixLengthFor(1,
            GridNamePreference.Automatic)).IsEqualTo(8);
        await Assert.That(GridLabelTruncation.SizingPrefix("", 0))
            .IsEqualTo("");
    }
}
