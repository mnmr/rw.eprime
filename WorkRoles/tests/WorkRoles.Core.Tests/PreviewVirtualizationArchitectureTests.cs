namespace WorkRoles.Core.Tests;

public class PreviewVirtualizationArchitectureTests
{
    [Test]
    public async Task ChangesPreviewUsesCachedVariableRowsAndVisibleLoops()
    {
        string source = Source("Dialog_ChangesPreview.cs");
        string draw = Method(source, "public override void DoWindowContents(");
        string visible = Method(source, "private void DrawVisibleEntries(");

        await Assert.That(source).Contains("VariableViewportLayout rowLayout");
        await Assert.That(source).Contains("private void EnsureRowLayout(");
        await Assert.That(draw).Contains("rowLayout.Calculate(");
        await Assert.That(draw).Contains("DrawVisibleEntries(visibleRows");
        await Assert.That(visible)
            .Contains("for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)");
        await Assert.That(source).DoesNotContain("private static float DrawEntries(");
        await Assert.That(source).DoesNotContain("private static float DrawLine(");
        await Assert.That(source).DoesNotContain("public string PawnLabel;");
        await Assert.That(visible).Contains("entry.pawn.LabelShortCap");
        await Assert.That(source).Contains("private readonly struct EntryLayout");
        await Assert.That(source).Contains("new ChipLayout[chipCount]");
        await Assert.That(source).DoesNotContain("new List<ChipLayout>");
        await Assert.That(source).DoesNotContain("chips.ToArray()");
    }

    [Test]
    public async Task ImportPreviewBuildsDescriptorsOnlyWhenLayoutInputsChange()
    {
        string source = Source("Dialog_ImportPreview.cs");
        string draw = Method(source, "public override void DoWindowContents(");
        string visible = Method(source, "private void DrawVisibleRows(");

        await Assert.That(source).Contains("VariableViewportLayout rowLayout");
        await Assert.That(source).Contains("private void EnsureRenderRows(");
        await Assert.That(source).Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(source).DoesNotContain("UiVersion.Current");
        await Assert.That(draw).Contains("rowLayout.Calculate(");
        await Assert.That(draw).Contains("DrawVisibleRows(visibleRows");
        await Assert.That(visible)
            .Contains("for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)");
        await Assert.That(source).DoesNotContain("private float MeasureContent(");
        await Assert.That(source).DoesNotContain("private void DrawSection(");
        await Assert.That(source).DoesNotContain("static readonly Dictionary<string, float>");
    }

    [Test]
    public async Task RestorePreviewUsesTheUniformHalfOpenRange()
    {
        string source = Source("Dialog_RestorePreview.cs");
        string draw = Method(source, "public override void DoWindowContents(");
        string visible = Method(source, "private void DrawVisibleRows(");

        await Assert.That(draw).Contains("UniformViewportRange.Calculate(");
        await Assert.That(draw).Contains("DrawVisibleRows(visibleRows");
        await Assert.That(visible)
            .Contains("for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)");
        await Assert.That(draw).DoesNotContain("foreach (var row in rows)");
    }

    [Test]
    public async Task AllPreviewScrollViewsRemainUnconditionalAndPaired()
    {
        foreach (string file in new[]
                 {
                     "Dialog_ChangesPreview.cs",
                     "Dialog_ImportPreview.cs",
                     "Dialog_RestorePreview.cs",
                 })
        {
            string draw = Method(Source(file), "public override void DoWindowContents(");
            int begin = draw.IndexOf("Widgets.BeginScrollView(", StringComparison.Ordinal);
            int visible = draw.IndexOf("DrawVisible", begin, StringComparison.Ordinal);
            int end = draw.IndexOf("Widgets.EndScrollView();", StringComparison.Ordinal);

            await Assert.That(begin).IsGreaterThanOrEqualTo(0).Because(file);
            await Assert.That(visible).IsGreaterThan(begin).Because(file);
            await Assert.That(end).IsGreaterThan(visible).Because(file);
        }
    }

    [Test]
    public async Task ExportPreviewRemainsASingleTextArea()
    {
        string source = Source("Dialog_ExportPreview.cs");

        await Assert.That(source).Contains("GUI.TextArea(viewRect, xml, style);");
        await Assert.That(source).DoesNotContain("VariableViewportLayout");
        await Assert.That(source).DoesNotContain("UniformViewportRange.Calculate(");
    }

    [Test]
    public async Task PreviewBaseRetainsItsProtectedStaticCompatibilitySurface()
    {
        string source = Source("Dialog_PreviewBase.cs");

        await Assert.That(source)
            .Contains("protected static float DrawPreviewTitle(Rect inRect, string title)");
        await Assert.That(source)
            .Contains("protected static bool DrawPreviewSelectAll(Rect inRect, float y, bool selected)");
    }

    [Test]
    public async Task ChangesPreviewRefreshesLanguageSnapshotsOnlyAfterRevisionChanges()
    {
        string source = Source("Dialog_ChangesPreview.cs");
        string constructor = Method(source, "public Dialog_ChangesPreview(string title,");
        string refresh = Method(source, "private void RefreshLanguageIfNeeded()");
        string draw = Method(source, "public override void DoWindowContents(");

        await Assert.That(constructor).Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(source).Contains("Func<string> titleFactory");
        await Assert.That(source).Contains("IdentitySelectionPreserver.Restore(");
        await Assert.That(refresh).Contains("if (observedLanguageRevision == current) return;");
        await Assert.That(refresh).Contains("Patch_ActiveTip_TipRect.ReleaseOwner(structuredTipOwner);");
        await Assert.That(refresh).Contains("entries.Clear();");
        await Assert.That(refresh).Contains("rowDescriptors = Array.Empty<EntryLayout>();");
        await Assert.That(refresh).Contains("rowLayout = null;");
        await Assert.That(refresh).Contains("rowLayoutWidth = -1f;");
        await Assert.That(refresh).Contains("rowLayoutStamp = -1;");
        await Assert.That(refresh).Contains("noChangesText = null;");
        await Assert.That(draw.IndexOf("RefreshLanguageIfNeeded();", StringComparison.Ordinal))
            .IsLessThan(draw.IndexOf("Patch_ActiveTip_TipRect.BeginGeneration(",
                StringComparison.Ordinal));
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

    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "WorkRoles", "UI", file));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
