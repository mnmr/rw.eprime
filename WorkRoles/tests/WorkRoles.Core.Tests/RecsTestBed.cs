using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Shared builders for the rule-pipeline suites: small synthetic colonies
/// with coverage tokens standing in for expanded giver sets.
internal static class RecsTestBed
{
    public static readonly Dictionary<string, IReadOnlyList<string>> Skills = new()
    {
        ["Cooking"] = new List<string> { "Cooking" },
        ["Crafting"] = new List<string> { "Crafting" },
        ["Doctor"] = new List<string> { "Medicine" },
        ["Hunting"] = new List<string> { "Shooting" },
    };

    /// Coverage tokens default to one token named after the work type.
    /// MinHolders arrives RESOLVED (0 = interest-only) like the game adapter's.
    public static RoleView Role(int id, string workType, params string[] coverage) => new()
    {
        Id = id,
        WorkTypes = { workType },
        Coverage = coverage.Length > 0
            ? new HashSet<string>(coverage) : new HashSet<string> { workType },
        PrimarySkill = Skills.TryGetValue(workType, out var s) && s.Count > 0 ? s[0] : null,
    };

    public static RoleView Unskilled(int id, string workType, params string[] coverage)
    {
        var role = Role(id, workType, coverage);
        role.Unskilled = true;
        role.PrimarySkill = null;
        return role;
    }

    public static PawnView Pawn() => new()
    {
        CapableWorkTypes = { "Cooking", "Crafting", "Doctor", "Hunting", "Hauling" },
    };

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
            OrderTemplate = roles.Select(r => r.Id).ToList(),
        };
        foreach (var pawn in pawns)
            foreach (var kv in pawn.SkillLevels)
                if (!colony.SkillMaxLevels.TryGetValue(kv.Key, out int max) || kv.Value > max)
                    colony.SkillMaxLevels[kv.Key] = kv.Value;
        return colony;
    }

    public static List<PawnResult> Run(ColonyView colony, params RecRule[] rules)
        => RecsEngine.Run(colony, rules);

    public static string Ids(PawnResult result)
        => string.Join(",", result.Assignments.Select(a => a.RoleId));
}
