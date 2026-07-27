using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    [StaticConstructorOnStartup]
    public static class ReadoutTextures
    {
        /// Right-facing triangle (game-speed style), generated procedurally so
        /// the mod ships no texture assets. Tinted via GUI.color at draw time.
        public static readonly Texture2D Triangle = MakeTriangle(14, 18);

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
    }
}
