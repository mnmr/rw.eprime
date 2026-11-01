using System.Collections.Generic;
using RimShared.Common;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Dev;

namespace WorkRoles.UI
{
    public class MainTabWindow_WorkRoles : ResizableMainTabWindow
    {
        private enum Tab { Colonists, Roles, Recommendations, Options, Help }

        private Tab curTab = Tab.Colonists;
        private readonly ColonistsTabView colonistsTab = new ColonistsTabView(ColonistsViewProfile.Colonists());
        private readonly RolesTabView rolesTab = new RolesTabView();
        private readonly RecommendationsTabView recommendationsTab = new RecommendationsTabView();
        private readonly OptionsTabView optionsTab = new OptionsTabView();
        private readonly HelpTabView helpTab =
            new HelpTabView(WorkRolesHelpHost.Instance);
        private int observedLanguageRevision;

        /// Top-right actions: the Roles tab groups Import, Export, Set defaults
        /// and Restore Defaults with measured widths and 8px gaps.
        private static readonly Color AutoManagedColor =
            new Color(0.55f, 0.8f, 0.45f);
        private static readonly Color AutoManagedBackground =
            new Color(0.10f, 0.16f, 0.09f, 0.95f);
        private static readonly Color AutoManagedOutline =
            new Color(0.55f, 0.8f, 0.45f, 0.65f);

        public MainTabWindow_WorkRoles()
        {
            observedLanguageRevision = LanguageChangeCoordinator.Revision;
            // The Roles tab's holder list mirrors whatever the colonist table lists.
            rolesTab.listedPawns = () => colonistsTab.ListedPawns();
            rolesTab.pawnListRevision = () => colonistsTab.PawnListRevision;
            rolesTab.presentRoleTip = (rect, roleId) => colonistsTab.PresentRoleTip(
                rect, roleId, RoleTipContext.TreeRow);
            optionsTab.showAutoOptimizeEnablePreview =
                colonistsTab.ShowAutoOptimizeEnablePreview;
        }

        /// Width floors at the design width and grows with the widest chip
        /// strip; whichever tab wants more height wins (small colonies would
        /// otherwise cramp the Roles tab). Both floored at the design minimum
        /// and capped at the screen.
        protected override Vector2 TargetSize()
        {
            Vector2 min = MinSize;
            float w = Mathf.Clamp(colonistsTab.DesiredWidth() + 200f,
                min.x, Verse.UI.screenWidth);
            float h = Mathf.Clamp(
                Mathf.Max(colonistsTab.DesiredHeight(), rolesTab.DesiredHeight()),
                min.y, Verse.UI.screenHeight - BottomBarHeight);
            return new Vector2(w, h);
        }

        /// The design floor is 1536x864 logical (1920x1080 at UI scale 1.25):
        /// the window never shrinks below its whole width and the height left
        /// above the 35px bottom bar. Smaller screens stay usable but are not
        /// designed for: the minimum caps at the screen and the welcome
        /// dialog warns about it. Tab contents scroll internally.
        internal const float DesignScreenWidth = 1536f;
        internal const float DesignScreenHeight = 864f;
        protected override Vector2 MinSize => new Vector2(
            Mathf.Min(DesignScreenWidth, Verse.UI.screenWidth),
            Mathf.Min(DesignScreenHeight, Verse.UI.screenHeight)
                - BottomBarHeight);

        internal static bool ScreenBelowDesignFloor =>
            Verse.UI.screenWidth < DesignScreenWidth
            || Verse.UI.screenHeight < DesignScreenHeight;

        /// The player's remembered window size lives in the mod settings
        /// (0 = automatic per axis); the write is deferred outside OnGUI.
        private sealed class SizePreferenceAdapter : IWindowSizePreference
        {
            public float Width
            {
                get => WorkRolesMod.Settings?.windowWidth ?? 0f;
                set { var s = WorkRolesMod.Settings; if (s != null) s.windowWidth = value; }
            }

            public float Height
            {
                get => WorkRolesMod.Settings?.windowHeight ?? 0f;
                set { var s = WorkRolesMod.Settings; if (s != null) s.windowHeight = value; }
            }

            public void RequestWrite() => WorkRolesGameComponent.RequestSettingsWrite();
        }

        private static readonly SizePreferenceAdapter sizePreference = new SizePreferenceAdapter();
        protected override IWindowSizePreference? SizePreference =>
            WorkRolesMod.Settings != null ? sizePreference : null;
        protected override string GripTipKey => "WR_ResizeGripTip";
        protected override int GripWindowId => 147723001;

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            RoleIconCatalog.WarmDefinitions();
            RoleIconPresentationCatalog.Refresh(RoleStore.Current);
            WrToast.Update();
            helpTab.FlushPendingWrites();
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
            defaultsLabel = "WR_NewGameDefaults".Translate().ToString();
            colonistsTab.InvalidateLanguageCaches();
            rolesTab.InvalidateLanguageCaches();
            recommendationsTab.InvalidateLanguageCaches();
            optionsTab.InvalidateLanguageCaches();
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
            rolesTab.CommitEdits();
            SetupDefaults.QueueFlush();
            KeyOverride.Restore();
            // Producer snapshots regrow on reopen (Reset forces every stamp
            // stale); dropping them here releases pawns from unloaded saves.
            RoleClipboard.Clear();
            colonistsTab.ReleaseSnapshots();
            rolesTab.ReleaseWindowData();
            recommendationsTab.ReleaseWindowData();
            optionsTab.ReleaseWindowData();
            helpTab.FlushPendingWrites();
            helpTab.ReleaseWindowData();
            WindowDataLifecycle.ReleaseShared();
            autoManagedGate.Reset();
            autoManagedSnapshot = false;
            tabs = null;
            actionMetricRevision = actionLanguageRevision = -1;
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
        private string defaultsLabel = null!;
        private static readonly System.Action exportSetup = ExportSetup;
        private static void ExportSetup()
        {
            var store = RoleStore.Current;
            if (store != null) Find.WindowStack.Add(new Dialog_ExportPreview(RoleIO.BuildXml(store)));
        }

