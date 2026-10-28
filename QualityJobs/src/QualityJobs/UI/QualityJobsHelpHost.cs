using System.Collections.Generic;
using System.IO;
using RimShared.UiLib;

namespace QualityJobs.UI
{
    internal sealed class QualityJobsHelpHost : IHelpHost
    {
        internal static readonly QualityJobsHelpHost Instance = new QualityJobsHelpHost();
        private static readonly HelpChapter[] chapters =
        {
            new HelpChapter("1-basics", "QJ_HelpChapterBasics"),
            new HelpChapter("2-crafting", "QJ_HelpChapterCrafting"),
            new HelpChapter("3-construction", "QJ_HelpChapterConstruction"),
            new HelpChapter("4-options", "QJ_HelpChapterOptions"),
        };

        // Owner: process. Key: fixed mod installation root. Value: immutable path.
        // Dependencies: ModContentPack.RootDir (fixed for this process). Refresh:
        // once on first Help use. Equality: retain the string. Teardown: process exit;
        // this holds no world, map, window, or owned resource.
        private string? helpRoot;
        public string HelpRoot => helpRoot ??= Path.Combine(QualityJobsMod.Instance.Content.RootDir, "Help");
        public string LogPrefix => "[Quality Jobs]";
        public HelpChapter[] Chapters => chapters;
        public string ReloadLabelKey => "QJ_HelpReload";
        public int UiMetricRevision => UiRevision.Current;
        public int LanguageRevision => UiRevision.LanguageCurrent;
        public IReadOnlyList<string> ReadTopicSlugs => QualityJobsMod.Settings.helpTopicsRead;
        public HelpTour? Tour => null;

        public void PersistReadTopics(List<string> slugs)
        {
            QualityJobsMod.Settings.helpTopicsRead.AddRange(slugs);
            QualityJobsMod.Instance.WriteSettings();
        }

        public bool TryGetDemo(string name, out IHelpDemo? demo)
        {
            demo = null;
            return false;
        }
    }
}
