using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Dev;

namespace WorkRoles.UI
{
    public class MainTabWindow_WorkRoles : MainTabWindow
    {
        private enum Tab { Colonists, Roles, Options }

        private Tab curTab = Tab.Colonists;
        private readonly ColonistsTabView colonistsTab = new ColonistsTabView(ColonistsViewProfile.Colonists());
        private readonly RolesTabView rolesTab = new RolesTabView();
        private readonly OptionsTabView optionsTab = new OptionsTabView();
        private readonly System.Action drawGrip;
        private int observedLanguageRevision;

        private const float TabHeight = 32f;

        public MainTabWindow_WorkRoles()
        {
            drawGrip = DrawGripContents;
            observedLanguageRevision = LanguageChangeCoordinator.Revision;
            resizeable = false;
            draggable = false;   // main tab windows are not draggable
            // The Roles tab's holder list mirrors whatever the colonist table lists.
            rolesTab.listedPawns = () => colonistsTab.ListedPawns();
            rolesTab.pawnListRevision = () => colonistsTab.PawnListRevision;
            rolesTab.roleTip = role => colonistsTab.RoleTip(
                role, RoleTipContext.TreeRow);
        }

        public override Vector2 RequestedTabSize => TargetSize();

        /// Width floors at the design width (fixed chrome overlaps below it) and
        /// grows with the widest chip strip; whichever tab wants more height wins
        /// (small colonies would otherwise cramp the Roles tab). Both capped at
        /// the screen.
        private Vector2 TargetSize()
        {
            float w = Mathf.Clamp(colonistsTab.DesiredWidth() + 200f,
                ColonistsTabView.DefaultWidth, Verse.UI.screenWidth);
            float h = Mathf.Min(
                Mathf.Max(colonistsTab.DesiredHeight(), RolesTabView.DesiredHeight()),
                Verse.UI.screenHeight - 35f);
            return new Vector2(w, h);
        }

        /// Re-applies the persisted size each open, clamped between the content
        /// minimums and the screen; bottom-left anchor holds.
        protected override void SetInitialSizeAndPosition()
        {
            resizing = false;
            pendingWindowRect.Clear();
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

        private Rect AutoSizeRect()
        {
            var size = TargetSize();
            return new Rect(0f, Verse.UI.screenHeight - 35f - size.y, size.x, size.y);
        }

        private bool resizing;
        private float resizeBottom;    // screen-space bottom edge, pinned while dragging
        private Vector2 resizeGrab;    // grab point: (distance from right edge, distance from top)
        private readonly PendingUpdate<Rect> pendingWindowRect = new PendingUpdate<Rect>();

        /// Resize grip in its own immediate window (the contents group clips
        /// the corner; plain ExtraOnGUI paints under GUI windows). Grows width
        /// right and height UP — the bottom is pinned. Double-click = auto.
        internal const float GripSize = 18f;
        private const int GripWindowId = 147723001;
        private Rect gripScreen;

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            gripScreen = new Rect(windowRect.xMax - GripSize - 2f,
                windowRect.y + 2f, GripSize, GripSize);
            // Dialog layer: clicking our window refocuses it to the top of
            // GameUI, which would bury a same-layer grip.
            Find.WindowStack.ImmediateWindow(GripWindowId, gripScreen, WindowLayer.Dialog,
                drawGrip, doBackground: false, absorbInputAroundWindow: false, 0f);
        }

