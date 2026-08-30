using System.Collections.Generic;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Dev;

namespace WorkRoles.UI
{
    public class MainTabWindow_WorkRoles : MainTabWindow
    {
        private enum Tab { Colonists, Roles, Recommendations, Options, Help }

        private Tab curTab = Tab.Colonists;
        private readonly ColonistsTabView colonistsTab = new ColonistsTabView(ColonistsViewProfile.Colonists());
        private readonly RolesTabView rolesTab = new RolesTabView();
        private readonly RecommendationsTabView recommendationsTab = new RecommendationsTabView();
        private readonly OptionsTabView optionsTab = new OptionsTabView();
        private readonly HelpTabView helpTab = new HelpTabView();
        private readonly System.Action drawGrip;
        private int observedLanguageRevision;

        private const float TabHeight = 32f;
        // Between TabRecord's normal white and its hover yellow.
        private static readonly Color ActiveTabLabelColor = new Color(1f, 0.95f, 0.55f);
        private static readonly Color AutoManagedColor =
            new Color(0.55f, 0.8f, 0.45f);
        private static readonly Color AutoManagedBackground =
            new Color(0.10f, 0.16f, 0.09f, 0.95f);
        private static readonly Color AutoManagedOutline =
            new Color(0.55f, 0.8f, 0.45f, 0.65f);

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
            optionsTab.showAutoOptimizeEnablePreview =
                colonistsTab.ShowAutoOptimizeEnablePreview;
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
                Mathf.Max(colonistsTab.DesiredHeight(), rolesTab.DesiredHeight()),
                Verse.UI.screenHeight - 35f);
            return new Vector2(w, h);
        }

        /// Player-sized floor: well below the content-fit TargetSize so the
        /// grip can shrink the window (tab contents scroll internally). Width
        /// keeps the design floor — fixed chrome overlaps below it.
        private static Vector2 MinManualSize =>
            new Vector2(ColonistsTabView.DefaultWidth, 480f);

        /// Re-applies the persisted size each open, clamped between the manual
        /// minimums and the screen; bottom-left anchor holds.
        protected override void SetInitialSizeAndPosition()
        {
            resizing = false;
            pendingWindowRect.Clear();
            base.SetInitialSizeAndPosition();
            var settings = WorkRolesMod.Settings;
            if (settings == null || (settings.windowWidth <= 0f && settings.windowHeight <= 0f))
                return;
            var min = MinManualSize;
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
                    WorkRolesGameComponent.RequestSettingsWrite();
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

                var min = MinManualSize;
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
                    WorkRolesGameComponent.RequestSettingsWrite();
                }
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        /// Pending size changes apply between frames: mid-event windowRect
        /// changes desync Layout from later passes.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            RoleIconCatalog.WarmDefinitions();
            RoleIconPresentationCatalog.Refresh(RoleStore.Current);
            WrToast.Update();
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
                new TabRecord("WR_RecommendationsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Recommendations;
                }, () => curTab == Tab.Recommendations),
                new TabRecord("WR_OptionsTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Options;
                }, () => curTab == Tab.Options),
                new TabRecord("WR_HelpTab".Translate(), () =>
                {
                    if (curTab == Tab.Roles) rolesTab.CommitEdits();
                    curTab = Tab.Help;
                }, () => curTab == Tab.Help),
            };
            fixMyColonyLabel = "WR_FixMyColony".Translate().ToString();
            autoManagedLabel = "WR_AutoManaged".Translate().ToString();
            restoreDefaultsLabel = "WR_RestoreDefaults".Translate().ToString();
            nothingToRestoreMessage = "WR_NothingToRestore".Translate().ToString();
            exportLabel = "WR_Export".Translate().ToString();
            importLabel = "WR_Import".Translate().ToString();
            colonistsTab.InvalidateLanguageCaches();
            rolesTab.InvalidateLanguageCaches();
            recommendationsTab.InvalidateLanguageCaches();
            optionsTab.InvalidateLanguageCaches();
            helpTab.InvalidateLanguageCaches();
        }

        public override void PreOpen()
        {
            ObserveLanguageRevision();
            WorkRolesTex.EnsureRuntimeTextures();
            RoleIconCatalog.WarmDefinitions();
            RoleIconPresentationCatalog.Refresh(RoleStore.Current);
            base.PreOpen();
            ActivityTracker.Enable();
            RoleDrag.Cancel();
            colonistsTab.Reset();
            rolesTab.Reset();
            recommendationsTab.Reset();
            optionsTab.Reset();
            helpTab.Reset();
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
            colonistsTab.ReleaseSnapshots();
            rolesTab.ReleaseWindowData();
            recommendationsTab.ReleaseWindowData();
            optionsTab.ReleaseWindowData();
            helpTab.ReleaseWindowData();
            WindowDataLifecycle.ReleaseShared();
            autoManagedGate.Reset();
            autoManagedSnapshot = false;
            tabs = null;
            StructuredTipPresenter.Reset();
        }

        // Owner: window. Key: LanguageChangeCoordinator.Revision. Value:
        // translated static labels plus tab records and their cached delegates.
        // Dependencies: language and this window's tab-selection state.
        // Refresh: immediately when the observed language revision changes.
        // Equality: matching revision reuses strings, list, and delegates.
        // Teardown: PostClose clears the tab list; strings follow the window.
        private List<TabRecord>? tabs;
        private string fixMyColonyLabel = null!;         // assigned by ObserveLanguageRevision before any draw
        private string autoManagedLabel = null!;
        private string restoreDefaultsLabel = null!;
        private string nothingToRestoreMessage = null!;
        private string exportLabel = null!;
        private string importLabel = null!;

        // Owner: WorkRoles window. Key: RoleStore identity and the narrow
        // AutoOptimizePresentationRevision. Value: immutable auto-managed
        // status bool. Dependencies: only RoleStore.autoOptimize. Refresh:
        // immediate on the first pass after the command advances the revision,
        // or when the store owner changes. Equality: unchanged owner/revision
        // reuses the bool without reading the model. Teardown: PostClose resets
        // the gate and cached value.
        private readonly OwnerRevisionGate<RoleStore> autoManagedGate =
            new OwnerRevisionGate<RoleStore>();
        private bool autoManagedSnapshot;

        private bool AutoManagedSnapshot()
        {
            RoleStore? store = RoleStore.Current;
            if (autoManagedGate.ShouldRefresh(store,
                    AutoOptimizePresentationRevision.Current))
                autoManagedSnapshot = store?.autoOptimize == true;
            return autoManagedSnapshot;
        }

        private void DrawAutoManagedIndicator(Rect rect)
        {
            WrTips.Key("WR_AutoManagedTip").Region(rect);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Widgets.DrawBoxSolidWithOutline(rect, AutoManagedBackground,
                    AutoManagedOutline);
                GUI.color = AutoManagedColor;
                const float DotSize = 8f;
                GUI.DrawTexture(new Rect(rect.x + 10f,
                    rect.y + (rect.height - DotSize) / 2f,
                    DotSize, DotSize), WorkRolesTex.Circle);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + 22f, rect.y,
                    rect.width - 26f, rect.height), autoManagedLabel);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (WrEvent.SkipContentPass()) return;
            using var guiState = new GuiStateScope(capture: true);
            ObserveLanguageRevision();
            bool repaint = Event.current.type == EventType.Repaint;
            // UiVersion is advanced by WorkRoles mutations and authoritative
            // time-rule events. Refresh at the frame's repaint pass before
            // drawing; an event stamp check, never a scan for external changes.
            if (repaint)
            {
                colonistsTab.RefreshExternalSnapshotIfNeeded();
                WrToast.RefreshLayout(inRect.width);
            }
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

            // Grow-only mid-session resize; manual geometry wins while
            // dragging, if both kinds of update are waiting for WindowUpdate,
            // and per axis the player has explicitly sized (auto-grow would
            // otherwise undo every shrink each frame).
            if (!resizing)
            {
                var sizeSettings = WorkRolesMod.Settings;
                var target = TargetSize();
                var nextWindowRect = windowRect;
                bool grew = false;
                if (target.x > nextWindowRect.width + 1f
                    && !(sizeSettings != null && sizeSettings.windowWidth > 0f))
                {
                    nextWindowRect.width = target.x;
                    grew = true;
                }
                if (target.y > nextWindowRect.height + 1f
                    && !(sizeSettings != null && sizeSettings.windowHeight > 0f))
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
            // Active-tab emphasis: TabRecord reads labelColor per pass, so a
            // per-frame field write is how selection tints the label (between
            // the normal white and the hover yellow).
            for (int i = 0; i < tabs!.Count; i++) // ObserveLanguageRevision built the list this pass
                tabs[i].labelColor = i == (int)curTab ? ActiveTabLabelColor : (Color?)null;
            WrTabs.DrawTabs(content, tabs);
            // Vanilla leaves the menu-section top border visible under the
            // active tab. Overpaint its span with the section fill so the
            // active tab connects seamlessly to the content (geometry mirrors
            // WrTabs: tabWidth capped at 200, 10px horizontal overlap).
            float tabWidth = Mathf.Min(200f,
                (content.width + (tabs.Count - 1) * 10f) / tabs.Count);
            float activeTabX = content.x + (int)curTab * (tabWidth - 10f);
            Widgets.DrawBoxSolid(new Rect(activeTabX + 1f, content.y, tabWidth - 2f, 2f),
                Widgets.MenuSectionBGFillColor);

            // Per-tab action button in the window's top-right corner, beside the tab
            // strip: Fix My Colony on Colonists, Restore Defaults on Roles.
            const float ActionBtnW = 130f;
            const float ActionBtnH = 28f;
            float btnY = inRect.y + (TabHeight - ActionBtnH) / 2f;
            var actionRect = new Rect(inRect.xMax - ActionBtnW, btnY, ActionBtnW, ActionBtnH);
            if (curTab == Tab.Colonists)
            {
                if (AutoManagedSnapshot())
                    DrawAutoManagedIndicator(actionRect);
                else
                {
                    // Colony planning is per location: with pawns from several
                    // maps (or caravans) in view, Fix My Colony disables.
                    bool spansLocations =
                        colonistsTab.ScopeSpansMultipleLocations;
                    (spansLocations ? WrTips.Key("WR_FixNeedsSingleLocation")
                        : WrTips.Key("WR_FixMyColonyTip")).Region(actionRect);
                    if (Widgets.ButtonText(actionRect, fixMyColonyLabel,
                            drawBackground: true, doMouseoverSound: true,
                            active: !spansLocations) && !spansLocations)
                        colonistsTab.ShowFixPreview();
                }
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
                {
                    RoleStore? store = RoleStore.Current;
                    if (store != null)
                        Find.WindowStack.Add(new Dialog_ExportPreview(
                            RoleIO.BuildXml(store)));
                }

                var importRect = new Rect(exportRect.x - 8f - IoBtnW, btnY, IoBtnW, ActionBtnH);
                WrTips.Key("WR_ImportTip", RoleIO.ExportFile).Region(importRect);
                if (Widgets.ButtonText(importRect, importLabel))
                    Find.WindowStack.Add(new Dialog_ImportSource());
            }

            content = content.ContractedBy(8f);
            if (curTab == Tab.Colonists) colonistsTab.Draw(content);
            else if (curTab == Tab.Roles) rolesTab.Draw(content);
            else if (curTab == Tab.Recommendations) recommendationsTab.Draw(content);
            else if (curTab == Tab.Options) optionsTab.Draw(content);
            else helpTab.Draw(content);

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
