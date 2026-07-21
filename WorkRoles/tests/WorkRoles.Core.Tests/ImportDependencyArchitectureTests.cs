namespace WorkRoles.Core.Tests;

public class ImportDependencyArchitectureTests
{
    [Test]
    public async Task PreviewClearsAndDisablesDependentSectionsWithoutRoles()
    {
        string source = Source("UI", "Dialog_ImportPreview.cs");

        await Assert.That(source).Contains("EnforceRoleDependencies();");
        await Assert.That(source).Contains("pathsInclude = false;");
        await Assert.That(source).Contains("orderInclude = false;");
        await Assert.That(source).Contains("GUI.enabled = previousEnabled && rolesInclude;");
    }

    [Test]
    public async Task ApplyNormalizesDependentSectionsWithoutRoles()
    {
        string source = Source("RoleIO.cs");
        string apply = Section(source, "public static string Apply", "private static Role RuntimeRole");
        int rolesGate = apply.IndexOf("if (rolesInclude)", StringComparison.Ordinal);
        int rolePlanning = apply.IndexOf("RoleRows(store, doc, rolesOverwrite)",
            StringComparison.Ordinal);

        await Assert.That(apply).Contains("if (!rolesInclude)");
        await Assert.That(apply).Contains("pathsInclude = false;");
        await Assert.That(apply).Contains("orderInclude = false;");
        await Assert.That(rolePlanning).IsGreaterThan(rolesGate);
    }

    private static string Section(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return start < 0 || end < 0 ? "" : source.Substring(start, end - start);
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
