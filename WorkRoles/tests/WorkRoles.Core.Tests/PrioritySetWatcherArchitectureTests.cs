namespace WorkRoles.Core.Tests;

public class PrioritySetWatcherArchitectureTests
{
    [Test]
    public async Task WatcherUsesSparseProbeAndNextTickFlush()
    {
        string watcher = Source("PrioritySetWatcher.cs");
        string component = Source("WorkRolesGameComponent.cs");
        string language = File.ReadAllText(Path.Combine(RepoRoot(), "mod", "Languages",
            "English", "Keyed", "WorkRoles.xml"));

        await Assert.That(watcher).DoesNotContain("settledTick");
        await Assert.That(watcher).DoesNotContain("callerTick");
        await Assert.That(watcher).Contains("PriorityWriterProbe");
        await Assert.That(watcher).Contains("probe.ObserveBlockedWrite(tick)");
        await Assert.That(component).Contains("PrioritySetWatcher.HasPendingWarning");
        await Assert.That(component).Contains("PrioritySetWatcher.ShowPendingWarning(now)");
        await Assert.That(language).Contains("{2} priority writes");
        await Assert.That(language).DoesNotContain("{2} colonist(s)");
    }

    [Test]
    public async Task WatcherStoresOnlySampledReportData()
    {
        string watcher = Source("PrioritySetWatcher.cs");

        await Assert.That(watcher).DoesNotContain("HashSet<Pawn>");
        await Assert.That(watcher).DoesNotContain("HashSet<string> workTypes");
        await Assert.That(watcher).Contains("sampledWorkType");
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
