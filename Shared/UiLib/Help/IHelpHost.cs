using System.Collections.Generic;
using UnityEngine;

namespace RimShared.UiLib
{
    /// <summary>
    /// An embedded help-page demo: a fixed-size, self-animating vignette.
    /// Draw is called on every window pass with the reserved rect; demos are
    /// stateless presentations driven by the realtime clock, mutate nothing,
    /// and allocate nothing per frame.
    /// </summary>
    public interface IHelpDemo
    {
        Vector2 Size { get; }
        void Draw(Rect rect);
    }

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
        /// it. The shared view calls it from WindowUpdate or window close,
        /// never from a render pass; one call per batch.</summary>
        void PersistReadTopics(List<string> slugs);

        /// <summary>Resolves an "@demo:name" block to the mod's embedded
        /// demo. False skips the block in the layout. Called only while a
        /// draw model is built behind the draw-model cache gate, never per
        /// steady render pass.</summary>
        bool TryGetDemo(string name, out IHelpDemo? demo);

        /// <summary>The guided tour shown on the first chapter's Start page,
        /// or null for a plain first chapter. First read on the main thread
        /// while drawing.</summary>
        HelpTour? Tour { get; }
    }

    /// <summary>
    /// An optional guided tour. With one, the first chapter becomes a Start
    /// page: that chapter's first topic as the welcome text, a checklist of
    /// the tour topics with a progress bar and, once all are read, a medal;
    /// the selected tour topic renders beside it, so the tour completes
    /// without leaving Start. Slugs that do not resolve are skipped.
    /// </summary>
    public sealed class HelpTour
    {
        public HelpTour(string[] slugs, string headerKey, string progressKey,
            string completeKey, string completeHintKey, Texture2D medal)
        {
            Slugs = slugs;
            HeaderKey = headerKey;
            ProgressKey = progressKey;
            CompleteKey = completeKey;
            CompleteHintKey = completeHintKey;
            Medal = medal;
        }

        /// <summary>Tour topics in reading order.</summary>
        public string[] Slugs { get; }
        public string HeaderKey { get; }
        /// <summary>Takes the read count and the tour length.</summary>
        public string ProgressKey { get; }
        public string CompleteKey { get; }
        public string CompleteHintKey { get; }
        public Texture2D Medal { get; }
    }
}
