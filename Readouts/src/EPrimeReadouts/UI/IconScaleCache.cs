using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Per-def icon scale correction so icons look visually same-sized: item
    /// textures carry wildly different amounts of transparent padding (Cloth
    /// nearly fills its texture, Penoxycyline floats in empty space). Each
    /// def's uiIcon is measured once per physical-resolution/UI-scale epoch —
    /// alpha bounding box via the shared IconAlphaProbe — and the resulting
    /// factor normalizes the opaque content toward a common coverage.
    /// Rendering only reads the cached value.
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
        // Equality policy: each def is measured at most once per display
        //               epoch, and publishing a value equal to the one
        //               consumers already observe (previous epoch's value, or
        //               the neutral 1 fallback) does not advance Revision.
        // Teardown: world teardown preserves CPU measurements and the process
        //           failure latch, and releases only the shared probe's
        //           readback texture.
        private static readonly DisplayEpochCache<ThingDef, float> measurements =
            new DisplayEpochCache<ThingDef, float>();
        private static readonly FrameBatchGate processGate = new FrameBatchGate();
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
                // Consumers already render missing entries at neutral scale,
                // so publishing a value equal to what ScaleFor reported is a
                // no-op and must not advance the revision (it would force a
                // full base-surface rebuild per measured def while the queue
                // drains after load or a display-metric change).
                float observed = measurements.TryGet(def, out float prior)
                    ? prior : 1f;
                measurements.Publish(def, scale);
                if (scale != observed) unchecked { revision++; }
            }
        }

        private static float Measure(ThingDef def)
        {
            var tex = def.uiIcon;
            if (tex == null || tex == BaseContent.BadTex) return 1f;

            int sampleSize = Mathf.Max(1,
                Mathf.RoundToInt(LayoutMetrics.IconSize * Prefs.UIScale));
            IconAlphaBounds bounds =
                IconAlphaProbe.Measure(tex, sampleSize, AlphaThreshold);
            if (!bounds.HasOpaque) return 1f; // fully transparent — leave alone

            // Vanilla ThingIcon already applies the def's own draw scale; fold
            // it in so we correct what actually lands on screen.
            return IconScaleMath.CorrectionFor(
                bounds.OpaqueExtent, sampleSize, GenUI.IconDrawScale(def));
        }

        internal static void ReleaseGraphics()
        {
            processGate.Reset();
            IconAlphaProbe.ReleaseReader();
        }
    }
}
