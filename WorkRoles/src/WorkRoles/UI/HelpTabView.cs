using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimShared.UiLib;

namespace WorkRoles.UI
{
    /// <summary>
    /// Help tab: a full-width chapter segmented control, a topic list on the
    /// left, and the rendered topic on the right. All content is loaded
    /// lazily by HelpContentState and rendered from its positioned draw
    /// model; the steady pass performs indexed draws only.
    /// </summary>
    public sealed class HelpTabView
    {
        private const float ChapterRowHeight = 28f;
        private const float TopicPanelWidth = 220f;
        private const float TopicRowHeight = 28f;
        private const float Gap = 8f;
        private const float TitleHeight = 30f;

        private static readonly Color LinkColor = new Color(0.45f, 0.7f, 1f);
        private static readonly Color LinkHoverColor =
            new Color(0.65f, 0.85f, 1f);
        private static readonly Color TopicSelectedFill =
            new Color(1f, 1f, 1f, 0.16f);

        /// Chapter selector accent carried by label color alone (fills stay
        /// the shared defaults): warm yellow for the active chapter, a
        /// matching pastel blue for the others.
        private static readonly SegmentedPalette ChapterPalette =
            new SegmentedPalette(
                fillActive: new Color(1f, 1f, 1f, 0.16f),
                fillInactive: new Color(1f, 1f, 1f, 0.04f),
                labelActive: new Color(1f, 0.95f, 0.6f),
                labelInactive: new Color(0.6f, 0.8f, 1f));

        private readonly HelpContentState content = new HelpContentState();

        private int activeChapter;   // Start hub greets first-time visitors
        private readonly string?[] selectedSlugs =
            new string?[HelpContentState.Chapters.Length];
        private Vector2 topicScroll;
        private Vector2 contentScroll;

        // Owner: window. Key: LanguageChangeCoordinator.Revision. Value:
        // translated chapter labels and the dev reload caption. Dependencies:
        // language only. Refresh: immediate on observed revision change.
        // Equality: matching revision reuses the array identity. Teardown:
        // ReleaseWindowData clears it; strings follow the window.
        private string[]? chapterLabels;
        private string reloadLabel = "";
        private string tourHeaderLabel = "";
        private string tourCompleteLabel = "";
        private string tourCompleteHintLabel = "";
        private int labelLanguageStamp = -1;

        /// The guided tour: essential topics in reading order. The Core
        /// content test validates that every slug exists in the shipped
        /// English content; an unresolved slug is skipped at runtime.
        private static readonly string[] TourSlugs =
            WorkRoles.Core.Help.HelpTour.Slugs;

        // Owner: window. Key: (LanguageChangeCoordinator.Revision,
        // HelpContentState.Generation). Value: resolved tour rows (topic
        // title, chapter index) and the formatted progress line.
        // Dependencies: loaded help content and language. Refresh: lazy when
        // either stamp moves or the read count changes (progress string).
        // Equality: unchanged stamps reuse the arrays. Teardown:
        // ReleaseWindowData clears them.
        private TourRow[]? tourRows;
        private int tourRowsLanguageStamp = -1;
        private int tourRowsGeneration = -1;
        private string tourProgressLabel = "";
        private int tourProgressReadCount = -1;

        // Owner: window. Key: none (mirror of the persisted settings list).
        // Value: fast lookup of read slugs. Dependencies: this view is the
        // only writer while the window is open. Refresh: rebuilt on first
        // use after ReleaseWindowData. Teardown: ReleaseWindowData clears.
        private HashSet<string>? readSlugs;

        private struct TourRow
        {
            public string Slug;
            public string Title;
            public int ChapterIndex;
        }

        public void Reset()
        {
            content.ObserveLanguage();
        }

        internal void ReleaseWindowData()
        {
            content.Release();
            chapterLabels = null;
            labelLanguageStamp = -1;
            tourRows = null;
            tourRowsLanguageStamp = -1;
            tourRowsGeneration = -1;
            tourProgressReadCount = -1;
            readSlugs = null;
            hubSelectedSlug = null;
            topicScroll = Vector2.zero;
            contentScroll = Vector2.zero;
        }

        internal void InvalidateLanguageCaches()
        {
            chapterLabels = null;
            labelLanguageStamp = -1;
            tourRows = null;
            tourRowsLanguageStamp = -1;
        }

