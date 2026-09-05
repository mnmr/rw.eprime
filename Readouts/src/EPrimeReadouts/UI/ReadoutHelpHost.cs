using System.Collections.Generic;
using System.IO;
using RimShared.UiLib;

namespace EPrimeReadouts.UI
{
    /// What the shared Help tab needs from this mod: the content folder,
    /// the chapter list, the UI revisions its caches key on, and the
    /// per-player list of topics already read.
    internal sealed class ReadoutHelpHost : IHelpHost
    {
        internal static readonly ReadoutHelpHost Instance = new ReadoutHelpHost();

        private static readonly HelpChapter[] chapters =
        {
            new HelpChapter("1-basics", "EPR.HelpChapterBasics"),
            new HelpChapter("2-groups", "EPR.HelpChapterGroups"),
            new HelpChapter("3-pools", "EPR.HelpChapterPools"),
            new HelpChapter("4-options", "EPR.HelpChapterOptions"),
        };

        // Owner: process. Key: none. Value: the Help folder path, derived
        // once from the install directory (fixed for the process).
        // Dependencies: none. Refresh: never. Teardown: none.
        private string? helpRoot;

        private ReadoutHelpHost()
        {
        }

        public string HelpRoot =>
            helpRoot ??= Path.Combine(EPrimeReadoutsMod.ContentRootDir, "Help");

        public string LogPrefix => "[EPrimeReadouts]";

        public HelpChapter[] Chapters => chapters;

        public string ReloadLabelKey => "EPR.HelpReload";

        public int UiMetricRevision => UiVersion.Current;

        public int LanguageRevision => UiVersion.LanguageCurrent;

        public IReadOnlyList<string> ReadTopicSlugs =>
            EPrimeReadoutsMod.Settings.helpTopicsRead;

        public void PersistReadTopics(List<string> slugs)
        {
            EPrimeReadoutsMod.Persist(s => s.helpTopicsRead.AddRange(slugs));
        }
    }
}
