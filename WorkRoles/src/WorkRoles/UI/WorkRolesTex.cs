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
        public static readonly Texture2D RoleCapabilityPartial;
        public static readonly Texture2D RoleCapabilityAll;
        public static readonly Texture2D DisplayOptions;
        public static readonly Texture2D Logo;
        // Runtime-built white disc (no art asset needed), tinted via GUI.color
        // at draw time — e.g. the training path color dot.
        public static readonly Texture2D Circle;

        static WorkRolesTex()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PassionMinor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");
            PassionMajor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor");
            BlockerMarker = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
            TimeMarker = ContentFinder<Texture2D>.Get("WorkRoles/Clock");
            LocationMarker = ContentFinder<Texture2D>.Get("WorkRoles/LocationPin");
            PinMarker = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
            RoleCapabilityPartial = ContentFinder<Texture2D>.Get(
                "UI/Icons/ColonistBar/MentalStateNonAggro");
            RoleCapabilityAll = ContentFinder<Texture2D>.Get(
                "UI/Icons/ColonistBar/MentalStateAggro");
            DisplayOptions = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsUI");
            Logo = ContentFinder<Texture2D>.Get("WorkRoles/Logo");
            Circle = MakeCircle(32);
            StartupTiming.Record("textures", sw.ElapsedMilliseconds);
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
    }
}
