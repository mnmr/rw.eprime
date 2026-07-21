namespace WorkRoles.Core.Tests;

public class RoleDragReleaseArchitectureTests
{
    [Test]
    public async Task DeferredClickPathStaysInRawMouseCoordinates()
    {
        string drag = Source("UI", "RoleDrag.cs");

        await Assert.That(drag).DoesNotContain("GUIToScreenPoint")
            .Because("GUIToScreenPoint depends on the active clip stack, so press-time "
                + "(inside a scroll view) and resolve-time (tab root) conversions disagree "
                + "for the same physical mouse position and reject valid clicks");
        await Assert.That(drag).Contains("UnityEngine.Input.mousePosition")
            .Because("the raw-pixel drag threshold is the one clip-independent bound "
                + "keeping a short release on the pressed control");
        await Assert.That(drag).Contains("StartDistanceSq");
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
