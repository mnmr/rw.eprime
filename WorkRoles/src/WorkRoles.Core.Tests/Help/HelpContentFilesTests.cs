using WorkRoles.Core.Help;

namespace WorkRoles.Core.Tests.Help;

/// <summary>
/// Validates the shipped English help content: every chapter file must carry
/// a front-matter title, and every topic link must point at a slug that
/// exists somewhere in the shipped set. Guards authors against typos that
/// would surface as broken pages in game.
/// </summary>
public class HelpContentFilesTests
{
    [Test]
    public async Task ShippedEnglishTopicsParseWithTitlesAndResolvableLinks()
    {
        string root = FindHelpRoot();
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        var documents = new List<(string File, HelpDocument Document)>();
        foreach (string chapterDir in
            Directory.GetDirectories(root).OrderBy(d => d))
        {
            string[] names = Directory.GetFiles(chapterDir, "*.md")
                .Select(Path.GetFileName).OfType<string>().ToArray();
            HelpTopicEntry[] plan = HelpIndexPlanner.PlanChapter(names, []);

            // Every markdown file must survive the naming plan; a skipped
            // file would silently vanish from the in-game topic list.
            await Assert.That(plan.Select(entry => entry.FileName).Order())
                .IsEquivalentTo(names.Order());

            foreach (HelpTopicEntry entry in plan)
            {
                slugs.Add(entry.Slug);
                string text = File.ReadAllText(
                    Path.Combine(chapterDir, entry.FileName));
                documents.Add((entry.FileName, HelpMarkdown.Parse(text)));
            }
        }

        await Assert.That(documents.Count).IsGreaterThan(0);
        foreach (string tourSlug in HelpTour.Slugs)
        {
            await Assert.That(slugs.Contains(tourSlug))
                .IsTrue()
                .Because($"tour references missing topic '{tourSlug}'");
        }
        string imagesDir = Path.Combine(
            Path.GetDirectoryName(root)!, "Images");
        foreach ((string file, HelpDocument document) in documents)
        {
            await Assert.That(document.Title)
                .IsNotEmpty()
                .Because($"{file} must declare a front-matter title");
            foreach (HelpBlock block in document.Blocks)
            {
                if (block.Kind == HelpBlockKind.Image)
                {
                    await Assert.That(File.Exists(
                            Path.Combine(imagesDir, block.ImagePath)))
                        .IsTrue()
                        .Because($"{file} references missing image "
                            + $"'{block.ImagePath}'");
                }
                foreach (HelpRun run in block.Runs)
                {
                    if (run.Kind == HelpRunKind.Image)
                    {
                        // "tex:" references borrow game textures and cannot
                        // be validated offline; a "|N" suffix scales height.
                        if (run.Target.StartsWith("tex:")) continue;
                        string imageFile = run.Target;
                        int split = imageFile.LastIndexOf('|');
                        if (split >= 0) imageFile = imageFile[..split];
                        await Assert.That(File.Exists(
                                Path.Combine(imagesDir, imageFile)))
                            .IsTrue()
                            .Because($"{file} references missing inline "
                                + $"image '{run.Target}'");
                        continue;
                    }
                    if (run.Kind != HelpRunKind.TopicLink) continue;
                    await Assert.That(slugs.Contains(run.Target))
                        .IsTrue()
                        .Because(
                            $"{file} links to unknown topic '{run.Target}'");
                }
            }
        }
    }

    private static string FindHelpRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(
                dir.FullName, "mod", "Help", "English");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "mod/Help/English not found above " + AppContext.BaseDirectory);
    }
}
