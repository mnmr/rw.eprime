using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class TreeVirtualizationArchitectureTests
{
    [Test]
    public async Task TreeRowsAreCachedAndOnlyVisibleRowsDraw()
    {
        string source = Source("UI", "ResourceTreeView.cs");
        await Assert.That(source).Contains("UniformViewportRange.Calculate(");
        await Assert.That(source)
            .Contains("for (int i = visible.Start; i < visible.EndExclusive; i++)");
        // Flatten runs only when tree state changes, not per frame.
        await Assert.That(source).Contains("private void EnsureRows(");
        await Assert.That(CountOf(source, "ResourceTreeFlattener.Flatten(")).IsEqualTo(1);
    }
}
