using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public class MainTabWindow_WorkRoles : MainTabWindow
    {
        private enum Tab { Colonists, Roles, Options }

        private Tab curTab = Tab.Colonists;
        private readonly ColonistsTabView colonistsTab = new ColonistsTabView();
        private readonly RolesTabView rolesTab = new RolesTabView();
        private readonly OptionsTabView optionsTab = new OptionsTabView();

        private const float TabHeight = 32f;

        public MainTabWindow_WorkRoles()
        {
            resizeable = false;
            draggable = false;   // main tab windows are not draggable
        }

        public override Vector2 RequestedTabSize => TargetSize();

        /// Width floors at the design width (fixed chrome overlaps below it) and
        /// grows with the widest chip strip; whichever tab wants more height wins
        /// (small colonies would otherwise cramp the Roles tab). Both capped at
        /// the screen.
        private static Vector2 TargetSize()
        {
            float w = Mathf.Clamp(ColonistsTabView.DesiredWidth() + 200f,
                ColonistsTabView.DefaultWidth, Verse.UI.screenWidth);
            float h = Mathf.Min(
                Mathf.Max(ColonistsTabView.DesiredHeight(), RolesTabView.DesiredHeight()),
                Verse.UI.screenHeight - 35f);
            return new Vector2(w, h);
        }

        /// Re-applies the persisted size each open, clamped between the content
        /// minimums and the screen; bottom-left anchor holds.
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            var settings = WorkRolesMod.Settings;
            if (settings == null || (settings.windowWidth <= 0f && settings.windowHeight <= 0f))
                return;
            var min = TargetSize();
            if (settings.windowWidth > 0f)
                windowRect.width = Mathf.Clamp(settings.windowWidth, min.x, Verse.UI.screenWidth);
            if (settings.windowHeight > 0f)
                windowRect.height = Mathf.Clamp(settings.windowHeight, min.y, Verse.UI.screenHeight - 35f);
            windowRect.x = 0f;
            windowRect.y = Verse.UI.screenHeight - 35f - windowRect.height;
        }

        private bool resizing;
        private float resizeBottom;    // screen-space bottom edge, pinned while dragging
        private Vector2 resizeGrab;    // grab point: (distance from right edge, distance from top)
        private Rect pendingResize;

        /// Resize grip in its own immediate window (the contents group clips
        /// the corner; plain ExtraOnGUI paints under GUI windows). Grows width
        /// right and height UP — the bottom is pinned. Double-click = auto.
        internal const float GripSize = 18f;
        private const int GripWindowId = 147723001;

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            var gripScreen = new Rect(windowRect.xMax - GripSize - 2f, windowRect.y + 2f, GripSize, GripSize);
            // Dialog layer: clicking our window refocuses it to the top of
            // GameUI, which would bury a same-layer grip.
            Find.WindowStack.ImmediateWindow(GripWindowId, gripScreen, WindowLayer.Dialog,
                () => GripContents(gripScreen), doBackground: false, absorbInputAroundWindow: false, 0f);
        }

        private void GripContents(Rect gripScreen)
        {
            var settings = WorkRolesMod.Settings;
            var local = new Rect(0f, 0f, GripSize, GripSize);
            TooltipHandler.TipRegion(local, "WR_ResizeGripTip".Translate());
            // Reddish = player-sized. UV flip, not rotation: GUI matrix
            // rotation misplaces draws inside GUI windows.
            bool custom = settings != null && (settings.windowWidth > 0f || settings.windowHeight > 0f);
            GUI.color = custom ? new Color(1f, 0.5f, 0.45f) : Color.white;
            GUI.DrawTextureWithTexCoords(local, TexUI.WinExpandWidget, new Rect(0f, 1f, 1f, -1f));
            GUI.color = Color.white;

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && local.Contains(e.mousePosition))
            {
                if (e.clickCount == 2 && settings != null)
                {
                    settings.windowWidth = settings.windowHeight = 0f;
                    settings.Write();
                    resizing = false;
                    SetInitialSizeAndPosition();
                }
                else
                {
                    var screenMouse = gripScreen.position + e.mousePosition;
                    resizing = true;
                    resizeBottom = windowRect.yMax;
                    resizeGrab = new Vector2(windowRect.xMax - screenMouse.x, screenMouse.y - windowRect.y);
                    pendingResize = windowRect;
                }
                e.Use();
            }

            if (!resizing) return;
            if (e.type == EventType.Repaint)
            {
                // Real cursor in screen-UI coords (game-window origin, y down).
                var gamePx = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                var mouseUI = gamePx / Prefs.UIScale;

                var min = TargetSize();
                float width = Mathf.Clamp(mouseUI.x + resizeGrab.x, min.x, Verse.UI.screenWidth);
                float top = mouseUI.y - resizeGrab.y;
                float height = Mathf.Clamp(resizeBottom - top, min.y, Verse.UI.screenHeight - 35f);
                pendingResize = new Rect(0f, resizeBottom - height, width, height);

                // Warp only when a clamp stops the window: the cursor rides the
                // handle instead of drifting over other UI. Windows-only.
                var grabUI = new Vector2(pendingResize.width - resizeGrab.x, pendingResize.y + resizeGrab.y);
                if ((mouseUI - grabUI).sqrMagnitude > 4f && Win32Cursor.TryGetPos(out var desktopPx))
                {
                    var desktopOrigin = desktopPx - gamePx;
                    Win32Cursor.SetPos(desktopOrigin + grabUI * Prefs.UIScale);
                }
            }
            if (e.type == EventType.MouseUp || !Input.GetMouseButton(0))
            {
                resizing = false;
                if (settings != null)
                {
                    settings.windowWidth = pendingResize.width;
                    settings.windowHeight = pendingResize.height;
                    settings.Write();
                }
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        /// Drag rect applies between frames: mid-event windowRect changes
        /// desync Layout from later passes.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (resizing)
                windowRect = pendingResize;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RoleDrag.Cancel();
            colonistsTab.Reset();
            rolesTab.Reset();
            optionsTab.Reset();
            KeyOverride.Apply();
        }

        public override void PostClose()
        {
            base.PostClose();
            rolesTab.CommitEdits();
            KeyOverride.Restore();
        }

        private List<TabRecord> tabs;

        // Dev-mode draw cost readout (EMA over GUI passes): the number that
        // proves the snapshot caches hold — idle should read well under 1ms.
        private static readonly System.Diagnostics.Stopwatch drawTimer = new System.Diagnostics.Stopwatch();
        private float drawMsEma = -1f;

        public override void DoWindowContents(Rect inRect)
        {
            drawTimer.Restart();
            try
            {
                DrawContents(inRect);
            }
            finally
            {
                drawTimer.Stop();
                float ms = (float)drawTimer.Elapsed.TotalMilliseconds;
                drawMsEma = drawMsEma < 0f ? ms : drawMsEma * 0.95f + ms * 0.05f;
                if (Prefs.DevMode)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    Widgets.Label(new Rect(inRect.x + 4f, inRect.yMax - 14f, 200f, 14f),
                        $"draw {drawMsEma:0.00} ms/pass");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
            }
        }

        private void DrawContents(Rect inRect)
        {
            // Keyboard navigation runs before any widget sees the event, and
            // only while no text field owns the keyboard (typing in the search
            // box stays typing).
            if (curTab == Tab.Colonists && Event.current.type == EventType.KeyDown
                && GUIUtility.keyboardControl == 0 && colonistsTab.HandleKey(Event.current))
                Event.current.Use();

            // Grow-only mid-session resize: if content grew, expand windowRect; never shrink.
            var target = TargetSize();

            bool grew = false;
            if (target.x > windowRect.width + 1f)
            {
                windowRect.width = target.x;
                grew = true;
            }
            if (target.y > windowRect.height + 1f)
            {
                windowRect.height = target.y;
                grew = true;
            }
            if (grew)
            {
                windowRect.x = 0f;
                windowRect.y = Verse.UI.screenHeight - 35f - windowRect.height;
            }

            // Built once (Func<bool> selection getters): a fresh list with three
            // closures per pass is pure GC pressure.
            tabs ??= new List<TabRecord>
            {
                new TabRecord("WR_ColonistsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Colonists;
                }, () => curTab == Tab.Colonists),
                new TabRecord("WR_RolesTab".Translate(), () => curTab = Tab.Roles, () => curTab == Tab.Roles),
                new TabRecord("WR_OptionsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Options;
                }, () => curTab == Tab.Options),
            };
            Rect content = new Rect(inRect.x, inRect.y + TabHeight, inRect.width, inRect.height - TabHeight);
            Widgets.DrawMenuSection(content);
            TabDrawer.DrawTabs(content, tabs);

            // Per-tab action button in the window's top-right corner, beside the tab
            // strip: Fix My Colony on Colonists, Restore Roles on Roles.
            const float ActionBtnW = 130f;
            const float ActionBtnH = 28f;
            float btnY = inRect.y + (TabHeight - ActionBtnH) / 2f;
            var actionRect = new Rect(inRect.xMax - ActionBtnW, btnY, ActionBtnW, ActionBtnH);
            if (curTab == Tab.Colonists)
            {
                // Colony planning is per location: with pawns from several maps
                // (or caravans) in view, Fix My Colony disables.
                bool spansLocations = ColonistsTabView.ScopeSpansMultipleLocations;
                TooltipHandler.TipRegion(actionRect, spansLocations
                    ? "WR_FixNeedsSingleLocation".Translate()
                    : "WR_FixMyColonyTip".Translate());
                if (Widgets.ButtonText(actionRect, "WR_FixMyColony".Translate(), drawBackground: true,
                        doMouseoverSound: true, active: !spansLocations)
                    && !spansLocations)
                    colonistsTab.ShowFixPreview();
            }
            else if (curTab == Tab.Roles)
            {
                TooltipHandler.TipRegion(actionRect, "WR_RestoreRolesTip".Translate());
                if (Widgets.ButtonText(actionRect, "WR_RestoreRoles".Translate()))
                {
                    var items = Seeding.ComputeRestoreItems();
                    if (items.Count == 0)
                        Messages.Message("WR_NothingToRestore".Translate(),
                            MessageTypeDefOf.RejectInput, historical: false);
                    else
                        Find.WindowStack.Add(new Dialog_RestorePreview(items));
                }

                const float IoBtnW = 90f;
                var exportRect = new Rect(actionRect.x - 8f - IoBtnW, btnY, IoBtnW, ActionBtnH);
                TooltipHandler.TipRegion(exportRect, "WR_ExportTip".Translate(RoleIO.ExportFile));
                if (Widgets.ButtonText(exportRect, "WR_Export".Translate()))
                    Find.WindowStack.Add(new Dialog_ExportPreview(RoleIO.BuildXml(RoleStore.Current)));

                var importRect = new Rect(exportRect.x - 8f - IoBtnW, btnY, IoBtnW, ActionBtnH);
                TooltipHandler.TipRegion(importRect, "WR_ImportTip".Translate(RoleIO.ExportFile));
                if (Widgets.ButtonText(importRect, "WR_Import".Translate()))
                    Find.WindowStack.Add(new Dialog_ImportSource());
            }

            content = content.ContractedBy(8f);
            if (curTab == Tab.Colonists) colonistsTab.Draw(content);
            else if (curTab == Tab.Roles) rolesTab.Draw(content);
            else optionsTab.Draw(content);

            // A wheel event that survives the draw wasn't over any inner
            // scroll view (table, palette, stats): scroll the colonist table
            // with it rather than letting the map zoom on it.
            if (curTab == Tab.Colonists && Event.current.type == EventType.ScrollWheel)
            {
                colonistsTab.ScrollTable(Event.current.delta.y);
                Event.current.Use();
            }
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
            if (Available && GetCursorPos(out var p))
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
