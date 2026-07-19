using System.Text;

namespace WorkRoles.Core.Tests;

/// Emits the StaticColony pawn table (and the best-in-colony maxima as a
/// comment) to bin/.../static-colony.g.cs. To regenerate: run, then paste the
/// emitted table over StaticColony.Pawns.
public class StaticColonyGenerator
{
    private static readonly string[] Names =
    {
        "Alva", "Brik", "Cass", "Dolf", "Edda", "Finn", "Gwen", "Hark",
        "Iris", "Jorn", "Kaya", "Lund", "Mira", "Nils", "Osla", "Piet",
        "Quin", "Rask", "Sune", "Tova",
    };

    [Test]
    [Explicit]
    public async Task Generate()
    {
        var rand = new Random(20260716);
        var skills = StaticColony.Skills;

        // Exactly 80% armed: 16 gun slots over 20 pawns, shuffled.
        var guns = Enumerable.Repeat(true, 16).Concat(Enumerable.Repeat(false, 4))
            .OrderBy(_ => rand.Next()).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("    public static readonly PawnSpec[] Pawns =");
        sb.AppendLine("    {");
        var specs = new List<PawnSpec>();
        for (int p = 0; p < 20; p++)
        {
            // One pool per pawn (the exact sum of its 12 levels), spread by
            // random weights. Doubled weights make a generalist, squared a
            // specialist — chosen per pawn.
            int pool = rand.Next(30, 101);
            bool doubled = rand.Next(2) == 0;
            var weights = new long[skills.Length];
            long totalWeight = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                long roll = rand.Next(1, 101);
                weights[i] = doubled ? roll * 2 : roll * roll;
                totalWeight += weights[i];
            }
            var levels = new int[skills.Length];
            int assigned = 0;
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = (int)(pool * weights[i] / totalWeight);
                assigned += levels[i];
            }
            // Leftover points to the largest fractional remainders; clamp at
            // 20 and push overflow down the same order. Pool <= 100 always
            // fits under the 12 x 20 ceiling.
            var byRemainder = Enumerable.Range(0, levels.Length)
                .OrderByDescending(i => pool * weights[i] % totalWeight)
                .ThenBy(i => i).ToList();
            for (int k = 0; assigned < pool; k = (k + 1) % byRemainder.Count)
            {
                int i = byRemainder[k];
                if (levels[i] >= 20) continue;
                levels[i]++;
                assigned++;
            }
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] <= 20) continue;
                int overflow = levels[i] - 20;
                levels[i] = 20;
                for (int k = 0; overflow > 0; k = (k + 1) % byRemainder.Count)
                {
                    int j = byRemainder[k];
                    if (j == i || levels[j] >= 20) continue;
                    levels[j]++;
                    overflow--;
                }
            }

            // 4 attributes: no duplicate kind+skill, no contradictions on one
            // skill (both passion kinds, or liking AND hating it).
            var attributes = new List<PawnAttribute>();
            while (attributes.Count < 4)
            {
                var kind = (AttributeKind)rand.Next(0, 5);
                string skill = skills[rand.Next(skills.Length)];
                bool passionKind = kind == AttributeKind.BurningPassion || kind == AttributeKind.Passion;
                bool attitudeKind = kind == AttributeKind.Aptitude || kind == AttributeKind.Trait;
                if (attributes.Any(a => a.Kind == kind && a.Skill == skill)) continue;
                if (passionKind && attributes.Any(a =>
                        (a.Kind == AttributeKind.BurningPassion || a.Kind == AttributeKind.Passion)
                        && a.Skill == skill)) continue;
                if (attitudeKind && attributes.Any(a =>
                        (a.Kind == AttributeKind.Aptitude || a.Kind == AttributeKind.Trait)
                        && a.Skill == skill)) continue;
                attributes.Add(new PawnAttribute(kind, skill));
            }

            var spec = new PawnSpec(Names[p], levels, attributes.ToArray(), guns[p]);
            specs.Add(spec);

            sb.AppendLine($"        new PawnSpec(\"{spec.Name}\",");
            sb.AppendLine($"            new[] {{ {string.Join(", ", levels)} }},");
            sb.AppendLine("            new[]");
            sb.AppendLine("            {");
            foreach (var a in spec.Attributes)
                sb.AppendLine($"                new PawnAttribute(AttributeKind.{a.Kind}, \"{a.Skill}\"),");
            sb.AppendLine("            },");
            sb.AppendLine($"            {(spec.HasGun ? "true" : "false")}),");
        }
        sb.AppendLine("    };");

        sb.AppendLine();
        sb.AppendLine("    // Best-in-colony maxima for the table above:");
        foreach (var kv in StaticColony.BestInColony(specs))
            sb.AppendLine($"    //   {kv.Key} = {kv.Value}");

        string path = Path.Combine(AppContext.BaseDirectory, "static-colony.g.cs");
        File.WriteAllText(path, sb.ToString());
        await Assert.That(File.Exists(path)).IsTrue();
    }
}
