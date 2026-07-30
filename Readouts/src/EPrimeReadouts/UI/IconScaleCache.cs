using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Per-def icon scale correction so icons look visually same-sized: item
    /// textures carry wildly different amounts of transparent padding (Cloth
    /// nearly fills its texture, Penoxycyline floats in empty space). Each
    /// def's uiIcon is measured ONCE — blitted to a small RenderTexture, read
    /// back, alpha bounding box computed — and the resulting factor normalizes
    /// the opaque content toward a common coverage. Cached forever; rendering
    /// only does a dictionary lookup.
    public static class IconScaleCache
    {
        private const int SampleSize = 48;
        private const byte AlphaThreshold = 24;
        private const float TargetCoverage = 0.88f;
        private const float MinScale = 0.80f;
        private const float MaxScale = 1.25f;

        private static readonly Dictionary<ThingDef, float> cache =
            new Dictionary<ThingDef, float>();
        private static Texture2D reader;

        /// Correction factor for the def's icon (1 when unmeasurable).
        /// Measurement runs lazily on the first Repaint that draws the def.
        public static float ScaleFor(ThingDef def)
        {
            if (def == null) return 1f;
            if (cache.TryGetValue(def, out float cached)) return cached;
            // GPU readback needs a render context; only measure on Repaint.
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return 1f;
            float scale = Measure(def);
            cache[def] = scale;
            return scale;
        }

        private static float Measure(ThingDef def)
        {
            var tex = def.uiIcon;
            if (tex == null || tex == BaseContent.BadTex) return 1f;

            if (reader == null)
                reader = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false);

            var rt = RenderTexture.GetTemporary(SampleSize, SampleSize, 0,
                RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            reader.ReadPixels(new Rect(0f, 0f, SampleSize, SampleSize), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = reader.GetPixels32();
            int minX = SampleSize, maxX = -1, minY = SampleSize, maxY = -1;
            for (int y = 0; y < SampleSize; y++)
            {
                int row = y * SampleSize;
                for (int x = 0; x < SampleSize; x++)
                {
                    if (pixels[row + x].a < AlphaThreshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return 1f; // fully transparent — leave alone

            float coverage = Mathf.Max(maxX - minX + 1, maxY - minY + 1) / (float)SampleSize;
            // Vanilla ThingIcon already applies the def's own draw scale; fold
            // it in so we correct what actually lands on screen.
            float effective = coverage * GenUI.IconDrawScale(def);
            if (effective <= 0f) return 1f;
            float scale = Mathf.Clamp(TargetCoverage / effective, MinScale, MaxScale);
            // Dead-zone: icons that are already roughly right stay untouched —
            // resampling a fine icon only smears it.
            if (Mathf.Abs(scale - 1f) < 0.08f) scale = 1f;
            return scale;
        }
    }
}
