using System.Text;
using RimShared.Common.Help;

namespace RimShared.Common.Tests.Help;

public class HelpIndexPlannerTests
{
    [Test]
    public async Task OrdersByNumericPrefixThenSlugAndIgnoresMalformedNames()
    {
        var entries = HelpIndexPlanner.PlanChapter(
            ["10-priority-grid.md", "02-pinning.md", "02-assigning.md",
             "notes.txt", "readme.md", "-bad.md", "3-.md"],
            ["10-priority-grid.md", "02-pinning.md", "02-assigning.md"]);

        await Assert.That(Dump(entries)).IsEqualTo(
            "2 assigning 02-assigning.md\n" +
            "2 pinning 02-pinning.md\n" +
            "10 priority-grid 10-priority-grid.md");
    }

    [Test]
    public async Task TranslatedFilesWinAndEnglishOnlyTopicsFallBack()
    {
        var entries = HelpIndexPlanner.PlanChapter(
            ["01-roles.md", "05-ordering.md"],
            ["01-roles.md", "02-ordering.md", "03-states.md"]);

        await Assert.That(Dump(entries)).IsEqualTo(
            "1 roles 01-roles.md\n" +
            "3 states 03-states.md fallback\n" +
            "5 ordering 05-ordering.md");
    }

    [Test]
    public async Task DuplicateSlugsKeepTheLowestOrderEntry()
    {
        var entries = HelpIndexPlanner.PlanChapter(
            ["07-pinning.md", "02-pinning.md"],
            []);

        await Assert.That(Dump(entries)).IsEqualTo(
            "2 pinning 02-pinning.md");
    }

    private static string Dump(HelpTopicEntry[] entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(entry.Order).Append(' ').Append(entry.Slug)
                .Append(' ').Append(entry.FileName);
            if (entry.FromFallback) sb.Append(" fallback");
        }
        return sb.ToString();
    }
}
