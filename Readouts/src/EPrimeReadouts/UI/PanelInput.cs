using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Input routing for the map overlay. WindowStack.GetWindowAt includes
    /// windows that Window.WindowOnGUI hides, such as an open dev palette
    /// after dev mode is disabled. Only visible windows may block our panel.
    internal static class PanelInput
    {
        internal static bool HasBlockingWindow(
            WindowStack windows, Vector2 mouse, bool devMode, bool screenshotMode)
        {
            // Inspect current input geometry through the stack's allocation-free
            // indexer. Do not retain windows or use Windows (it allocates a
            // read-only wrapper). Keep looking past hidden windows so a visible
            // window underneath still owns its input.
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                Window window = windows[i];
                if (window.onlyDrawInDevMode && !devMode) continue;
                if (!window.drawInScreenshotMode && screenshotMode) continue;
                if (window.absorbInputAroundWindow
                    || window.windowRect.Contains(mouse)) return true;
            }
            return false;
        }

        /// The panel's visibility-aware input gate has already passed. Vanilla
        /// ButtonImage rechecks Mouse.IsOver against hidden windows for its tint;
        /// draw that feedback locally and keep the normal IMGUI button behavior.
        internal static bool ButtonImage(Rect rect, Texture2D texture, Color tint)
        {
            GUI.color = rect.Contains(Event.current.mousePosition)
                ? GenUI.MouseoverColor : tint;
            GUI.DrawTexture(rect, texture);
            GUI.color = tint;
            bool clicked = Widgets.ButtonInvisible(rect);
            GUI.color = Color.white;
            return clicked;
        }
    }
}
