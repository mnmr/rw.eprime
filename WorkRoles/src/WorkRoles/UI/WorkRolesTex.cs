using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Textures that must be loaded on the main thread at startup. Loaded in
    /// the constructor body (not field initializers) so the cost is timed.
    [StaticConstructorOnStartup]
    public static class WorkRolesTex
    {
        public static readonly Texture2D PassionMinor;
        public static readonly Texture2D PassionMajor;

        // Role/chip markers. BlockerMarker keeps its own colors (red X); the
        // others are monochrome and tinted with RuleMarkerColor at draw time.
        public static readonly Texture2D BlockerMarker;
        public static readonly Texture2D TimeMarker;
        public static readonly Texture2D LocationMarker;
        public static readonly Texture2D PinMarker;
        public static readonly Texture2D ForceOnMarker;
        public static readonly Texture2D RoleCapabilityPartial;
        public static readonly Texture2D RoleCapabilityAll;
        public static readonly Texture2D RoleIconPlaceholder;
        public static readonly Texture2D DisplayOptions;
        public static readonly Texture2D PriorityGrid;
        // Chip layout toggle faces: equal-width grid columns, or chips
        // stacked at natural widths (a brick wall). White, tinted at draw.
        public static readonly Texture2D ChipGrid;
        public static readonly Texture2D ChipStack;
        public static readonly Texture2D Logo;
        // Help tour completion medal (vanilla star-in-badge), tinted gold at
        // draw time.
        public static readonly Texture2D HelpMedal;
        // The vanilla tab atlas (TabRecord keeps its copy private); drawn by
        // the shared TabStrip, never mutated.
        public static readonly Texture2D TabAtlas;
        // Owner: world session. Key: the current world lifecycle (one shared
        // slot). Value: two WorkRoles-owned Texture2D assets. Dependencies:
        // fixed dimensions and the UI section background used by the fade.
        // Refresh: eagerly at startup/PreOpen after teardown. Equality: reuse
        // the existing texture references while present. Teardown:
        // ReleaseForTeardown destroys only these owned assets and clears them.
        // Runtime-built white disc (no art asset needed), tinted via GUI.color
        // at draw time — e.g. the training path color dot.
        // null! pattern: EnsureRuntimeTextures repopulates these at startup and
        // PreOpen after teardown, before any draw can observe them.
        public static Texture2D Circle { get; private set; } = null!;
        // Runtime-built white 5-pointed star (point up), tinted via GUI.color
        // at draw time — the chip verdict marker stack.
        public static Texture2D Star { get; private set; } = null!;
        // The same star with its middle cut out (same silhouette): the
        // verdict of a colonist who cannot do all of the role's jobs.
        public static Texture2D StarHollow { get; private set; } = null!;
        // 1px-wide gradient (section bg fading to transparent), stretched to
        // the panel width at draw time; bilinear sampling makes it smooth
        // where stacked 1px strips banded.
        public static Texture2D ScrollEdgeFade { get; private set; } = null!;
        // 1px-tall white ramp, transparent at the left edge and opaque at
        // the right, tinted with a chip's fill at draw time to fade out a
        // clipped label's tail (ChipUI.FadeLabelWidth).
        public static Texture2D LabelFade { get; private set; } = null!;

        // Owner: process (world session lifetime, like the textures above).
        // Key: Prefs.UIScale (ChipDotsScale). Value: one WorkRoles-owned
        // point-filtered atlas holding every RoleChipDots mask as a
        // physical-pixel stripe cell, plus the cell metrics; immutable after
        // Apply. Dependencies: the UI scale only (light size and cell height
        // are whole physical pixels of the 24-unit chip). Refresh: immediate,
        // on the first measure or draw after the scale changed. Equality:
        // an unchanged scale reuses the texture. Teardown: ReleaseForTeardown
        // destroys it; the next EnsureChipDots rebuilds it.
        public static Texture2D? ChipDots { get; private set; }
        internal static float ChipDotsScale { get; private set; }
        /// Stripe cell size in physical pixels: one light plus a shaded
        /// pixel each side, by the chip's height inside its outline.
        internal static int ChipDotsCellW { get; private set; }
        internal static int ChipDotsCellH { get; private set; }
        internal const int ChipDotsColumns =
            WorkRoles.Core.RoleChipDots.Combinations;

        static WorkRolesTex()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PassionMinor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");
            PassionMajor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor");
            BlockerMarker = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
            TimeMarker = ContentFinder<Texture2D>.Get("WorkRoles/Clock");
            LocationMarker = ContentFinder<Texture2D>.Get("WorkRoles/LocationPin");
            PinMarker = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
            ForceOnMarker = ContentFinder<Texture2D>.Get("UI/Designators/Claim");
            RoleCapabilityPartial = ContentFinder<Texture2D>.Get(
                "UI/Icons/ColonistBar/MentalStateNonAggro");
            RoleCapabilityAll = ContentFinder<Texture2D>.Get(
                "UI/Icons/ColonistBar/MentalStateAggro");
            RoleIconPlaceholder = ContentFinder<Texture2D>.Get(
                "UI/Icons/QuestionMark");
            DisplayOptions = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsUI");
            PriorityGrid = ContentFinder<Texture2D>.Get(
                "WorkRoles/PriorityGrid");
            ChipGrid = ContentFinder<Texture2D>.Get("WorkRoles/ChipGrid");
            ChipStack = ContentFinder<Texture2D>.Get("WorkRoles/ChipStack");
            Logo = ContentFinder<Texture2D>.Get("WorkRoles/Logo");
            HelpMedal = ContentFinder<Texture2D>.Get(
                "UI/Icons/UnwaveringlyLoyal");
            TabAtlas = ContentFinder<Texture2D>.Get("UI/Widgets/TabAtlas");
            EnsureRuntimeTextures();
            StartupTiming.Record("textures", sw.ElapsedMilliseconds);
        }

        internal static void EnsureRuntimeTextures()
        {
            if (Circle == null) Circle = MakeCircle(32);
            if (Star == null) Star = MakeStar(32, hollow: false);
            if (StarHollow == null) StarHollow = MakeStar(32, hollow: true);
            if (ScrollEdgeFade == null) ScrollEdgeFade = MakeScrollEdgeFade(20);
            if (LabelFade == null) LabelFade = MakeLabelFade(32);
        }

        internal static void ReleaseForTeardown()
        {
            if (Circle != null) UnityEngine.Object.Destroy(Circle);
            if (Star != null) UnityEngine.Object.Destroy(Star);
            if (StarHollow != null) UnityEngine.Object.Destroy(StarHollow);
            StarHollow = null!;
            if (ScrollEdgeFade != null) UnityEngine.Object.Destroy(ScrollEdgeFade);
            if (LabelFade != null) UnityEngine.Object.Destroy(LabelFade);
            if (ChipDots != null) UnityEngine.Object.Destroy(ChipDots);
            ChipDots = null;
            ChipDotsScale = 0f;
            Circle = null!; // cleared at teardown; rebuilt by EnsureRuntimeTextures
            Star = null!;
            ScrollEdgeFade = null!;
            LabelFade = null!;
        }

        // Dot colours in stacking order (top to bottom), by RoleChipDots
        // bit: always on, has rules, pinned.
        private static readonly Color[] DotColors =
        {
            Color.white, // always on
            new Color(1f, 0.92f, 0.2f), // yellow: has rules
            new Color(0f, 1f, 0.863f), // aqua: pinned
        };

        /// Builds the dot atlas for the current UI scale when it is missing
        /// or was built for another scale. Each cell stacks the dots of its
        /// mask, centred on the verdict star pair; three dots span exactly
        /// the pair (first row on the stars' top row, last on their bottom).
        internal static void EnsureChipDots()
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            if (ChipDots != null && ChipDotsScale == scale) return;
            if (ChipDots != null) UnityEngine.Object.Destroy(ChipDots);
            int inner = Mathf.RoundToInt(RoleChipUI.Height * scale) - 2;
            // The star pair's extent inside the chip, in physical pixels
            // (RoleChipUI.DrawVerdictBadge geometry).
            float starTop = RoleChipUI.VerdictStarsTop * scale;
            int top = Mathf.RoundToInt(starTop);
            int span = Mathf.RoundToInt(
                starTop + RoleChipUI.VerdictStarsSpan * scale) - top;
            // Largest light leaving two equal gaps of at least a pixel.
            int light = Mathf.Max(2, (span - 2) / 3);
            if ((span - 3 * light) % 2 != 0) light--;
            int gap = (span - 3 * light) / 2;
            int cellW = light + 2;
            int width = cellW * ChipDotsColumns;
            int height = inner;
            var pixels = new Color[width * height];
            for (int mask = 1; mask < ChipDotsColumns; mask++)
            {
                int count = Core.RoleChipDots.Count(mask);
                // Texture rows run bottom-up; the cell starts one pixel
                // inside the chip (below its outline row).
                int y = inner - 1 - (top - 1)
                    - (span - count * light - (count - 1) * gap) / 2;
                for (int bit = 0; bit < DotColors.Length; bit++)
                {
                    if ((mask & (1 << bit)) == 0) continue;
                    PaintLight(pixels, width, mask * cellW + 1, y, light,
                        DotColors[bit]);
                    y -= light + gap;
                }
            }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "WorkRolesChipDots",
            };
            tex.SetPixels(pixels);
            tex.Apply();
            ChipDots = tex;
            ChipDotsScale = scale;
            ChipDotsCellW = cellW;
            ChipDotsCellH = inner;
        }

        /// One flat round light, size x size, its top-left pixel at
        /// (left, top) in bottom-up texture rows, with an anti-aliased edge
        /// (straight alpha over the transparent cell).
        private static void PaintLight(Color[] pixels, int stride, int left,
            int top, int size, Color color)
        {
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float cover = Mathf.Clamp01(
                        r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (cover > 0f)
                        pixels[(top - y) * stride + left + x] =
                            new Color(color.r, color.g, color.b, cover);
                }
        }

        /// 32px so a 16px draw stays anti-aliased at UI scales above 1.
        private static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            float c = (size - 1) / 2f;
            float r = size / 2f - 1.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "WorkRolesCircle";
            return tex;
        }

        /// 32px filled 5-pointed star, point up, anti-aliased by 4x4
        /// supersampled polygon coverage. Inner radius 0.5R keeps the legs
        /// chunky enough to read at a 10px draw size.
        private static Texture2D MakeStar(int size, bool hollow)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            float c = (size - 1) / 2f;
            float outer = size / 2f - 1f;
            float inner = outer * 0.5f;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                // Vertex 0 at the top; texture y-up matches drawn image y-up.
                float angle = Mathf.PI / 2f - i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(c + radius * Mathf.Cos(angle),
                    c + radius * Mathf.Sin(angle));
            }

            bool Inside(float px, float py)
            {
                bool inside = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    Vector2 a = points[i], b = points[j];
                    if (a.y > py != b.y > py
                        && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                return inside;
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float px = x + (sx + 0.5f) / 4f, py = y + (sy + 0.5f) / 4f;
                            // The hole is the star itself at half size
                            // about its centre.
                            if (Inside(px, py) && !(hollow
                                    && Inside(c + (px - c) * 2f, c + (py - c) * 2f)))
                                hits++;
                        }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, hits / 16f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = hollow ? "WorkRolesStarHollow" : "WorkRolesStar";
            return tex;
        }

        /// Horizontal alpha ramp, white, 0 at the left column and 1 at the
        /// right; bilinear so a 12px draw stays smooth.
        private static Texture2D MakeLabelFade(int steps)
        {
            var tex = new Texture2D(steps, 1, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "WorkRolesLabelFade",
            };
            for (int x = 0; x < steps; x++)
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, x / (float)(steps - 1)));
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeScrollEdgeFade(int steps)
        {
            var tex = new Texture2D(1, steps, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "WorkRolesScrollEdgeFade",
            };
            Color bg = Widgets.MenuSectionBGFillColor;
            // Texture top row (highest index) is opaque; alpha ramps to
            // transparent toward the bottom row.
            for (int y = 0; y < steps; y++)
                tex.SetPixel(0, y,
                    new Color(bg.r, bg.g, bg.b, y / (float)(steps - 1)));
            tex.Apply();
            return tex;
        }
    }
}
