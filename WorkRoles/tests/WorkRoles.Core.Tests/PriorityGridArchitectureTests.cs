namespace WorkRoles.Core.Tests;

public class PriorityGridArchitectureTests
{
    [Test]
    public async Task InclinedLabelsUseSizesCachedForTheCurrentRevisions()
    {
        string grid = Source("UI", "Dialog_PriorityGrid.cs");
        string ensure = Method(grid, "private void EnsureColumnCache()");
        string draw = Method(grid, "private void DrawVisibleHeaderLabels(");
        string wrText = Source("UI", "WrText.cs");
        string inclined = Method(wrText,
            "public static void InclinedLabel(");

        await Assert.That(grid).Contains("private Vector2[] columnLabelSizes;");
        await Assert.That(grid).Contains("private Vector2 phantomLabelSize;");
        await Assert.That(ensure).Contains("columnLabelSizes = new Vector2[workTypes.Count];");
        await Assert.That(ensure).Contains("phantomLabelSize = Text.CalcSize(\"\");");
        await Assert.That(ensure).Contains("columnLabelSizes[c] = Text.CalcSize(columnLabels[c]);");
        await Assert.That(draw)
            .Contains("WrText.InclinedLabel(headRect, columnLabels[c], columnLabelSizes[c], LabelAngle);");
        await Assert.That(draw).Contains("phantomLabelSize, LabelAngle);");
        await Assert.That(inclined).Contains("Vector2 labelSize");
        await Assert.That(inclined).DoesNotContain("Text.CalcSize(");
        await Assert.That(wrText)
            .Contains("public static void InclinedLabel(Rect columnRect, string label, float degrees)");
        await Assert.That(wrText).Contains("Vector2 labelSize = Text.CalcSize(label);");
        await Assert.That(wrText)
            .Contains("InclinedLabel(columnRect, label, labelSize, degrees);");
    }

