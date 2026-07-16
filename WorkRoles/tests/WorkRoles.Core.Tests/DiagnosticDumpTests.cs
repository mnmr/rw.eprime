using System.Text;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// TEMPORARY diagnostic: dumps the shipped pipeline's real output (template,
/// positions, per-pawn recommendations and plans) to a file for inspection.
public class DiagnosticDumpTests
{
    [Test]
    public async Task DumpPipeline()
    {
        var dump = new StringBuilder();
        var catalog = ColonyScenarioTests.ShippedCatalog();
        var positions = RecommendationOrder.PositionsFor(catalog.Recs, catalog.OrderTemplate);

        dump.AppendLine("== TEMPLATE ==");
        foreach (var id in catalog.OrderTemplate)
            dump.AppendLine($"  {catalog.DefNames[id]}");

        dump.AppendLine("== POSITIONS (all roles, sorted) ==");
        foreach (var kv in positions.OrderBy(kv => kv.Value))
        {
            var rec = catalog.Recs.First(r => r.Id == kv.Key);
            dump.AppendLine($"  {kv.Value,12}  {catalog.DefNames[kv.Key]}"
                + $"{(rec.AutoAssign ? " [auto]" : "")}{(rec.Blocker ? " [blocker]" : "")}"
                + $"{(rec.Hunting ? " [hunting]" : "")}"
                + $"{(rec.TrainTargets.Count > 0 ? " trains->" + string.Join("/", rec.TrainTargets.Select(t => catalog.DefNames[t])) : "")}"
                + $" min={rec.MinHolders} np={rec.NaturalPriority}");
        }

        var (_, pawns, colony, targets, recsPerPawn) = ColonyScenarioTests.ExecutePipeline(6, 7);
        for (int i = 0; i < pawns.Count; i++)
        {
            dump.AppendLine($"== PAWN {i} ==");
            dump.AppendLine("  skills: " + string.Join(" ", pawns[i].Rec.SkillLevels
                .Select(kv => $"{kv.Key}={kv.Value}{(pawns[i].Rec.PassionScores.TryGetValue(kv.Key, out var p) && p > 0 ? new string('*', p) : "")}")));
            dump.AppendLine("  recs:   " + string.Join(", ", recsPerPawn[i]
                .Select(r => $"{catalog.DefNames[r.RoleId]}({r.Reason})")));
            dump.AppendLine("  plan:   " + string.Join(", ", targets[i]
                .Select(a => catalog.DefNames[a.RoleId])));
        }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "diag.txt"), dump.ToString());
        await Assert.That(true).IsTrue();
    }
}
