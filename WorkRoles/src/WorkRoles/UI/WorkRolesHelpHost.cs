using System;
using System.Collections.Generic;
using System.IO;
using RimShared.UiLib;
using Verse;
using Verse.Sound;

namespace WorkRoles.UI
{
    /// <summary>
    /// What WorkRoles supplies to the shared help content state: its content
    /// folder, chapters, the UI and language revisions its help caches have
    /// always keyed on, the @demo vignettes, and the per-player read-topic
    /// list in the mod settings.
    /// </summary>
    internal sealed class WorkRolesHelpHost : IHelpHost
    {
        internal static readonly WorkRolesHelpHost Instance = new WorkRolesHelpHost();

        private static readonly HelpChapter[] chapters =
        {
            new HelpChapter("start", "WR_HelpChapterStart"),
            new HelpChapter("1-basics", "WR_HelpChapterBasics"),
            new HelpChapter("3-editing", "WR_HelpChapterEditing"),
            new HelpChapter("4-organizing", "WR_HelpChapterOrganizing"),
            new HelpChapter("5-recommendations", "WR_HelpChapterRecommendations"),
            new HelpChapter("6-advanced", "WR_HelpChapterAdvanced"),
        };

        private static readonly string[] TourSlugs =
            WorkRoles.Core.Help.HelpTour.Slugs;

        private WorkRolesHelpHost()
        {
        }

        public string HelpRoot =>
            Path.Combine(WorkRolesMod.ContentRootDir, "Help");

        public string LogPrefix => "[WorkRoles]";

        public HelpChapter[] Chapters => chapters;

        public string ReloadLabelKey => "WR_HelpReload";

        public int UiMetricRevision => RimShared.UiLib.UiRevision.Current;

        public int LanguageRevision => LanguageChangeCoordinator.Revision;

        public IReadOnlyList<string> ReadTopicSlugs =>
            WorkRolesMod.Settings?.helpTopicsRead
                ?? (IReadOnlyList<string>)Array.Empty<string>();

        private HelpTour? tour;

        /// The guided tour on the Start chapter. Built on first use, which is
        /// the Help tab's first draw on the main thread, because it carries
        /// the medal texture.
        public HelpTour? Tour => tour ??= new HelpTour(TourSlugs,
            "WR_HelpTourHeader", "WR_HelpTourProgress",
            "WR_HelpTourComplete", "WR_HelpTourCompleteHint",
            WorkRolesTex.HelpMedal);

        /// <summary>The shared Help view calls this from the window's
        /// WindowUpdate (and on close) with the topics read since the last
        /// call. The first time the whole tour is read a quiet chime plays
        /// and the Start page shows the medal from then on; the settings file
        /// write itself is deferred outside OnGUI.</summary>
        public void PersistReadTopics(List<string> slugs)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            settings.helpTopicsRead.AddRange(slugs);
            if (!settings.helpTourCelebrated
                && TourComplete(settings.helpTopicsRead))
            {
                settings.helpTourCelebrated = true;
                // Once-per-settings event; the good-letter chime has no
                // SoundDefOf entry, so resolve it by name at this boundary.
                SoundDef.Named("LetterArrive_Good").PlayOneShotOnCamera();
            }
            WorkRolesGameComponent.RequestSettingsWrite();
        }

        public bool TryGetDemo(string name, out IHelpDemo? demo) =>
            HelpDemos.TryGet(name, out demo);

        private static bool TourComplete(List<string> read)
        {
            for (int i = 0; i < TourSlugs.Length; i++)
            {
                if (!read.Contains(TourSlugs[i])) return false;
            }
            return true;
        }
    }
}
