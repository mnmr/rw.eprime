using System;
using System.Collections.Generic;
using System.IO;
using RimShared.Common;
using RimShared.Common.Help;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// <summary>One loaded topic: plan entry, parsed document, list title.</summary>
    public sealed class HelpTopicData
    {
        internal HelpTopicData(HelpTopicEntry entry, HelpDocument document,
            string listTitle)
        {
            Entry = entry;
            Document = document;
            ListTitle = listTitle;
        }

        public HelpTopicEntry Entry { get; }
        public HelpDocument Document { get; }
        public string ListTitle { get; }
    }

    /// <summary>
    /// Fully positioned draw model for one topic at one width: parallel
    /// arrays the render pass iterates by index. Kinds: 0 text, 1 topic
    /// link, 2 list marker, 3 image, 4 demo. Texts carry final rich-text
    /// markup.
    /// </summary>
    public sealed class HelpDrawModel
    {
        internal Rect[] Rects = Array.Empty<Rect>();
        internal string[] Texts = Array.Empty<string>();
        internal byte[] Kinds = Array.Empty<byte>();
        internal GameFont[] Fonts = Array.Empty<GameFont>();
        internal string[] Targets = Array.Empty<string>();
        internal Texture2D?[] Images = Array.Empty<Texture2D?>();
        internal IHelpDemo?[] Demos = Array.Empty<IHelpDemo?>();
        internal float Height;

        internal const byte KindText = 0;
        internal const byte KindLink = 1;
        internal const byte KindMarker = 2;
        internal const byte KindImage = 3;
        internal const byte KindDemo = 4;
    }

    /// <summary>
    /// Measurement provider for help layout, backed by Text.CalcSize.
    /// </summary>
    // Owner: HelpContentState. Key: (font, style, word) for widths, (font,
    // style) for spaces, font for line heights. Value: measured floats
    // (immutable). Dependencies: the host's UI metric revision (UI scale,
    // tiny-font preference, and language advance it); the owning store
    // additionally drops the whole measurer on language change and window
    // close. Refresh: lazy re-measure after the stamp moves. Equality: pure
    // value cache. Teardown: Clear() from HelpContentState.Release().
    internal sealed class HelpTextMeasurer : IHelpTextMeasurer
    {
        private readonly IHelpHost host;
        private readonly Dictionary<(HelpFont, HelpRunStyle, string), float>
            wordWidths =
                new Dictionary<(HelpFont, HelpRunStyle, string), float>();
        private readonly Dictionary<(HelpFont, HelpRunStyle), float>
            spaceWidths = new Dictionary<(HelpFont, HelpRunStyle), float>();
        private readonly float[] lineHeights = new float[4];
        private bool lineHeightsBuilt;
        private int stamp = -1;

        internal HelpTextMeasurer(IHelpHost host)
        {
            this.host = host;
        }

        internal void Clear()
        {
            wordWidths.Clear();
            spaceWidths.Clear();
            lineHeightsBuilt = false;
            stamp = -1;
        }

        private void ObserveStamp()
        {
            int current = host.UiMetricRevision;
            if (stamp == current) return;
            stamp = current;
            wordWidths.Clear();
            spaceWidths.Clear();
            lineHeightsBuilt = false;
        }

        internal static GameFont GameFontOf(HelpFont font) =>
            font == HelpFont.H1 ? GameFont.Medium : GameFont.Small;

        /// <summary>Draw markup for a word: bold for H2/H3 headings and bold
        /// runs, italic preserved. The same markup is measured and drawn so
        /// widths always match the rendered glyphs.</summary>
        internal static string Markup(
            string word, HelpFont font, HelpRunStyle style)
        {
            bool bold = (style & HelpRunStyle.Bold) != 0
                || font == HelpFont.H2 || font == HelpFont.H3;
            bool italic = (style & HelpRunStyle.Italic) != 0;
            if (!bold && !italic) return word;
            if (bold && italic) return "<b><i>" + word + "</i></b>";
            return bold ? "<b>" + word + "</b>" : "<i>" + word + "</i>";
        }

        public float WordWidth(string word, HelpFont font, HelpRunStyle style)
        {
            ObserveStamp();
            var key = (font, style, word);
            if (wordWidths.TryGetValue(key, out float width)) return width;
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFontOf(font);
                string markup = Markup(word, font, style);
                // Synthesized bold/italic glyphs render a couple of pixels
                // wider than CalcSize reports; an exact-fit rect clips the
                // last glyph, so styled words take the fit width. CJK text
                // is measured one glyph at a time and the glyphs that share
                // a line merge into one drawn string, so a per-glyph
                // constant or ceil would pile up into a visible gap after
                // every styled run. Glyphs keep only the proportional
                // drift; the per-string constant is added to the draw rect
                // once in BuildDrawModel (StyledDrawSlack).
                if (ReferenceEquals(markup, word))
                    width = Text.CalcSize(markup).x;
                else if (word.Length == 1
                    && LineBreakRules.IsCharacterBreakable(word[0]))
                    width = Text.CalcSize(markup).x * 1.02f;
                else
                    width = WrText.MeasureFitWidth(markup);
            }
            finally
            {
                Text.Font = oldFont;
            }
            wordWidths[key] = width;
            return width;
        }

        public float SpaceWidth(HelpFont font, HelpRunStyle style)
        {
            ObserveStamp();
            var key = (font, style);
            if (spaceWidths.TryGetValue(key, out float width)) return width;
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFontOf(font);
                // CalcSize trims trailing spaces; the difference between a
                // spaced and an unspaced pair isolates one space advance.
                width = Mathf.Max(3f,
                    Text.CalcSize("x x").x - Text.CalcSize("xx").x);
            }
            finally
            {
                Text.Font = oldFont;
            }
            spaceWidths[key] = width;
            return width;
        }

        public float LineHeight(HelpFont font)
        {
            ObserveStamp();
            if (!lineHeightsBuilt)
            {
                GameFont oldFont = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    float small = Text.LineHeight;
                    Text.Font = GameFont.Medium;
                    float medium = Text.LineHeight;
                    lineHeights[(int)HelpFont.Body] = small;
                    lineHeights[(int)HelpFont.H1] = medium;
                    lineHeights[(int)HelpFont.H2] = small;
                    lineHeights[(int)HelpFont.H3] = small;
                }
                finally
                {
                    Text.Font = oldFont;
                }
                lineHeightsBuilt = true;
            }
            return lineHeights[(int)font];
        }
    }

    /// <summary>
    /// Lazily loaded help content: chapter topic lists, parsed documents,
    /// the selected topic's positioned draw model, and owned image textures.
    /// Nothing is read from disk until the Help tab is opened, and Release()
    /// returns the store to that empty state.
    /// </summary>
    // Owner: window (via HelpTabView). Key: chapter index for topic lists;
    // (chapter, slug, width, host UI metric revision) for the two LRU draw
    // models; image path for textures. Value: immutable HelpTopicData
    // arrays, two HelpDrawModels (demo instances borrowed from the host,
    // never owned), and textures (file-loaded ones owned and destroyed here;
    // "tex:" references borrowed from game content and never destroyed).
    // Dependencies: the host HelpRoot, fixed active mod list and their Help
    // folders (read on explicit user actions only), the host's language
    // revision, UI metric revision, content
    // width, and the host's fixed demo set. Refresh: lazy on first access
    // after a stamp mismatch; language change drops everything via
    // Release(), which advances Generation. Equality: unchanged stamps reuse
    // the cached objects by identity. Teardown: Release() destroys owned
    // textures and clears all caches (window close, language change, dev
    // reload).
    public sealed class HelpContentState
    {
        private readonly IHelpHost host;
        private readonly HelpTopicData[]?[] chapters;

        // Owner: window. Key: host HelpRoot and language revision. Value:
        // privately owned, immutable-after-publication priority-ordered topic
        // and image directories. Dependencies: selected language canonical/
        // legacy names, active mod order, package IDs, load folders, HelpRoot.
        // Refresh: first help access after language change or explicit Reload;
        // mod changes restart play data/windows. Equality: unchanged revision
        // preserves arrays. Teardown: Release clears both arrays on close,
        // language change and explicit Reload. No game/mod objects retained.
        private HelpSourceDirectory[]? sourceDirectories;
        private string[]? imageDirectories;

        private readonly struct HelpSourceDirectory
        {
            internal HelpSourceDirectory(string path, bool english)
            {
                Path = path;
                English = english;
            }

            internal string Path { get; }
            internal bool English { get; }
        }

        private readonly struct TextureEntry
        {
            internal TextureEntry(Texture2D? texture, bool owned)
            {
                Texture = texture;
                Owned = owned;
            }

            internal Texture2D? Texture { get; }
            /// <summary>False for game textures borrowed via ContentFinder
            /// ("tex:" paths); those are never destroyed by this store.</summary>
            internal bool Owned { get; }
        }

        private readonly Dictionary<string, TextureEntry> textures =
            new Dictionary<string, TextureEntry>(
                StringComparer.OrdinalIgnoreCase);
        private readonly HelpTextMeasurer measurer;
        private readonly HelpFlowLayout.ImageSizeResolver imageSizeResolver;
        private readonly HelpFlowLayout.DemoSizeResolver demoSizeResolver;
        private readonly Func<string, string?> keyResolver;

        private sealed class ModelSlot
        {
            internal HelpDrawModel? Model;
            internal int Chapter = -1;
            internal string Slug = "";
            internal float Width = -1f;
            internal int UiStamp = -1;
            internal int Age;
        }

        // Two LRU slots: a player who toggles between two topics reuses both
        // without rebuilding either every time, and a view that draws two
        // topics in one pass (a start page beside the selected topic) does
        // not rebuild both every frame.
        private readonly ModelSlot[] modelSlots =
            { new ModelSlot(), new ModelSlot() };
        private int modelAge;
        private int languageStamp = -1;

        public HelpContentState(IHelpHost host)
        {
            this.host = host;
            chapters = new HelpTopicData[]?[host.Chapters.Length];
            measurer = new HelpTextMeasurer(host);
            imageSizeResolver = TryGetImageSize;
            demoSizeResolver = TryGetDemoSize;
            keyResolver = ResolveKey;
        }

        public HelpChapter[] Chapters => host.Chapters;

        private static string? ResolveKey(string key) =>
            key.TryTranslate(out TaggedString result)
                ? result.ToString() : null;

        /// <summary>Advances whenever Release() drops the loaded content, so
        /// consumers caching derived rows (e.g. a tour checklist) can
        /// observe reloads.</summary>
        public int Generation { get; private set; }

        /// <summary>Drops all content, layouts, measurements, and destroys
        /// owned textures. Safe to call repeatedly and after partial loads.</summary>
        public void Release()
        {
            Generation++;
            for (int i = 0; i < chapters.Length; i++) chapters[i] = null;
            foreach (KeyValuePair<string, TextureEntry> pair in textures)
            {
                if (pair.Value.Owned && pair.Value.Texture != null)
                    UnityEngine.Object.Destroy(pair.Value.Texture);
            }
            textures.Clear();
            measurer.Clear();
            foreach (ModelSlot slot in modelSlots)
            {
                slot.Model = null;
                slot.Chapter = -1;
                slot.Slug = "";
                slot.Width = -1f;
                slot.UiStamp = -1;
                slot.Age = 0;
            }
            modelAge = 0;
            languageStamp = -1;
            sourceDirectories = null;
            imageDirectories = null;
        }

        /// <summary>Observes the language revision; on change everything is
        /// dropped so titles, keyed labels, and fallbacks re-resolve.</summary>
        public void ObserveLanguage()
        {
            int current = host.LanguageRevision;
            if (languageStamp == current) return;
            Release();
            languageStamp = current;
        }

        public HelpTopicData[] ChapterTopics(int chapterIndex)
        {
            HelpTopicData[]? loaded = chapters[chapterIndex];
            if (loaded != null) return loaded;
            loaded = LoadChapter(host.Chapters[chapterIndex].Folder);
            chapters[chapterIndex] = loaded;
            return loaded;
        }

        /// <summary>Finds a topic slug across chapters for link navigation,
        /// loading further chapters only until the slug is found.</summary>
        public bool TryFindTopic(
            string slug, out int chapterIndex, out int topicIndex)
        {
            for (int c = 0; c < chapters.Length; c++)
            {
                HelpTopicData[] topics = ChapterTopics(c);
                for (int t = 0; t < topics.Length; t++)
                {
                    if (!string.Equals(topics[t].Entry.Slug, slug,
                            StringComparison.Ordinal))
                        continue;
                    chapterIndex = c;
                    topicIndex = t;
                    return true;
                }
            }
            chapterIndex = -1;
            topicIndex = -1;
            return false;
        }

        /// <summary>Returns the draw model for the topic at the width,
        /// rebuilding only when topic, width, UI metric, or language moved.
        /// Two least-recently-used slots back the cache.</summary>
        public HelpDrawModel Model(
            int chapterIndex, HelpTopicData topic, float width)
        {
            int uiStamp = host.UiMetricRevision;
            ModelSlot? target = null;
            for (int i = 0; i < modelSlots.Length; i++)
            {
                ModelSlot slot = modelSlots[i];
                if (slot.Model != null && slot.Chapter == chapterIndex
                    && slot.Slug == topic.Entry.Slug && slot.Width == width
                    && slot.UiStamp == uiStamp)
                {
                    slot.Age = ++modelAge;
                    return slot.Model;
                }
                if (target == null || slot.Age < target.Age) target = slot;
            }

            HelpDocument resolved = HelpDocuments.ResolveKeyedLabels(
                topic.Document, keyResolver);
            // @demo blocks the host does not resolve are skipped by the
            // layout, so no Demo item reaches BuildDrawModel for them.
            HelpTopicLayout layout = HelpFlowLayout.Build(resolved, width,
                measurer, DefaultMetrics, imageSizeResolver,
                demoSizeResolver);
            target!.Model = BuildDrawModel(layout);
            target.Chapter = chapterIndex;
            target.Slug = topic.Entry.Slug;
            target.Width = width;
            target.UiStamp = uiStamp;
            target.Age = ++modelAge;
            return target.Model;
        }

        private static readonly HelpLayoutMetrics DefaultMetrics =
            new HelpLayoutMetrics();

        /// <summary>Extra draw-rect width for styled (bold/italic) text so
        /// the synthesized glyph overhang is not clipped. Draw rects only:
        /// layout advance already carries the per-word pad for Latin words
        /// and deliberately not for merged CJK glyph runs (see
        /// HelpTextMeasurer.WordWidth).</summary>
        private const float StyledDrawSlack = 2f;

        private HelpDrawModel BuildDrawModel(HelpTopicLayout layout)
        {
            int count = layout.Items.Length;
            var built = new HelpDrawModel
            {
                Rects = new Rect[count],
                Texts = new string[count],
                Kinds = new byte[count],
                Fonts = new GameFont[count],
                Targets = new string[count],
                Images = new Texture2D?[count],
                Demos = new IHelpDemo?[count],
                Height = layout.Height,
            };
            for (int i = 0; i < count; i++)
            {
                HelpLayoutItem item = layout.Items[i];
                built.Rects[i] = new Rect(
                    item.X, item.Y, item.Width, item.Height);
                built.Fonts[i] = HelpTextMeasurer.GameFontOf(item.Font);
                built.Targets[i] = item.Target;
                switch (item.Kind)
                {
                    case HelpItemKind.Demo:
                        built.Kinds[i] = HelpDrawModel.KindDemo;
                        built.Texts[i] = item.Text;
                        host.TryGetDemo(item.Text, out IHelpDemo? demo);
                        built.Demos[i] = demo;
                        break;
                    case HelpItemKind.Image:
                        built.Kinds[i] = HelpDrawModel.KindImage;
                        built.Texts[i] = item.Text;
                        built.Images[i] = LoadTexture(item.Text);
                        break;
                    case HelpItemKind.TopicLink:
                        built.Kinds[i] = HelpDrawModel.KindLink;
                        built.Texts[i] = HelpTextMeasurer.Markup(
                            item.Text, item.Font, item.Style);
                        if (!ReferenceEquals(built.Texts[i], item.Text))
                            built.Rects[i].width += StyledDrawSlack;
                        break;
                    case HelpItemKind.ListMarker:
                        built.Kinds[i] = HelpDrawModel.KindMarker;
                        built.Texts[i] = item.Text;
                        break;
                    default:
                        built.Kinds[i] = HelpDrawModel.KindText;
                        built.Texts[i] = HelpTextMeasurer.Markup(
                            item.Text, item.Font, item.Style);
                        if (!ReferenceEquals(built.Texts[i], item.Text))
                            built.Rects[i].width += StyledDrawSlack;
                        break;
                }
            }
            return built;
        }

        private HelpTopicData[] LoadChapter(string folder)
        {
            EnsureSourceDirectories();
            HelpSourceDirectory[] directories = sourceDirectories!;
            var sources = new HelpTopicSource[directories.Length];
            for (int s = 0; s < sources.Length; s++)
                sources[s] = new HelpTopicSource(
                    ListMarkdownFiles(Path.Combine(directories[s].Path, folder)),
                    directories[s].English);
            HelpTopicEntry[] plan = HelpIndexPlanner.PlanChapter(sources);

            var topics = new List<HelpTopicData>(plan.Length);
            for (int i = 0; i < plan.Length; i++)
            {
                string dir = Path.Combine(directories[plan[i].SourceIndex].Path, folder);
                string path = Path.Combine(dir, plan[i].FileName);
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception exception)
                {
                    Log.Warning(host.LogPrefix + " Could not read help topic "
                        + path + ": " + exception.Message);
                    continue;
                }
                HelpDocument document = HelpMarkdown.Parse(text);
                topics.Add(new HelpTopicData(plan[i], document,
                    document.Title.Length > 0
                        ? document.Title : plan[i].Slug));
            }
            return topics.ToArray();
        }

        private void EnsureSourceDirectories()
        {
            if (sourceDirectories != null) return;
            LoadedLanguage? language = LanguageDatabase.activeLanguage;
            string[] languages = LanguageFolders.Candidates(language?.folderName, language?.LegacyFolderName);
            var topics = new List<HelpSourceDirectory>();
            var images = new List<string>();
            string helpRoot = host.HelpRoot;
            List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
            string? ownerPackage = null;
            for (int m = 0; m < mods.Count; m++)
                if (string.Equals(Path.GetFullPath(Path.Combine(mods[m].RootDir, "Help")),
                    Path.GetFullPath(helpRoot), StringComparison.OrdinalIgnoreCase))
                {
                    ownerPackage = mods[m].PackageIdPlayerFacing.ToLowerInvariant();
                    break;
                }

            // A translation companion opts in by target package ID, without
            // declaring a hardcoded translator package name or load-order rule:
            // <companion>/Help/<target-package-id>/<language>/<chapter>/*.md.
            // Among companions the last mod wins, matching normal keyed text.
            if (ownerPackage != null)
                for (int m = mods.Count - 1; m >= 0; m--)
                    for (int f = 0; f < mods[m].foldersToLoadDescendingOrder.Count; f++)
                        AddSourceRoot(Path.Combine(mods[m].foldersToLoadDescendingOrder[f],
                            "Help", ownerPackage), languages, topics, images);

            AddSourceRoot(helpRoot, languages, topics, images);
            if (!string.Equals(languages[0], "English", StringComparison.OrdinalIgnoreCase))
            {
                topics.Add(new HelpSourceDirectory(Path.Combine(helpRoot, "English"), true));
                // English-specific artwork precedes the existing shared image
                // directory; localized artwork is optional at every level.
                images.Insert(images.Count - 1, Path.Combine(helpRoot, "English", "Images"));
            }
            sourceDirectories = topics.ToArray();
            imageDirectories = images.ToArray();
        }

        private static void AddSourceRoot(string root, string[] languages,
            List<HelpSourceDirectory> topics, List<string> images)
        {
            for (int l = 0; l < languages.Length; l++)
            {
                string localized = Path.Combine(root, languages[l]);
                topics.Add(new HelpSourceDirectory(localized, false));
                images.Add(Path.Combine(localized, "Images"));
            }
            images.Add(Path.Combine(root, "Images"));
        }

        private string[] ListMarkdownFiles(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return Array.Empty<string>();
                string[] paths = Directory.GetFiles(directory, "*.md");
                for (int i = 0; i < paths.Length; i++)
                    paths[i] = Path.GetFileName(paths[i]);
                Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                return paths;
            }
            catch (Exception exception)
            {
                Log.Warning(host.LogPrefix + " Could not list help folder "
                    + directory + ": " + exception.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>Image sizes for layout; loads (and caches) the texture on
        /// first use. Failures cache as null so a topic renders without its
        /// image instead of retrying every layout. A "|N" suffix displays
        /// the image at height N with the width scaled by aspect (used for
        /// small inline icons).</summary>
        private bool TryGetImageSize(
            string path, out float width, out float height)
        {
            Texture2D? texture = LoadTexture(path);
            if (texture == null)
            {
                width = 0f;
                height = 0f;
                return false;
            }
            if (TryParseHeightSuffix(path, out float targetHeight)
                && texture.height > 0)
            {
                height = targetHeight;
                width = texture.width * targetHeight / texture.height;
                return true;
            }
            width = texture.width;
            height = texture.height;
            return true;
        }

        /// <summary>Demo sizes for layout, from the host's demo set.</summary>
        private bool TryGetDemoSize(
            string name, out float width, out float height)
        {
            if (host.TryGetDemo(name, out IHelpDemo? demo) && demo != null)
            {
                width = demo.Size.x;
                height = demo.Size.y;
                return true;
            }
            width = 0f;
            height = 0f;
            return false;
        }

        private static bool TryParseHeightSuffix(
            string path, out float height)
        {
            height = 0f;
            int split = path.LastIndexOf('|');
            if (split < 0) return false;
            if (!int.TryParse(path.Substring(split + 1), out int parsed)
                || parsed <= 0)
                return false;
            height = parsed;
            return true;
        }

        /// <summary>Loads a help image. File paths resolve through companion,
        /// bundled language, English, then shared image directories. Textures
        /// are owned (destroyed on Release); "tex:Path"
        /// references borrow a game texture via ContentFinder and are never
        /// destroyed here. Cached by the full reference including any "|N"
        /// suffix.</summary>
        internal Texture2D? LoadTexture(string path)
        {
            if (textures.TryGetValue(path, out TextureEntry cached))
                return cached.Texture;

            int split = path.LastIndexOf('|');
            string basePath = split >= 0
                && TryParseHeightSuffix(path, out _)
                ? path.Substring(0, split) : path;

            const string TexturePrefix = "tex:";
            if (basePath.StartsWith(TexturePrefix, StringComparison.Ordinal))
            {
                Texture2D? borrowed = ContentFinder<Texture2D>.Get(
                    basePath.Substring(TexturePrefix.Length),
                    reportFailure: false);
                if (borrowed == null)
                    Log.Warning(host.LogPrefix + " Help texture reference not "
                        + "found: " + basePath);
                textures[path] = new TextureEntry(borrowed, owned: false);
                return borrowed;
            }

            EnsureSourceDirectories();
            Texture2D? loaded = null;
            string fullPath = Path.Combine(imageDirectories![imageDirectories.Length - 1], basePath);
            for (int d = 0; d < imageDirectories.Length; d++)
            {
                string candidate = Path.Combine(imageDirectories[d], basePath);
                if (!File.Exists(candidate)) continue;
                fullPath = candidate;
                break;
            }
            try
            {
                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32,
                        mipChain: false);
                    if (texture.LoadImage(bytes))
                    {
                        texture.name = host.LogPrefix + "Help_" + basePath;
                        loaded = texture;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warning(host.LogPrefix + " Could not load help image "
                    + fullPath + ": " + exception.Message);
            }
            if (loaded == null)
                Log.Warning(host.LogPrefix
                    + " Help image not found or unreadable: " + fullPath);
            textures[path] = new TextureEntry(loaded, owned: loaded != null);
            return loaded;
        }
    }
}
