namespace WorkRoles.Core.Tests;

public class ScopeCacheArchitectureTests
{
    [Test]
    public async Task ColonistCacheConsumersUseTheObservedPawnListStamp()
    {
        string source = Source("UI", "ColonistsTabView.cs");

        await Assert.That(source)
            .Contains("private readonly PawnListRevisionTracker pawnListRevisions");
        await Assert.That(Method(source, "private ScopeCacheStamp PawnListStamp"))
            .Contains("pawnListRevisions.Stamp(");

        string[] consumers =
        {
            "private List<PawnFixPlan> GetPlan()",
            "private void EnsurePreview(",
            "private void EnsureSizes()",
            "internal string RoleTipText(",
            "private List<GroupSection<Pawn>> Sections(",
            "internal List<Pawn> ListedPawns()",
            "ChipLayoutFor(Pawn pawn",
            "private bool RulesPass(",
        };
        foreach (string consumer in consumers)
            await Assert.That(Method(source, consumer)).Contains("PawnListStamp")
                .Because($"{consumer} must observe the map before storing its cache stamp");

        await Assert.That(Method(source, "internal List<Pawn> ListedPawns()"))
            .DoesNotContain("pawnsCache != null && pawnsMapId != mapId");
    }

    [Test]
    public async Task RolesHolderCacheReceivesTheObservedRevisionAccessor()
    {
        string roles = Source("UI", "RolesTabView.cs");
        string window = Source("UI", "MainTabWindow_WorkRoles.cs");

        await Assert.That(Method(roles, "private void DrawAssignedPawnNames("))
            .Contains("pawnListRevision?.Invoke()");
        await Assert.That(window)
            .Contains("rolesTab.pawnListRevision = () => colonistsTab.PawnListRevision;");
    }

    [Test]
    public async Task DesiredSizeStoresThePostComputeStamp()
    {
        string source = Source("UI", "ColonistsTabView.cs");
        string sizes = Method(source, "private void EnsureSizes()");

        int sizeRebuild = sizes.IndexOf(
            "desiredHeightCache = ComputeDesiredHeight();", StringComparison.Ordinal);
        int sizeStamp = sizes.IndexOf("sizeStamp = PawnListStamp;", StringComparison.Ordinal);
        await Assert.That(sizeRebuild).IsGreaterThanOrEqualTo(0);
        await Assert.That(sizeStamp > sizeRebuild).IsTrue()
            .Because("size computation can revalidate the pawn scope");
    }

    [Test]
    public async Task RoleTipStoresThePostComputeStamp()
    {
        string source = Source("UI", "ColonistsTabView.cs");
        string roleTips = Method(source, "internal string RoleTipText(");

        int tipRebuild = roleTips.IndexOf("BuildRoleTip(", StringComparison.Ordinal);
        int tipStamp = roleTips.IndexOf("roleTipStamp = PawnListStamp;", StringComparison.Ordinal);
        await Assert.That(tipRebuild).IsGreaterThanOrEqualTo(0);
        await Assert.That(tipStamp > tipRebuild).IsTrue()
            .Because("best-fit tooltip construction can revalidate the pawn scope");
    }

    [Test]
    public async Task RoleTreeExpansionRoutesInvalidateCachedNodes()
    {
        string source = Source("UI", "RolesTabView.cs");
        string draw = Method(source, "private void DrawJobTree(");
        string labelClick = Between(draw,
            "if (Widgets.ButtonInvisible(typeLabelRect))", "if (Mouse.IsOver(row))");
        string arrowClick = Between(draw,
            "if (Widgets.ButtonImage(new Rect(row.x + 2f", "var checkboxRect");

        await Assert.That(labelClick).Contains("ToggleWorkTypeExpanded(type.defName);")
            .Because("the label path previously changed expansion without invalidating cached rows");
        await Assert.That(arrowClick).Contains("ToggleWorkTypeExpanded(type.defName);");
        foreach (string route in new[] { labelClick, arrowClick })
        {
            await Assert.That(route).DoesNotContain("expanded.");
            await Assert.That(route).DoesNotContain("treeRev");
        }

        string toggle = Method(source, "private void ToggleWorkTypeExpanded(");
        await Assert.That(toggle)
            .Contains("if (!expanded.Add(defName)) expanded.Remove(defName);");
        await Assert.That(Occurrences(toggle, "expanded.Add(")).IsEqualTo(1);
        await Assert.That(Occurrences(toggle, "expanded.Remove(")).IsEqualTo(1);
        await Assert.That(Occurrences(toggle, "treeRev++")).IsEqualTo(1);

        await Assert.That(Occurrences(draw, "ToggleWorkTypeExpanded(type.defName);")).IsEqualTo(2);
        await Assert.That(Occurrences(draw, "expanded.Add(")).IsEqualTo(1);
        await Assert.That(Occurrences(draw, "expanded.Remove(")).IsEqualTo(0);
        await Assert.That(Occurrences(draw, "treeRev++")).IsEqualTo(1);
        await Assert.That(Between(draw,
                "if (treeTarget?.giver != null && expanded.Add(treeTarget.Value.type.defName))",
                "var nodes = TreeNodes(filtering);"))
            .Contains("treeRev++;")
            .Because("selection expansion must invalidate only when Add changes state");

        int nodes = draw.IndexOf("var nodes = TreeNodes(filtering);", StringComparison.Ordinal);
        int rows = draw.IndexOf("for (int i = firstNode; i <= lastNode; i++)", StringComparison.Ordinal);
        await Assert.That(nodes >= 0 && nodes < rows).IsTrue();
        await Assert.That(draw.Substring(rows)).DoesNotContain("TreeNodes(");
    }

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int index = 0; (index = source.IndexOf(
                 value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) return "";
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end < 0 ? "" : source.Substring(start, end - start);
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
