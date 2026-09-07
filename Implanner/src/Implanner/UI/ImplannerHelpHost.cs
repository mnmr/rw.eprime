using System.Collections.Generic;
using System.IO;
using RimShared.UiLib;

namespace Implanner.UI
{
    /// <summary>
    /// What Implanner supplies to the shared Help tab: its content folder,
    /// chapters, the mod's own UI metric and language revisions, and the
    /// per-player read-topic list in the mod settings.
    /// </summary>
    internal sealed class ImplannerHelpHost : IHelpHost
    {
        internal static readonly ImplannerHelpHost Instance = new ImplannerHelpHost();

        private static readonly HelpChapter[] chapters =
        {
            new HelpChapter("1-basics", "IMP_HelpChapterBasics"),
            new HelpChapter("2-plans", "IMP_HelpChapterPlans"),
            new HelpChapter("3-automation", "IMP_HelpChapterAutomation"),
            new HelpChapter("4-sharing", "IMP_HelpChapterSharing"),
            new HelpChapter("5-mods", "IMP_HelpChapterMods"),
        };

        // Owner: process. Key: none. Value: the Help folder path, derived
        // once from the install directory (fixed for the process).
        // Dependencies: none. Refresh: never. Teardown: none.
        private string? helpRoot;

        private ImplannerHelpHost()
        {
        }

        public string HelpRoot =>
            helpRoot ??= Path.Combine(ImplannerMod.ContentRootDir, "Help");

        public string LogPrefix => "[Implanner]";

        public HelpChapter[] Chapters => chapters;

        public string ReloadLabelKey => "IMP_HelpReload";

        public int UiMetricRevision => UiVersion.Current;

        public int LanguageRevision => UiVersion.LanguageCurrent;

        public IReadOnlyList<string> ReadTopicSlugs =>
            ImplannerMod.Settings.helpTopicsRead;

        public void PersistReadTopics(List<string> slugs)
        {
            ImplannerMod.Settings.helpTopicsRead.AddRange(slugs);
            ImplannerMod.Instance.WriteSettings();
        }
    }
}
