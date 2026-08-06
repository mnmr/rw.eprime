using System.Text;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Lab;

// Offline production-planner CLI. Current = RecommendationPlan.Build.
// The planner boundary and tuning snapshot leave room for a separate Future
// implementation later; no duplicate experimental path is maintained now.
// New defaults (2026-08-01): reordered template and re-anchored paths.
internal static partial class Program
{
    private static readonly RecommendationsTuningOptions Tuning =
        RecommendationsTuningOptions.Default;

    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length > 0 && args[0] == "order") { PrintDefaultOrder(); return; }
        bool baseline = args.Length > 0 && args[0] == "baseline";
        int offset = baseline ? 1 : 0;
        int seed = args.Length > offset ? int.Parse(args[offset]) : 23;
        int count = args.Length > offset + 1 ? int.Parse(args[offset + 1]) : 10;
        int? requiredTotalCap = args.Length > offset + 2
            ? Math.Max(0, int.Parse(args[offset + 2]))
            : null;
        var catalog = Shipped();
        if (requiredTotalCap.HasValue)
            ApplyRequiredTotalCap(catalog, count, requiredTotalCap.Value);
        var colonists = GenColonists(count, seed);
        var pawns = colonists.Select(c => c.Pawn).ToList();

        ColonyView colony = catalog.Projection.CreateColony(
            catalog.Template, pawns);
        RecommendationPlan.Build(colony, Tuning);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        RecommendationPlan plan = RecommendationPlan.Build(colony, Tuning);
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        if (baseline)
        {
            Console.WriteLine("Paths:");
            for (int pawnIndex = 0; pawnIndex < plan.PawnCount; pawnIndex++)
                Console.WriteLine(PathCell(catalog, plan, pawnIndex)
                    .Replace("*", string.Empty));
            Console.WriteLine("Roles:");
            for (int pawnIndex = 0; pawnIndex < plan.PawnCount; pawnIndex++)
                Console.WriteLine(string.Join(", ", Enumerable
                    .Range(0, plan.RoleCountAt(pawnIndex))
                    .Select(index => catalog.LabelOf(
                        plan.RoleAt(pawnIndex, index)))));
            return;
        }

        Console.WriteLine($"Seed {seed}, {count} colonists, shipped defs + new defaults, "
            + "full colony pipelines"
            + (requiredTotalCap.HasValue
                ? $", required total cap {requiredTotalCap.Value}."
                : "."));
        Console.WriteLine();
        Console.WriteLine("Recommendation order: "
            + string.Join(", ", catalog.Template.Select(catalog.LabelOf)));
        Console.WriteLine();
        var rows = new List<string[]>(count);
        for (int i = 0; i < count; i++)
        {
            string passions = string.Join(", ", colonists[i].Passions
                .Select(p => $"{p.skill} {p.level}{(p.major ? "★★" : "★")}"));
            passions += $", {colonists[i].AwfulSkill} awful";
            string skills = string.Join(" ", AllSkills
                .Select(s => $"{SkillAbbrev[s]}{colonists[i].Pawn.SkillLevels[s]}"));
            int[] plannedRoleIds = Enumerable
                .Range(0, plan.RoleCountAt(i))
                .Select(index => plan.RoleAt(i, index))
                .ToArray();
            rows.Add(new[]
            {
                $"C{i + 1}", passions, skills, PathCell(catalog, plan, i),
                string.Join(", ", plannedRoleIds.Select(catalog.LabelOf)),
            });
        }
        PrintTable(
            new[] { "#", "Passions", "Skills", "Training", "Current" },
            new[] { 3, 30, 28, 29, 59 },
            rows);
        Console.WriteLine();
        PrintTargetRoleTable(catalog, colony, plan);
        Console.WriteLine();
        Console.WriteLine($"Planned core: {stopwatch.Elapsed.TotalMilliseconds:F3} ms, "
            + $"{allocated:N0} allocated bytes after warm-up.");
    }
}
