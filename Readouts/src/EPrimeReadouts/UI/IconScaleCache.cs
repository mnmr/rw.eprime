using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Per-def icon scale correction so icons look visually same-sized: item
    /// textures carry wildly different amounts of transparent padding (Cloth
    /// nearly fills its texture, Penoxycyline floats in empty space). Each
    /// def's uiIcon is measured once per physical-resolution/UI-scale epoch —
    /// blitted at its effective display size, read back, alpha bounding box
    /// computed — and the resulting factor normalizes the opaque content toward
    /// a common coverage. Rendering only reads the cached value.
    ///
    /// [StaticConstructorOnStartup] satisfies the vanilla dev-mode scanner,
    /// which flags any static Texture2D field regardless of lazy creation;
    /// the static constructor only initializes plain collections, so running
    /// it eagerly on the main thread is harmless. The readback texture itself
    /// is still created lazily in Measure, on the main thread.
    [StaticConstructorOnStartup]
    public static class IconScaleCache
    {
        private const byte AlphaThreshold = 24;

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: ThingDef identity plus physical resolution and UI scale epoch.
        // Value: immutable measured icon-scale factor.
        // Dependencies: uiIcon pixels, GenUI.IconDrawScale, Screen dimensions,
        //               and Prefs.UIScale.
        // Refresh policy: once initially and once after either display metric
        //               changes; processed in bounded MapComponentUpdate batches,
        //               never measured by OnGUI. The first measurement failure
        //               disables further probes and publishes neutral scales.
        // Equality policy: each def is measured at most once per display epoch.
        // Teardown: world teardown preserves CPU measurements and the process
        //           failure latch, and releases only the owned readback texture.
        private static readonly DisplayEpochCache<ThingDef, float> measurements =
            new DisplayEpochCache<ThingDef, float>();
        private static readonly FrameBatchGate processGate = new FrameBatchGate();
        private static Texture2D? reader;
        private static int revision;
        private static bool measurementFailed;

        internal static int Revision => revision;

        /// Correction factor for the def's icon (1 when unmeasurable).
        /// Missing values use neutral scale until the update queue publishes one.
        public static float ScaleFor(ThingDef? def)
        {
            if (def == null) return 1f;
            return measurements.TryGet(def, out float cached) ? cached : 1f;
        }

        internal static void Request(ThingDef? def)
        {
            if (def != null) measurements.Request(def);
        }

        internal static void ProcessPending(int budget = 4)
        {
            measurements.Observe(new DisplayEpoch(
                Screen.width, Screen.height, Prefs.UIScale));
            if (measurements.PendingCount == 0) return;
            if (!processGate.TryEnter(Time.frameCount)) return;
            while (budget-- > 0 && measurements.TryTake(out ThingDef def))
            {
                float scale = 1f;
                if (!measurementFailed)
                {
                    try
                    {
                        scale = Measure(def);
                    }
                    catch (System.Exception exception)
                    {
                        measurementFailed = true;
                        Log.Warning("[EPrimeReadouts] Icon scale measurement "
                            + "failed; further measurements use neutral scale: "
                            + exception.GetType().Name + ": "
                            + exception.Message);
                    }
                }
                measurements.Publish(def, scale);
                unchecked { revision++; }
            }
        }

        private static float Measure(ThingDef def)
        {
            var tex = def.uiIcon;
            if (tex == null || tex == BaseContent.BadTex) return 1f;

            int sampleSize = Mathf.Max(1,
                Mathf.RoundToInt(LayoutMetrics.IconSize * Prefs.UIScale));
            EnsureReader(sampleSize);

            var rt = RenderTexture.GetTemporary(sampleSize, sampleSize, 0,
                RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                reader!.ReadPixels(new Rect(0f, 0f, sampleSize, sampleSize), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            var pixels = reader!.GetRawTextureData<Color32>();
            int minX = sampleSize, maxX = -1, minY = sampleSize, maxY = -1;
            for (int y = 0; y < sampleSize; y++)
            {
                int row = y * sampleSize;
                for (int x = 0; x < sampleSize; x++)
                {
                    if (pixels[row + x].a < AlphaThreshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return 1f; // fully transparent — leave alone

            int opaqueExtent = Mathf.Max(
                maxX - minX + 1, maxY - minY + 1);
            // Vanilla ThingIcon already applies the def's own draw scale; fold
            // it in so we correct what actually lands on screen.
            return IconScaleMath.CorrectionFor(
                opaqueExtent, sampleSize, GenUI.IconDrawScale(def));
        }

        private static void EnsureReader(int sampleSize)
        {
            if (reader != null && reader.width == sampleSize
                && reader.height == sampleSize) return;
            if (reader != null) Object.Destroy(reader);
            reader = new Texture2D(
                sampleSize, sampleSize, TextureFormat.RGBA32, false);
        }

        internal static void ReleaseGraphics()
        {
            processGate.Reset();
            if (reader != null)
            {
                Texture2D owned = reader;
                reader = null;
                // World teardown may originate on a long-event worker thread;
                // Unity objects must only be destroyed after returning to the
                // main thread.
                LongEventHandler.ExecuteWhenFinished(() => Object.Destroy(owned));
            }
        }
    }
}
