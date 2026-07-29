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
        await Assert.That(source).Contains("builtVersion");
    }

    [Test]
    public async Task PoolEditorViewHasVersionGate()
    {
        string source = Source("UI", "PoolEditorView.cs");
        // Either builtVersion or an equivalent stamp gate
        bool hasVersionGate = source.Contains("builtVersion") || source.Contains("builtPoolId");
        await Assert.That(hasVersionGate).IsTrue();
    }

    [Test]
    public async Task DialogBuildsPoolSnapshotExactlyOnce()
    {
        string source = Source("UI", "Dialog_ReadoutConfig.cs");
        await Assert.That(CountOf(source, "PoolSnapshot.Build(")).IsEqualTo(1);
    }
}
