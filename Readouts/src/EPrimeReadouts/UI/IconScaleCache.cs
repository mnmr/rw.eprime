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

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: ThingDef identity.
        // Value: immutable measured icon-scale factor.
        // Dependencies: uiIcon pixels and GenUI.IconDrawScale for the loaded def.
        // Refresh policy: requested by snapshot builders and processed in bounded
        // batches by MapComponentUpdate, never by OnGUI.
        // Equality policy: each value is measured once per cache lifetime.
        // Teardown: Reset clears entries and destroys the owned readback texture.
        private static readonly Dictionary<ThingDef, float> cache =
            new Dictionary<ThingDef, float>();
        private static readonly Queue<ThingDef> pending = new Queue<ThingDef>();
        private static readonly HashSet<ThingDef> pendingSet = new HashSet<ThingDef>();
        private static Texture2D reader;

        /// Correction factor for the def's icon (1 when unmeasurable).
        /// Missing values use neutral scale until the update queue publishes one.
        public static float ScaleFor(ThingDef def)
        {
            if (def == null) return 1f;
            return cache.TryGetValue(def, out float cached) ? cached : 1f;
        }

        internal static void Request(ThingDef def)
        {
            if (def == null || cache.ContainsKey(def) || !pendingSet.Add(def)) return;
            pending.Enqueue(def);
        }

        internal static void ProcessPending(int budget = 4)
        {
            while (budget-- > 0 && pending.Count > 0)
            {
                ThingDef def = pending.Dequeue();
                pendingSet.Remove(def);
                if (!cache.ContainsKey(def)) cache.Add(def, Measure(def));
            }
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
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                reader.ReadPixels(new Rect(0f, 0f, SampleSize, SampleSize), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

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

        internal static void Reset()
        {
            cache.Clear();
            pending.Clear();
            pendingSet.Clear();
            if (reader != null)
            {
                Object.Destroy(reader);
                reader = null;
            }
        }
    }
}
