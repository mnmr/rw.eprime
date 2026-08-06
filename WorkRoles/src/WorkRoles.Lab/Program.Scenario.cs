using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Lab.Data;

namespace WorkRoles.Lab;

internal static partial class Program
{
    private static readonly string[] AllSkills =
    {
        "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
        "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual",
    };

    private static readonly Dictionary<string, string> SkillAbbrev = new()
    {
        ["Shooting"] = "Sho", ["Melee"] = "Mel", ["Construction"] = "Con",
        ["Mining"] = "Min", ["Cooking"] = "Coo", ["Plants"] = "Pla",
        ["Animals"] = "Ani", ["Crafting"] = "Cra", ["Artistic"] = "Art",
        ["Medicine"] = "Med", ["Social"] = "Soc", ["Intellectual"] = "Int",
    };

    private sealed class Colonist
    {
        public PawnView Pawn;
        public List<(string skill, int level, bool major)> Passions = new();
        public string AwfulSkill;
    }

    private static List<Colonist> GenColonists(int count, int seed)
    {
        var rand = new Random(seed);
        var allTypes = VanillaJobSkillBaseline.Index.WorkTypes.Keys.ToArray();
        var colonists = new List<Colonist>();
        for (int i = 0; i < count; i++)
        {
            var colonist = new Colonist { Pawn = new PawnView() };
            foreach (var skill in AllSkills)
            {
                colonist.Pawn.SkillLevels[skill] = rand.Next(0, 14);
                colonist.Pawn.SignalBuckets[skill] = SignalBucket.Neutral;
            }
            string[] passionateSkills = AllSkills
                .OrderBy(_ => rand.Next())
                .Take(4)
                .ToArray();
            foreach (var skill in passionateSkills)
            {
                bool major = rand.Next(2) == 0;
                int level = Math.Max(colonist.Pawn.SkillLevels[skill], rand.Next(5, 15));
                colonist.Pawn.SkillLevels[skill] = level;
                colonist.Pawn.SignalBuckets[skill] = major ? SignalBucket.Great : SignalBucket.Strong;
                colonist.Passions.Add((skill, level, major));
            }
            colonist.AwfulSkill = AllSkills
                .Where(skill => !passionateSkills.Contains(skill))
                .OrderBy(_ => rand.Next())
                .First();
            colonist.Pawn.SignalBuckets[colonist.AwfulSkill] =
                SignalBucket.Awful;
            colonist.Passions = colonist.Passions
                .OrderByDescending(p => p.major).ThenByDescending(p => p.level).ToList();
            colonist.Pawn.CapableWorkTypes.UnionWith(allTypes);
            colonist.Pawn.HasRangedWeapon = false;
            colonist.Pawn.FireFear = false;
            colonist.Pawn.ShootingLevel = colonist.Pawn.SkillLevels["Shooting"];
            colonists.Add(colonist);
        }
        return colonists;
    }

    private static void ApplyRequiredTotalCap(
        Catalog catalog,
        int colonySize,
        int requiredTotalCap)
    {
        var scaling = new RecommendationScaling(Tuning);
        foreach (RoleView role in catalog.Roles)
        {
            HolderRequirement requirement = scaling.Requirement(role, colonySize);
            int directMinimum = Math.Min(
                requiredTotalCap,
                requirement.DirectMinimum);
            int trainingWaivers = Math.Min(
                requirement.TrainingWaivers,
                Math.Max(0, requiredTotalCap - directMinimum));
            int requiredTotal = directMinimum + trainingWaivers;
            int maximum = role.MaxHoldersAt(colonySize);
            var scale = new HolderScale
            {
                Name = $"WorkRoles.Lab cap {requiredTotalCap}",
            };
            Array.Fill(scale.RequiredTotals, requiredTotal);
            Array.Fill(scale.TrainingWaivers, trainingWaivers);
            Array.Fill(scale.Max, maximum);
            scale.Normalize();
            role.Scale = scale;
        }
    }
}