        private string[] ChapterLabels()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (chapterLabels != null && labelLanguageStamp == revision)
                return chapterLabels;
            var labels = new string[HelpContentState.Chapters.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = HelpContentState.Chapters[i]
                    .LabelKey.Translate().ToString();
            reloadLabel = "WR_HelpReload".Translate().ToString();
            tourHeaderLabel = "WR_HelpTourHeader".Translate().ToString();
            tourCompleteLabel = "WR_HelpTourComplete".Translate().ToString();
            tourCompleteHintLabel =
                "WR_HelpTourCompleteHint".Translate().ToString();
            chapterLabels = labels;
            labelLanguageStamp = revision;
            return labels;
        }

        public void Draw(Rect rect)
        {
            content.ObserveLanguage();
            string[] labels = ChapterLabels();

            var chapterRow = new Rect(
                rect.x, rect.y, rect.width, ChapterRowHeight);
            if (Prefs.DevMode)
            {
                const float ReloadWidth = 70f;
                var reloadRect = new Rect(rect.xMax - ReloadWidth, rect.y,
                    ReloadWidth, ChapterRowHeight);
                chapterRow.width -= ReloadWidth + Gap;
                if (Widgets.ButtonText(reloadRect, reloadLabel))
                    content.Release();
            }
            int clicked = SegmentedControl.Row(
                chapterRow, labels, activeChapter, ChapterPalette);
            if (clicked >= 0 && clicked != activeChapter)
                SelectChapter(clicked);

            var body = new Rect(rect.x, rect.y + ChapterRowHeight + Gap,
                rect.width, rect.height - ChapterRowHeight - Gap);
            if (activeChapter == 0)
            {
                DrawStartHub(body);
                return;
            }
            var panelRect = new Rect(
                body.x, body.y, TopicPanelWidth, body.height);
            var contentRect = new Rect(panelRect.xMax + Gap, body.y,
                body.width - TopicPanelWidth - Gap, body.height);

            HelpTopicData[] topics = content.ChapterTopics(activeChapter);
            int selected = SelectedTopicIndex(topics);
            DrawTopicPanel(panelRect, topics, selected);
            if (selected >= 0)
                DrawTopic(contentRect, activeChapter, topics[selected]);
        }

        private const float TourRowHeight = 28f;
        private const float TourHeaderHeight = 30f;
        private const float TourProgressHeight = 22f;
        // Medal block: 40px medal, Medium title, then the hint wrapped over
        // up to two Small lines so the full sentence stays visible.
        private const float TourBadgeHeight = 84f;
        private const float HubPanelWidth = 470f;
        private static readonly Color MedalGold = new Color(1f, 0.84f, 0.35f);
        private static readonly Color TourChapterDim =
            new Color(1f, 1f, 1f, 0.45f);

        private string? hubSelectedSlug;

