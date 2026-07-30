using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class ReadoutPanelArchitectureTests
{
    [Test]
    public async Task RebuildIsGatedAndOnGuiOnlyBlitsTheCachedModel()
    {
        string source = Source("UI", "ReadoutPanel.cs");
        await Assert.That(source).Contains("private static bool NeedsRebuild(");
        string onGui = Method(source, "public static void OnGUI(");
        await Assert.That(onGui).Contains("var renderData = GameRenderData.Get(map, store);");
        await Assert.That(onGui).Contains("if (NeedsRebuild(store, map, width, renderData))");
        await Assert.That(onGui).Contains("Rebuild(store, map, width, renderData);");
        // The layout engine runs only inside Rebuild, never per-frame.
        await Assert.That(CountOf(source, "new LayoutInput")).IsEqualTo(1);
        await Assert.That(Method(source, "private static void Rebuild(")).Contains("new LayoutInput");
        await Assert.That(onGui).DoesNotContain("ReadoutLayoutEngine.Build(");
    }

    [Test]
    public async Task CellRendererLoopsCellsWithoutLinqOrAllocation()
    {
        string source = Source("UI", "CellRenderer.cs");
        await Assert.That(source).Contains("for (int i = 0; i < cells.Count; i++)");
        await Assert.That(source).DoesNotContain(".Select(");
        await Assert.That(source).DoesNotContain(".Where(");
        await Assert.That(source).DoesNotContain("new List<");
    }

    [Test]
    public async Task VanillaPrefixHasEscapeHatch()
    {
        string source = Source("Patches", "Patch_ResourceReadout.cs");
        await Assert.That(source)
            .Contains("if (EPrimeReadoutsMod.Settings.useVanillaReadout) return true;");
        await Assert.That(source).Contains("ReadoutPanel.OnGUI();");
        await Assert.That(source).Contains("return false;");
    }
}
