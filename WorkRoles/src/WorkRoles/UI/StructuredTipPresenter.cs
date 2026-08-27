using System;
using RimShared.Common;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
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
        private const int WindowId = 0x57525450; // WRTP

        // Cache contract:
        // Owner: process-level structured-tooltip presenter.
        // Key: producer stable key for the continuously hovered region.
        // Value: one frozen StructuredTip and its immutable cached geometry.
        // Dependencies: stable key, continuous-hover session, explicit menu
        // suppression, and the model's cached layout inputs.
        // Refresh policy: resolve once when the hover delay opens a session;
        // menu open/close events reset the session immediately.
        // Equality policy: the same session retains model identity.
        // Teardown: Reset on language change, window close, and game teardown.
        private static readonly TooltipDisplayGate displayGate =
            new TooltipDisplayGate();
        private static readonly Action drawWindow = DrawWindow;
        private static readonly Texture2D atlas = ActiveTip.TooltipBGAtlas;
        private static StructuredTip? frozen;
        private static Vector2 frozenSize;

        internal static void TipRegion(Rect rect, StructuredTip tip)
        {
            if (tip == null || !IsHovered(rect)) return;
            Present(tip.StableKey, tip, null);
        }

        internal static void TipRegion(Rect rect, IStructuredTipSource source)
        {
            if (source == null || !IsHovered(rect)) return;
            Present(source.StableKey, null, source);
        }

        internal static void Reset()
        {
            displayGate.Reset();
            frozen = null;
            frozenSize = default(Vector2);
        }

        internal static void SetSuppressed(bool value)
        {
            displayGate.SetSuppressed(value);
            frozen = null;
            frozenSize = default(Vector2);
        }

        private static bool IsHovered(Rect rect) =>
            Event.current.type == EventType.Repaint && Mouse.IsOver(rect);

        private static void Present(
            string stableKey, StructuredTip? ready, IStructuredTipSource? source)
        {
            TooltipDisplayState state = displayGate.Observe(
                stableKey, Time.frameCount, Time.realtimeSinceStartup, HoverDelay);
            if (state == TooltipDisplayState.Suppressed
                || state == TooltipDisplayState.Pending)
                return;
            if (state == TooltipDisplayState.Opened)
            {
                frozen = ready ?? source!.Resolve(); // exactly one of ready/source is set
                if (frozen == null) return;
                frozenSize = WrTipUI.Measure(frozen.Model, WrTipUI.MaxContentWidth);
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
            WrTipUI.Draw(rect, frozen.Model);
        }
    }
}
