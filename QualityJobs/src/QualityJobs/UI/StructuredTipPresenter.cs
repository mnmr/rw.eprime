using System;
using QualityJobs.Core;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    internal interface IStructuredTipSource
    {
        string StableKey { get; }
        StructuredTip? Resolve();
    }

    /// Owns the complete lifecycle and window for this mod's structured tips.
    [StaticConstructorOnStartup]
    internal static class StructuredTipPresenter
    {
        private const float HoverDelay = 0.45f;
        private const int WindowId = 0x514A5450; // QJTP

        // Cache contract:
        // Owner: process-level structured-tooltip presenter.
        // Key: producer stable key for the continuously hovered region.
        // Value: one frozen StructuredTip and its immutable cached geometry.
        // Dependencies: stable key, continuous-hover session, geometry's own
        // UI metric revision, explicit suppression depth, and an optional
        // screen-space exclusion rectangle supplied by the producer.
        // Refresh policy: resolve once when the hover delay opens a session;
        // suppress and reset immediately when an owned popup takes interaction.
        // Equality policy: the same session retains model identity.
        // Teardown: Reset on UI-metric changes and producer reset; every popup
        // owner pairs BeginSuppression with EndSuppression on close, failure,
        // and owner teardown.
        private static readonly TooltipDisplayGate displayGate =
            new TooltipDisplayGate();
        private static readonly Action drawWindow = DrawWindow;
        private static readonly Texture2D atlas = ActiveTip.TooltipBGAtlas;
        private static StructuredTip? frozen;
        private static Vector2 frozenSize;
        private static int suppressionDepth;

        internal static void TipRegion(Rect rect, IStructuredTipSource source)
            => TipRegion(rect, default, hasExclusion: false, source);

        internal static void TipRegion(Rect rect, Rect exclusionRect,
            IStructuredTipSource source)
            => TipRegion(rect, exclusionRect, hasExclusion: true, source);

        private static void TipRegion(Rect rect, Rect exclusionRect,
            bool hasExclusion, IStructuredTipSource source)
        {
            if (source == null || suppressionDepth != 0 || !IsHovered(rect)) return;
            TooltipDisplayState state = displayGate.Observe(
                source.StableKey, Time.frameCount,
                Time.realtimeSinceStartup, HoverDelay);
            if (state == TooltipDisplayState.Pending) return;
            if (state == TooltipDisplayState.Opened)
            {
                frozen = source.Resolve();
                if (frozen == null) return;
                frozenSize = WrTipUI.Measure(frozen.Model, WrTipUI.MaxContentWidth);
            }
            if (frozen == null) return;

            Vector2 mouse = Verse.UI.GUIToScreenPoint(Event.current.mousePosition);
            Rect screenExclusion = default;
            if (hasExclusion)
            {
                Vector2 min = Verse.UI.GUIToScreenPoint(exclusionRect.min);
                Vector2 max = Verse.UI.GUIToScreenPoint(exclusionRect.max);
                screenExclusion = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            if (!TooltipPlacement.TryPlace(
                    mouse.x, mouse.y, frozenSize.x, frozenSize.y,
                    Verse.UI.screenWidth, Verse.UI.screenHeight, hasExclusion,
                    screenExclusion.x, screenExclusion.y,
                    screenExclusion.width, screenExclusion.height,
                    out float x, out float y))
                return;
            var position = new Vector2(x, y);
            var windowRect = new Rect(position.x, position.y,
                frozenSize.x, frozenSize.y);
            Find.WindowStack.ImmediateWindow(WindowId, windowRect,
                WindowLayer.Super, drawWindow, doBackground: false,
                absorbInputAroundWindow: false, shadowAlpha: 0f);
        }

        internal static void Reset()
        {
            displayGate.Reset();
            frozen = null;
            frozenSize = default;
        }

        internal static void BeginSuppression()
        {
            suppressionDepth++;
            Reset();
        }

        internal static void EndSuppression()
        {
            if (suppressionDepth > 0) suppressionDepth--;
            Reset();
        }

        private static bool IsHovered(Rect rect) =>
            Event.current.type == EventType.Repaint && Mouse.IsOver(rect);

        private static void DrawWindow()
        {
            if (frozen == null || atlas == null) return;
            var rect = new Rect(0f, 0f, frozenSize.x, frozenSize.y);
            Widgets.DrawAtlas(rect, atlas);
            WrTipUI.Draw(rect, frozen.Model);
        }
    }
}
