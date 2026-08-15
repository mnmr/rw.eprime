using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class RuntimeCacheArchitectureTests
{
    [Test]
    public async Task CountSnapshotDoesNotDependOnReadoutConfiguration()
    {
        string source = Source("GameCounts.cs");
        string build = Method(source,
            "internal static RenderCountSnapshot BuildSnapshot(");

        await Assert.That(build).DoesNotContain("ReadoutStore");
        await Assert.That(build).DoesNotContain("PoolSnapshot");
        await Assert.That(build).DoesNotContain("store.Model");
        await Assert.That(source).DoesNotContain("CollectExtraDefs");
    }

    // The planned-work scan's guards are all statements about RimWorld types
    // and vanilla contracts; no executable boundary in Core can express them,
    // and each one is easy to mistake for a redundant check and delete.
    [Test]
    public async Task PlannedWorkScanSkipsTheConstructibleThatLogsOnCostQueries()
    {
        // Blueprint_Install is IConstructible but its TotalMaterialCost is a
        // Log.Error stub. The reservation scan runs on the count refresh
        // cadence, so reaching it would log forever.
        string scan = Method(Source("PlannedWorkCounts.cs"),
            "private static void AccumulateConstructibles(");

        await Assert.That(scan).Contains(
            "if (!(thing is Blueprint_Build) && !(thing is Frame)) continue;");
    }

    [Test]
    public async Task PlannedWorkScanIgnoresForbiddenConstruction()
    {
        // A forbidden blueprint or frame will not be worked on, so it must not
        // shrink a counter.
        string scan = Method(Source("PlannedWorkCounts.cs"),
            "private static void AccumulateConstructibles(");

        await Assert.That(scan).Contains("if (thing.IsForbidden(player)) continue;");
    }

    [Test]
    public async Task TargetCountBillsAreGatedOnVanillasOwnProductCounterCheck()
    {
        // CanCountProducts is vanilla's guard that products[0] is the single
        // meaningful product. Without it a multi- or special-product recipe
        // returns a count for the wrong thing and the shortfall is nonsense.
        string iterations = Method(Source("PlannedWorkCounts.cs"),
            "private static int IterationsOf(");

        await Assert.That(iterations).Contains("CanCountProducts(bill)");
    }

    [Test]
    public async Task MapAndWorldCachesHaveExplicitLifecycleRelease()
    {
        string renderData = Source("GameRenderData.cs");
        string lifecycle = Source("RuntimeTeardown.cs");

        await Assert.That(renderData).Contains("internal static void Remove(Map map)");
        await Assert.That(renderData).Contains("internal static void Reset()");
        await Assert.That(lifecycle).Contains("class ReadoutRenderMapComponent : MapComponent");
        await Assert.That(lifecycle).Contains("GameRenderData.Remove(map)");
        await Assert.That(lifecycle).Contains("MemoryUtility.ClearAllMapsAndWorld");
        await Assert.That(lifecycle).Contains("RuntimeTeardown.ResetAll()");
    }

    [Test]
    public async Task OwnedUnityResourcesAreReleasedSafely()
    {
        string iconScale = Source("UI", "IconScaleCache.cs");
        string textures = Source("UI", "ReadoutTextures.cs");
        string lookup = Method(iconScale, "public static float ScaleFor(ThingDef def)");

        await Assert.That(lookup).DoesNotContain("Measure(");
        await Assert.That(iconScale).Contains("internal static void ProcessPending(");
        await Assert.That(iconScale).Contains("finally");
        await Assert.That(iconScale).Contains("RenderTexture.ReleaseTemporary(rt)");
        await Assert.That(iconScale).Contains("Object.Destroy(owned)");
        await Assert.That(textures).Contains("internal static void ResetOwned()");
        await Assert.That(textures).Contains("Object.Destroy(owned)");
    }

    [Test]
    public async Task MapLoadNeverCreatesOrDestroysUnityResourcesOnTheWorkerThread()
    {
        string lifecycle = Source("RuntimeTeardown.cs");
        string textures = Source("UI", "ReadoutTextures.cs");
        string iconScale = Source("UI", "IconScaleCache.cs");
        string constructor = Method(lifecycle,
            "public ReadoutRenderMapComponent(Map map)");
        string update = Method(lifecycle,
            "public override void MapComponentUpdate()");
        string textureReset = Method(textures,
            "internal static void ResetOwned()");
        string iconReset = Method(iconScale,
            "internal static void Reset()");

        await Assert.That(constructor).DoesNotContain("ReadoutTextures");
        await Assert.That(constructor).DoesNotContain("Texture2D");
        await Assert.That(update).Contains("ReadoutTextures.EnsureOwned()");
        await Assert.That(textureReset).Contains("LongEventHandler.ExecuteWhenFinished");
        await Assert.That(iconReset).Contains("LongEventHandler.ExecuteWhenFinished");
    }

    [Test]
    public async Task SteadyDrawMethodsConsumeResolvedRowsOnly()
    {
        string cells = Method(Source("UI", "CellRenderer.cs"),
            "public static void Draw(DrawModel draw)");
        string groups = Method(Source("UI", "GroupListView.cs"),
            "public void Draw(Rect rect, Dialog_ReadoutConfig owner)");
        string poolRow = Method(Source("UI", "PoolEditorView.cs"),
            "private void DrawEditorRow(");
        string resourceRow = Method(Source("UI", "ResourceTreeView.cs"),
            "private void DrawRow(");

        await Assert.That(cells).DoesNotContain(".Translate(");
        await Assert.That(groups).DoesNotContain("InDisplayOrder(");
        await Assert.That(poolRow).DoesNotContain("DefDatabase");
        await Assert.That(resourceRow).DoesNotContain("DefDatabase");
        await Assert.That(resourceRow).DoesNotContain("TierOps.Contains");
    }

    [Test]
    public async Task DragTargetsDoNotAllocateCapturingCallbacksPerPass()
    {
        string drag = Source("UI", "EprDrag.cs");
        string groups = Source("UI", "GroupListView.cs");
        string editor = Source("UI", "EditorView.cs");

        await Assert.That(drag).DoesNotContain("Action HoverDropAction");
        await Assert.That(groups).DoesNotContain("HoverDropAction = ()");
        await Assert.That(editor).DoesNotContain("HoverDropAction = ()");
    }

    [Test]
    public async Task PanelHotRectsHaveADependencyGate()
    {
        string source = Source("UI", "ReadoutPanel.cs");
        string onGui = Method(source, "public static void OnGUI()");

        await Assert.That(source).Contains("private static void EnsureHotRects(");
        await Assert.That(onGui).DoesNotContain("hotRects.Clear()");
    }

    [Test]
    public async Task GlobalGuiStateIsProtectedByScope()
    {
        string scope = Source("UI", "GuiStateScope.cs");
        string cells = Method(Source("UI", "CellRenderer.cs"),
            "public static void Draw(DrawModel draw)");
        string tips = Method(Source("UI", "WrTipUI.cs"),
            "public static void Draw(Rect bgRect, TipModel model)");

        await Assert.That(scope).Contains("internal readonly struct GuiStateScope");
        await Assert.That(scope).Contains("public void Dispose()");
        await Assert.That(cells).Contains("new GuiStateScope()");
        await Assert.That(tips).Contains("new GuiStateScope()");
    }

    [Test]
    public async Task ActiveTooltipSessionDrawsItsPreparedGeometry()
    {
        string tipUi = Source("UI", "WrTipUI.cs");
        string presenter = Source("UI", "StructuredTipPresenter.cs");
        string present = Method(presenter, "private static void Present(");
        string drawWindow = Method(presenter, "private static void DrawWindow()");
        string drawPrepared = Method(tipUi,
            "internal static void Draw(Rect bgRect, PreparedTip prepared)");

        await Assert.That(present).Contains("frozenGeometry = WrTipUI.Prepare(");
        await Assert.That(drawWindow).Contains("WrTipUI.Draw(rect, frozenGeometry)");
        await Assert.That(drawWindow).DoesNotContain("frozen.Model");
        await Assert.That(drawPrepared).DoesNotContain("Ensure(");
        await Assert.That(drawPrepared).DoesNotContain("UiVersion");
    }

    [Test]
    public async Task WindowDrawsReuseTheirListingHelpers()
    {
        string settings = Method(Source("EPrimeReadoutsMod.cs"),
            "public override void DoSettingsWindowContents(Rect inRect)");
        string options = Method(Source("UI", "Dialog_PanelOptions.cs"),
            "public override void DoWindowContents(Rect inRect)");

        await Assert.That(settings).DoesNotContain("new Listing_Standard");
        await Assert.That(options).DoesNotContain("new Listing_Standard");
    }

    [Test]
    public async Task LanguageOnlyCachesUseTheLanguageRevision()
    {
        string catalog = Method(Source("GameResourceCatalog.cs"),
            "public string CategoryLabelOf(string categoryDefName)");
        string tree = Method(Source("GameResourceTree.cs"),
            "public static List<ResourceTreeNode> GetRoots()");
        string text = Method(Source("UI", "UiText.cs"),
            "internal static string Get(string key)");
        string preview = Method(Source("UI", "ReadoutsPreviewUI.cs"),
            "private void EnsureLines(ReadoutSnapshot snapshot)");
        string tip = Method(Source("UI", "IconTips.cs"),
            "StructuredTip IStructuredTipSource.Resolve()");

        await Assert.That(catalog).Contains("UiVersion.LanguageCurrent");
        await Assert.That(tree).Contains("UiVersion.LanguageCurrent");
        await Assert.That(text).Contains("UiVersion.LanguageCurrent");
        await Assert.That(preview).Contains("UiVersion.LanguageCurrent");
        await Assert.That(tip).Contains("UiVersion.LanguageCurrent");
    }

    [Test]
    public async Task DialogDrawsUseCachedPresentationAndFileMetadata()
    {
        string import = Source("UI", "Dialog_ImportReadouts.cs");
        string preview = Method(import, "private void DrawPreview(Rect inRect)");
        string source = Method(import, "private void DrawSource(Rect inRect)");
        string picker = Method(Source("UI", "Dialog_EprFilePicker.cs"),
            "protected string CachedResolvedPath(");
        string exportDraw = Method(Source("UI", "Dialog_ExportReadouts.cs"),
            "public override void DoWindowContents(Rect inRect)");

        await Assert.That(preview).DoesNotContain(".Translate(");
        await Assert.That(source).DoesNotContain("modified.ToString(");
        await Assert.That(source).DoesNotContain("EnsureFiles()");
        await Assert.That(picker).DoesNotContain("|| interact");
        await Assert.That(picker).DoesNotContain("File.Exists");
        await Assert.That(exportDraw).DoesNotContain("RebuildSnapshot()");
    }

    [Test]
    public async Task TooltipCacheObservesLanguageRevision()
    {
        string source = Source("UI", "IconTips.cs");
        string revision = source.Substring(
            source.IndexOf("private readonly struct TipRevision", StringComparison.Ordinal),
            source.IndexOf("private struct BuildState", StringComparison.Ordinal)
                - source.IndexOf("private readonly struct TipRevision", StringComparison.Ordinal));

        await Assert.That(revision).Contains("languageVersion");
    }

    [Test]
    public async Task IconTipHoverPathDefersGatheringToDisplay()
    {
        string source = Source("UI", "IconTips.cs");
        string hover = Method(source, "public static void Tip(");
        string resolve = Method(source,
            "StructuredTip IStructuredTipSource.Resolve()");
        string presenterSource = Source("UI", "StructuredTipPresenter.cs");
        string present = Method(presenterSource, "private static void Present(");

        // Hovering must cost a dictionary read plus field writes: no model
        // building or cache probing until the tooltip opens after its delay.
        await Assert.That(hover).DoesNotContain("Build(");
        await Assert.That(hover).DoesNotContain("cache.Get");
        await Assert.That(hover).Contains("StructuredTipPresenter.TipRegion");

        await Assert.That(resolve).Contains("cache.Get");
        await Assert.That(present).Contains(
            "state == TooltipDisplayState.Opened");
        await Assert.That(present).Contains("source.Resolve()");
    }

    [Test]
    public async Task CatalogDoesNotPublishItsMutableMemberLists()
    {
        string source = Source("GameResourceCatalog.cs");

        await Assert.That(source).DoesNotContain(
            "Dictionary<string, List<string>> categoryMembersCache");
        await Assert.That(source).Contains("result.AsReadOnly()");
    }
}
