using System;
using System.Collections.Generic;

namespace Implanner.Core.Help
{
    /// <summary>
    /// One discovered help topic: its numeric order prefix, stable slug (the
    /// file name between the prefix and the extension, used for
    /// cross-links), the file to load, and whether the file comes from the
    /// English fallback rather than the active language.
    /// </summary>
    public readonly struct HelpTopicEntry
    {
        public HelpTopicEntry(int order, string slug, string fileName,
            bool fromFallback)
        {
            Order = order;
            Slug = slug;
            FileName = fileName;
            FromFallback = fromFallback;
        }

        public int Order { get; }
        public string Slug { get; }
        public string FileName { get; }
        public bool FromFallback { get; }
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
        public static HelpTopicEntry[] PlanChapter(
            IReadOnlyList<string> languageFiles,
            IReadOnlyList<string> englishFiles)
        {
            var bySlug = new Dictionary<string, HelpTopicEntry>(
                StringComparer.Ordinal);
            Add(languageFiles, false, bySlug);
            Add(englishFiles, true, bySlug);

            var entries = new List<HelpTopicEntry>(bySlug.Count);
            foreach (var pair in bySlug) entries.Add(pair.Value);
            entries.Sort(CompareEntries);
            return entries.ToArray();
        }

        private static void Add(IReadOnlyList<string> files,
            bool fromFallback, Dictionary<string, HelpTopicEntry> bySlug)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (!TryParseName(files[i], out int order, out string slug))
                    continue;
                if (bySlug.TryGetValue(slug, out HelpTopicEntry existing))
                {
                    // The active language always wins; within one source the
                    // lowest order keeps the slug.
                    bool sameSource = existing.FromFallback == fromFallback;
                    if (!sameSource || existing.Order <= order) continue;
                }
                bySlug[slug] = new HelpTopicEntry(
                    order, slug, files[i], fromFallback);
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
