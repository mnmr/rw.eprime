using RimShared.Common.Help;

namespace RimShared.Tests.Support;

/// <summary>
/// Validates a mod's shipped English help content: every markdown file must
/// survive the topic naming plan and carry a front-matter title, every
/// referenced image must exist, and every topic link and required slug must
/// point at a slug that exists somewhere in the shipped set. Guards authors
/// against typos that would surface as broken pages in game. Test-only:
/// source-included into each mod's Core.Tests project, never shipped.
/// </summary>
internal static class HelpContentChecks
{
    /// <param name="root">The mod's mod/Help/English directory; null or
    /// missing fails with a clear message.</param>
    /// <param name="requiredSlugs">Slugs the mod links to from code (the
    /// WorkRoles tour); each must be a shipped topic.</param>
    internal static async Task VerifyEnglish(
        string? root, IEnumerable<string>? requiredSlugs = null)
    {
        await Assert.That(root != null && Directory.Exists(root))
            .IsTrue()
            .Because("the shipped English help content is missing: no "
                + "mod/Help/English directory was found ("
                + (root ?? "walking up from " + AppContext.BaseDirectory)
                + ")");

        var slugs = new HashSet<string>(StringComparer.Ordinal);
        var documents = new List<(string File, HelpDocument Document)>();
        foreach (string chapterDir in
            Directory.GetDirectories(root!).OrderBy(d => d))
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
        if (requiredSlugs != null)
        {
            foreach (string required in requiredSlugs)
            {
                await Assert.That(slugs.Contains(required))
                    .IsTrue()
                    .Because($"code references missing topic '{required}'");
            }
        }
        string imagesDir = Path.Combine(
            Path.GetDirectoryName(root!)!, "Images");
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

    /// <summary>Walks up from the test binary to the mod repo root and
    /// returns its mod/Help/English directory, or null when the shipped
    /// content does not exist (VerifyEnglish asserts with a clear message
    /// rather than throwing, so a missing content drop fails cleanly).</summary>
    internal static string? FindEnglishRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(
                dir.FullName, "mod", "Help", "English");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
