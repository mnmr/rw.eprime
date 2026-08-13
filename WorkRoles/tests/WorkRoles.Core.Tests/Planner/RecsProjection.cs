using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Builds a recommendation plan through the real projection pipeline
/// (RecommendationRoleSource -> RecommendationCatalogBuilder -> ColonyView ->
/// RecommendationPlan), the same path the game adapter feeds. Prefer this over
/// hand-built RoleViews so tests exercise coverage, skill, Unskilled-flag, and
/// demand derivation instead of asserting them into existence.
internal sealed class RecsProjection
{
    private readonly FakeCatalog jobs = new();
    private readonly JobProfileIndexBuilder profiles = new();
    private readonly Dictionary<string, int> naturalPriorities = new();
    private readonly List<RecommendationRoleSource> sources = new();
    private readonly List<PathView> paths = new();
    private int nextWorkTypeId = 1;

    /// Registers a work type, its givers, and the skill its givers train
    /// (null = unskilled work, no skill evidence).
    internal RecsProjection WorkType(string name, string skill, int naturalPriority, params string[] givers)
    {
        if (givers.Length == 0)
            givers = [name + "_g"];
        jobs.WithWorkType(name, givers);
        JobProfileSkillSource[] skills = skill != null ? [new JobProfileSkillSource(10, skill)] : [];
        string[] curated = skill != null ? [skill] : [];
        profiles.AddWorkType(nextWorkTypeId, name, skills, givers);
        foreach (string giver in givers)
            profiles.AddGiver(giver, nextWorkTypeId, skills, hasCuratedXp: true, curatedXpSkillDefNames: curated);
        naturalPriorities[name] = naturalPriority;
        nextWorkTypeId++;
        return this;
    }

    internal RecommendationRoleSource RoleByWorkType(int id, int colonyMin, int coverage, params string[] workTypes)
    {
        var source = new RecommendationRoleSource
        {
            Id = id,
            ColonyMin = colonyMin,
            Coverage = coverage,
        };
        foreach (string workType in workTypes)
            source.Entries.Add(new JobEntry(JobEntryKind.WorkType, workType));
        sources.Add(source);
        return source;
    }

    internal RecommendationRoleSource RoleByGiver(int id, int colonyMin, int coverage, params string[] givers)
    {
        var source = new RecommendationRoleSource
        {
            Id = id,
            ColonyMin = colonyMin,
            Coverage = coverage,
        };
        foreach (string giver in givers)
            source.Entries.Add(new JobEntry(JobEntryKind.WorkGiver, giver));
        sources.Add(source);
        return source;
    }

    internal RecsProjection AutoAssign(int id)
    {
        sources.Single(s => s.Id == id).AutoAssign = true;
        return this;
    }

    internal RecsProjection Path(PathView path)
    {
        paths.Add(path);
        return this;
    }

    internal RecommendationPlan Plan(params PawnView[] pawns)
    {
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(sources, paths, jobs, naturalPriorities, profiles.Build());
        ColonyView colony = projection.CreateColony(sources.Select(s => s.Id), pawns);
        return RecommendationPlan.Build(colony);
    }

    internal static bool Holds(RecommendationPlan plan, int pawn, int roleId)
    {
        for (int index = 0; index < plan.RoleCountAt(pawn); index++)
            if (plan.RoleAt(pawn, index) == roleId)
                return true;
        return false;
    }
}