    [Test]
    public async Task ColumnCacheRebuildsOnlyWhenLanguageOrDefinitionsChange()
    {
        string grid = Source("UI", "Dialog_PriorityGrid.cs");
        string constructor = Method(grid, "public Dialog_PriorityGrid(");
        string initialSize = Method(grid, "public override Vector2 InitialSize");
        string draw = Method(grid, "public override void DoWindowContents(");
        string ensure = Method(grid, "private void EnsureColumnCache()");
        string definitions = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "WorkRoles", "DefinitionReloadCoordinator.cs"));

        await Assert.That(grid).Contains(
            "private readonly RevisionPairGate columnCacheRevisions");
        await Assert.That(constructor).Contains("EnsureColumnCache();");
        await Assert.That(initialSize).Contains("EnsureColumnCache();");
        await Assert.That(draw).Contains("EnsureColumnCache();");
        await Assert.That(ensure).Contains(
            "int languageRevision = LanguageChangeCoordinator.Revision;");
        await Assert.That(ensure).Contains(
            "int definitionRevision = DefinitionReloadCoordinator.Revision;");
        await Assert.That(ensure).Contains(
            "if (!columnCacheRevisions.ShouldRefresh(languageRevision, definitionRevision)) return;");
        await Assert.That(ensure).Contains("workTypes.Clear();");
        await Assert.That(ensure).Contains("columnLabels = new string[workTypes.Count];");
        await Assert.That(ensure).Contains("columnLabelSizes = new Vector2[workTypes.Count];");
        await Assert.That(definitions).Contains("internal static int Revision =>");
    }

    [Test]
    public async Task GridUsesHalfOpenHorizontalAndVerticalViewportRanges()
    {
        string grid = Source("UI", "Dialog_PriorityGrid.cs");
        string draw = Method(grid, "public override void DoWindowContents(");
        string rows = Method(grid, "private void DrawVisibleRows(");

        await Assert.That(draw).Contains("var scrollViewport = new Rect(");
        await Assert.That(Occurrences(draw, "UniformViewportRange.Calculate(")).IsEqualTo(3);
        await Assert.That(draw).Contains("var visibleBodyColumns = UniformViewportRange.Calculate(");
        await Assert.That(draw).Contains("var visibleHeaderColumns = UniformViewportRange.Calculate(");
        await Assert.That(draw).Contains("contentStart: NameW");
        await Assert.That(draw).Contains("viewportStart: scrollViewport.x - headerRunOut");
        await Assert.That(draw).Contains("viewportExtent: scrollViewport.width + headerRunOut");
        await Assert.That(draw).Contains("contentStart: headerH");
        await Assert.That(rows)
            .Contains("for (int r = visibleRows.Start; r < visibleRows.EndExclusive; r++)");
        await Assert.That(rows)
            .Contains("for (int c = visibleBodyColumns.Start; c < visibleBodyColumns.EndExclusive; c++)");
        await Assert.That(grid).Contains("HeaderHorizontalRunOut(headerH)");
        await Assert.That(draw).DoesNotContain("firstRow");
        await Assert.That(draw).DoesNotContain("lastRow");
    }

    [Test]
    public async Task ScrollHandlingIsUnconditionalAndGridVisualsAreRepaintOnly()
    {
        string draw = Method(Source("UI", "Dialog_PriorityGrid.cs"),
            "public override void DoWindowContents(");

        int begin = draw.IndexOf("Widgets.BeginScrollView(", StringComparison.Ordinal);
        int repaint = draw.IndexOf("if (Event.current.type == EventType.Repaint)", begin,
            StringComparison.Ordinal);
        int chrome = draw.IndexOf("DrawVisibleColumnChrome(", StringComparison.Ordinal);
        int headerGuard = draw.IndexOf("if (headerVisible)", StringComparison.Ordinal);
        int headers = draw.IndexOf("DrawVisibleHeaderLabels(", StringComparison.Ordinal);
        int rows = draw.IndexOf("DrawVisibleRows(", StringComparison.Ordinal);
        int end = draw.IndexOf("Widgets.EndScrollView();", StringComparison.Ordinal);

        await Assert.That(begin).IsGreaterThanOrEqualTo(0);
        await Assert.That(repaint).IsGreaterThan(begin);
        await Assert.That(headerGuard).IsGreaterThan(repaint);
        await Assert.That(headers).IsGreaterThan(headerGuard);
        await Assert.That(chrome).IsGreaterThan(headers);
        await Assert.That(rows).IsGreaterThan(chrome);
        await Assert.That(end).IsGreaterThan(rows);

        await Assert.That(draw)
            .Contains("bool headerVisible = scrollViewport.y < headerH && scrollViewport.yMax > 0f;");

        string headerMethod = Method(Source("UI", "Dialog_PriorityGrid.cs"),
            "private void DrawVisibleHeaderLabels(");
        await Assert.That(headerMethod).Contains("WrText.InclinedLabel(");
        await Assert.That(headerMethod).DoesNotContain("TooltipHandler.TipRegion(");
        await Assert.That(headerMethod).DoesNotContain("WrText.LineVertical(");

        string chromeMethod = Method(Source("UI", "Dialog_PriorityGrid.cs"),
            "private void DrawVisibleColumnChrome(");
        await Assert.That(chromeMethod).Contains("TooltipHandler.TipRegion(");
        await Assert.That(chromeMethod).Contains("WrText.LineVertical(");
        await Assert.That(chromeMethod).DoesNotContain("WrText.InclinedLabel(");
    }

    [Test]
    public async Task VanillaNumericDisplayReusesDisplayedPriorityForItsColor()
    {
        string rows = Method(Source("UI", "Dialog_PriorityGrid.cs"),
            "private void DrawVisibleRows(");
        string compact = string.Join(" ", rows.Split(
            (char[])null, StringSplitOptions.RemoveEmptyEntries));

        await Assert.That(compact).Contains(
            "int colorKey = managed ? (showVanilla ? priority " +
            ": CompiledJobOrders.VanillaPriorityFor(pawn, wt)) " +
            ": Mathf.Clamp(priority, 0, 4);");
    }

    [Test]
    public async Task CompiledPrioritiesRetainOnlyGuardedDefIndexArrays()
    {
        string compiled = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "WorkRoles", "CompiledJobOrders.cs"));
        string sync = Method(compiled, "private static void SyncVanillaFallback(");
        string build = Method(compiled, "private static Entry Build(");

        await Assert.That(compiled).DoesNotContain(
            "Dictionary<WorkTypeDef, int> Priorities");
        await Assert.That(compiled).DoesNotContain(
            "Dictionary<WorkTypeDef, int> VanillaBuckets");
        await Assert.That(sync).Contains("entry.VanillaByIndex");
        await Assert.That(build).Contains("PriorityByIndex = new int[defCount],");
        await Assert.That(build).Contains("VanillaByIndex = new int[defCount],");
        await Assert.That(build).Contains("if ((uint)index < (uint)defCount)");
    }

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int index = 0; (index = source.IndexOf(
                 value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return "";
        int open = source.IndexOf('{', start);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        return "";
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
