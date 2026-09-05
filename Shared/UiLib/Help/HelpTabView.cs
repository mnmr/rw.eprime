using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// <summary>
    /// Help tab: a full-width chapter segmented control, a topic list on the
    /// left, and the rendered topic on the right. All content is loaded
    /// lazily by HelpContentState and rendered from its positioned draw
    /// model; the steady pass performs indexed draws only. Content location,
    /// chapters, revisions, and read-topic persistence come from the host.
    /// </summary>
    public sealed class HelpTabView
    {
        private const float ChapterRowHeight = 28f;
        private const float TopicPanelWidth = 220f;
        private const float TopicRowHeight = 28f;
        private const float Gap = 8f;

        private static readonly Color LinkColor = new Color(0.45f, 0.7f, 1f);
        private static readonly Color LinkHoverColor =
            new Color(0.65f, 0.85f, 1f);
        private static readonly Color TopicSelectedFill =
            new Color(1f, 1f, 1f, 0.16f);

        /// Already-read topics render a faint shade lighter than unread, like
        /// visited links; deliberately far from the disabled-grey look.
        private static readonly Color ReadTopicColor =
            new Color(0.70f, 0.70f, 0.70f);

        /// Chapter selector accent carried by label color alone (fills stay
        /// the shared defaults): warm yellow for the active chapter, a
        /// matching pastel blue for the others.
        private static readonly SegmentedPalette ChapterPalette =
            new SegmentedPalette(
                fillActive: new Color(1f, 1f, 1f, 0.16f),
                fillInactive: new Color(1f, 1f, 1f, 0.04f),
                labelActive: new Color(1f, 0.95f, 0.6f),
                labelInactive: new Color(0.6f, 0.8f, 1f));

        private readonly IHelpHost host;
        private readonly HelpContentState content;

        private int activeChapter;
        private readonly string?[] selectedSlugs;
        private Vector2 topicScroll;
        private Vector2 contentScroll;

        // Owner: window. Key: the host's language revision. Value: translated
        // chapter labels and the dev reload caption. Dependencies: language
        // only. Refresh: immediate on observed revision change. Equality:
        // matching revision reuses the array identity. Teardown:
        // ReleaseWindowData clears it; strings follow the window.
        private string[]? chapterLabels;
        private string reloadLabel = "";
        private int labelLanguageStamp = -1;

        // Owner: window. Key: the host's UI metric revision. Value: the
        // ceiled Medium line height used for the topic title rect.
        // Dependencies: UI metric revision (scale, tiny-font preference,
        // language). Refresh: immediate on observed stamp change. Equality:
        // pure value cache. Teardown: none required (one float).
        private float titleHeight;
        private int titleHeightStamp = -1;

        // Owner: window. Key: none (mirror of the host's persisted list).
        // Value: fast lookup of read slugs. Dependencies: this view is the
        // only writer while the window is open. Refresh: rebuilt on first
        // use after ReleaseWindowData. Teardown: ReleaseWindowData clears.
        private HashSet<string>? readSlugs;

        // Owner: window. Key: the chapter's topic array identity plus the
        // read revision. Value: one bool per topic, true when read
        // (immutable once built). Dependencies: the topic array and the
        // read set. Refresh: immediate on the next read after either moves.
        // Equality: an unchanged key reuses the array. Teardown:
        // ReleaseWindowData clears.
        private bool[]? topicReadFlags;
        private HelpTopicData[]? topicReadFlagsTopics;
        private int topicReadFlagsRevision = -1;
        private int readRevision;

        /// Slugs marked read during drawing and not yet persisted: the host
        /// writes them from WindowUpdate (and on close), never inside a
        /// render pass, one write per batch.
        private List<string>? pendingReadSlugs;

        public HelpTabView(IHelpHost host)
        {
            this.host = host;
            content = new HelpContentState(host);
            selectedSlugs = new string?[host.Chapters.Length];
        }

        /// <summary>Call from the owning window's PreOpen.</summary>
        public void Reset()
        {
            content.ObserveLanguage();
        }

        /// <summary>Call from the owning window's close path, after
        /// FlushPendingWrites.</summary>
        public void ReleaseWindowData()
        {
            content.Release();
            chapterLabels = null;
            labelLanguageStamp = -1;
            titleHeightStamp = -1;
            readSlugs = null;
            topicReadFlags = null;
            topicReadFlagsTopics = null;
            topicReadFlagsRevision = -1;
            pendingReadSlugs = null;
            topicScroll = Vector2.zero;
            contentScroll = Vector2.zero;
        }

        private string[] ChapterLabels()
        {
            int revision = host.LanguageRevision;
            if (chapterLabels != null && labelLanguageStamp == revision)
                return chapterLabels;
            HelpChapter[] chapters = host.Chapters;
            var labels = new string[chapters.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = chapters[i].LabelKey.Translate().ToString();
            reloadLabel = host.ReloadLabelKey.Translate().ToString();
            chapterLabels = labels;
            labelLanguageStamp = revision;
            return labels;
        }

        private float TitleHeight()
        {
            int stamp = host.UiMetricRevision;
            if (titleHeightStamp != stamp)
            {
                titleHeight = Mathf.Ceil(Text.LineHeightOf(GameFont.Medium));
                titleHeightStamp = stamp;
            }
            return titleHeight;
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

        private void DrawTopicPanel(
            Rect rect, HelpTopicData[] topics, int selected)
        {
            Rect inner = SegmentedControl.Panel(rect);
            float viewHeight = topics.Length * TopicRowHeight;
            bool scrollbar = viewHeight > inner.height;
            var viewRect = new Rect(0f, 0f,
                inner.width - (scrollbar ? 16f : 0f), viewHeight);
            bool[] read = TopicReadFlags(topics);
            Widgets.BeginScrollView(inner, ref topicScroll, viewRect);
            try
            {
                using (GuiStateScope.Capture())
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.WordWrap = false;
                    Color plainColor = GUI.color;
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
                        GUI.color = read[i] ? ReadTopicColor : plainColor;
                        Widgets.Label(label, topics[i].ListTitle);
                        GUI.color = plainColor;
                        if (Widgets.ButtonInvisible(row) && i != selected)
                        {
                            selectedSlugs[activeChapter] = topics[i].Entry.Slug;
                            contentScroll = Vector2.zero;
                        }
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private HashSet<string> ReadSlugs()
        {
            if (readSlugs != null) return readSlugs;
            readSlugs = new HashSet<string>(System.StringComparer.Ordinal);
            IReadOnlyList<string> persisted = host.ReadTopicSlugs;
            for (int i = 0; i < persisted.Count; i++)
                readSlugs.Add(persisted[i]);
            return readSlugs;
        }

        private bool[] TopicReadFlags(HelpTopicData[] topics)
        {
            if (topicReadFlags == null
                || !ReferenceEquals(topicReadFlagsTopics, topics)
                || topicReadFlagsRevision != readRevision)
            {
                HashSet<string> read = ReadSlugs();
                var flags = new bool[topics.Length];
                for (int i = 0; i < topics.Length; i++)
                    flags[i] = read.Contains(topics[i].Entry.Slug);
                topicReadFlags = flags;
                topicReadFlagsTopics = topics;
                topicReadFlagsRevision = readRevision;
            }
            return topicReadFlags;
        }

        /// Records that a topic was displayed: the view's read set updates
        /// at once (the list tint follows this frame); the per-player
        /// settings write is deferred to FlushPendingWrites.
        private void MarkTopicRead(string slug)
        {
            HashSet<string> read = ReadSlugs();
            if (!read.Add(slug)) return;
            readRevision++;
            (pendingReadSlugs ??= new List<string>()).Add(slug);
        }

        /// Persists topics marked read since the last flush, one host write
        /// per batch. Call from the window's WindowUpdate and on close, never
        /// from a render pass.
        public void FlushPendingWrites()
        {
            List<string>? pending = pendingReadSlugs;
            if (pending == null || pending.Count == 0) return;
            host.PersistReadTopics(pending);
            pending.Clear();
        }

        private void DrawTopic(Rect rect, int chapterIndex,
            HelpTopicData topic)
        {
            MarkTopicRead(topic.Entry.Slug);
            float titleLineHeight = TitleHeight();
            var titleRect = new Rect(rect.x, rect.y,
                rect.width, titleLineHeight);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(titleRect, topic.ListTitle);
            }

            var scrollOut = new Rect(rect.x, rect.y + titleLineHeight + 4f,
                rect.width, rect.height - titleLineHeight - 4f);
            float contentWidth = scrollOut.width - 20f;   // scrollbar + margin
            HelpDrawModel model = content.Model(
                chapterIndex, topic, contentWidth);
            var viewRect = new Rect(0f, 0f, contentWidth, model.Height);
            Widgets.BeginScrollView(scrollOut, ref contentScroll, viewRect);
            try
            {
                DrawModelItems(model, contentScroll.y, scrollOut.height);
            }
            finally
            {
                Widgets.EndScrollView();
            }
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