        private void DrawGripContents()
        {
            var settings = WorkRolesMod.Settings;
            var local = new Rect(0f, 0f, GripSize, GripSize);
            WrTips.Key("WR_ResizeGripTip").Region(local);
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
                    pendingWindowRect.QueueUser(AutoSizeRect());
                }
                else
                {
                    var screenMouse = gripScreen.position + e.mousePosition;
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
                var mouseUI = gamePx / Prefs.UIScale;

                var min = TargetSize();
                float width = Mathf.Clamp(mouseUI.x + resizeGrab.x, min.x, Verse.UI.screenWidth);
                float top = mouseUI.y - resizeGrab.y;
                float height = Mathf.Clamp(resizeBottom - top, min.y, Verse.UI.screenHeight - 35f);
                var nextWindowRect = new Rect(0f, resizeBottom - height, width, height);
                pendingWindowRect.QueueUser(nextWindowRect);

                // Warp only when a clamp stops the window: the cursor rides the
                // handle instead of drifting over other UI. Windows-only.
                var grabUI = new Vector2(nextWindowRect.width - resizeGrab.x, nextWindowRect.y + resizeGrab.y);
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
                    var persistedRect = pendingWindowRect.TryGetUser(out var pendingUserRect)
                        ? pendingUserRect : windowRect;
                    settings.windowWidth = persistedRect.width;
                    settings.windowHeight = persistedRect.height;
                    settings.Write();
                }
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        /// Pending size changes apply between frames: mid-event windowRect
        /// changes desync Layout from later passes.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (pendingWindowRect.TryConsume(out var nextWindowRect))
                windowRect = nextWindowRect;
        }

