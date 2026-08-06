using System.Text;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Lab;

internal static partial class Program
{
    private static void PrintTargetRoleTable(
        Catalog catalog,
        ColonyView colony,
        RecommendationPlan plan)
    {
        var facts = new EngineContext(colony);
        var scaling = new RecommendationScaling(Tuning);
        IReadOnlyDictionary<int, long> positions = facts.BasePositions();
        var targetRoles = new List<RoleView>(catalog.Roles);
        targetRoles.Sort((left, right) =>
        {
            int position = positions[left.Id].CompareTo(positions[right.Id]);
            return position != 0 ? position : left.Id.CompareTo(right.Id);
        });

        var rows = new List<string[]>();
        for (int roleIndex = 0; roleIndex < targetRoles.Count; roleIndex++)
        {
            RoleView role = targetRoles[roleIndex];
            if (!HasTargetPath(colony, role.Id)) continue;

            HolderRequirement requirement = scaling.Requirement(
                role, colony.Pawns.Count);
            int requiredTotal = requirement.RequiredTotal;
            int trainingWaivers = requirement.TrainingWaivers;
            int directMinimum = requirement.DirectMinimum;
            var directAssignments = new List<string>();
            var waiverAssignments = new List<string>();
            var surplusAssignments = new List<string>();

            for (int assignmentIndex = 0;
                 assignmentIndex < plan.TargetAssignmentCount;
                 assignmentIndex++)
            {
                RecommendationTargetAssignment assignment =
                    plan.TargetAssignmentAt(assignmentIndex);
                if (assignment.TargetRoleId != role.Id) continue;
                string formatted = FormatTargetAssignment(
                    catalog, assignment);
                switch (assignment.Kind)
                {
                    case RecommendationTargetAssignmentKind.Direct:
                        directAssignments.Add(formatted);
                        break;
                    case RecommendationTargetAssignmentKind.TrainingWaiver:
                        waiverAssignments.Add(formatted);
                        break;
                    case RecommendationTargetAssignmentKind.Surplus:
                        surplusAssignments.Add(formatted);
                        break;
                }
            }

            if (requiredTotal == 0
                && trainingWaivers == 0
                && directAssignments.Count == 0
                && waiverAssignments.Count == 0
                && surplusAssignments.Count == 0)
                continue;

            (int Priority, int PawnIndex) bestPriority = BestPriority(
                plan, role.Id);
            rows.Add(new[]
            {
                catalog.LabelOf(role.Id),
                requiredTotal.ToString(),
                trainingWaivers.ToString(),
                directMinimum.ToString(),
                FormatAssignments(directAssignments),
                FormatAssignments(waiverAssignments),
                FormatAssignments(surplusAssignments),
                bestPriority.Priority == int.MaxValue
                    ? "-"
                    : $"{bestPriority.Priority} (C{bestPriority.PawnIndex + 1})",
            });
        }

        Console.WriteLine("Target-role totals (Current):");
        PrintTable(
            new[]
            {
                "Role", "Required total", "Training waivers", "Direct minimum",
                "Direct assignments", "Training waiver assignments", "Surplus",
                "Best target pos",
            },
            new[] { 16, 14, 17, 15, 38, 46, 50, 15 },
            rows);
    }

    private static string FormatTargetAssignment(
        Catalog catalog,
        RecommendationTargetAssignment assignment)
    {
        var labels = new string[assignment.RoleCount];
        for (int index = 0; index < labels.Length; index++)
            labels[index] = catalog.LabelOf(assignment.RoleAt(index));
        return $"C{assignment.PawnIndex + 1}: " + string.Join("+", labels);
    }

    private static string FormatAssignments(List<string> assignments) =>
        assignments.Count == 0 ? "-" : string.Join(", ", assignments);

    private static bool HasTargetPath(
        ColonyView colony,
        int targetRoleId)
    {
        for (int pathIndex = 0; pathIndex < colony.Paths.Count; pathIndex++)
            if (PathActivation.UniqueTargetRoleId(colony.Paths[pathIndex])
                == targetRoleId)
                return true;
        return false;
    }

    private static (int Priority, int PawnIndex) BestPriority(
        RecommendationPlan plan,
        int targetRoleId)
    {
        int best = int.MaxValue;
        int bestPawnIndex = -1;
        bool bestIsDirect = false;
        for (int pawnIndex = 0; pawnIndex < plan.PawnCount; pawnIndex++)
            for (int roleIndex = 0;
                 roleIndex < plan.RoleCountAt(pawnIndex);
                 roleIndex++)
                if (plan.RoleAt(pawnIndex, roleIndex) == targetRoleId)
                {
                    int priority = roleIndex + 1;
                    bool direct = HasDirectTargetAssignment(
                        plan, targetRoleId, pawnIndex);
                    if (priority > best
                        || priority == best
                            && (bestIsDirect || !direct))
                        continue;
                    best = priority;
                    bestPawnIndex = pawnIndex;
                    bestIsDirect = direct;
                }
        return (best, bestPawnIndex);
    }

