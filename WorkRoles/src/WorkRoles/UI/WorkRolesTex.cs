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
        public static readonly Texture2D DisplayOptions;
        public static readonly Texture2D Logo;
        // Owner: world session. Key: the current world lifecycle (one shared
        // slot). Value: two WorkRoles-owned Texture2D assets. Dependencies:
        // fixed dimensions and the UI section background used by the fade.
        // Refresh: eagerly at startup/PreOpen after teardown. Equality: reuse
        // the existing texture references while present. Teardown:
        // ReleaseForTeardown destroys only these owned assets and clears them.
        // Runtime-built white disc (no art asset needed), tinted via GUI.color
        // at draw time — e.g. the training path color dot.
        public static Texture2D Circle { get; private set; }
        // Runtime-built white 5-pointed star (point up), tinted via GUI.color
        // at draw time — the chip verdict marker stack.
        public static Texture2D Star { get; private set; }
        // 1px-wide gradient (section bg fading to transparent), stretched to
        // the panel width at draw time; bilinear sampling makes it smooth
        // where stacked 1px strips banded.
        public static Texture2D ScrollEdgeFade { get; private set; }

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
            DisplayOptions = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsUI");
            Logo = ContentFinder<Texture2D>.Get("WorkRoles/Logo");
            EnsureRuntimeTextures();
            StartupTiming.Record("textures", sw.ElapsedMilliseconds);
        }

        internal static void EnsureRuntimeTextures()
        {
            if (Circle == null) Circle = MakeCircle(32);
            if (Star == null) Star = MakeStar(32);
            if (ScrollEdgeFade == null) ScrollEdgeFade = MakeScrollEdgeFade(20);
        }

        internal static void ReleaseForTeardown()
        {
            if (Circle != null) UnityEngine.Object.Destroy(Circle);
            if (Star != null) UnityEngine.Object.Destroy(Star);
            if (ScrollEdgeFade != null) UnityEngine.Object.Destroy(ScrollEdgeFade);
            Circle = null;
            Star = null;
            ScrollEdgeFade = null;
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
        private static Texture2D MakeStar(int size)
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
                            if (Inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))
                                hits++;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, hits / 16f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "WorkRolesStar";
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
