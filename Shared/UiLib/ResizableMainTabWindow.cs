using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// A player-sized main tab's remembered size, one axis at a time; 0 means
    /// automatic (content-fitted) on that axis. Backed by the mod's own
    /// ModSettings; RequestWrite defers the settings file write to the mod's
    /// outside-OnGUI batch.
    public interface IWindowSizePreference
    {
        float Width { get; set; }
        float Height { get; set; }
        void RequestWrite();
    }

    /// A bottom-anchored main tab the player can resize, which vanilla main
    /// tabs cannot. Vanilla's own resizer is off; the grip sits in the
    /// window's top-right corner in its own immediate window on the Dialog
    /// layer (the contents group clips the corner, plain ExtraOnGUI paints
    /// under GUI windows, and a same-layer grip gets buried when a click
    /// refocuses the tab). Width grows to the right and height grows UP,
    /// the bottom edge stays pinned to the bar. Double-click returns to
    /// automatic sizing. Geometry changes are queued and applied between
    /// frames in WindowUpdate: a windowRect change mid-event desyncs
    /// Layout from the later passes of the same frame. While an axis is not
    /// player-sized the window grows (never shrinks) to TargetSize so a
    /// colony that outgrows the design floor still fits.
    ///
    /// Subclasses supply the content-fitted target, the minimum, the
    /// persisted preference, the grip tooltip and a unique immediate-window
    /// id, and draw their contents in DrawTabContents.
    public abstract class ResizableMainTabWindow : MainTabWindow
    {
        public const float BottomBarHeight = 35f;
        public const float GripSize = 18f;

        private readonly System.Action drawGrip;
        private bool resizing;
        private float resizeBottom;    // screen-space bottom edge, pinned while dragging
        private Vector2 resizeGrab;    // grab point: (distance from right edge, distance from top)
        private readonly PendingUpdate<Rect> pendingWindowRect = new PendingUpdate<Rect>();
        private Rect gripScreen;

        protected ResizableMainTabWindow()
        {
            drawGrip = DrawGripContents;
            resizeable = false;
            draggable = false;   // main tab windows are not draggable
        }

        /// Content-fitted size before clamping; the window never auto-sizes
        /// below MinSize or beyond the screen.
        protected abstract Vector2 TargetSize();

        /// Smallest size the player may drag to.
        protected abstract Vector2 MinSize { get; }

        /// Remembered player size; null keeps every size session-only.
        protected abstract IWindowSizePreference? SizePreference { get; }

        /// Translated tooltip key for the grip (WrTips registry).
        protected abstract string GripTipKey { get; }

        /// Immediate-window id of the grip: unique per window class across
        /// every loaded mod (immediate windows share one stack).
        protected abstract int GripWindowId { get; }

        protected abstract void DrawTabContents(Rect inRect);

        /// Whether the player has sized either axis (grip tint, auto-grow).
        protected bool PlayerSized
        {
            get
            {
                IWindowSizePreference? preference = SizePreference;
                return preference != null && (preference.Width > 0f || preference.Height > 0f);
            }
        }

        public override Vector2 RequestedTabSize => ClampedTargetSize();

        private Vector2 ClampedTargetSize()
        {
            Vector2 min = MinSize;
            Vector2 target = TargetSize();
            return new Vector2(
                Mathf.Clamp(target.x, min.x, Verse.UI.screenWidth),
                Mathf.Clamp(target.y, min.y, Verse.UI.screenHeight - BottomBarHeight));
        }

        private Rect AutoSizeRect()
        {
            Vector2 size = ClampedTargetSize();
            return new Rect(0f, Verse.UI.screenHeight - BottomBarHeight - size.y, size.x, size.y);
        }

        /// Re-applies the persisted size each open, clamped between the
        /// minimum and the screen; bottom-left anchor holds.
        protected override void SetInitialSizeAndPosition()
        {
            resizing = false;
            pendingWindowRect.Clear();
            base.SetInitialSizeAndPosition();
            IWindowSizePreference? preference = SizePreference;
            if (preference == null || (preference.Width <= 0f && preference.Height <= 0f))
                return;
            Vector2 min = MinSize;
            if (preference.Width > 0f)
                windowRect.width = Mathf.Clamp(preference.Width, min.x, Verse.UI.screenWidth);
            if (preference.Height > 0f)
                windowRect.height = Mathf.Clamp(preference.Height, min.y,
                    Verse.UI.screenHeight - BottomBarHeight);
            windowRect.x = 0f;
            windowRect.y = Verse.UI.screenHeight - BottomBarHeight - windowRect.height;
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            gripScreen = new Rect(windowRect.xMax - GripSize - 2f,
                windowRect.y + 2f, GripSize, GripSize);
            Find.WindowStack.ImmediateWindow(GripWindowId, gripScreen, WindowLayer.Dialog,
                drawGrip, doBackground: false, absorbInputAroundWindow: false, 0f);
        }

        private void DrawGripContents()
        {
            IWindowSizePreference? preference = SizePreference;
            var local = new Rect(0f, 0f, GripSize, GripSize);
            WrTips.Key(GripTipKey).Region(local);
            // Reddish = player-sized. UV flip, not rotation: GUI matrix
            // rotation misplaces draws inside GUI windows.
            GUI.color = PlayerSized ? new Color(1f, 0.5f, 0.45f) : Color.white;
            GUI.DrawTextureWithTexCoords(local, TexUI.WinExpandWidget, new Rect(0f, 1f, 1f, -1f));
            GUI.color = Color.white;

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && local.Contains(e.mousePosition))
            {
                if (e.clickCount == 2 && preference != null)
                {
                    preference.Width = preference.Height = 0f;
                    preference.RequestWrite();
                    resizing = false;
                    pendingWindowRect.QueueUser(AutoSizeRect());
                }
                else
                {
                    Vector2 screenMouse = gripScreen.position + e.mousePosition;
                    resizing = true;
                    resizeBottom = windowRect.yMax;
                    resizeGrab = new Vector2(windowRect.xMax - screenMouse.x, screenMouse.y - windowRect.y);
                    pendingWindowRect.QueueUser(windowRect);
                }
                e.Use();
            }

            if (!resizing) return;
            if (e.type == EventType.Repaint)
            {
                // Real cursor in screen-UI coords (game-window origin, y down).
                var gamePx = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                Vector2 mouseUI = gamePx / Prefs.UIScale;

                Vector2 min = MinSize;
                float width = Mathf.Clamp(mouseUI.x + resizeGrab.x, min.x, Verse.UI.screenWidth);
                float top = mouseUI.y - resizeGrab.y;
                float height = Mathf.Clamp(resizeBottom - top, min.y,
                    Verse.UI.screenHeight - BottomBarHeight);
                var nextWindowRect = new Rect(0f, resizeBottom - height, width, height);
                pendingWindowRect.QueueUser(nextWindowRect);

                // Warp only when a clamp stops the window: the cursor rides the
                // handle instead of drifting over other UI. Windows-only.
                var grabUI = new Vector2(nextWindowRect.width - resizeGrab.x, nextWindowRect.y + resizeGrab.y);
                if ((mouseUI - grabUI).sqrMagnitude > 4f && Win32Cursor.TryGetPos(out Vector2 desktopPx))
                {
                    Vector2 desktopOrigin = desktopPx - gamePx;
                    Win32Cursor.SetPos(desktopOrigin + grabUI * Prefs.UIScale);
                }
            }
            if (e.type == EventType.MouseUp || !Input.GetMouseButton(0))
            {
                resizing = false;
                if (preference != null)
                {
                    Rect persistedRect = pendingWindowRect.TryGetUser(out Rect pendingUserRect)
                        ? pendingUserRect : windowRect;
                    preference.Width = persistedRect.width;
                    preference.Height = persistedRect.height;
                    preference.RequestWrite();
                }
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        /// Pending geometry applies between frames.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (pendingWindowRect.TryConsume(out Rect nextWindowRect))
                windowRect = nextWindowRect;
        }

        /// Grow-only mid-session resize before the contents draw; manual
        /// geometry wins while dragging, if both kinds of update are waiting
        /// for WindowUpdate, and per axis the player has explicitly sized
        /// (auto-grow would otherwise undo every shrink each frame).
        public override void DoWindowContents(Rect inRect)
        {
            if (!resizing)
            {
                IWindowSizePreference? preference = SizePreference;
                Vector2 target = ClampedTargetSize();
                Rect nextWindowRect = windowRect;
                bool grew = false;
                if (target.x > nextWindowRect.width + 1f
                    && !(preference != null && preference.Width > 0f))
                {
                    nextWindowRect.width = target.x;
                    grew = true;
                }
                if (target.y > nextWindowRect.height + 1f
                    && !(preference != null && preference.Height > 0f))
                {
                    nextWindowRect.height = target.y;
                    grew = true;
                }
                if (grew)
                {
                    nextWindowRect.x = 0f;
                    nextWindowRect.y = Verse.UI.screenHeight - BottomBarHeight - nextWindowRect.height;
                    pendingWindowRect.QueueAutomatic(nextWindowRect);
                }
            }
            DrawTabContents(inRect);
        }

        public override void PostClose()
        {
            base.PostClose();
            resizing = false;
            pendingWindowRect.Clear();
        }
    }

    /// Unity has no portable cursor-warp API: Windows-only, no-ops elsewhere.
    internal static class Win32Cursor
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Point { public int X, Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point p);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private static bool Available =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        public static bool TryGetPos(out Vector2 px)
        {
            if (Available && GetCursorPos(out Point p))
            {
                px = new Vector2(p.X, p.Y);
                return true;
            }
            px = default;
            return false;
        }

        public static void SetPos(Vector2 px)
        {
            if (Available) SetCursorPos((int)px.x, (int)px.y);
        }
    }
}
