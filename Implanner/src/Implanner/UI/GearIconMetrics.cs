using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// Per-def icon correction so gear icons center on their opaque pixels:
    /// item textures carry uneven transparent padding (the minigun's glyph
    /// sits low in its canvas), and vanilla icon drawing centers the canvas,
    /// not the artwork. Each def's uiIcon is measured once per display epoch —
    /// alpha bounding box via the shared IconAlphaProbe — publishing a
    /// normalized center offset and a coverage scale. Rendering only reads
    /// the cached value; measurement runs from the game-component update,
    /// never from OnGUI.
    // Cache contract:
    // Owner: process/loaded def set.
    // Key: ThingDef identity plus the display epoch (screen size, UI scale).
    // Value: immutable normalized correction (offset in icon-rect fractions,
    //   coverage scale factor).
    // Dependencies: uiIcon pixels, screen dimensions, Prefs.UIScale.
    // Refresh policy: measured once per epoch, in bounded game-component
    //   update batches queued by render requests; a stale correction stays
    //   readable until its replacement publishes; the first measurement
    //   failure disables further probes and publishes neutral corrections.
    // Equality policy: each def is measured at most once per epoch.
    // Teardown: world teardown keeps CPU measurements and the failure latch,
    //   releasing only the shared probe's readback texture (main thread).
    internal static class GearIconMetrics
    {
        private const byte AlphaThreshold = 24;
        private const int SampleSize = 64;

        internal readonly struct Correction
        {
            internal Correction(float coverage, Vector2 offset)
            {
                Coverage = coverage;
                Offset = offset;
            }

            /// Opaque extent as a fraction of the texture canvas (0 while
            /// unmeasured or unmeasurable). Consumers size their draw rect
            /// from this so the artwork — not the padded canvas — hits the
            /// intended on-screen size.
            internal float Coverage { get; }

            /// Fractions of the icon rect, GUI coordinates (y grows down).
            internal Vector2 Offset { get; }
        }

        private static readonly DisplayEpochCache<ThingDef, Correction> measurements =
            new DisplayEpochCache<ThingDef, Correction>();
        private static bool measurementFailed;

        /// Cached correction, or neutral while unmeasured (the def is queued).
        internal static Correction For(ThingDef def)
        {
            if (measurements.TryGet(def, out Correction correction))
                return correction;
            measurements.Request(def);
            return new Correction(0f, Vector2.zero);
        }

        /// Drained from GameComponentUpdate with a small per-frame budget.
        internal static void ProcessPending(int budget = 2)
        {
            measurements.Observe(new DisplayEpoch(
                Screen.width, Screen.height, Prefs.UIScale));
            while (budget-- > 0 && measurements.TryTake(out ThingDef def))
            {
                var correction = new Correction(0f, Vector2.zero);
                if (!measurementFailed)
                {
                    try
                    {
                        correction = Measure(def);
                    }
                    catch (System.Exception exception)
                    {
                        measurementFailed = true;
                        Log.Warning("[Implanner] Icon measurement failed; "
                            + "icons use neutral placement: "
                            + exception.GetType().Name + ": " + exception.Message);
                    }
                }
                measurements.Publish(def, correction);
            }
        }

        private static Correction Measure(ThingDef def)
        {
            Texture2D? tex = def.uiIcon;
            if (tex == null || tex == BaseContent.BadTex)
                return new Correction(0f, Vector2.zero);

            IconAlphaBounds bounds =
                IconAlphaProbe.Measure(tex, SampleSize, AlphaThreshold);
            if (!bounds.HasOpaque) return new Correction(0f, Vector2.zero);
            return new Correction(bounds.Coverage, bounds.CenterOffsetGui);
        }

        internal static void ReleaseGraphics() => IconAlphaProbe.ReleaseReader();
    }
}
