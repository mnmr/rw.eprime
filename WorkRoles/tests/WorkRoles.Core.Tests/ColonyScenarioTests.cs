using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Colony-scale scenario tests: the full planning pipeline (ColonyPlanner ->
/// RecommendationEngine -> TargetPlanner) over the SHIPPED role catalog with
/// seeded pseudo-random colonies (sizes 1/3/10/50; varied skills, passions,
/// expertise, capability profiles, weapons, fire-terror). Assertions are
/// INVARIANTS, not exact plans; failures name the size and seed so a colony
/// can be replayed.
public class ColonyScenarioTests
{
    // ----- shipped catalog projection (mirrors the game adapter's rules) -----

    internal sealed class Catalog
    {
        public List<RecRole> Recs = new();
        public List<TargetRole> Targets = new();
        public Dictionary<int, string> DefNames = new();
        public Dictionary<int, int> EssentialRank = new();
        public List<int> OrderTemplate = new();
        public int HunterId = -1, DoctorId = -1, MedicId = -1, FireBlockerId = -1;
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

    private static readonly (string workType, string template)[] Essentials =
    {
        ("Doctor", "WS_Doctor"), ("Cooking", "WS_Cook"), ("Construction", "WS_Builder"),
        ("Growing", "WS_Farmer"), ("Mining", "WS_Miner"), ("Smithing", "WS_Smith"),
        ("Tailoring", "WS_Tailor"), ("Crafting", "WS_Crafter"),
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

    private static Catalog Shipped()
    {
        var path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");
        var catalog = new Catalog();
        var trainTargetNames = new Dictionary<int, List<string>>();
        int id = 1;
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
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
                    : VanillaGiverBaseline.GiverWorkType.TryGetValue(entry.DefName, out var parent) ? parent : null;
                if (workType != null && !workTypes.Contains(workType)) workTypes.Add(workType);
            }

            string trainSkill = def.Element("trainSkill")?.Value.Trim();
            bool blocker = def.Element("blocker")?.Value.Trim() == "true";
            bool autoAssign = def.Element("autoAssign")?.Value.Trim() == "true";
            bool unskilled = workTypes.All(wt => !SkillsByWorkType.ContainsKey(wt));

            var rec = new RecRole
            {
                Id = id,
                Coverage = CoverageMath.CoverageOf(entries, JobCatalog),
                AutoAssign = autoAssign,
                Blocker = blocker,
                Unskilled = unskilled,
                Hunting = workTypes.Contains("Hunting"),
                TrainSkill = trainSkill,
                TrainMin = int.TryParse(def.Element("trainMinLevel")?.Value, out var mn) ? mn : 0,
                TrainMax = int.TryParse(def.Element("trainMaxLevel")?.Value, out var mx) ? mx : 0,
                MinHolders = int.TryParse(def.Element("minHolders")?.Value, out var mnh) ? mnh : -1,
                NaturalPriority = workTypes
                    .Select(wt => VanillaWorkOrder.NaturalPriority.TryGetValue(wt, out var np) ? np : 0)
                    .DefaultIfEmpty(0).Max(),
                Enabled = true,
                Gated = trainSkill != null,
            };
            rec.WorkTypes.AddRange(workTypes);
            catalog.Recs.Add(rec);
            trainTargetNames[id] = def.Element("trainTargets")?.Elements("li")
                .Select(li => li.Value.Trim()).ToList() ?? new List<string>();

            catalog.Targets.Add(new TargetRole
            {
                Id = id,
                AutoAssign = autoAssign,
                HasRules = false,
                Blocker = blocker,
                Unskilled = unskilled,
                Doctoring = workTypes.Contains("Doctor") && !blocker,
                NaturalPriority = autoAssign ? 100f : 0f,
                Coverage = CoverageMath.CoverageOf(entries, JobCatalog),
                OrderedCoverage = CoverageMath.OrderedCoverageOf(entries, JobCatalog),
            });

            catalog.DefNames[id] = defName;
            if (defName == "WS_Hunter") catalog.HunterId = id;
            if (defName == "WS_Doctor") catalog.DoctorId = id;
            if (defName == "WS_Medic") catalog.MedicId = id;
            if (defName == "WS_NoFirefighting") catalog.FireBlockerId = id;
            id++;
        }
        // Train targets resolve after every def has landed.
        var idByDef = catalog.DefNames.ToDictionary(kv => kv.Value, kv => kv.Key);
        foreach (var rec in catalog.Recs)
            if (trainTargetNames.TryGetValue(rec.Id, out var names))
                rec.TrainTargets.AddRange(names
                    .Where(idByDef.ContainsKey).Select(n => idByDef[n]));
        foreach (var targetRole in catalog.Targets)
        {
            var rec = catalog.Recs.First(r => r.Id == targetRole.Id);
            targetRole.TrainTargets.AddRange(rec.TrainTargets);
        }

        // The REAL derivation — no mirror: the game adapter calls the same
        // Core function on the same projection.
        catalog.OrderTemplate = RecommendationOrder.DeriveTemplate(catalog.Recs);

        for (int rank = 0; rank < Essentials.Length; rank++)
            foreach (var kv in catalog.DefNames)
                if (kv.Value == Essentials[rank].template)
                    catalog.EssentialRank[kv.Key] = rank;
        return catalog;
    }

    // Shared with the quality-certification suite and the diagnostic dump.
    internal static Catalog ShippedCatalog() => Shipped();

    internal static (Catalog catalog, List<PlanPawn> pawns, ColonyPlanResult colony,
        List<List<PlannedAssignment>> targets, List<List<Recommendation>> recs)
        ExecutePipeline(int size, int seed)
    {
        var catalog = Shipped();
        var pawns = Colony(size, seed);
        var skillMax = SkillMax(pawns);
        var colony = ColonyPlanner.Compute(catalog.Recs, pawns, skillMax, SkillsByWorkType,
            catalog.EssentialRank, catalog.HunterId, catalog.DoctorId, catalog.MedicId,
            catalog.FireBlockerId);
        var positions = RecommendationOrder.PositionsFor(catalog.Recs, catalog.OrderTemplate);
        var targets = new List<List<PlannedAssignment>>();
        var recs = new List<List<Recommendation>>();
        for (int i = 0; i < pawns.Count; i++)
        {
            var pawnRecs = RecommendationEngine.Compute(
                catalog.Recs, pawns[i].Rec, skillMax, SkillsByWorkType, catalog.OrderTemplate);
            recs.Add(pawnRecs);
            targets.Add(TargetPlanner.Build(pawns[i].Existing, catalog.Targets,
                pawnRecs.Select(r => r.RoleId).ToList(),
                colony.VirtualSets[i], positions,
                colony.HunterTiers[i], catalog.HunterId));
        }
        return (catalog, pawns, colony, targets, recs);
    }

    // ----- seeded colony generation -----

    private static readonly string[] AllSkills =
    {
        "Medicine", "Cooking", "Construction", "Plants", "Mining", "Crafting",
        "Artistic", "Intellectual", "Shooting", "Animals", "Social",
    };

    private static List<PlanPawn> Colony(int size, int seed)
    {
        var rand = new Random(seed);
        var allTypes = SkillsByWorkType.Keys
            .Concat(new[] { "Hauling", "Cleaning", "Firefighter", "Patient", "PatientBedRest", "BasicWorker" })
            .Distinct().ToArray();
        var pawns = new List<PlanPawn>();
        for (int i = 0; i < size; i++)
        {
            var pawn = new PlanPawn();
            foreach (var skill in AllSkills)
            {
                pawn.Rec.SkillLevels[skill] = rand.Next(0, 15);
                int roll = rand.Next(100);
                pawn.Rec.PassionScores[skill] = roll < 8 ? 2 : roll < 28 ? 1 : 0;
                if (rand.Next(100) < 4) pawn.Rec.ExpertiseSkills.Add(skill);
            }
            pawn.Rec.CapableWorkTypes.UnionWith(allTypes);
            if (rand.Next(100) < 10) // incapable of violence
                pawn.Rec.CapableWorkTypes.Remove("Hunting");
            if (rand.Next(100) < 10) // incapable of dumb labor
                pawn.Rec.CapableWorkTypes.ExceptWith(new[] { "Hauling", "Cleaning", "PlantCutting" });
            if (rand.Next(100) < 8) // incapable of caring
                pawn.Rec.CapableWorkTypes.ExceptWith(new[] { "Doctor", "Childcare" });
            pawn.Rec.HasRangedWeapon = rand.Next(100) < 40;
            pawn.Rec.ShootingLevel = pawn.Rec.SkillLevels["Shooting"];
            pawn.FireFear = rand.Next(100) < 5;
            pawns.Add(pawn);
        }
        return pawns;
    }

    private static Dictionary<string, int> SkillMax(List<PlanPawn> pawns)
    {
        var max = new Dictionary<string, int>();
        foreach (var pawn in pawns)
            foreach (var kv in pawn.Rec.SkillLevels)
                if (!max.TryGetValue(kv.Key, out var m) || kv.Value > m)
                    max[kv.Key] = kv.Value;
        return max;
    }

    // ----- pipeline execution (mirrors BuildColonyFixPlan's Core calls) -----

    private sealed class Run
    {
        public Catalog Catalog;
        public List<PlanPawn> Pawns;
        public ColonyPlanResult Colony;
        public List<List<PlannedAssignment>> Targets = new();
    }

    private static Run Execute(int size, int seed)
    {
        var catalog = Shipped();
        var pawns = Colony(size, seed);
        var skillMax = SkillMax(pawns);
        var colony = ColonyPlanner.Compute(catalog.Recs, pawns, skillMax, SkillsByWorkType,
            catalog.EssentialRank, catalog.HunterId, catalog.DoctorId, catalog.MedicId,
            catalog.FireBlockerId);
        var run = new Run { Catalog = catalog, Pawns = pawns, Colony = colony };
        var positions = RecommendationOrder.PositionsFor(catalog.Recs, catalog.OrderTemplate);
        for (int i = 0; i < pawns.Count; i++)
        {
            var recs = RecommendationEngine
                .Compute(catalog.Recs, pawns[i].Rec, skillMax, SkillsByWorkType, catalog.OrderTemplate)
                .Select(r => r.RoleId).ToList();
            run.Targets.Add(TargetPlanner.Build(pawns[i].Existing, catalog.Targets, recs,
                colony.VirtualSets[i], positions,
                colony.HunterTiers[i], catalog.HunterId));
        }
        return run;
    }

    private static RecRole RecOf(Run run, int id) => run.Catalog.Recs.First(r => r.Id == id);

    private static bool ProvidesDoctoring(Run run, int id) =>
        id == run.Catalog.DoctorId || id == run.Catalog.MedicId
        || (!RecOf(run, id).Blocker
            && CoverageMath.CoversOrMatches(RecOf(run, id).Coverage, RecOf(run, run.Catalog.DoctorId).Coverage));

    // ----- invariants -----

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task EssentialWorkIsCoveredWheneverAnyoneCan(int size, int seed)
    {
        var run = Execute(size, seed);
        foreach (var (workType, template) in Essentials)
        {
            int roleId = run.Catalog.DefNames.First(kv => kv.Value == template).Key;
            if (!run.Pawns.Any(p => p.Rec.CapableWorkTypes.Contains(workType))) continue;
            bool covered = run.Colony.VirtualSets.Any(ids => ids.Contains(roleId)
                || ids.Any(other => CoverageMath.MakesRedundant(
                    RecOf(run, other).Coverage, other, RecOf(run, roleId).Coverage, roleId)));
            await Assert.That(covered).IsTrue()
                .Because($"{template} uncovered (size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)] [Arguments(50, 66)]
    public async Task DealtRolesStayWithinTheCoverageCap(int size, int seed)
    {
        var run = Execute(size, seed);
        int cap = Math.Max(1, (size + 5) / 6);
        foreach (var role in run.Catalog.Recs)
        {
            // Special allocations have their own rules: hunters (every gun),
            // doctoring (redundancy floor), researcher (bench override).
            if (role.AutoAssign || role.Blocker || role.Unskilled || role.Hunting) continue;
            if (role.Id == run.Catalog.DoctorId || role.Id == run.Catalog.MedicId) continue;
            if (role.MinHolders >= 0) continue; // own colonist count
            int holders = run.Colony.VirtualSets.Count(ids => ids.Contains(role.Id));
            await Assert.That(holders <= cap).IsTrue()
                .Because($"{run.Catalog.DefNames[role.Id]} held by {holders} > cap {cap} (size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task DoctoringFloorHolds(int size, int seed)
    {
        var run = Execute(size, seed);
        int capable = run.Pawns.Count(p => p.Rec.CapableWorkTypes.Contains("Doctor"));
        int providers = run.Colony.VirtualSets.Count(ids => ids.Any(id => ProvidesDoctoring(run, id)));
        await Assert.That(providers >= Math.Min(2, capable)).IsTrue()
            .Because($"doctoring providers {providers} < min(2, capable {capable}) (size {size}, seed {seed})");
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task ArmedCapablePawnsHunt_OthersNever_OneTierZeroWhenAnyHunt(int size, int seed)
    {
        var run = Execute(size, seed);
        for (int i = 0; i < run.Pawns.Count; i++)
        {
            bool armedCapable = run.Pawns[i].Rec.HasRangedWeapon
                && run.Pawns[i].Rec.CapableWorkTypes.Contains("Hunting");
            if (armedCapable)
                await Assert.That(run.Colony.HunterTiers[i] >= 0).IsTrue()
                    .Because($"armed capable pawn {i} does not hunt (size {size}, seed {seed})");
            else
                await Assert.That(run.Colony.HunterTiers[i]).IsEqualTo(-1)
                    .Because($"pawn {i} hunts without gun/capability (size {size}, seed {seed})");
        }
        if (run.Colony.HunterTiers.Any(t => t >= 0))
            await Assert.That(run.Colony.HunterTiers.Any(t => t == 0)).IsTrue()
                .Because($"hunters exist but no tier 0 (size {size}, seed {seed})");
    }

    [Test]
    [Arguments(10, 33)] [Arguments(50, 44)] [Arguments(50, 66)]
    public async Task FireTerrorGetsTheBlocker_NobodyElseDoes(int size, int seed)
    {
        // The shipped catalog no longer seeds a fire blocker; the pass only
        // fires when a player-made one exists, and never grants without it.
        var run = Execute(size, seed);
        bool hasBlocker = run.Catalog.FireBlockerId != -1;
        for (int i = 0; i < run.Pawns.Count; i++)
        {
            await Assert.That(run.Colony.FireGranted[i])
                .IsEqualTo(run.Pawns[i].FireFear && hasBlocker)
                .Because($"fire blocker mismatch on pawn {i} (size {size}, seed {seed})");
            if (run.Pawns[i].FireFear && hasBlocker)
                await Assert.That(run.Colony.VirtualSets[i].Contains(run.Catalog.FireBlockerId)).IsTrue()
                    .Because($"fire-fearing pawn {i} lacks the blocker (size {size}, seed {seed})");
        }
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    [Arguments(10, 55)] [Arguments(50, 66)]
    public async Task NoGrantWithoutCapability_NoGateBreaks(int size, int seed)
    {
        var run = Execute(size, seed);
        var skillMax = SkillMax(run.Pawns);
        for (int i = 0; i < run.Pawns.Count; i++)
            foreach (var id in run.Colony.VirtualSets[i])
            {
                var role = RecOf(run, id);
                if (role.Blocker) continue; // vetoes need no capability
                if (role.WorkTypes.Count > 0)
                    await Assert.That(role.WorkTypes.Any(run.Pawns[i].Rec.CapableWorkTypes.Contains)).IsTrue()
                        .Because($"pawn {i} granted {run.Catalog.DefNames[id]} without capability (size {size}, seed {seed})");
                // The doctoring floor may deliberately waive gates; everything else must pass.
                if (role.Gated && id != run.Catalog.DoctorId && id != run.Catalog.MedicId)
                    await Assert.That(RecommendationEngine.PassesGates(role, run.Pawns[i].Rec, skillMax)).IsTrue()
                        .Because($"pawn {i} granted gated {run.Catalog.DefNames[id]} without passing (size {size}, seed {seed})");
            }
    }

    [Test]
    public async Task DefaultTemplateIsTheVanillaGridColumns()
    {
        // Pins the shipped default: one chip per vanilla-grid work column,
        // autos included at their priority slots (Core above Doctor, Basics
        // below). Hunting is dynamic; everything covered or specialized floats.
        var catalog = Shipped();
        var names = catalog.OrderTemplate.Select(id => catalog.DefNames[id]);
        await Assert.That(string.Join(",", names)).IsEqualTo(
            "WS_Core,WS_Doctor,WS_Basics,WS_Childminder,WS_Warden,WS_Handler,"
            + "WS_Cook,WS_Builder,WS_Farmer,WS_Miner,WS_Smith,WS_Tailor,"
            + "WS_Artist,WS_Crafter,WS_Fisher,WS_Grunt,WS_DarkStudier,WS_Researcher");
    }

    [Test]
    [Arguments(1, 11)] [Arguments(3, 22)] [Arguments(10, 33)] [Arguments(50, 44)]
    public async Task TargetsFollowTheTemplateOrder(int size, int seed)
    {
        // The plan's order IS the recommendation-order template — the single
        // ordering every surface shows; autos interleave by work priority
        // (Core above Doctor, Basics below). Hunter (tiers), doctoring
        // demotion and protected assignments are the only sanctioned
        // deviations.
        var run = Execute(size, seed);
        var positions = RecommendationOrder.PositionsFor(run.Catalog.Recs, run.Catalog.OrderTemplate);
        for (int i = 0; i < run.Targets.Count; i++)
        {
            var ids = run.Targets[i].Select(a => a.RoleId).ToList();
            bool Exempt(int id) => id == run.Catalog.HunterId
                || RecOf(run, id).Blocker || RecOf(run, id).HasRules
                || run.Pawns[i].Existing.Any(a => a.RoleId == id && a.Pinned);
            var plain = ids.Where(id => !Exempt(id)).ToList();
            for (int k = 1; k < plain.Count; k++)
                await Assert.That(positions[plain[k - 1]] <= positions[plain[k]]).IsTrue()
                    .Because($"{run.Catalog.DefNames[plain[k - 1]]} outranks "
                        + $"{run.Catalog.DefNames[plain[k]]} against the template "
                        + $"for pawn {i} (size {size}, seed {seed})");
        }
    }

}
