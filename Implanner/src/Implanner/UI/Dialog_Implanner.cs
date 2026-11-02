using RimShared.Common;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// The free-floating Implanner window (toolbar button, welcome dialog):
    /// a resizable Window hosting one ImplannerView, with frame-only
    /// dragging. The bottom-bar alternative is MainTabWindow_Implanner; the
    /// contents and their lifecycle live in the view.
    public class Dialog_Implanner : Window
    {
        private readonly ImplannerView view;

        public Dialog_Implanner()
        {
            view = new ImplannerView(this);
            doCloseX = true;
            // Window movement is frame-only (DoFrameDragZones): content drags
            // must stay with the content (paintable pickers, whose draggable
            // widgets poll Input and never consume the IMGUI event), so
            // vanilla whole-window dragging cannot be used.
            draggable = false;
            resizeable = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            // Cached per-zone delegates: immediate windows draw every frame.
            dragZoneDraws = new System.Action[]
            {
                () => DragZoneContents(0),
                () => DragZoneContents(1),
                () => DragZoneContents(2),
                () => DragZoneContents(3),
            };
        }

        public override Vector2 InitialSize => new Vector2(1220f, 760f);

        public override void PreOpen()
        {
            base.PreOpen();
            // One host at a time: the view's selected tab carries over.
            ImplannerEntry.CloseTab();
            view.Open();
        }

        public override void PostClose()
        {
            base.PostClose();
            view.Close();
        }

        public override void DoWindowContents(Rect inRect) => view.Draw(inRect);

        // Frame-only window dragging via immediate windows over the margin
        // strips (the proven WorkRoles resize-grip pattern): they sit on the
        // Dialog layer with their own event flow, so content controls and
        // Verse's own MouseDown consumption never interfere. Movement tracks
        // the real cursor on Repaint and applies between frames through
        // WindowUpdate — mid-event windowRect changes desync IMGUI passes.
        private const int DragZoneIdBase = 173350010;
        private const float CloseClearance = 48f;
        private const float GripClearance = 30f;

        private readonly PendingUpdate<Rect> pendingWindowRect = new PendingUpdate<Rect>();
        private readonly System.Action[] dragZoneDraws;
        private readonly Rect[] dragZoneScreens = new Rect[4];
        private bool frameDragging;
        private Vector2 frameDragGrab;

        private void DoFrameDragZones()
        {
            float m = Margin;
            dragZoneScreens[0] = new Rect(windowRect.x, windowRect.y,
                windowRect.width - CloseClearance, m);
            dragZoneScreens[1] = new Rect(windowRect.x, windowRect.y + m,
                m, windowRect.height - m * 2f);
            dragZoneScreens[2] = new Rect(windowRect.xMax - m,
                windowRect.y + CloseClearance, m,
                windowRect.height - CloseClearance - GripClearance);
            dragZoneScreens[3] = new Rect(windowRect.x, windowRect.yMax - m,
                windowRect.width - GripClearance, m);
            // SubSuper layer: clicking the dialog refocuses it to the top of
            // its own (Dialog) layer, which would bury same-layer zones —
            // runtime-verified: same-layer zones never receive their events.
            for (int i = 0; i < 4; i++)
                Find.WindowStack.ImmediateWindow(DragZoneIdBase + i,
                    dragZoneScreens[i], WindowLayer.SubSuper, dragZoneDraws[i],
                    doBackground: false, absorbInputAroundWindow: false, 0f);
        }

        private void DragZoneContents(int zone)
        {
            Rect screen = dragZoneScreens[zone];
            var local = new Rect(0f, 0f, screen.width, screen.height);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0
                && local.Contains(current.mousePosition))
            {
                frameDragging = true;
                frameDragGrab = screen.position + current.mousePosition
                    - windowRect.position;
                current.Use();
            }
            if (!frameDragging) return;
            if (current.type == EventType.Repaint)
            {
                // Real cursor in screen-UI coords (game-window origin, y down).
                var gamePx = new Vector2(Input.mousePosition.x,
                    Screen.height - Input.mousePosition.y);
                Vector2 mouseUI = gamePx / Prefs.UIScale;
                Vector2 position = mouseUI - frameDragGrab;
                position.x = Mathf.Clamp(position.x,
                    100f - windowRect.width, Verse.UI.screenWidth - 100f);
                position.y = Mathf.Clamp(position.y,
                    0f, Verse.UI.screenHeight - 100f);
                pendingWindowRect.QueueUser(new Rect(position, windowRect.size));
            }
            if (current.type == EventType.MouseUp || !Input.GetMouseButton(0))
            {
                frameDragging = false;
                if (current.type == EventType.MouseUp) current.Use();
            }
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            DoFrameDragZones();
        }

        /// Pending geometry applies between frames: mid-event windowRect
        /// changes desync Layout from later passes. The view refreshes its
        /// front snapshots afterwards against the settled width.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (pendingWindowRect.TryConsume(out Rect nextWindowRect))
                windowRect = nextWindowRect;
            view.Update(windowRect.width - 2f * Margin);
        }
    }
}
