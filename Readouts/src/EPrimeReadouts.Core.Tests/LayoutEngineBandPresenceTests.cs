namespace EPrimeReadouts.Core.Tests;

/// Band presence must be depth-invariant: a group that renders no visible
/// slots keeps its thin identification band whether it is collapsed (depth 0)
/// or expanded. Without this, hover-expanding an empty collapsed band removes
/// the band under the pointer, shifts every band below it, and the hover
/// state oscillates — rebuilding the layout every frame.
public class LayoutEngineBandPresenceTests
{
    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
        DepthOf = g => 1,
    };

    [Test]
    public async Task ExpandedGroupWithNoVisibleSlotsKeepsItsIdentificationBand()
    {
        // "~Cloth" at zero count yields no visible slots at depth 1; the band
        // must render collapsed-style instead of disappearing.
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "~Cloth")));
        await Assert.That(model.Bands.Count).IsEqualTo(1);
        await Assert.That(model.Bands[0].GroupId).IsEqualTo(1);
        await Assert.That(model.Bands[0].SlotCount).IsEqualTo(0);
        await Assert.That(model.SlotHits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BandPresenceAndPositionsAreDepthInvariant()
    {
        // Hover-expanding the empty first group (depth 0 -> 1) must not
        // change which bands exist or where the bands below them sit.
        var collapsed = ReadoutLayoutEngine.Build(
            CollapsedThenExpanded(hoverExpandFirst: false));
        var hovered = ReadoutLayoutEngine.Build(
            CollapsedThenExpanded(hoverExpandFirst: true));

        await Assert.That(hovered.Bands.Count).IsEqualTo(collapsed.Bands.Count);
        for (int i = 0; i < collapsed.Bands.Count; i++)
        {
            await Assert.That(hovered.Bands[i].GroupId)
                .IsEqualTo(collapsed.Bands[i].GroupId);
            await Assert.That(hovered.Bands[i].Rect.Y)
                .IsEqualTo(collapsed.Bands[i].Rect.Y);
            await Assert.That(hovered.Bands[i].Rect.H)
                .IsEqualTo(collapsed.Bands[i].Rect.H);
        }
    }

    private static LayoutInput CollapsedThenExpanded(bool hoverExpandFirst)
    {
        // Group 1 has nothing visible ("~Cloth" at zero); group 2 is normal.
        var input = Input(Group(1, "~Cloth"), Group(2, "Steel"));
        input.DepthOf = g => g.Id == 1 ? (hoverExpandFirst ? 1 : 0) : 1;
        return input;
    }
}
