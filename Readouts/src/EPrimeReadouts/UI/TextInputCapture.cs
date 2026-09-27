using UnityEngine;

namespace EPrimeReadouts.UI
{
    /// Keyboard capture for the mod's dialog text fields, reported through
    /// the same WindowStack.AnySearchWidgetFocused gate as the panel search
    /// (Patch_WindowStack), so typed keys reach only the field and never game
    /// key bindings or camera panning. Every such field is a named control,
    /// and IMGUI grants keyboard focus only to text fields here.
    internal static class TextInputCapture
    {
        // Frame stamp, like the panel search: capture ends within one frame
        // of the dialog no longer drawing.
        private static int capturedFrame = -1;

        internal static bool Active => Time.frameCount - capturedFrame <= 1;

        /// Call from a window's DoWindowContents. Checks once per frame, on
        /// Layout, and reads the focused control's name only while some
        /// control holds keyboard focus.
        internal static void Observe()
        {
            if (Event.current.type != EventType.Layout) return;
            if (GUIUtility.keyboardControl == 0) return;
            if (GUI.GetNameOfFocusedControl().Length == 0) return;
            capturedFrame = Time.frameCount;
        }
    }
}
