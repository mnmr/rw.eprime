using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Shared builders for the rule-pipeline suites: small synthetic colonies
/// with coverage tokens standing in for expanded giver sets. Roles publish
/// real RoleWorkSpecs derived through RoleWorkSpecBuilder, so fixtures run
/// the production weighting, participation, and primary selection.
internal static class RecsTestBed
{
    public static readonly Dictionary<string, IReadOnlyList<string>> Skills = new()
    {
        ["Cooking"] = ["Cooking"],
        ["Crafting"] = ["Crafting"],
        ["Doctor"] = ["Medicine"],
        ["Hunting"] = ["Shooting"],
    };

    public static RoleWorkGiverSpec Giver(string name, string[]? used = null, string[]? trained = null, params (string Skill, int Min)[] gates)
    {
        var useSpecs = new List<RoleSkillUseSpec>();
        foreach (string skill in used ?? [])
            useSpecs.Add(new RoleSkillUseSpec(skill, RoleWorkEffect.Unspecified));
        var contents = new List<RoleWorkContentSpec>();
        foreach ((string skill, int min) in gates)
            contents.Add(new RoleWorkContentSpec(RoleWorkContentKind.Recipe, $"{name}_{skill}{min}", null!, RoleWorkEffect.Unspecified, false, [new RoleContentGate(skill, min)]));
        return new RoleWorkGiverSpec(name, useSpecs, trained ?? [], contents);
    }

    public static RoleWorkCapabilitySpec Capability(string workType, int priority, params RoleWorkGiverSpec[] givers) => new(workType, priority, false, givers);

    /// Rebuilds the role's spec from capabilities, preserving assignment gates.
    public static void SetSpec(RoleView role, params RoleWorkCapabilitySpec[] capabilities) =>
        role.WorkSpec = RoleWorkSpecBuilder.Build(role.Id, capabilities, role.WorkSpec.AssignmentSkillGates);

    /// Replaces the user-authored enabled-skill gates.
    public static void SetGates(RoleView role, params string[] gates) =>
        role.WorkSpec = RoleWorkSpecBuilder.Build(role.Id, role.WorkSpec.Capabilities, gates);

    public static void SetNaturalPriority(RoleView role, int priority)
    {
        var capabilities = new List<RoleWorkCapabilitySpec>();
        foreach (RoleWorkCapabilitySpec capability in role.WorkSpec.Capabilities)
            capabilities.Add(new RoleWorkCapabilitySpec(capability.WorkTypeDefName, priority, capability.IncludesWholeWorkType, capability.Givers));
        role.WorkSpec = RoleWorkSpecBuilder.Build(role.Id, capabilities, role.WorkSpec.AssignmentSkillGates);
    }

    /// Appends a gate-only content giver, adding gate evidence to the profile.
    public static void AddGate(RoleView role, string skill, int min = 1)
    {
        var capabilities = new List<RoleWorkCapabilitySpec>(role.WorkSpec.Capabilities.Count);
        for (int index = 0; index < role.WorkSpec.Capabilities.Count; index++)
        {
            RoleWorkCapabilitySpec capability = role.WorkSpec.Capabilities[index];
            if (index == 0)
            {
                var givers = new List<RoleWorkGiverSpec>(capability.Givers) { Giver($"{skill}GateWork", gates: (skill, min)) };
                capability = new RoleWorkCapabilitySpec(capability.WorkTypeDefName, capability.NaturalPriority, capability.IncludesWholeWorkType, givers);
            }
            capabilities.Add(capability);
        }
        role.WorkSpec = RoleWorkSpecBuilder.Build(role.Id, capabilities, role.WorkSpec.AssignmentSkillGates);
    }

    /// Coverage tokens default to one token named after the work type; the
    /// first token carries the work type's mapped skill, used and trained
    /// like real skilled givers. Ordinary roles carry no demand by default
    /// (surplus-eligible), like the shipped catalog; Require sets the two
    /// demand numbers.
    public static RoleView Role(int id, string workType, params string[] coverage)
    {
        string? primary = Skills.TryGetValue(workType, out IReadOnlyList<string>? skills) && skills.Count > 0 ? skills[0] : null;
        return Build(id, workType, primary, coverage);
    }

    public static RoleView Unskilled(int id, string workType, params string[] coverage) => Build(id, workType, null, coverage);

    public static RoleView Skilled(int id, string workType, string skill, params string[] coverage) => Build(id, workType, skill, coverage);

    private static RoleView Build(int id, string workType, string? primary, string[] coverage)
    {
        string[] tokens = coverage.Length > 0 ? coverage : [workType];
        var role = new RoleView { Id = id, Coverage = [.. tokens] };
        var givers = new RoleWorkGiverSpec[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
            givers[index] = index == 0 && primary != null ? Giver(tokens[index], used: [primary], trained: [primary]) : Giver(tokens[index]);
        SetSpec(role, Capability(workType, 0, givers));
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
            OrderTemplate = WorkRoles.Core.Recs.OrderTemplate.ResolveTemplate(null!, roles),
        };
        foreach (var pawn in pawns)
        foreach (var kv in pawn.SkillLevels)
            if (!colony.SkillMaxLevels.TryGetValue(kv.Key, out int max) || kv.Value > max)
                colony.SkillMaxLevels[kv.Key] = kv.Value;
        return colony;
    }
}
