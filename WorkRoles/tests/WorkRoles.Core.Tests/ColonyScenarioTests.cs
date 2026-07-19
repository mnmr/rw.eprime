using System.Xml.Linq;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Colony-scale scenario tests: the rule-pipeline engine over the SHIPPED
/// roles + training paths with seeded pseudo-random colonies. Assertions are
/// INVARIANTS, not exact plans; failures name the size and seed.
public class ColonyScenarioTests
{
    internal sealed class Catalog
    {
        public List<RoleView> Roles = new();
        public List<PathView> Paths = new();
        public Dictionary<int, string> DefNames = new();
        public List<int> OrderTemplate = new();
        public int HunterId = -1, FireBlockerId = -1;
    }

    /// workType -> relevant skills (vanilla values for the shipped catalog).
    internal static readonly Dictionary<string, IReadOnlyList<string>> SkillsByWorkType = new()
    {
        ["Doctor"] = new List<string> { "Medicine" },
        ["Cooking"] = new List<string> { "Cooking" },
        ["Construction"] = new List<string> { "Construction" },
        ["Repair"] = new List<string> { "Construction" },
        ["Growing"] = new List<string> { "Plants" },
        ["PlantCutting"] = new List<string> { "Plants" },
        ["Mining"] = new List<string> { "Mining" },
        ["Smithing"] = new List<string> { "Crafting" },
        ["Tailoring"] = new List<string> { "Crafting" },
        ["Crafting"] = new List<string> { "Crafting" },
        ["Art"] = new List<string> { "Artistic" },
        ["Research"] = new List<string> { "Intellectual" },
        ["DarkStudy"] = new List<string> { "Intellectual" },
        ["Hunting"] = new List<string> { "Shooting" },
        ["Handling"] = new List<string> { "Animals" },
        ["Warden"] = new List<string> { "Social" },
        ["Childcare"] = new List<string> { "Medicine" },
        ["Fishing"] = new List<string> { "Animals" },
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    /// The vanilla job catalog, for expanding role entries into coverage.
    private static readonly FakeCatalog JobCatalog = BuildJobCatalog();

    private static FakeCatalog BuildJobCatalog()
    {
        var catalog = new FakeCatalog();
        foreach (var group in VanillaGiverBaseline.GiverWorkType.GroupBy(kv => kv.Value))
            catalog.WithWorkType(group.Key, group.Select(kv => kv.Key).ToArray());
        return catalog;
    }

    /// Test-side primary skill (the game adapter uses the XP tables instead).
    private static string PrimarySkillOf(HashSet<string> coverage)
    {
        var counts = new Dictionary<string, int>();
        foreach (var giver in coverage)
            if (VanillaGiverBaseline.GiverWorkType.TryGetValue(giver, out var wt)
                && SkillsByWorkType.TryGetValue(wt, out var skills))
                foreach (var s in skills)
                    counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
        return counts.Count == 0 ? null
            : counts.OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
    }

    internal static Catalog Shipped()
    {
        var defsDir = Path.Combine(RepoRoot(), "mod", "1.6", "Defs");
        var catalog = new Catalog();
        var idByDef = new Dictionary<string, int>();
        int id = 1;
        foreach (var def in XElement.Load(Path.Combine(defsDir, "Roles.xml"))
                     .Elements("WorkRoles.RoleDef"))
        {
            string defName = def.Element("defName")?.Value.Trim() ?? $"?{id}";
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry))
                    entries.Add(entry);
            var workTypes = new List<string>();
            foreach (var entry in entries)
            {
                string workType = entry.Kind == JobEntryKind.WorkType ? entry.DefName
                    : VanillaGiverBaseline.GiverWorkType.TryGetValue(entry.DefName, out var parent)
                        ? parent : null;
                if (workType != null && !workTypes.Contains(workType)) workTypes.Add(workType);
            }
            var coverage = CoverageMath.CoverageOf(entries, JobCatalog);
            var role = new RoleView
            {
                Id = id,
                Coverage = coverage,
                OrderedCoverage = CoverageMath.OrderedCoverageOf(entries, JobCatalog),
                AutoAssign = def.Element("autoAssign")?.Value.Trim() == "true",
                Blocker = def.Element("blocker")?.Value.Trim() == "true",
                PreserveRecommendationOrder = defName == "WS_Researcher"
                    || defName == "WS_DarkStudier",
                Unskilled = workTypes.All(wt => !SkillsByWorkType.ContainsKey(wt)),
                Hunting = workTypes.Contains("Hunting"),
                // Defs ARE the Auto defaults, so the def value is the resolved count.
                MinHolders = int.TryParse(def.Element("minHolders")?.Value, out var mnh) ? mnh : -1,
                NaturalPriority = workTypes
                    .Select(wt => VanillaWorkOrder.NaturalPriority.TryGetValue(wt, out var np) ? np : 0)
                    .DefaultIfEmpty(0).Max(),
                PrimarySkill = PrimarySkillOf(coverage),
            };
            role.WorkTypes.AddRange(workTypes);
            catalog.Roles.Add(role);
            catalog.DefNames[id] = defName;
            idByDef[defName] = id;
            if (defName == "WS_Hunter") catalog.HunterId = id;
            if (defName == "WS_NoFirefighting") catalog.FireBlockerId = id;
            id++;
        }
        int pathId = 1;
        foreach (var def in XElement.Load(Path.Combine(defsDir, "TrainingPaths.xml"))
                     .Elements("WorkRoles.TrainingPathDef"))
        {
            var path = new PathView { Id = pathId++ };
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string roleDef = li.Element("role")?.Value.Trim();
                if (roleDef == null || !idByDef.TryGetValue(roleDef, out int roleId)) continue;
                path.RoleIds.Add(roleId);
                path.BandMins.Add(int.TryParse(li.Element("min")?.Value, out var mn) ? mn : 0);
                path.BandMaxes.Add(int.TryParse(li.Element("max")?.Value, out var mx)
                    ? mx : SkillProgressionMath.MaxLevel);
            }
            string anchor = def.Element("anchorRole")?.Value.Trim();
            if (anchor != null && idByDef.TryGetValue(anchor, out int anchorId))
                path.AnchorRoleId = anchorId;
            path.AnchorBefore = def.Element("anchorBefore")?.Value.Trim() != "false";
            if (path.RoleIds.Count >= 2) catalog.Paths.Add(path);
        }
        // The REAL derivation — no mirror: the game adapter calls the same
        // Core function on the same projection.
        catalog.OrderTemplate = OrderTemplate.DeriveTemplate(catalog.Roles);
        return catalog;
    }

    internal static Catalog ShippedCatalog() => Shipped();

    private static readonly string[] AllSkills =
    {
        "Medicine", "Cooking", "Construction", "Plants", "Mining", "Crafting",
        "Artistic", "Intellectual", "Shooting", "Animals", "Social",
    };

    private static List<PawnView> GenColony(int size, int seed)
    {
        var rand = new Random(seed);
        var allTypes = SkillsByWorkType.Keys
            .Concat(new[] { "Hauling", "Cleaning", "Firefighter", "Patient",
                "PatientBedRest", "BasicWorker" })
            .Distinct().ToArray();
        var pawns = new List<PawnView>();
        for (int i = 0; i < size; i++)
        {
            var pawn = new PawnView();
            foreach (var skill in AllSkills)
            {
                pawn.SkillLevels[skill] = rand.Next(0, 15);
                int roll = rand.Next(100);
                SignalBucket bucket = roll < 8 ? SignalBucket.Great
                    : roll < 28 ? SignalBucket.Strong
                    : roll < 36 ? SignalBucket.Poor
                    : roll < 40 ? SignalBucket.Awful
                    : SignalBucket.Neutral;
                if (rand.Next(100) < 4) bucket = SignalBucket.Exceptional;
                pawn.SignalBuckets[skill] = bucket;
            }
            pawn.CapableWorkTypes.UnionWith(allTypes);
            if (rand.Next(100) < 10) pawn.CapableWorkTypes.Remove("Hunting");
            if (rand.Next(100) < 10)
                pawn.CapableWorkTypes.ExceptWith(new[] { "Hauling", "Cleaning", "PlantCutting" });
            if (rand.Next(100) < 8)
                pawn.CapableWorkTypes.ExceptWith(new[] { "Doctor", "Childcare" });
            pawn.HasRangedWeapon = rand.Next(100) < 40;
            pawn.ShootingLevel = pawn.SkillLevels["Shooting"];
            pawn.FireFear = rand.Next(100) < 5;
            pawns.Add(pawn);
        }
        return pawns;
    }

    internal static ColonyView Build(Catalog catalog, List<PawnView> pawns)
    {
        var colony = new ColonyView
        {
            Roles = catalog.Roles,
            Paths = catalog.Paths,
            OrderTemplate = catalog.OrderTemplate,
            WorkTypeSkills = SkillsByWorkType,
            HunterRoleId = catalog.HunterId,
            FireBlockerRoleId = catalog.FireBlockerId,
            Pawns = pawns,
        };
        foreach (var pawn in pawns)
            foreach (var kv in pawn.SkillLevels)
                if (!colony.SkillMaxLevels.TryGetValue(kv.Key, out int max) || kv.Value > max)
                    colony.SkillMaxLevels[kv.Key] = kv.Value;
        return colony;
    }

    internal static (Catalog catalog, ColonyView colony, List<PawnResult> results)
        Execute(int size, int seed)
    {
        var catalog = Shipped();
        var colony = Build(catalog, GenColony(size, seed));
        return (catalog, colony, RecsEngine.Run(colony));
    }

    private static RoleView RoleOf(Catalog catalog, int id)
        => catalog.Roles.First(r => r.Id == id);

    private static bool Holds(PawnResult result, Catalog catalog, RoleView role)
        => result.Assignments.Any(a => a.RoleId == role.Id
            || (RoleOf(catalog, a.RoleId) is RoleView other && !other.Blocker
                && CoverageMath.MakesRedundant(other.Coverage, other.Id, role.Coverage, role.Id)));

    /// Mirror of EngineContext.PassesBands (INTEREST gating) for invariant
    /// checks. The need-driven floor ignores this — see the draft rule.
    private static bool PassesBands(ColonyView colony, int pawnIndex, RoleView role)
    {
        bool member = false;
        foreach (var path in colony.Paths)
        {
            int entry = path.RoleIds.IndexOf(role.Id);
            if (entry < 0) continue;
            member = true;
            var skills = role.Skills.Where(s => s.Required).ToList();
            if (skills.Count == 0 && role.PrimarySkill != null)
                skills.Add(new RoleSkillView { SkillDefName = role.PrimarySkill });
            if (skills.Count == 0) return true;
            if (skills.All(skill => colony.Pawns[pawnIndex].SkillLevels
                    .TryGetValue(skill.SkillDefName, out int level)
                    && PathMath.InsideBand(path, entry, level)))
                return true;
        }
        return !member;
    }

    // ----- invariants -----

    /// The engine's hard veto: an Awful bucket on any required skill (or the
    /// primary skill when nothing is marked required) excludes the pawn.
    private static bool AwfulAt(ColonyView colony, int pawnIndex, WorkRoles.Core.Recs.RoleView role)
    {
        var skills = role.Skills.Where(s => s.Required).Select(s => s.SkillDefName).ToList();
        if (skills.Count == 0 && role.PrimarySkill != null) skills.Add(role.PrimarySkill);
        return skills.Any(skill =>
            colony.Pawns[pawnIndex].SignalBuckets.TryGetValue(skill, out var bucket)
            && bucket == SignalBucket.Awful);
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task NeededRolesAreCoveredWheneverAnEligiblePawnExists(int size, int seed)
    {
        // minHolders is an ABSOLUTE floor: bands gate interest, never the
        // need-driven fill. Eligible = capable and not hard-vetoed by an
        // Awful bucket (the draft never assigns Awful pawns).
        var (catalog, colony, results) = Execute(size, seed);
        foreach (var role in catalog.Roles)
        {
            if (role.MinHolders < 1 || role.Blocker || role.HasRules || role.Hunting) continue;
            bool anyEligible = Enumerable.Range(0, colony.Pawns.Count).Any(i =>
                role.WorkTypes.Any(colony.Pawns[i].CapableWorkTypes.Contains)
                && !AwfulAt(colony, i, role));
            if (!anyEligible) continue;
            bool covered = results.Any(r => Holds(r, catalog, role));
            await Assert.That(covered).IsTrue()
                .Because($"{catalog.DefNames[role.Id]} uncovered (size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(10, 33)] [Arguments(50, 44)] [Arguments(50, 66)]
    public async Task DraftNeverGrantsARoleToAnAwfulPawn(int size, int seed)
    {
        var (catalog, colony, results) = Execute(size, seed);
        for (int i = 0; i < results.Count; i++)
            foreach (var assignment in results[i].Assignments)
            {
                if (!results[i].Reasons.TryGetValue(assignment.RoleId, out var reason)
                    || reason.RuleId != "draft") continue;
                var role = catalog.Roles.First(r => r.Id == assignment.RoleId);
                await Assert.That(AwfulAt(colony, i, role)).IsFalse()
                    .Because($"{catalog.DefNames[role.Id]} drafted to an Awful pawn "
                        + $"(pawn {i}, size {size}, seed {seed})");
            }
    }

    [Test]
    [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)] [Arguments(50, 66)]
    public async Task DraftGrantsStayWithinTheScaledWant(int size, int seed)
    {
        var (catalog, colony, results) = Execute(size, seed);
        var scaling = new UnitScaling();
        foreach (var role in catalog.Roles)
        {
            int want = scaling.Want(role, size);
            if (want <= 0) continue;
            int drafted = results.Count(r =>
                r.Reasons.TryGetValue(role.Id, out var reason) && reason.RuleId == "draft");
            await Assert.That(drafted <= want).IsTrue()
                .Because($"{catalog.DefNames[role.Id]} drafted {drafted} > want {want} "
                    + $"(size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task ArmedCapablePawnsHunt_OthersNever_OneTierZeroWhenAnyHunt(int size, int seed)
    {
        var (_, colony, results) = Execute(size, seed);
        for (int i = 0; i < colony.Pawns.Count; i++)
        {
            bool armedCapable = colony.Pawns[i].HasRangedWeapon
                && colony.Pawns[i].CapableWorkTypes.Contains("Hunting");
            if (armedCapable)
                await Assert.That(results[i].HunterTier >= 0).IsTrue()
                    .Because($"armed capable pawn {i} does not hunt (size {size}, seed {seed})");
            else
                await Assert.That(results[i].HunterTier).IsEqualTo(-1)
                    .Because($"pawn {i} hunts without gun/capability (size {size}, seed {seed})");
        }
        if (results.Any(r => r.HunterTier >= 0))
            await Assert.That(results.Any(r => r.HunterTier == 0)).IsTrue()
                .Because($"hunters exist but no tier 0 (size {size}, seed {seed})");
    }

    [Test]
    [Arguments(10, 33)] [Arguments(50, 44)] [Arguments(50, 66)]
    public async Task FireTerrorGetsTheBlockerFirst_NobodyElseDoes(int size, int seed)
    {
        // The shipped catalog seeds WS_NoFirefighting; a fearing pawn gets it
        // at the TOP of its list (the veto must precede everything).
        var (catalog, colony, results) = Execute(size, seed);
        bool hasBlocker = catalog.FireBlockerId != -1;
        for (int i = 0; i < colony.Pawns.Count; i++)
        {
            await Assert.That(results[i].FireGranted)
                .IsEqualTo(colony.Pawns[i].FireFear && hasBlocker)
                .Because($"fire blocker mismatch on pawn {i} (size {size}, seed {seed})");
            if (results[i].FireGranted)
                await Assert.That(results[i].Assignments[0].RoleId)
                    .IsEqualTo(catalog.FireBlockerId)
                    .Because($"fire blocker not first for pawn {i} (size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task NoAssignmentWithoutCapability_InterestRespectsBands(int size, int seed)
    {
        var (catalog, colony, results) = Execute(size, seed);
        for (int i = 0; i < results.Count; i++)
            foreach (var assignment in results[i].Assignments)
            {
                var role = RoleOf(catalog, assignment.RoleId);
                if (role.Blocker) continue; // vetoes need no capability
                if (role.WorkTypes.Count > 0)
                    await Assert.That(role.WorkTypes
                            .Any(colony.Pawns[i].CapableWorkTypes.Contains)).IsTrue()
                        .Because($"pawn {i} got {catalog.DefNames[role.Id]} without capability "
                            + $"(size {size}, seed {seed})");
                // Only INTEREST (signal) assignments gate on bands; the
                // need-driven floor (draft) may place below-band.
                if (results[i].Reasons.TryGetValue(assignment.RoleId, out var reason)
                    && reason.RuleId == "signals")
                    await Assert.That(PassesBands(colony, i, role)).IsTrue()
                        .Because($"pawn {i} got {catalog.DefNames[role.Id]} outside its band "
                            + $"(size {size}, seed {seed})");
            }
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    public async Task AssignmentsFollowTheTemplateOutsidePathBlocksAndOverrides(int size, int seed)
    {
        var (catalog, colony, results) = Execute(size, seed);
        var positions = Ordering.BasePositions(catalog.Roles, catalog.OrderTemplate);
        var pathMembers = catalog.Paths
            .Where(p => p.AnchorRoleId != -1)
            .SelectMany(p => p.RoleIds).ToHashSet();
        for (int i = 0; i < results.Count; i++)
        {
            bool Exempt(int roleId) => roleId == catalog.HunterId
                || roleId == catalog.FireBlockerId
                || RoleOf(catalog, roleId).Blocker
                || pathMembers.Contains(roleId);
            var plain = results[i].Assignments
                .Select(a => a.RoleId).Where(rid => !Exempt(rid)).ToList();
            for (int k = 1; k < plain.Count; k++)
                await Assert.That(positions[plain[k - 1]] <= positions[plain[k]]).IsTrue()
                    .Because($"{catalog.DefNames[plain[k - 1]]} outranks "
                        + $"{catalog.DefNames[plain[k]]} for pawn {i} (size {size}, seed {seed})");
        }
    }

    [Test]
    public async Task DefaultTemplateIsTheVanillaGridColumns()
    {
        // Pins the shipped default: one chip per vanilla-grid work column,
        // autos included at their priority slots.
        var catalog = Shipped();
        var names = catalog.OrderTemplate.Select(id => catalog.DefNames[id]);
        await Assert.That(string.Join(",", names)).IsEqualTo(
            "WS_Core,WS_Doctor,WS_Basics,WS_Childminder,WS_Warden,WS_Handler,"
            + "WS_Cook,WS_Builder,WS_Farmer,WS_Miner,WS_Smith,WS_Tailor,"
            + "WS_Artist,WS_Crafter,WS_Fisher,WS_Grunt,WS_DarkStudier,WS_Researcher");
    }

    [Test]
    public async Task ResearcherUsesItsExactRecommendationSlotInsteadOfItsTrainingPath()
    {
        var catalog = Shipped();
        var names = new[] { "WS_Core", "WS_Basics", "WS_Cook", "WS_Grunt", "WS_Researcher" };
        var roleIds = names.Select(name => catalog.DefNames.Single(pair => pair.Value == name).Key)
            .ToList();
        var colony = Build(catalog, new List<PawnView> { new PawnView() });
        var context = new EngineContext(colony);
        foreach (int roleId in roleIds)
            context.AddCandidate(0, roleId,
                new Reason { RuleId = "test", TowardRoleId = -1 }, SignalBucket.Neutral);

        new OrderingRule().Apply(context, 0);

        string actual = string.Join(",", context.Results[0].Assignments
            .Select(assignment => catalog.DefNames[assignment.RoleId]));
        await Assert.That(actual).IsEqualTo(string.Join(",", names));
    }

    [Test]
    public async Task UnlistedResearchRolesLeadTheTrailingUnskilledBlock()
    {
        var catalog = Shipped();
        int IdOf(string name) => catalog.DefNames.Single(pair => pair.Value == name).Key;
        catalog.Paths.Clear();
        catalog.OrderTemplate = new List<int>
            { IdOf("WS_Core"), IdOf("WS_Cook"), IdOf("WS_Grunt") };
        var candidates = new[]
        {
            "WS_Core", "WS_Cook", "WS_Grunt", "WS_DarkStudier", "WS_Researcher",
        };
        var colony = Build(catalog, new List<PawnView> { new PawnView() });
        var context = new EngineContext(colony);
        foreach (string name in candidates)
            context.AddCandidate(0, IdOf(name),
                new Reason { RuleId = "test", TowardRoleId = -1 }, SignalBucket.Neutral);

        new OrderingRule().Apply(context, 0);

        string actual = string.Join(",", context.Results[0].Assignments
            .Select(assignment => catalog.DefNames[assignment.RoleId]));
        await Assert.That(actual).IsEqualTo(
            "WS_Core,WS_Cook,WS_DarkStudier,WS_Researcher,WS_Grunt");
    }
}
