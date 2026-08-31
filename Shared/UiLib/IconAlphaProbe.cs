using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Alpha bounding box of a texture blitted at a given sample size, in
    /// bottom-up pixel coordinates as read back by ReadPixels.
    public readonly struct IconAlphaBounds
    {
        public IconAlphaBounds(
            int minX, int maxX, int minY, int maxY, int sampleSize)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            SampleSize = sampleSize;
        }

        public readonly int MinX;
        public readonly int MaxX;
        public readonly int MinY;
        public readonly int MaxY;
        public readonly int SampleSize;

        /// False when no pixel met the alpha threshold (fully transparent).
        public bool HasOpaque => MaxX >= 0;

        /// Longest opaque side in sample pixels. Valid only when HasOpaque.
        public int OpaqueExtent =>
            Mathf.Max(MaxX - MinX + 1, MaxY - MinY + 1);

        /// Opaque extent as a fraction of the sample canvas (0 when
        /// fully transparent).
        public float Coverage =>
            HasOpaque ? OpaqueExtent / (float)SampleSize : 0f;

        /// Offset, in fractions of the sample canvas and GUI coordinates
        /// (y grows down), that recenters a canvas-centered draw rect on the
        /// opaque pixels. ReadPixels rows are bottom-up, so the visual
        /// (top-down) center is the flipped pixel center: a glyph sitting
        /// visually low yields a negative GUI y — the icon draws higher to
        /// recenter. Zero when fully transparent.
        public Vector2 CenterOffsetGui
        {
            get
            {
                if (!HasOpaque) return Vector2.zero;
                float centerX = (MinX + MaxX + 1) * 0.5f / SampleSize;
                float centerYTopDown = 1f - (MinY + MaxY + 1) * 0.5f / SampleSize;
                return new Vector2(0.5f - centerX, 0.5f - centerYTopDown);
            }
        }
    }

    /// Blit/readback measurement core shared by the per-mod icon metric
    /// caches: blits a texture at the requested sample size, reads it back,
    /// and computes the alpha bounding box. Main-thread only (it drives the
    /// GPU and a readback texture); callers own budgeting, failure latching,
    /// and result caching. Exceptions propagate so callers can latch.
    ///
    /// [StaticConstructorOnStartup] satisfies the vanilla dev-mode scanner,
    /// which flags any static Texture2D field regardless of lazy creation;
    /// the readback texture itself is created lazily in Measure, on the
    /// main thread.
    // Cache contract (readback texture):
    // Owner: process (one per compiled mod assembly).
    // Key: none — a single reusable reader, resized to the requested sample
    //   size.
    // Value: mutable scratch texture; never escapes this class.
    // Dependencies: the sample size of the current Measure call.
    // Refresh policy: recreated only when a call requests a different size.
    // Equality policy: same-size requests reuse the reader instance.
    // Teardown: ReleaseReader defers destruction to the main thread (world
    //   teardown may originate on a long-event worker thread); safe to call
    //   repeatedly or before first use.
    [StaticConstructorOnStartup]
    public static class IconAlphaProbe
    {
        private static Texture2D? reader;

        /// Measures the texture's alpha bounding box at sampleSize. The
        /// texture must be non-null; pixels with alpha below alphaThreshold
        /// are treated as transparent.
        public static IconAlphaBounds Measure(
            Texture2D texture, int sampleSize, byte alphaThreshold)
        {
            EnsureReader(sampleSize);

            RenderTexture rt = RenderTexture.GetTemporary(
                sampleSize, sampleSize, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                reader!.ReadPixels(
                    new Rect(0f, 0f, sampleSize, sampleSize), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            Unity.Collections.NativeArray<Color32> pixels =
                reader!.GetRawTextureData<Color32>();
            int minX = sampleSize, maxX = -1, minY = sampleSize, maxY = -1;
            for (int y = 0; y < sampleSize; y++)
            {
                int row = y * sampleSize;
                for (int x = 0; x < sampleSize; x++)
                {
                    if (pixels[row + x].a < alphaThreshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return new IconAlphaBounds(minX, maxX, minY, maxY, sampleSize);
        }

        private static void EnsureReader(int sampleSize)
        {
            if (reader != null && reader.width == sampleSize
                && reader.height == sampleSize) return;
            if (reader != null) Object.Destroy(reader);
            reader = new Texture2D(
                sampleSize, sampleSize, TextureFormat.RGBA32, false);
        }

        public static void ReleaseReader()
        {
            if (reader == null) return;
            Texture2D owned = reader;
            reader = null;
            // World teardown may originate on a long-event worker thread;
            // Unity objects must only be destroyed after returning to the
            // main thread.
            LongEventHandler.ExecuteWhenFinished(() => Object.Destroy(owned));
        }
    }
}
