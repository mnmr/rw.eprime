using System.Text.RegularExpressions;

namespace WorkRoles.Core.Tests;

public class RoleCopyArchitectureTests
{
    [Test]
    public async Task DuplicateRoleRoutesThroughTheCorePlayerCopyPolicy()
    {
        string source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "WorkRoles", "RoleCommands.cs"));
        string command = Method(source, "public static void DuplicateRole");
        string route = Method(source, "private static Role PlayerDuplicate");
        string roleSource = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "WorkRoles", "Role.cs"));
        IReadOnlyList<string> fields = PublicInstanceFields(roleSource);

        await Assert.That(command)
            .Contains("PlayerDuplicate(source, Store.NextId(), label)");
        await Assert.That(command).DoesNotContain("new Role");
        await Assert.That(route).Contains("new RoleCopyValues<Color>");

        const string policyCall = ".ForPlayerDuplicate();";
        int policy = route.IndexOf(policyCall, StringComparison.Ordinal);
        string projection = policy < 0 ? "" : route.Substring(0, policy);
        string materialization = policy < 0
            ? ""
            : route.Substring(policy + policyCall.Length);
        await Assert.That(policy >= 0).IsTrue();
        await Assert.That(fields.Contains("id")).IsTrue();
        await Assert.That(fields.Contains("label")).IsTrue();
        await Assert.That(fields.Count > 2).IsTrue();

        foreach (string field in fields.Where(field => field != "id" && field != "label"))
        {
            string value = char.ToUpperInvariant(field[0]) + field.Substring(1);
            await Assert.That(projection).Contains(value + " = source." + field)
                .Because(field + " must enter the Core copy policy");
            await Assert.That(materialization).Contains(field + " = values." + value)
                .Because(field + " must leave the Core copy policy");
        }

        await Assert.That(materialization).Contains("id = id");
        await Assert.That(materialization).Contains("label = label");
        await Assert.That(materialization).DoesNotContain("source.");
    }

    private static IReadOnlyList<string> PublicInstanceFields(string source) =>
        Regex.Matches(source,
                @"^\s*public\s+(?!const\b|static\b)(?:(?:readonly|volatile)\s+)*(?:[^=;{(]+?)\s+(?<name>[A-Za-z_]\w*)\s*(?:=(?!>)[^;]*)?;",
                RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .ToList();

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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "WorkRoles.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Repo root not found");
    }
}
