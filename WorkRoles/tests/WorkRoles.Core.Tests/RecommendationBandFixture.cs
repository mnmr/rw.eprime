using System.Xml.Linq;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

internal static class RecommendationBandFixture
{
    internal sealed class Scenario
    {
        internal ColonyView Colony;
        internal Dictionary<int, string> RoleNames;
        internal Dictionary<int, string> PathNames;
    }

    private static readonly string[] AllSkills =
    {
        "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
        "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual",
    };

    internal static Scenario Build(int size, int seed)
    {
        ColonyScenarioTests.Catalog catalog = ColonyScenarioTests.ShippedCatalog();
        string defsDirectory = Path.Combine(RepoRoot(), "mod", "1.6", "Defs");
        Dictionary<string, HolderScale> scales = LoadScales(defsDirectory);
        List<XElement> roleDefs = XElement.Load(
            Path.Combine(defsDirectory, "Roles.xml"))
            .Elements("WorkRoles.RoleDef").ToList();
        var roleNames = new Dictionary<int, string>();
        for (int index = 0; index < catalog.Roles.Count; index++)
        {
            RoleView role = catalog.Roles[index];
            XElement definition = roleDefs[index];
            string label = definition.Element("label")?.Value.Trim()
                ?? catalog.DefNames[role.Id].Substring(3);
            roleNames[role.Id] = label;
            string scaleName = definition.Element("holderScale")?.Value.Trim();
            if (scaleName != null && scales.TryGetValue(scaleName, out HolderScale scale))
                role.Scale = scale;
            XElement minimum = definition.Element("minHolders");
            role.TrainingWaivers = int.TryParse(
                minimum?.Attribute("waivers")?.Value, out int waivers)
                ? waivers : 0;
            if (catalog.DefNames[role.Id] == "WS_DrugMaker")
                role.PrimarySkill = "Intellectual";
            if (role.PrimarySkill != null)
            {
                role.Skills.Clear();
                role.Skills.Add(new RoleSkillView
                {
                    SkillDefName = role.PrimarySkill,
                    Primary = true,
                    Required = true,
                    Importance = 1,
                    UsedJobs = 1,
                    TrainedJobs = 1,
                });
            }
        }

        var pathNames = new Dictionary<int, string>();
        List<XElement> pathDefs = XElement.Load(
            Path.Combine(defsDirectory, "TrainingPaths.xml"))
            .Elements("WorkRoles.TrainingPathDef").ToList();
        for (int index = 0; index < catalog.Paths.Count; index++)
            pathNames[catalog.Paths[index].Id] =
                pathDefs[index].Element("label")?.Value.Trim() ?? $"Path{index + 1}";

        int IdOf(string label) => roleNames.First(pair => pair.Value == label).Key;
        catalog.OrderTemplate = new[]
        {
            "Core", "Doctor", "Basics", "Caretaker", "Warden", "Handler",
            "Builder", "Cook", "Farmer", "Miner", "Tailor", "Smith", "Crafter",
            "Artist", "Fisher", "Grunt", "Researcher", "Anomalist",
        }.Select(IdOf).ToList();
        Anchor(catalog.Paths, pathNames, "Drug Maker", IdOf("Warden"), false);
        Anchor(catalog.Paths, pathNames, "Fabricator", IdOf("Warden"), false);
        Anchor(catalog.Paths, pathNames, "Builder", IdOf("Warden"), false);
        Anchor(catalog.Paths, pathNames, "Cook", IdOf("Handler"), false);
        Anchor(catalog.Paths, pathNames, "Socialist", IdOf("Basics"), false);
        Anchor(catalog.Paths, pathNames, "Handler", IdOf("Warden"), false);

        List<PawnView> pawns = GeneratePawns(size, seed);
        return new Scenario
        {
            Colony = ColonyScenarioTests.Build(catalog, pawns),
            RoleNames = roleNames,
            PathNames = pathNames,
        };
    }

    private static void Anchor(
        IReadOnlyList<PathView> paths,
        IReadOnlyDictionary<int, string> names,
        string pathName,
        int anchorRoleId,
        bool before)
    {
        PathView path = paths.First(candidate => names[candidate.Id] == pathName);
        path.AnchorRoleId = anchorRoleId;
        path.AnchorBefore = before;
    }

    private static List<PawnView> GeneratePawns(int count, int seed)
    {
        var random = new Random(seed);
        string[] allWorkTypes = ColonyScenarioTests.SkillsByWorkType.Keys
            .Concat(new[]
            {
                "Hauling", "Cleaning", "Firefighter", "Patient",
                "PatientBedRest", "BasicWorker",
            })
            .Distinct().ToArray();
        var pawns = new List<PawnView>();
        for (int pawnIndex = 0; pawnIndex < count; pawnIndex++)
        {
            var pawn = new PawnView();
            foreach (string skill in AllSkills)
            {
                pawn.SkillLevels[skill] = random.Next(0, 14);
                pawn.SignalBuckets[skill] = SignalBucket.Neutral;
            }
            string[] passionateSkills = AllSkills
                .OrderBy(_ => random.Next()).Take(4).ToArray();
            foreach (string skill in passionateSkills)
            {
                bool major = random.Next(2) == 0;
                pawn.SkillLevels[skill] = System.Math.Max(
                    pawn.SkillLevels[skill], random.Next(5, 15));
                pawn.SignalBuckets[skill] = major
                    ? SignalBucket.Great : SignalBucket.Strong;
            }
            string awfulSkill = AllSkills
                .Where(skill => !passionateSkills.Contains(skill))
                .OrderBy(_ => random.Next()).First();
            pawn.SignalBuckets[awfulSkill] = SignalBucket.Awful;
            pawn.CapableWorkTypes.UnionWith(allWorkTypes);
            pawn.HasRangedWeapon = false;
            pawn.FireFear = false;
            pawn.ShootingLevel = pawn.SkillLevels["Shooting"];
            pawns.Add(pawn);
        }
        return pawns;
    }

    private static Dictionary<string, HolderScale> LoadScales(string directory)
    {
        var result = new Dictionary<string, HolderScale>(StringComparer.Ordinal);
        foreach (XElement definition in XElement.Load(
                     Path.Combine(directory, "Scales.xml"))
                 .Elements("WorkRoles.ScaleDef"))
        {
            string label = definition.Element("label")?.Value.Trim();
            if (string.IsNullOrEmpty(label)) continue;
            var scale = new HolderScale
            {
                Name = label,
                Preset = true,
                RequiredTotals = HolderScaleCodec.DecodeRow(
                    definition.Element("min")?.Value, 0),
                TrainingWaivers = HolderScaleCodec.DecodeRow(
                    definition.Element("train")?.Value, 0),
                Max = HolderScaleCodec.DecodeRow(
                    definition.Element("max")?.Value, RoleHolderRange.Uncapped),
            };
            scale.Normalize();
            result[label] = scale;
        }
        return result;
    }

    private static string RepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "WorkRoles.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("repo root not found");
    }
}
