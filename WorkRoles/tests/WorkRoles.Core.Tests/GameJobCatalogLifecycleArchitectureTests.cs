namespace WorkRoles.Core.Tests;

public class GameJobCatalogLifecycleArchitectureTests
{
    [Test]
    public async Task SessionInvalidatorClearsBothCatalogDictionaries()
    {
        string catalog = Source("GameJobCatalog.cs");
        string invalidator = Method(catalog, "internal void InvalidateSessionCache()");

        await Assert.That(invalidator).Contains("givers = null;");
        await Assert.That(invalidator).Contains("giversByType = null;");
    }

    [Test]
    public async Task LanguageCompletionAndWorldTeardownInvalidateCatalogSession()
    {
        string complete = Method(Source("LanguageChangeCoordinator.cs"),
            "internal static void Complete()");
        string teardown = Method(Source("Patches", "Patch_PawnWorkSettings.cs"),
            "public static class Patch_MemoryUtility_ClearAllMapsAndWorld");
        string definitionOwners = Method(Source("DefinitionReloadCoordinator.cs"),
            "private static void ReleaseOwners()");
        const string invalidate = "GameJobCatalog.Instance.InvalidateSessionCache();";

        await Assert.That(complete).Contains(invalidate);
        await Assert.That(complete.IndexOf(invalidate, StringComparison.Ordinal))
            .IsLessThan(complete.IndexOf("UI.GroupSources.InvalidateLanguageCaches();",
                StringComparison.Ordinal))
            .Because("the catalog must clear before language-dependent owners can rebuild");
        await Assert.That(teardown)
            .Contains("DefinitionReloadCoordinator.ReleaseForTeardown();");
        await Assert.That(definitionOwners).Contains(invalidate);
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
