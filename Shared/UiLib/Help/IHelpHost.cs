using System.Collections.Generic;

namespace RimShared.UiLib
{
    /// <summary>One fixed help chapter: its content folder under
    /// <c>Help/&lt;Language&gt;/</c> and the translation key of its label.</summary>
    public readonly struct HelpChapter
    {
        public HelpChapter(string folder, string labelKey)
        {
            Folder = folder;
            LabelKey = labelKey;
        }

        public string Folder { get; }
        public string LabelKey { get; }
    }

    /// <summary>
    /// What a mod supplies to the shared Help tab: where its content lives,
    /// which chapters exist, the revisions its caches must observe, and the
    /// per-player store for topics already read. Revisions are the mod's own
    /// UI metric and language stamps, so the shared caches follow the same
    /// invalidation the rest of the mod uses.
    /// </summary>
    public interface IHelpHost
    {
        /// <summary>Directory holding <c>&lt;Language&gt;/</c> chapter folders
        /// and the <c>Images/</c> folder.</summary>
        string HelpRoot { get; }

        /// <summary>Prefix for log warnings, e.g. "[Implanner]".</summary>
        string LogPrefix { get; }

        /// <summary>Chapters in display order. The array is immutable.</summary>
        HelpChapter[] Chapters { get; }

        /// <summary>Translation key of the dev-mode Reload button.</summary>
        string ReloadLabelKey { get; }

        /// <summary>Advances when UI scale, tiny-font preference, or language
        /// change; measurement and draw-model caches key on it.</summary>
        int UiMetricRevision { get; }

        /// <summary>Advances on language change only.</summary>
        int LanguageRevision { get; }

        /// <summary>Slugs this player has already opened, as persisted.</summary>
        IReadOnlyList<string> ReadTopicSlugs { get; }

        /// <summary>Appends newly read slugs to the persisted list and writes
        /// it. Called from WindowUpdate or window close, never from a render
        /// pass; one call per batch.</summary>
        void PersistReadTopics(List<string> slugs);
    }
}
