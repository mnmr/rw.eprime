namespace EPrimeReadouts.Core.Tests;

/// Architecture contract tests read game-assembly source (which tests cannot
/// reference as a project) and assert structural patterns hold.
public static class ArchitectureTestSupport
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "EPrimeReadouts.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    /// Reads a file under src/EPrimeReadouts (path segments, e.g. "UI", "ReadoutPanel.cs").
    public static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "EPrimeReadouts" }
            .Concat(parts).ToArray()));

    /// Extracts a brace-balanced method body starting at the given signature.
    public static string Method(string source, string signature)
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

    public static int CountOf(string source, string token)
    {
        int count = 0, index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
