namespace WorkRoles.Core.Tests;

public class RecommendationProjectionArchitectureTests
{
    [Test]
    public async Task PlanningConsumersUseOneBatchProjectionAndKeepAvailabilityLive()
    {
        string adapter = Source("RecsAdapter.cs");
        string colony = Method(adapter, "internal static ColonyView BuildColonyView(");
        string resolved = Method(adapter,
            "internal static List<int> ResolvedRecommendationOrder(RoleStore store)");
        string single = Method(adapter, "internal static RoleView RoleViewOf(Role role)");
        string materialize = Method(adapter,
            "private static RoleView RoleViewOf(Role role, RecommendationRoleProjection projection)");
        string options = Method(Source("UI", "OptionsTabState.cs"),
            "internal void EnsureOrder(RoleStore store, float width)");

        await Assert.That(colony).Contains("BuildRoleProjectionBatch(store.roles)");
        await Assert.That(colony).DoesNotContain("Select(RoleViewOf)");
        await Assert.That(resolved).Contains("RoleViewsOf(store.roles)");
        await Assert.That(single).Contains("BuildRoleProjectionBatch");
        await Assert.That(materialize).Contains("projection.CopyWorkTypes()");
        await Assert.That(materialize).Contains("projection.CopySkillViews()");
        await Assert.That(materialize).Contains("Available = RoleAvailable(role)");
        await Assert.That(options).Contains("RecsAdapter.RoleViewsOf(store.roles)");
        await Assert.That(options).DoesNotContain("Select(RecsAdapter.RoleViewOf)");
    }

    [Test]
    public async Task SpecialRoleResolutionUsesLiteralProjectedEntriesInCatalogOrder()
    {
        string adapter = Source("RecsAdapter.cs");
        string provider = Method(adapter,
            "private static Role RoleProviding(RoleProjectionBatch batch,");
        string blocker = Method(adapter,
            "private static Role FireBlocker(RoleProjectionBatch batch)");

        await Assert.That(provider).Contains("projection.HasLiteralWorkType(workType)");
        await Assert.That(provider).Contains("role.entries.Count < best.entries.Count");
        await Assert.That(provider).DoesNotContain("projection.WorkTypes.Contains");
        await Assert.That(blocker).Contains("projection.HasLiteralWorkType(\"Firefighter\")");
        await Assert.That(blocker).DoesNotContain("projection.WorkTypes.Contains");
    }

    [Test]
    public async Task RoleSkillProfilesUsesReusableFlatScratchWithoutPerGiverCollections()
    {
        string profiles = Source("RoleSkillProfiles.cs");
        string evidence = Method(profiles,
            "internal static IReadOnlyList<RoleSkillEvidence> EvidenceForCoverage(");

        await Assert.That(evidence).Contains("RoleSkillEvidenceAccumulator");
        await Assert.That(evidence).DoesNotContain("new HashSet");
        await Assert.That(evidence).DoesNotContain(".GroupBy(");
        await Assert.That(evidence).DoesNotContain(".ToDictionary(");
        await Assert.That(evidence).DoesNotContain(".Union(");
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
