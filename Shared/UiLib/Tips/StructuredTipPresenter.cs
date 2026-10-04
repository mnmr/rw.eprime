using System;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    internal interface IStructuredTipSource
    {
        string StableKey { get; }
        StructuredTip? Resolve();
    }

    /// Owns the complete lifecycle and window for the mod's structured tips
    /// (one presenter per mod assembly, window id from TipHost).
    [StaticConstructorOnStartup]
    internal static class StructuredTipPresenter
    {
        private const float HoverDelay = 0.45f;

        // Cache contract:
        // Owner: process-level structured-tooltip presenter.
        // Key: producer stable key for the continuously hovered region.
        // Value: one frozen StructuredTip and its immutable cached geometry.
        // Dependencies: stable key, continuous-hover session, geometry's own
        // UI metric revision (TipHost), explicit suppression (SetSuppressed or
        // the Begin/EndSuppression depth), and an optional screen-space
        // exclusion rectangle supplied by the producer.
        // Refresh policy: resolve once when the hover delay opens a session;
        // suppression changes reset the session immediately.
        // Equality policy: the same session retains model identity.
        // Teardown: Reset on world teardown, registry-revision change and
        // producer reset; every Begin/EndSuppression owner pairs them on close,
        // failure and owner teardown.
        private static readonly TooltipDisplayGate displayGate =
            new TooltipDisplayGate();
        private static readonly Action drawWindow = DrawWindow;
        private static readonly Texture2D atlas = ActiveTip.TooltipBGAtlas;
        private static StructuredTip? frozen;
        private static WrTipUI.PreparedTip frozenGeometry;
        private static Vector2 frozenSize;
        private static int suppressionDepth;

        internal static void TipRegion(Rect rect, StructuredTip? tip)
        {
            if (tip == null || !IsHovered(rect)) return;
            Present(tip.StableKey, tip, null, default, hasExclusion: false);
        }

        internal static void TipRegion(Rect rect, IStructuredTipSource? source)
        {
            if (source == null || !IsHovered(rect)) return;
            Present(source.StableKey, null, source, default, hasExclusion: false);
        }

        /// Keeps the tooltip window outside exclusionRect (an adjacent
        /// interactive control), or omits it when nothing fits.
        internal static void TipRegion(Rect rect, Rect exclusionRect,
            IStructuredTipSource? source)
        {
            if (source == null || !IsHovered(rect)) return;
            Present(source.StableKey, null, source, exclusionRect, hasExclusion: true);
        }

        /// The caller has already performed its one authoritative hit test for
        /// this repaint, so registering that source must not hit-test again.
        internal static void PresentHovered(IStructuredTipSource? source)
        {
            if (source == null || Event.current.type != EventType.Repaint) return;
            Present(source.StableKey, null, source, default, hasExclusion: false);
        }

        internal static void Reset()
        {
            displayGate.Reset();
            frozen = null;
            frozenGeometry = default(WrTipUI.PreparedTip);
            frozenSize = default(Vector2);
        }

        internal static void SetSuppressed(bool value)
        {
            displayGate.SetSuppressed(value);
            frozen = null;
            frozenGeometry = default(WrTipUI.PreparedTip);
            frozenSize = default(Vector2);
        }

        /// Hides tips while an owned popup takes interaction; pair with
        /// EndSuppression on close, failure and owner teardown.
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
            suppressionDepth == 0
            && Event.current.type == EventType.Repaint && Mouse.IsOver(rect);

        private static void Present(string stableKey, StructuredTip? ready,
            IStructuredTipSource? source, Rect exclusionRect, bool hasExclusion)
        {
            TooltipDisplayState state = displayGate.Observe(
                stableKey, Time.frameCount, Time.realtimeSinceStartup, HoverDelay);
            if (state == TooltipDisplayState.Suppressed
                || state == TooltipDisplayState.Pending)
                return;
            if (state == TooltipDisplayState.Opened)
            {
                frozen = ready ?? source?.Resolve();
                if (frozen == null) return;
                frozenGeometry = WrTipUI.PrepareTip(
                    frozen.Model, WrTipUI.MaxContentWidth);
                frozenSize = frozenGeometry.Size;
            }
            if (frozen == null || Find.WindowStack == null) return;

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
            var windowRect = new Rect(x, y, frozenSize.x, frozenSize.y);
            Find.WindowStack.ImmediateWindow(TipHost.WindowId, windowRect,
                WindowLayer.Super, drawWindow, doBackground: false,
                absorbInputAroundWindow: false, shadowAlpha: 0f);
        }

        private static void DrawWindow()
        {
            if (frozen == null || atlas == null) return;
            var rect = new Rect(0f, 0f, frozenSize.x, frozenSize.y);
            Widgets.DrawAtlas(rect, atlas);
            WrTipUI.Draw(rect, frozenGeometry);
        }
    }
}
