using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Shared builders for the rule-pipeline suites: small synthetic colonies
/// with coverage tokens standing in for expanded giver sets.
internal static class RecsTestBed
{
    public static readonly Dictionary<string, IReadOnlyList<string>> Skills = new()
    {
        ["Cooking"] = ["Cooking"],
        ["Crafting"] = ["Crafting"],
        ["Doctor"] = ["Medicine"],
        ["Hunting"] = ["Shooting"],
    };

    /// Coverage tokens default to one token named after the work type.
    /// Ordinary roles carry no demand by default (surplus-eligible), like the
    /// shipped catalog; Require sets the two demand numbers.
    public static RoleView Role(int id, string workType, params string[] coverage) =>
        new()
        {
            Id = id,
            WorkTypes = { workType },
            Coverage = coverage.Length > 0 ? [.. coverage] : [workType],
            PrimarySkill = Skills.TryGetValue(workType, out var s) && s.Count > 0 ? s[0] : null,
        };

    public static RoleView Unskilled(int id, string workType, params string[] coverage)
    {
        var role = Role(id, workType, coverage);
        role.Unskilled = true;
        role.PrimarySkill = null;
        return role;
    }

    public static void Require(RoleView role, int colonyMin, int coverage = 0)
    {
        role.ColonyMin = colonyMin;
        role.CoveragePercent = coverage;
    }

    public static PawnView Pawn() => new() { CapableWorkTypes = { "Cooking", "Crafting", "Doctor", "Hunting", "Hauling" } };

    public static PathView Path(int id, params (int roleId, int min, int max)[] entries)
    {
        var path = new PathView { Id = id };
        foreach (var (roleId, min, max) in entries)
        {
            path.RoleIds.Add(roleId);
            path.BandMins.Add(min);
            path.BandMaxes.Add(max);
        }
        return path;
    }

    /// Template = the given roles in list order; skill maxima from the pawns.
    public static ColonyView Colony(List<RoleView> roles, params PawnView[] pawns)
    {
        var colony = new ColonyView
        {
            Roles = roles,
            Pawns = pawns.ToList(),
            WorkTypeSkills = Skills,
            OrderTemplate = WorkRoles.Core.Recs.OrderTemplate.ResolveTemplate(null, roles),
        };
        foreach (var pawn in pawns)
        foreach (var kv in pawn.SkillLevels)
            if (!colony.SkillMaxLevels.TryGetValue(kv.Key, out int max) || kv.Value > max)
                colony.SkillMaxLevels[kv.Key] = kv.Value;
        return colony;
    }
}