        // Owner: window. Key: language and UI metric revisions. Value: measured
        // Small-font action widths and line bounds. Dependencies: the six
        // translated captions and active font metrics. Refresh: immediate at
        // either revision change. Equality: unchanged keys reuse scalar values.
        // Teardown: window-owned values; PostClose resets the measurement stamp.
        private int actionMetricRevision = -1, actionLanguageRevision = -1;
        private float actionWidth, defaultsWidth, ioWidth, actionHeight;

        private void EnsureActionMetrics()
        {
            int metrics = UiRevision.Current, language = LanguageChangeCoordinator.Revision;
            if (actionMetricRevision == metrics && actionLanguageRevision == language) return;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                actionWidth = Mathf.Max(130f, Mathf.Max(
                    Mathf.Max(WrText.FitWidth(restoreDefaultsLabel), WrText.FitWidth(fixMyColonyLabel)) + 24f,
                    WrText.FitWidth(autoManagedLabel) + 34f));
                defaultsWidth = Mathf.Max(130f, WrText.FitWidth(defaultsLabel) + 24f);
                ioWidth = Mathf.Max(90f, Mathf.Max(WrText.FitWidth(importLabel), WrText.FitWidth(exportLabel)) + 24f);
                actionHeight = Mathf.Max(28f, Mathf.Ceil(Text.LineHeight) + 6f);
            }
            actionMetricRevision = metrics;
            actionLanguageRevision = language;
        }

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

        protected override void DrawTabContents(Rect inRect)
        {
            if (WrEvent.SkipContentPass()) return;
            using var guiState = GuiStateScope.Capture();
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
            Text.Font = GameFont.Small;
            EnsureActionMetrics();
            // Keyboard navigation runs before any widget sees the event, and
            // only while no text field owns the keyboard (typing in the search
            // box stays typing).
            if (curTab == Tab.Colonists && Event.current.type == EventType.KeyDown
                && GUIUtility.keyboardControl == 0 && colonistsTab.HandleKey(Event.current))
                Event.current.Use();

            Rect content = new Rect(inRect.x, inRect.y + TabStrip.TabHeight, inRect.width, inRect.height - TabStrip.TabHeight);
            Widgets.DrawMenuSection(content);
            // Active-tab emphasis: TabRecord reads labelColor per pass, so a
            // per-frame field write is how selection tints the label.
            for (int i = 0; i < tabs!.Count; i++) // ObserveLanguageRevision built the list this pass
                tabs[i].labelColor = i == (int)curTab ? TabStrip.ActiveLabelColor : (Color?)null;
            // The strip stops short of the per-tab action buttons at the top
            // right (widest cluster: Import, Export, Set defaults, Restore Defaults on the
            // Roles tab), so narrow windows shrink the tabs instead of
            // burying the last ones under the buttons.
            var tabStrip = new Rect(content.x, content.y,
                content.width - actionWidth - defaultsWidth - 2f * ioWidth - 32f, content.height);
            TabStrip.Draw(tabStrip, tabs, WorkRolesTex.TabAtlas);
            TabStrip.DrawActiveTabSeam(tabStrip, (int)curTab, tabs.Count);

            // Per-tab action button in the window's top-right corner, beside the tab
            // strip: Fix My Colony on Colonists, Restore Defaults on Roles.
            float btnY = inRect.y + (TabStrip.TabHeight - actionHeight) / 2f;
            var actionRect = new Rect(inRect.xMax - actionWidth, btnY, actionWidth, actionHeight);
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

                var defaultsRect = new Rect(actionRect.x - 8f - defaultsWidth, btnY, defaultsWidth, actionHeight);
                if (Widgets.ButtonText(defaultsRect, defaultsLabel))
                    SetupDefaults.QueueDefaultDialog();

                var exportRect = new Rect(defaultsRect.x - 8f - ioWidth, btnY, ioWidth, actionHeight);
                WrTips.Key("WR_ExportTip", RoleIO.ExportFile).Region(exportRect);
                if (Widgets.ButtonText(exportRect, exportLabel))
                {
                    WorkRolesGameComponent.RunOutsideOnGUI(exportSetup);
                }

                var importRect = new Rect(exportRect.x - 8f - ioWidth, btnY, ioWidth, actionHeight);
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
}
