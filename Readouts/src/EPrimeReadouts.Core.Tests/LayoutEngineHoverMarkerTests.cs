using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// When hover expands a group beyond its configured depth, the triangles for
/// the hover-added tiers render as HoverLit (yellow) instead of Lit (white),
/// so the configured depth stays visible while cycling markers.
public class LayoutEngineHoverMarkerTests
{
    private static ReadoutGroup ThreeTierGroup()
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(new List<string> { "Steel" });
        group.Tiers.Add(new List<string> { "WoodLog" });
        group.Tiers.Add(new List<string> { "Silver" });
        return group;
    }

    private static LayoutInput Input(int depth, int? configured)
    {
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { ThreeTierGroup() },
            Counts = StaticResources.Counts(
                ("Steel", 120), ("WoodLog", 75), ("Silver", 900)),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
            DepthOf = g => depth,
            ConfiguredDepthOf = configured.HasValue ? g => configured.Value : null,
        };
    }

    private static TriangleState[] Triangles(RenderModel model) =>
        model.Cells.Where(c => c.Kind == CellKind.Triangle)
            .Select(c => c.Triangle).ToArray();

    [Test]
    public async Task HoverAddedTiersRenderHoverLitBeyondConfiguredDepth()
    {
        // Configured 1, hover-expanded to all 3: tier 1 white, tiers 2-3 yellow.
        var model = ReadoutLayoutEngine.Build(Input(depth: 3, configured: 1));
        await Assert.That(Triangles(model)).IsEquivalentTo(new[]
        {
            TriangleState.Lit, TriangleState.HoverLit, TriangleState.HoverLit,
        });
    }

    [Test]
    public async Task NoHoverLitWhenDepthEqualsConfiguredDepth()
    {
        var model = ReadoutLayoutEngine.Build(Input(depth: 2, configured: 2));
        await Assert.That(Triangles(model)).IsEquivalentTo(new[]
        {
            TriangleState.Lit, TriangleState.Lit, TriangleState.Dim,
        });
    }

    [Test]
    public async Task NullConfiguredDepthKeepsAllVisibleTiersLit()
    {
        var model = ReadoutLayoutEngine.Build(Input(depth: 3, configured: null));
        await Assert.That(Triangles(model)).IsEquivalentTo(new[]
        {
            TriangleState.Lit, TriangleState.Lit, TriangleState.Lit,
        });
    }
}
