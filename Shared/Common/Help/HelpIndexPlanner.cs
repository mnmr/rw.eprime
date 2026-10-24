using System;
using System.Collections.Generic;

namespace RimShared.Common.Help
{
    /// <summary>
    /// One discovered help topic: its numeric order prefix, stable slug (the
    /// file name between the prefix and the extension, used for cross-links
    /// and tour progress), the file to load, and whether the file comes from
    /// the English fallback rather than the active language.
    /// </summary>
    public readonly struct HelpTopicEntry
    {
        public HelpTopicEntry(int order, string slug, string fileName,
            bool fromFallback)
            : this(order, slug, fileName, fromFallback, 0)
        {
        }

        public HelpTopicEntry(int order, string slug, string fileName,
            bool fromFallback, int sourceIndex)
        {
            Order = order;
            Slug = slug;
            FileName = fileName;
            FromFallback = fromFallback;
            SourceIndex = sourceIndex;
        }

        public int Order { get; }
        public string Slug { get; }
        public string FileName { get; }
        public bool FromFallback { get; }
        public int SourceIndex { get; }
    }

    /// <summary>A source's topic names, in descending translation priority.</summary>
    public readonly struct HelpTopicSource
    {
        public HelpTopicSource(IReadOnlyList<string> files, bool fromEnglish)
        {
            Files = files;
            FromEnglish = fromEnglish;
        }

        public IReadOnlyList<string> Files { get; }
        public bool FromEnglish { get; }
    }

    /// <summary>
    /// Pure planning of one chapter's topic list from the file names found in
    /// the active language's chapter folder and the English chapter folder.
    /// Valid names are "&lt;digits&gt;-&lt;slug&gt;.md"; anything else is
    /// ignored. Translated files win per slug, English-only topics fall back,
    /// and the result is ordered by (order, slug).
    /// </summary>
    public static class HelpIndexPlanner
    {
        public static HelpTopicEntry[] PlanChapter(IReadOnlyList<HelpTopicSource> sources)
        {
            var bySlug = new Dictionary<string, HelpTopicEntry>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
                Add(sources[i].Files, sources[i].FromEnglish, i, bySlug);
            var entries = new List<HelpTopicEntry>(bySlug.Count);
            foreach (var pair in bySlug) entries.Add(pair.Value);
            entries.Sort(CompareEntries);
            return entries.ToArray();
        }

        public static HelpTopicEntry[] PlanChapter(
            IReadOnlyList<string> languageFiles,
            IReadOnlyList<string> englishFiles)
        {
            return PlanChapter(new[]
            {
                new HelpTopicSource(languageFiles, false),
                new HelpTopicSource(englishFiles, true)
            });
        }

        private static void Add(IReadOnlyList<string> files,
            bool fromFallback, int sourceIndex, Dictionary<string, HelpTopicEntry> bySlug)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (!TryParseName(files[i], out int order, out string slug))
                    continue;
                if (bySlug.TryGetValue(slug, out HelpTopicEntry existing))
                {
                    // The active language always wins; within one source the
                    // lowest order keeps the slug.
                    bool sameSource = existing.SourceIndex == sourceIndex;
                    if (!sameSource || existing.Order <= order) continue;
                }
                bySlug[slug] = new HelpTopicEntry(
                    order, slug, files[i], fromFallback, sourceIndex);
            }
        }

        private static bool TryParseName(
            string fileName, out int order, out string slug)
        {
            order = 0;
            slug = "";
            const string Extension = ".md";
            if (fileName.Length <= Extension.Length
                || !fileName.EndsWith(Extension,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            int digits = 0;
            while (digits < fileName.Length && char.IsDigit(fileName[digits]))
                digits++;
            int slugStart = digits + 1;
            int slugLength = fileName.Length - Extension.Length - slugStart;
            if (digits == 0 || slugStart >= fileName.Length
                || fileName[digits] != '-' || slugLength <= 0)
                return false;
            if (!int.TryParse(fileName.Substring(0, digits), out order))
                return false;

            slug = fileName.Substring(slugStart, slugLength);
            return true;
        }

        private static int CompareEntries(HelpTopicEntry a, HelpTopicEntry b)
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0
                ? byOrder : string.CompareOrdinal(a.Slug, b.Slug);
        }
    }
}
