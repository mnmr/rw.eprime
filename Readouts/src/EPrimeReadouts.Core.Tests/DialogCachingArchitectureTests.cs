using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class DialogCachingArchitectureTests
{
    [Test]
    public async Task EditorViewHasNeedsRebuildGate()
    {
        string source = Source("UI", "EditorView.cs");
        await Assert.That(source).Contains("private bool NeedsRebuild(");
    }

    [Test]
    public async Task EditorViewBuildsEngineOnlyInsideRebuild()
    {
        string source = Source("UI", "EditorView.cs");
        // ReadoutLayoutEngine.Build must appear exactly once — in Rebuild()
        await Assert.That(CountOf(source, "ReadoutLayoutEngine.Build(")).IsEqualTo(1);
        string rebuild = Method(source, "private void Rebuild(");
        await Assert.That(rebuild).Contains("ReadoutLayoutEngine.Build(");
    }

    [Test]
    public async Task PoolListViewHasVersionGate()
    {
        string source = Source("UI", "PoolListView.cs");
        await Assert.That(source).Contains("builtPoolsVersion");
    }

    [Test]
    public async Task PoolEditorViewHasVersionGate()
    {
        string source = Source("UI", "PoolEditorView.cs");
        // Either the pool-domain revision or an equivalent stamp gate
        bool hasVersionGate = source.Contains("builtPoolsVersion") || source.Contains("builtPoolId");
        await Assert.That(hasVersionGate).IsTrue();
    }

    [Test]
    public async Task DialogBuildsPoolSnapshotExactlyOnce()
    {
        string source = Source("UI", "Dialog_ReadoutConfig.cs");
        await Assert.That(CountOf(source, "PoolSnapshot.Build(")).IsEqualTo(1);
    }

    [Test]
    public async Task PreviewCacheIsOwnedByEachDialogInstance()
    {
        string preview = Source("UI", "ReadoutsPreviewUI.cs");
        string export = Source("UI", "Dialog_ExportReadouts.cs");
        string import = Source("UI", "Dialog_ImportReadouts.cs");

        await Assert.That(preview).Contains("internal sealed class ReadoutsPreviewView");
        await Assert.That(preview).DoesNotContain("private static string[]");
        await Assert.That(export).Contains("new ReadoutsPreviewView()");
        await Assert.That(import).Contains("new ReadoutsPreviewView()");
    }
}
