namespace WorkRoles.Core.Tests;

public class OwnerGenerationRegistryArchitectureTests
{
    [Test]
    public async Task LookupCollisionsUseIntrusiveChainsWithoutPerKeyCollectionsOrRebuilds()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "WorkRoles.Core",
            "OwnerGenerationRegistry.cs"));

        await Assert.That(source).Contains("internal Entry CollisionNewer;");
        await Assert.That(source).Contains("internal Entry CollisionOlder;");
        await Assert.That(source).Contains("private int entryCount;");
        await Assert.That(source).Contains("public int Count => entryCount;");
        await Assert.That(source).Contains("MoveToLookupHead(entry);");
        await Assert.That(source).Contains("UnlinkLookup(entry);");
        await Assert.That(source)
            .Contains("private readonly List<Entry> removalBuffer = new List<Entry>();");

        string flush = Method(source, "public int FlushRetired()");
        await Assert.That(flush).Contains("foreach (Entry entry in retiredEntries)");
        await Assert.That(flush).Contains("removalBuffer.Add(entry);");
        await Assert.That(flush).Contains("int removed = removalBuffer.Count;");
        await Assert.That(flush).Contains("finally");
        await Assert.That(flush).Contains("removalBuffer.Clear();");
        await Assert.That(flush).DoesNotContain("new List<Entry>(retiredEntries)");

        foreach (string forbidden in new[]
                 {
                     "Dictionary<TLookupKey, HashSet<Entry>>", "allEntries", "RebuildLookup",
                     "Sequence", "sequence", "affected",
                 })
            await Assert.That(source).DoesNotContain(forbidden);
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
