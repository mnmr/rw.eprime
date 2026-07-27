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
}