    private static bool HasDirectTargetAssignment(
        RecommendationPlan plan,
        int targetRoleId,
        int pawnIndex)
    {
        for (int index = 0; index < plan.TargetAssignmentCount; index++)
        {
            RecommendationTargetAssignment assignment =
                plan.TargetAssignmentAt(index);
            if (assignment.TargetRoleId == targetRoleId
                && assignment.PawnIndex == pawnIndex
                && assignment.Kind
                    == RecommendationTargetAssignmentKind.Direct)
                return true;
        }
        return false;
    }

    private static string PathCell(
        Catalog catalog, RecommendationPlan plan, int pawnIndex)
    {
        int count = plan.PathCountAt(pawnIndex);
        return count == 0 ? "-" : string.Join(", ", Enumerable
            .Range(0, count)
            .Select(index => catalog.PathNames[plan.PathAt(pawnIndex, index)]
                + (plan.PathActivatedAt(pawnIndex, index) ? "*" : string.Empty)));
    }

    /// The full default role order (template + natural fallback slots).
    private static void PrintDefaultOrder()
    {
        var catalog = Shipped();
        var positions = Ordering.BasePositions(catalog.Roles, catalog.Template);
        var templateIndex = new Dictionary<int, int>();
        for (int i = 0; i < catalog.Template.Count; i++)
            templateIndex[catalog.Template[i]] = i;
        var rows = new List<string[]>();
        int position = 1;
        foreach (var pair in positions.OrderBy(p => p.Value).ThenBy(p => p.Key))
        {
            string source = templateIndex.TryGetValue(pair.Key, out int slot)
                ? $"template slot {slot}"
                : "natural (unlisted)";
            rows.Add(new[]
            {
                (position++).ToString(), catalog.LabelOf(pair.Key), source,
                $"{pair.Value:N0}",
            });
        }
        PrintTable(
            new[] { "Pos", "Role", "Source", "Key" },
            new[] { 5, 24, 20, 16 },
            rows);
    }

    private static void PrintTable(
        string[] headers,
        int[] widths,
        IReadOnlyList<string[]> rows)
    {
        PrintBorder('┌', '┬', '┐', widths);
        PrintCells(headers, widths, centerAll: true);
        PrintBorder('├', '┼', '┤', widths);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var wrapped = new List<string>[widths.Length];
            int height = 1;
            for (int column = 0; column < widths.Length; column++)
            {
                wrapped[column] = Wrap(rows[rowIndex][column], widths[column]);
                height = Math.Max(height, wrapped[column].Count);
            }
            for (int line = 0; line < height; line++)
            {
                var cells = new string[widths.Length];
                for (int column = 0; column < widths.Length; column++)
                {
                    int topPadding = column == 0
                        ? (height - wrapped[column].Count) / 2
                        : 0;
                    int sourceLine = line - topPadding;
                    cells[column] = sourceLine >= 0
                        && sourceLine < wrapped[column].Count
                            ? wrapped[column][sourceLine]
                            : string.Empty;
                }
                PrintCells(cells, widths, centerAll: false);
            }
            if (rowIndex + 1 < rows.Count)
                PrintBorder('├', '┼', '┤', widths);
        }
        PrintBorder('└', '┴', '┘', widths);
    }

    private static void PrintBorder(
        char left,
        char join,
        char right,
        int[] widths)
    {
        Console.Write(left);
        for (int column = 0; column < widths.Length; column++)
        {
            if (column > 0) Console.Write(join);
            Console.Write(new string('─', widths[column] + 2));
        }
        Console.WriteLine(right);
    }

    private static void PrintCells(
        string[] cells,
        int[] widths,
        bool centerAll)
    {
        Console.Write('│');
        for (int column = 0; column < widths.Length; column++)
        {
            string value = centerAll || column == 0
                ? Center(cells[column], widths[column])
                : cells[column] + new string(
                    ' ', Math.Max(0, widths[column] - VisibleLength(cells[column])));
            Console.Write(' ');
            Console.Write(value);
            Console.Write(" │");
        }
        Console.WriteLine();
    }

    private static string Center(string value, int width)
    {
        int length = VisibleLength(value);
        int left = Math.Max(0, (width - length) / 2);
        return new string(' ', left)
            + value
            + new string(' ', Math.Max(0, width - length - left));
    }

    private static List<string> Wrap(string value, int width)
    {
        var result = new List<string>();
        var line = new StringBuilder(width);
        int lineWidth = 0;
        foreach (string word in value.Split(
                     new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int wordWidth = VisibleLength(word);
            if (lineWidth > 0 && lineWidth + 1 + wordWidth > width)
            {
                result.Add(line.ToString());
                line.Clear();
                lineWidth = 0;
            }
            if (lineWidth > 0)
            {
                line.Append(' ');
                lineWidth++;
            }
            line.Append(word);
            lineWidth += wordWidth;
        }
        if (line.Length > 0) result.Add(line.ToString());
        if (result.Count == 0) result.Add(string.Empty);
        return result;
    }

    private static int VisibleLength(string value)
    {
        int length = 0;
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '\u001b'
                && index + 1 < value.Length
                && value[index + 1] == '[')
            {
                index += 2;
                while (index < value.Length && value[index] != 'm') index++;
                continue;
            }
            length++;
        }
        return length;
    }
}
