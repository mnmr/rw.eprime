namespace RimShared.Common
{
    /// <summary>
    /// The IMGUI pass kinds window content distinguishes. Mapped from the
    /// engine event type at the window boundary; Core stays engine-free.
    /// </summary>
    public enum ContentPassKind
    {
        Repaint,
        Layout,
        MouseDown,
        MouseUp,
        MouseDrag,
        KeyDown,
        ScrollWheel,
        /// Clipboard command passes (execute/validate) for text fields.
        Command,
        ContextClick,
        Other,
    }

    /// <summary>
    /// Decides whether a window content pass runs for an IMGUI event.
    /// The engine delivers one full pass per queued event; while a mouse
    /// button is held and moving, drag events arrive at the mouse polling
    /// rate, multiplying window draws many times per frame. Rect-based
    /// content needs no Layout pass and reads drags by polling positions,
    /// so both are skipped - except drags owned by a native control
    /// (scrollbar thumb, slider, text selection), which consume the events.
    /// </summary>
    public static class ContentPassPolicy
    {
        public static bool DrawsContent(ContentPassKind kind, bool nativeControlOwnsDrag)
        {
            switch (kind)
            {
                case ContentPassKind.Repaint:
                case ContentPassKind.MouseDown:
                case ContentPassKind.MouseUp:
                case ContentPassKind.KeyDown:
                case ContentPassKind.ScrollWheel:
                case ContentPassKind.Command:
                case ContentPassKind.ContextClick:
                    return true;
                case ContentPassKind.MouseDrag:
                    return nativeControlOwnsDrag;
                default:
                    return false;
            }
        }
    }
}