        private void ObserveLanguageRevision()
        {
            int current = LanguageChangeCoordinator.Revision;
            if (observedLanguageRevision == current && tabs != null) return;
            observedLanguageRevision = current;
            tabs = new List<TabRecord>
            {
                new TabRecord("WR_ColonistsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Colonists;
                }, () => curTab == Tab.Colonists),
                new TabRecord("WR_RolesTab".Translate(),
                    () => curTab = Tab.Roles, () => curTab == Tab.Roles),
                new TabRecord("WR_OptionsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Options;
                }, () => curTab == Tab.Options),
            };
            fixMyColonyLabel = "WR_FixMyColony".Translate().ToString();
            restoreDefaultsLabel = "WR_RestoreDefaults".Translate().ToString();
            nothingToRestoreMessage = "WR_NothingToRestore".Translate().ToString();
            exportLabel = "WR_Export".Translate().ToString();
            importLabel = "WR_Import".Translate().ToString();
            colonistsTab.InvalidateLanguageCaches();
            rolesTab.InvalidateLanguageCaches();
            optionsTab.InvalidateLanguageCaches();
        }

        public override void PreOpen()
        {
            ObserveLanguageRevision();
            WorkRolesTex.EnsureRuntimeTextures();
            base.PreOpen();
            ActivityTracker.Enable();
            RoleDrag.Cancel();
            colonistsTab.Reset();
            rolesTab.Reset();
            optionsTab.Reset();
            KeyOverride.Apply();
        }

        public override void PostClose()
        {
            base.PostClose();
            ActivityTracker.Disable();
            RoleDrag.Cancel();
            resizing = false;
            pendingWindowRect.Clear();
            rolesTab.CommitEdits();
            KeyOverride.Restore();
            // Producer snapshots regrow on reopen (Reset forces every stamp
            // stale); dropping them here releases pawns from unloaded saves.
            RoleClipboard.Clear();
            ScaleEditorUI.ReleaseState();
            colonistsTab.ReleaseSnapshots();
            rolesTab.ReleaseWindowData();
            optionsTab.ReleaseWindowData();
            WindowDataLifecycle.ReleaseShared();
            tabs = null;
            StructuredTipPresenter.Reset();
        }

        // Owner: window. Key: LanguageChangeCoordinator.Revision. Value:
        // translated static labels plus tab records and their cached delegates.
        // Dependencies: language and this window's tab-selection state.
        // Refresh: immediately when the observed language revision changes.
        // Equality: matching revision reuses strings, list, and delegates.
        // Teardown: PostClose clears the tab list; strings follow the window.
        private List<TabRecord> tabs;
        private string fixMyColonyLabel;
        private string restoreDefaultsLabel;
        private string nothingToRestoreMessage;
        private string exportLabel;
        private string importLabel;

        public override void DoWindowContents(Rect inRect)
        {
            if (WrEvent.SkipContentPass()) return;
            ObserveLanguageRevision();
            bool repaint = Event.current.type == EventType.Repaint;
            // UiVersion is advanced by WorkRoles mutations and authoritative
            // time-rule events. Refresh at the frame's repaint pass before
            // drawing; an event stamp check, never a scan for external changes.
            if (repaint)
                colonistsTab.RefreshExternalSnapshotIfNeeded();
            DrawContents(inRect);
        }

        private void DrawContents(Rect inRect)
        {
            // Keyboard navigation runs before any widget sees the event, and
            // only while no text field owns the keyboard (typing in the search
            // box stays typing).
            if (curTab == Tab.Colonists && Event.current.type == EventType.KeyDown
                && GUIUtility.keyboardControl == 0 && colonistsTab.HandleKey(Event.current))
                Event.current.Use();

            // Grow-only mid-session resize; manual geometry wins while dragging
            // and if both kinds of update are waiting for WindowUpdate.
            if (!resizing)
            {
                var target = TargetSize();
                var nextWindowRect = windowRect;
                bool grew = false;
                if (target.x > nextWindowRect.width + 1f)
                {
                    nextWindowRect.width = target.x;
                    grew = true;
                }
                if (target.y > nextWindowRect.height + 1f)
                {
                    nextWindowRect.height = target.y;
                    grew = true;
                }
                if (grew)
                {
                    nextWindowRect.x = 0f;
                    nextWindowRect.y = Verse.UI.screenHeight - 35f - nextWindowRect.height;
                    pendingWindowRect.QueueAutomatic(nextWindowRect);
                }
            }

            Rect content = new Rect(inRect.x, inRect.y + TabHeight, inRect.width, inRect.height - TabHeight);
            Widgets.DrawMenuSection(content);
            TabDrawer.DrawTabs(content, tabs);

            // Per-tab action button in the window's top-right corner, beside the tab
            // strip: Fix My Colony on Colonists, Restore Defaults on Roles.
            const float ActionBtnW = 130f;
            const float ActionBtnH = 28f;
            float btnY = inRect.y + (TabHeight - ActionBtnH) / 2f;
            var actionRect = new Rect(inRect.xMax - ActionBtnW, btnY, ActionBtnW, ActionBtnH);
            if (curTab == Tab.Colonists)
            {
                // Colony planning is per location: with pawns from several maps
                // (or caravans) in view, Fix My Colony disables.
                bool spansLocations = colonistsTab.ScopeSpansMultipleLocations;
                (spansLocations ? WrTips.Key("WR_FixNeedsSingleLocation")
                    : WrTips.Key("WR_FixMyColonyTip")).Region(actionRect);
                if (Widgets.ButtonText(actionRect, fixMyColonyLabel, drawBackground: true,
                        doMouseoverSound: true, active: !spansLocations)
                    && !spansLocations)
                    colonistsTab.ShowFixPreview();
            }
            else if (curTab == Tab.Roles)
            {
                WrTips.Key("WR_RestoreDefaultsTip").Region(actionRect);
                if (Widgets.ButtonText(actionRect, restoreDefaultsLabel))
                {
                    var items = Seeding.ComputeRestoreItems();
                    if (items.Count == 0)
                        WrToast.Show(nothingToRestoreMessage,
                            MessageTypeDefOf.RejectInput);
                    else
                        Find.WindowStack.Add(new Dialog_RestorePreview(items));
                }

                const float IoBtnW = 90f;
                var exportRect = new Rect(actionRect.x - 8f - IoBtnW, btnY, IoBtnW, ActionBtnH);
                WrTips.Key("WR_ExportTip", RoleIO.ExportFile).Region(exportRect);
                if (Widgets.ButtonText(exportRect, exportLabel))
                    Find.WindowStack.Add(new Dialog_ExportPreview(RoleIO.BuildXml(RoleStore.Current)));

                var importRect = new Rect(exportRect.x - 8f - IoBtnW, btnY, IoBtnW, ActionBtnH);
                WrTips.Key("WR_ImportTip", RoleIO.ExportFile).Region(importRect);
                if (Widgets.ButtonText(importRect, importLabel))
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

            // Last on purpose: toasts paint over everything the window drew.
            WrToast.Draw(inRect);
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
