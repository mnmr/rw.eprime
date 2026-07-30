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
        await Assert.That(iconScale).Contains("Object.Destroy(reader)");
        await Assert.That(textures).Contains("internal static void ResetOwned()");
        await Assert.That(textures).Contains("Object.Destroy(triangle)");
    }

    [Test]
    public async Task ActiveTipPrefixDoesNotPerformReflection()
    {
        string source = Source("Patches", "Patch_ActiveTip.cs");
        string prefix = Method(source, "public static bool Prefix(Rect bgRect, string label)");
        string rectPrefix = Method(source,
            "public static bool Prefix(TipSignal ___signal, ref Rect __result)");

        await Assert.That(prefix).DoesNotContain("AccessTools.Field");
        await Assert.That(prefix).DoesNotContain("TrimEnd(");
        await Assert.That(rectPrefix).DoesNotContain("TrimEnd(");
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
    public async Task TooltipCacheObservesPresentationRevision()
    {
        string source = Source("UI", "IconTips.cs");
        string revision = source.Substring(
            source.IndexOf("private readonly struct TipRevision", StringComparison.Ordinal),
            source.IndexOf("private struct BuildState", StringComparison.Ordinal)
                - source.IndexOf("private readonly struct TipRevision", StringComparison.Ordinal));

        await Assert.That(revision).Contains("uiVersion");
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
