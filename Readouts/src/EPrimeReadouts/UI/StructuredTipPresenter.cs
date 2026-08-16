using System;
using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    internal interface IStructuredTipSource
    {
        string StableKey { get; }
        StructuredTip Resolve();
    }

    /// Owns the complete lifecycle and window for this mod's structured tips.
    [StaticConstructorOnStartup]
    internal static class StructuredTipPresenter
    {
        private const float HoverDelay = 0.45f;
        private const int WindowId = 0x45505254; // EPRT

        // Cache contract:
        // Owner: process-level structured-tooltip presenter.
        // Key: producer stable key for the continuously hovered region.
        // Value: one frozen StructuredTip and its immutable cached geometry.
        // Dependencies: stable key, continuous-hover session, and geometry's
        // own UI metric revision.
        // Refresh policy: resolve once when the hover delay opens a session.
        // Equality policy: the same session retains model identity.
        // Teardown: Reset on world teardown and producer reset.
        private static readonly TooltipDisplayGate displayGate =
            new TooltipDisplayGate();
        private static readonly Action drawWindow = DrawWindow;
        private static readonly Texture2D atlas = ActiveTip.TooltipBGAtlas;
        private static StructuredTip? frozen;
        private static WrTipUI.PreparedTip frozenGeometry;
        private static Vector2 frozenSize;

        internal static void TipRegion(Rect rect, StructuredTip? tip)
        {
            if (tip == null || !IsHovered(rect)) return;
            Present(tip.StableKey, tip, null);
        }

        internal static void TipRegion(Rect rect, IStructuredTipSource? source)
        {
            if (source == null || !IsHovered(rect)) return;
            Present(source.StableKey, null, source);
        }

        internal static void Reset()
        {
            displayGate.Reset();
            frozen = null;
            frozenGeometry = default(WrTipUI.PreparedTip);
            frozenSize = default(Vector2);
        }

        private static bool IsHovered(Rect rect) =>
            Event.current.type == EventType.Repaint && Mouse.IsOver(rect);

        private static void Present(
            string stableKey, StructuredTip? ready, IStructuredTipSource? source)
        {
            TooltipDisplayState state = displayGate.Observe(
                stableKey, Time.frameCount, Time.realtimeSinceStartup, HoverDelay);
            if (state == TooltipDisplayState.Pending) return;
            if (state == TooltipDisplayState.Opened)
            {
                frozen = ready ?? source?.Resolve();
                if (frozen == null) return;
                frozenGeometry = WrTipUI.Prepare(
                    frozen.Model, WrTipUI.MaxContentWidth);
                frozenSize = frozenGeometry.Size;
            }
            if (frozen == null || Find.WindowStack == null) return;

            Vector2 mouse = Verse.UI.GUIToScreenPoint(Event.current.mousePosition);
            Vector2 position = Position(mouse, frozenSize);
            var windowRect = new Rect(position.x, position.y,
                frozenSize.x, frozenSize.y);
            Find.WindowStack.ImmediateWindow(WindowId, windowRect,
                WindowLayer.Super, drawWindow, doBackground: false,
                absorbInputAroundWindow: false, shadowAlpha: 0f);
        }

        private static Vector2 Position(Vector2 mouse, Vector2 size)
        {
            float y = mouse.y + 14f + size.y < Verse.UI.screenHeight
                ? mouse.y + 14f
                : mouse.y - 5f - size.y >= 0f
                    ? mouse.y - 5f - size.y
                    : Verse.UI.screenHeight - 14f - size.y;
            float x = mouse.x + 16f + size.x < Verse.UI.screenWidth
                ? mouse.x + 16f
                : mouse.x - 4f - size.x;
            return new Vector2(
                Mathf.Clamp(x, 0f, Mathf.Max(0f, Verse.UI.screenWidth - size.x)),
                Mathf.Clamp(y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - size.y)));
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
