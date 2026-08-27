using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    [StaticConstructorOnStartup]
    public static class ReadoutTextures
    {
        /// Right-facing triangle (game-speed style), generated procedurally so
        /// the mod ships no texture assets. Tinted via GUI.color at draw time.
        // Cache contract:
        // Owner: mod process; this mod owns only the generated triangle.
        // Key: fixed 14x18 procedural geometry.
        // Value: owned Texture2D.
        // Dependencies: none after construction.
        // Refresh policy: eager at map construction, lazy safety fallback.
        // Equality policy: preserve texture identity until teardown.
        // Teardown: ResetOwned destroys only triangle, never vanilla/mod assets.
        private static Texture2D? triangle;
        public static Texture2D Triangle => triangle ?? (triangle = MakeTriangle(14, 18));

        public static readonly Texture2D Gear =
            ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsGeneral", false)
            ?? BaseContent.BadTex;

        public static readonly Texture2D ModIcon =
            ContentFinder<Texture2D>.Get("EPrimeReadouts/ModIcon", false)
            ?? BaseContent.BadTex;

        private static Texture2D MakeTriangle(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float half = (h - 1) / 2f;
            for (int y = 0; y < h; y++)
            {
                float rowWidth = (1f - Mathf.Abs(y - half) / half) * (w - 1);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, x <= rowWidth ? Color.white : Color.clear);
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        internal static void EnsureOwned()
        {
            _ = Triangle;
        }

        internal static void ResetOwned()
        {
            if (triangle == null) return;
            Texture2D owned = triangle;
            triangle = null;
            // ClearAllMapsAndWorld can run on RimWorld's asynchronous long-event
            // thread. ExecuteWhenFinished runs this immediately when already on
            // the main thread, or queues it until the long event completes.
            LongEventHandler.ExecuteWhenFinished(() => Object.Destroy(owned));
        }
    }
}