        /// <summary>The Start page: welcome text and the guided-tour
        /// checklist on the left, the selected tour topic rendered on the
        /// right so the whole tour completes without leaving Start.</summary>
        private void DrawStartHub(Rect rect)
        {
            var leftRect = new Rect(rect.x, rect.y, HubPanelWidth,
                rect.height);
            var rightRect = new Rect(leftRect.xMax + Gap, rect.y,
                rect.width - HubPanelWidth - Gap, rect.height);
            Rect inner = SegmentedControl.Panel(leftRect);

            HelpTopicData[] topics = content.ChapterTopics(0);
            float leftWidth = inner.width - 24f;
            HelpDrawModel? welcome = topics.Length > 0
                ? content.Model(0, topics[0], leftWidth) : null;

            TourRow[] rows = TourRowsFor();
            int readCount = 0;
            HashSet<string> read = ReadSlugs();
            for (int i = 0; i < rows.Length; i++)
            {
                if (read.Contains(rows[i].Slug)) readCount++;
            }
            bool complete = rows.Length > 0 && readCount == rows.Length;
            if (tourProgressReadCount != readCount)
            {
                tourProgressLabel = "WR_HelpTourProgress".Translate(
                    readCount, rows.Length).ToString();
                tourProgressReadCount = readCount;
            }

            // Default selection: the first unread tour topic, else the first.
            // LATCHED into hubSelectedSlug: displaying a topic marks it read,
            // so recomputing the default every frame would advance through
            // (and auto-complete) the whole tour one frame at a time.
            if (hubSelectedSlug == null && rows.Length > 0)
            {
                hubSelectedSlug = rows[0].Slug;
                for (int i = 0; i < rows.Length; i++)
                {
                    if (read.Contains(rows[i].Slug)) continue;
                    hubSelectedSlug = rows[i].Slug;
                    break;
                }
            }
            string? selectedSlug = hubSelectedSlug;

            float welcomeHeight = welcome?.Height ?? 0f;
            float tourTop = welcomeHeight + 16f;
            float tourHeight = TourHeaderHeight + TourProgressHeight + 8f
                + rows.Length * TourRowHeight
                + (complete ? TourBadgeHeight + 8f : 0f);
            var scrollOut = new Rect(inner.x + 8f, inner.y + 8f,
                inner.width - 16f, inner.height - 16f);
            bool scrollbar = tourTop + tourHeight > scrollOut.height;
            var viewRect = new Rect(0f, 0f,
                scrollOut.width - (scrollbar ? 16f : 0f),
                tourTop + tourHeight);
            Widgets.BeginScrollView(scrollOut, ref topicScroll, viewRect);
            if (welcome != null)
                DrawModelItems(welcome, topicScroll.y, scrollOut.height);

            WrText.HeaderLabel(new Rect(0f, tourTop, viewRect.width,
                TourHeaderHeight), tourHeaderLabel);
            DrawTourProgress(new Rect(0f, tourTop + TourHeaderHeight,
                viewRect.width, TourProgressHeight), readCount, rows.Length);

            float rowY = tourTop + TourHeaderHeight + TourProgressHeight + 8f;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = new Rect(0f, rowY + i * TourRowHeight,
                        viewRect.width, TourRowHeight);
                    bool isSelected = rows[i].Slug == selectedSlug;
                    if (isSelected)
                        Widgets.DrawBoxSolid(row, TopicSelectedFill);
                    else
                        Widgets.DrawHighlightIfMouseover(row);
                    bool isRead = read.Contains(rows[i].Slug);
                    var checkRect = new Rect(row.x + 4f,
                        row.y + (TourRowHeight - 20f) / 2f, 20f, 20f);
                    // Unread is an empty box, deliberately: the vanilla off
                    // texture is a red X and reads as failure.
                    if (isRead)
                        GUI.DrawTexture(checkRect, Widgets.CheckboxOnTex);
                    else
                        Widgets.DrawBoxSolidWithOutline(checkRect,
                            new Color(0f, 0f, 0f, 0.35f),
                            SegmentedControl.PanelOutline);
                    var labelRect = new Rect(row.x + 32f, row.y,
                        row.width - 190f, row.height);
                    Widgets.Label(labelRect, rows[i].Title);
                    GUI.color = TourChapterDim;
                    var chapterRect = new Rect(row.xMax - 154f, row.y,
                        150f, row.height);
                    Widgets.Label(chapterRect,
                        chapterLabels![rows[i].ChapterIndex]);
                    GUI.color = oldColor;
                    if (Widgets.ButtonInvisible(row)
                        && rows[i].Slug != selectedSlug)
                    {
                        hubSelectedSlug = rows[i].Slug;
                        contentScroll = Vector2.zero;
                    }
                }

