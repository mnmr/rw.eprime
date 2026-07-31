using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class LayoutEngineSearchTests
{
    private static LayoutInput Input(string search)
    {
        var group = new ReadoutGroup { Id = 1, Name = "Meals" };
        group.Tiers.Add(new List<string> { "MealSimple", "Steel" });
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = StaticResources.Counts(
                ("MealSimple", 8), ("MealFine", 3), ("Steel", 120), ("RawRice", 0)),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
            SearchText = search,
        };
    }

    private static string Icons(RenderModel model) =>
        string.Join(",", model.Cells.Where(c => c.Kind == CellKind.Icon).Select(c => c.DefName));

    [Test]
    public async Task ResultsSectionListsAllMatchingCountedResources()
    {
        var model = ReadoutLayoutEngine.Build(Input("meal"));
        var labels = model.Cells.Where(c => c.Kind == CellKind.Label).ToList();
        await Assert.That(labels[0].Text).IsEqualTo(ReadoutLayoutEngine.ResultsLabelKey);
        // Results grid holds MealFine + MealSimple (sorted by label: "fine meal"
        // before "simple meal"); the group below still shows MealSimple + Steel.
        await Assert.That(Icons(model)).IsEqualTo("MealFine,MealSimple,MealSimple,Steel");
    }

    [Test]
    public async Task ZeroCountResourcesAppearInResults()
    {
        var model = ReadoutLayoutEngine.Build(Input("rice"));
        await Assert.That(Icons(model)).Contains("RawRice");
    }

    [Test]
    public async Task GroupsStayVisibleWithMatchesHighlighted()
    {
        var model = ReadoutLayoutEngine.Build(Input("meal"));
        var highlights = model.Cells.Where(c => c.Kind == CellKind.Highlight).ToList();
        await Assert.That(highlights.Count).IsEqualTo(1);
        await Assert.That(highlights[0].DefName).IsEqualTo("MealSimple");
    }

    [Test]
    public async Task NoMatchesShowsNoMatchesLabel()
    {
        var model = ReadoutLayoutEngine.Build(Input("xyzzy"));
        var labels = model.Cells.Where(c => c.Kind == CellKind.Label).ToList();
        await Assert.That(labels[0].Text).IsEqualTo(ReadoutLayoutEngine.NoMatchesLabelKey);
    }

    [Test]
    public async Task NoSearchMeansNoLabelCells()
    {
        var model = ReadoutLayoutEngine.Build(Input(""));
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Label)).IsFalse();
    }

    [Test]
    public async Task SingleCharacterSearchShowsNoResultsOrHighlights()
    {
        var model = ReadoutLayoutEngine.Build(Input("m"));
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Label)).IsFalse();
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Highlight)).IsFalse();
    }

    /// Groupless input whose counted defs all match the query; labels default
    /// to the defName so "match" hits every entry.
    private static LayoutInput CapInput(int matchCount, float width)
    {
        var defs = new string[matchCount];
        for (int i = 0; i < matchCount; i++) defs[i] = $"Match{i:D2}";
        var counts = new Dictionary<string, int>();
        foreach (var d in defs) counts[d] = 1;
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup>(),
            Counts = counts,
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.CatalogWith(defs),
            Width = width,
            SearchText = "match",
        };
    }

    [Test]
    public async Task ResultsAreCappedToThreeRowsOfSixItems()
    {
        // Width 400 fits far more than 6 columns; the cap must win.
        var model = ReadoutLayoutEngine.Build(CapInput(25, 400f));
        var icons = model.Cells.Where(c => c.Kind == CellKind.Icon).ToList();
        await Assert.That(icons.Count).IsEqualTo(18);
        await Assert.That(icons.Select(c => c.Rect.X).Distinct().Count()).IsEqualTo(6);
        await Assert.That(icons.Select(c => c.Rect.Y).Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task NarrowPanelStillCapsResultsAtThreeRows()
    {
        // Width 140 fits 3 columns, so at most 9 items may appear.
        var model = ReadoutLayoutEngine.Build(CapInput(25, 140f));
        var icons = model.Cells.Where(c => c.Kind == CellKind.Icon).ToList();
        await Assert.That(icons.Count).IsEqualTo(9);
        await Assert.That(icons.Select(c => c.Rect.Y).Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task MatchesBelowTheCapAreAllShown()
    {
        var model = ReadoutLayoutEngine.Build(CapInput(5, 400f));
        var icons = model.Cells.Where(c => c.Kind == CellKind.Icon).ToList();
        await Assert.That(icons.Count).IsEqualTo(5);
    }

    [Test]
    public async Task TruncatedResultsShowMoreIndicatorInsideThePanel()
    {
        // 25 matches, 18 shown -> 7 hidden.
        var model = ReadoutLayoutEngine.Build(CapInput(25, 400f));
        var more = model.Cells.Where(c => c.Kind == CellKind.Label
            && c.Text == ReadoutLayoutEngine.MoreResultsLabelKey).ToList();
        await Assert.That(more.Count).IsEqualTo(1);
        await Assert.That(more[0].Count).IsEqualTo(7);

        // Below the last grid row but inside the results container.
        float lastIconY = model.Cells.Where(c => c.Kind == CellKind.Icon)
            .Max(c => c.Rect.Y);
        var back = model.Cells.First(c => c.Kind == CellKind.GroupBack);
        await Assert.That(more[0].Rect.Y > lastIconY).IsTrue();
        await Assert.That(more[0].Rect.Y + more[0].Rect.H <= back.Rect.Y + back.Rect.H)
            .IsTrue();
    }

    [Test]
    public async Task UntruncatedResultsShowNoMoreIndicator()
    {
        var model = ReadoutLayoutEngine.Build(CapInput(18, 400f));
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Label
            && c.Text == ReadoutLayoutEngine.MoreResultsLabelKey)).IsFalse();
    }
}