                if (complete)
                {
                    float badgeY = rowY + rows.Length * TourRowHeight + 8f;
                    var medalRect = new Rect(4f, badgeY + 8f, 40f, 40f);
                    GUI.color = MedalGold;
                    GUI.DrawTexture(medalRect, WorkRolesTex.HelpMedal);
                    GUI.color = oldColor;
                    Text.Font = GameFont.Medium;
                    Widgets.Label(new Rect(56f, badgeY, viewRect.width - 56f,
                        32f), tourCompleteLabel);
                    Text.Font = GameFont.Small;
                    GUI.color = TourChapterDim;
                    Text.WordWrap = true;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(56f, badgeY + 32f,
                        viewRect.width - 56f, 48f), tourCompleteHintLabel);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.WordWrap = false;
                    GUI.color = oldColor;
                }
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
                GUI.color = oldColor;
            }
            Widgets.EndScrollView();

            if (selectedSlug != null && content.TryFindTopic(selectedSlug,
                    out int chapterIndex, out int topicIndex))
                DrawTopic(rightRect, chapterIndex,
                    content.ChapterTopics(chapterIndex)[topicIndex]);
        }

        private void DrawTourProgress(Rect rect, int readCount, int total)
        {
            var barRect = new Rect(rect.x, rect.y + 4f, 220f, 12f);
            Widgets.DrawBoxSolidWithOutline(barRect,
                new Color(0f, 0f, 0f, 0.4f), SegmentedControl.PanelOutline);
            if (total > 0 && readCount > 0)
            {
                float fraction = (float)readCount / total;
                Widgets.DrawBoxSolid(new Rect(barRect.x + 1f, barRect.y + 1f,
                    (barRect.width - 2f) * fraction, barRect.height - 2f),
                    MedalGold);
            }
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(barRect.xMax + 10f, rect.y,
                    rect.width - barRect.width - 10f, rect.height),
                    tourProgressLabel);
            }
            finally
            {
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }

        private TourRow[] TourRowsFor()
        {
            int language = LanguageChangeCoordinator.Revision;
            if (tourRows != null && tourRowsLanguageStamp == language
                && tourRowsGeneration == content.Generation)
                return tourRows;
            var rows = new List<TourRow>(TourSlugs.Length);
            for (int i = 0; i < TourSlugs.Length; i++)
            {
                if (!content.TryFindTopic(TourSlugs[i],
                        out int chapterIndex, out int topicIndex))
                    continue;
                rows.Add(new TourRow
                {
                    Slug = TourSlugs[i],
                    Title = content.ChapterTopics(chapterIndex)[topicIndex]
                        .ListTitle,
                    ChapterIndex = chapterIndex,
                });
            }
            tourRows = rows.ToArray();
            tourRowsLanguageStamp = language;
            tourRowsGeneration = content.Generation;
            tourProgressReadCount = -1;   // reformat with fresh row count
            return tourRows;
        }

        private HashSet<string> ReadSlugs()
        {
            if (readSlugs != null) return readSlugs;
            readSlugs = new HashSet<string>(System.StringComparer.Ordinal);
            var settings = WorkRolesMod.Settings;
            if (settings != null)
            {
                for (int i = 0; i < settings.helpTopicsRead.Count; i++)
                    readSlugs.Add(settings.helpTopicsRead[i]);
            }
            return readSlugs;
        }

        /// <summary>Records that a topic was displayed. Persists per player;
        /// the first time the whole tour is read a quiet chime plays and the
        /// Start page shows the medal from then on.</summary>
        private void MarkTopicRead(string slug)
        {
            HashSet<string> read = ReadSlugs();
            if (!read.Add(slug)) return;
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            settings.helpTopicsRead.Add(slug);
            if (!settings.helpTourCelebrated && TourComplete(read))
            {
                settings.helpTourCelebrated = true;
                // Once-per-settings event; the good-letter chime has no
                // SoundDefOf entry, so resolve it by name at this boundary.
                SoundDef.Named("LetterArrive_Good").PlayOneShotOnCamera();
            }
            WorkRolesGameComponent.RequestSettingsWrite();
        }

        private static bool TourComplete(HashSet<string> read)
        {
            for (int i = 0; i < TourSlugs.Length; i++)
            {
                if (!read.Contains(TourSlugs[i])) return false;
            }
            return true;
        }

        private void SelectChapter(int chapterIndex)
        {
            activeChapter = chapterIndex;
            topicScroll = Vector2.zero;
            contentScroll = Vector2.zero;
        }

        private int SelectedTopicIndex(HelpTopicData[] topics)
        {
            if (topics.Length == 0) return -1;
            string? slug = selectedSlugs[activeChapter];
            if (slug != null)
            {
                for (int i = 0; i < topics.Length; i++)
                {
                    if (topics[i].Entry.Slug == slug) return i;
                }
            }
            selectedSlugs[activeChapter] = topics[0].Entry.Slug;
            return 0;
        }

        /// Already-read topics render a faint shade lighter than unread, like
        /// visited links; deliberately far from the disabled-grey look.
        private static readonly Color ReadTopicColor =
            new Color(0.70f, 0.70f, 0.70f);

        private void DrawTopicPanel(
            Rect rect, HelpTopicData[] topics, int selected)
        {
            Rect inner = SegmentedControl.Panel(rect);
            float viewHeight = topics.Length * TopicRowHeight;
            bool scrollbar = viewHeight > inner.height;
            var viewRect = new Rect(0f, 0f,
                inner.width - (scrollbar ? 16f : 0f), viewHeight);
            Widgets.BeginScrollView(inner, ref topicScroll, viewRect);
            HashSet<string> read = ReadSlugs();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                for (int i = 0; i < topics.Length; i++)
                {
                    var row = new Rect(0f, i * TopicRowHeight,
                        viewRect.width, TopicRowHeight);
                    if (i == selected)
                        Widgets.DrawBoxSolid(row, TopicSelectedFill);
                    else
                        Widgets.DrawHighlightIfMouseover(row);
                    var label = new Rect(row.x + 8f, row.y,
                        row.width - 12f, row.height);
                    if (read.Contains(topics[i].Entry.Slug))
                        GUI.color = ReadTopicColor;
                    Widgets.Label(label, topics[i].ListTitle);
                    GUI.color = oldColor;
                    if (Widgets.ButtonInvisible(row) && i != selected)
                    {
                        selectedSlugs[activeChapter] = topics[i].Entry.Slug;
                        contentScroll = Vector2.zero;
                    }
                }
            }
            finally
            {
                GUI.color = oldColor;
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
            Widgets.EndScrollView();
        }

        private void DrawTopic(Rect rect, int chapterIndex,
            HelpTopicData topic)
        {
            MarkTopicRead(topic.Entry.Slug);
            // Explicit render offset: HeaderLabel's calibrated bearing puts
            // the Medium glyph top ~4px above the panel frame line in this
            // context (verified in game at the shared 1.25-scale baseline).
            const float TitleTopOffset = 4f;
            var titleRect = new Rect(rect.x, rect.y + TitleTopOffset,
                rect.width, TitleHeight);
            WrText.HeaderLabel(titleRect, topic.ListTitle);

            var scrollOut = new Rect(rect.x, rect.y + TitleHeight + 4f,
                rect.width, rect.height - TitleHeight - 4f);
            float contentWidth = scrollOut.width - 20f;   // scrollbar + margin
            HelpDrawModel model = content.Model(
                chapterIndex, topic, contentWidth);
            var viewRect = new Rect(0f, 0f, contentWidth, model.Height);
            Widgets.BeginScrollView(scrollOut, ref contentScroll, viewRect);
            DrawModelItems(model, contentScroll.y, scrollOut.height);
            Widgets.EndScrollView();
        }

        private void DrawModelItems(
            HelpDrawModel model, float scrollY, float visibleHeight)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GameFont currentFont = Text.Font = GameFont.Small;
                float visibleTop = scrollY - TopicRowHeight;
                float visibleBottom = scrollY + visibleHeight + TopicRowHeight;
                for (int i = 0; i < model.Rects.Length; i++)
                {
                    Rect itemRect = model.Rects[i];
                    if (itemRect.yMax < visibleTop
                        || itemRect.y > visibleBottom)
                        continue;
                    byte kind = model.Kinds[i];
                    if (kind == HelpDrawModel.KindDemo)
                    {
                        model.Demos[i]?.Draw(itemRect);
                        continue;
                    }
                    if (kind == HelpDrawModel.KindImage)
                    {
                        Texture2D? texture = model.Images[i];
                        if (texture != null)
                            GUI.DrawTexture(itemRect, texture,
                                ScaleMode.StretchToFill);
                        continue;
                    }
                    if (model.Fonts[i] != currentFont)
                        currentFont = Text.Font = model.Fonts[i];
                    if (kind == HelpDrawModel.KindLink)
                    {
                        bool hover = Mouse.IsOver(itemRect);
                        GUI.color = hover ? LinkHoverColor : LinkColor;
                        Widgets.Label(itemRect, model.Texts[i]);
                        GUI.color = oldColor;
                        if (Widgets.ButtonInvisible(itemRect))
                            NavigateTo(model.Targets[i]);
                    }
                    else
                    {
                        Widgets.Label(itemRect, model.Texts[i]);
                    }
                }
            }
            finally
            {
                GUI.color = oldColor;
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }

        private void NavigateTo(string slug)
        {
            if (!content.TryFindTopic(slug,
                    out int chapterIndex, out int topicIndex))
                return;
            SelectChapter(chapterIndex);
            selectedSlugs[chapterIndex] =
                content.ChapterTopics(chapterIndex)[topicIndex].Entry.Slug;
        }
    }
}
