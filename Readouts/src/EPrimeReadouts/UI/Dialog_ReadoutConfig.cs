using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The configuration window: an Overview tab (Groups on the left, a
    /// segmented center panel switching between group editing and resource
    /// pools, and an always-visible Resources panel on the right), an
    /// Options tab, and a Help tab.
    /// Every completed action fires a sync command immediately — no Apply/Cancel.
    /// Resizable; size persists.
    public class Dialog_ReadoutConfig : Window
    {
        private enum Tab { Overview, Options, Help }

        // Session-scoped view state: survives close/reopen, never persisted.
        private static Tab curTab = Tab.Overview;

        private const float TabHeight = TabStrip.TabHeight;
        private const float Pad = 10f;
        private const float PanelH = 56f;
        private const float Gap = 10f;
        private const float LeftW = 220f;
        private const float ModeHeaderH = 34f;
        private const float ModeBodyGap = 6f;

        // Between TabRecord's normal white and its hover yellow.
        private static readonly Color ActiveTabLabelColor = new Color(1f, 0.95f, 0.55f);

        /// Currently selected group id; -1 = none.
        public int selectedGroupId = -1;

        /// Currently selected pool id; -1 = none.
        public int selectedPoolId = -1;

        /// Canonical token of the currently selected slot (e.g. "Steel" or "#3").
        /// Set by the editor view; may be null. The group resource tree reads this.
        public string? selectedCanonical;

        // Shared per-frame-safe pools snapshot — rebuilt only for pool edits.
        public PoolSnapshot? PoolsSnapshot { get; private set; }
        public int poolsSnapshotVersion = -1;
        private ReadoutStore? poolsSnapshotStore;
        internal RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? RenderData { get; private set; }

        private ReadoutConfigMode centerMode = ReadoutConfigMode.GroupEditor;

        private readonly GroupListView groups = new GroupListView();
        private readonly EditorView editor = new EditorView();
        private readonly PoolListView poolList = new PoolListView();
        private readonly ResourcePanelView resources = new ResourcePanelView();
        private readonly OptionsTabView options = new OptionsTabView();
        private readonly HelpTabView help = new HelpTabView(ReadoutHelpHost.Instance);
        private string? ghostPayload;
        private PoolSnapshot? ghostPools;
        private ThingDef? ghostDef;

        // Cache contract:
        // Owner: this window.
        // Key: UiVersion.LanguageCurrent.
        // Value: the three TabRecords with translated labels.
        // Dependencies: language only; selection reads curTab live.
        // Refresh policy: rebuilt on the first draw after the revision moves.
        // Equality policy: an unchanged revision reuses the list.
        // Teardown: PreClose drops the list.
        private List<TabRecord>? tabs;
        private int tabsLanguageStamp = -1;

        public Dialog_ReadoutConfig()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize =>
            EPrimeReadoutsMod.Settings.dialogW > 0f
                ? new Vector2(EPrimeReadoutsMod.Settings.dialogW, EPrimeReadoutsMod.Settings.dialogH)
                : new Vector2(960f, 660f);

        public override void PreOpen()
        {
            base.PreOpen();
            help.Reset();
        }

        public override void PreClose()
        {
            base.PreClose();
            EPrimeReadoutsMod.Persist(s =>
            {
                s.dialogW = windowRect.width;
                s.dialogH = windowRect.height;
            });
            EprDrag.Cancel();
            groups.Reset();
            editor.Reset();
            poolList.Reset();
            resources.Reset();
            help.FlushPendingWrites();
            help.ReleaseWindowData();
            PoolsSnapshot = null;
            poolsSnapshotStore = null;
            RenderData = null;
            ghostPayload = null;
            ghostPools = null;
            ghostDef = null;
            tabs = null;
            tabsLanguageStamp = -1;
        }

        /// Help read-marks persist between frames, never inside a render pass.
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            help.FlushPendingWrites();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;

            using (GuiStateScope.Capture())
            {
                UiVersion.ObserveCurrentMetrics();
                EnsureTabs();

                var content = new Rect(
                    inRect.x, inRect.y + TabHeight,
                    inRect.width, inRect.height - TabHeight);
                Widgets.DrawMenuSection(content);
                // Active-tab emphasis: TabRecord reads labelColor per pass, so
                // a per-frame field write is how selection tints the label.
                for (int i = 0; i < tabs!.Count; i++)
                    tabs[i].labelColor = i == (int)curTab ? ActiveTabLabelColor : (Color?)null;
                TabStrip.Draw(content, tabs, ReadoutTextures.TabAtlas);
                TabStrip.DrawActiveTabSeam(content, (int)curTab, tabs.Count);

                content = content.ContractedBy(Pad);
                switch (curTab)
                {
                    case Tab.Overview: DrawOverview(content, store); break;
                    case Tab.Options: options.Draw(content); break;
                    default: help.Draw(content); break;
                }
                if (curTab != Tab.Overview) EprDrag.Cancel();
            }
        }

        private void EnsureTabs()
        {
            if (tabs != null && tabsLanguageStamp == UiVersion.LanguageCurrent) return;
            tabsLanguageStamp = UiVersion.LanguageCurrent;
            tabs = new List<TabRecord>
            {
                new TabRecord(UiText.Get("EPR.Overview"),
                    static () => curTab = Tab.Overview, () => curTab == Tab.Overview),
                new TabRecord(UiText.Get("EPR.Options"),
                    static () => curTab = Tab.Options, () => curTab == Tab.Options),
                new TabRecord(UiText.Get("EPR.Help"),
                    static () => curTab = Tab.Help, () => curTab == Tab.Help),
            };
        }

        private void DrawOverview(Rect inRect, ReadoutStore store)
        {
            EprDrag.Update();

            // --- Read the same per-map snapshot used by the main panel. ---
            var map = Find.CurrentMap;
            RenderData = map != null ? GameRenderData.Get(map, store) : null;
            if (RenderData != null)
            {
                PoolsSnapshot = RenderData.Structure;
                poolsSnapshotStore = store;
                poolsSnapshotVersion = store.PoolsVersion;
            }
            else if (!ReferenceEquals(poolsSnapshotStore, store)
                || store.PoolsVersion != poolsSnapshotVersion)
            {
                PoolsSnapshot = PoolSnapshot.Build(store.Model.Pools, GameResourceCatalog.Instance);
                poolsSnapshotStore = store;
                poolsSnapshotVersion = store.PoolsVersion;
            }

            // --- Top panel ---
            var panelRect = new Rect(inRect.x, inRect.y, inRect.width, PanelH);
            Widgets.DrawBoxSolidWithOutline(panelRect, EprStyle.PanelBackground, EprStyle.PanelOutline);

            // Mod icon (40x40, 8px left padding, vertically centred)
            var iconRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, 40f, 40f);
            GUI.DrawTexture(iconRect, ReadoutTextures.ModIcon);

            // Title "EPrime's Readouts"
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = EprStyle.HeaderText;
            Widgets.Label(new Rect(iconRect.xMax + 8f, panelRect.y,
                panelRect.width - iconRect.xMax - 8f - 150f, PanelH),
                UiText.Get("EPR.Title"));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Right-cluster buttons, all vertically centred in panel, 28px tall, 8px gaps,
            // right-to-left: [Restore defaults] [Export] [Import]
            float btnY = panelRect.y + (PanelH - 28f) / 2f;
            const float BtnGap = 8f;

            // [Restore defaults] — 130px wide, 8px from right edge
            var restoreRect = new Rect(panelRect.xMax - 138f, btnY, 130f, 28f);
            if (Widgets.ButtonText(restoreRect, UiText.Get("EPR.RestoreDefaults")))
            {
                string restorePayload = DefaultGroups.GetRestorePayload();
                Find.WindowStack.Add(new Dialog_CompactConfirm(
                    "EPR.RestoreConfirm".Translate(),
                    () => ReadoutCommands.RestoreDefaults(restorePayload), destructive: true));
            }

            // [Export] — 90px wide, to the left of Restore
            var exportRect = new Rect(restoreRect.x - BtnGap - 90f, btnY, 90f, 28f);
            if (Widgets.ButtonText(exportRect, UiText.Get("EPR.Export")))
                Find.WindowStack.Add(new Dialog_ExportReadouts());

            // [Import] — 90px wide, to the left of Export
            var importRect = new Rect(exportRect.x - BtnGap - 90f, btnY, 90f, 28f);
            if (Widgets.ButtonText(importRect, UiText.Get("EPR.Import")))
                Find.WindowStack.Add(new Dialog_ImportReadouts());

            // --- Content area (below top panel) ---
            var content = new Rect(inRect.x, inRect.y + PanelH + Gap,
                inRect.width, inRect.height - PanelH - Gap);

            // Left column: Groups panel (fixed 220px, full height)
            var leftRect = new Rect(content.x, content.y, LeftW, content.height);

            float columnsX = leftRect.xMax + Gap;
            float columnsWidth = content.xMax - columnsX;
            float columnWidth = (columnsWidth - Gap) / 2f;
            var centerRect = new Rect(
                columnsX, content.y, columnWidth, content.height);
            var rightRect = new Rect(
                centerRect.xMax + Gap, content.y, columnWidth, content.height);
            var centerBodyRect = new Rect(
                centerRect.x,
                centerRect.y + ModeHeaderH + ModeBodyGap,
                centerRect.width,
                centerRect.height - ModeHeaderH - ModeBodyGap);

            groups.Draw(leftRect, this);
            DrawModeHeader(new Rect(
                centerRect.x, centerRect.y, centerRect.width, ModeHeaderH));
            if (centerMode == ReadoutConfigMode.GroupEditor)
                editor.Draw(centerBodyRect, this);
            else
                poolList.Draw(centerBodyRect, this);
            resources.Draw(rightRect, this, centerMode);

            DrawDragGhost();
            EprDrag.ResolveMouseUp();
        }

        public override void OnCancelKeyPressed()
        {
            if (curTab == Tab.Overview
                && (groups.HandleEscape() || editor.HandleEscape()
                    || resources.HandleEscape()))
                return;
            base.OnCancelKeyPressed();
        }

        internal void SelectGroup(int groupId)
        {
            // A slot selection belongs to the group it was made in; navigating
            // to another group must drop it (and the tree tint it drives).
            if (selectedGroupId != groupId) selectedCanonical = null;
            selectedGroupId = groupId;
            SetCenterMode(ReadoutConfigMode.GroupEditor);
        }

        private void SetCenterMode(ReadoutConfigMode mode)
        {
            if (centerMode == mode) return;
            EprDrag.Cancel();
            if (centerMode == ReadoutConfigMode.GroupEditor)
                editor.Unfocus();
            centerMode = mode;
        }

        /// Reused label slots for the mode header, so the render pass hands
        /// SegmentedRow a stable array of cached translated strings.
        private static readonly string[] modeLabels = new string[2];

        private void DrawModeHeader(Rect rect)
        {
            modeLabels[(int)ReadoutConfigMode.GroupEditor] =
                UiText.Get("EPR.GroupEditor");
            modeLabels[(int)ReadoutConfigMode.ResourcePools] =
                UiText.Get("EPR.ResourcePoolEditor");
            int clicked = SegmentedControl.Row(rect, modeLabels, (int)centerMode);
            if (clicked >= 0) SetCenterMode((ReadoutConfigMode)clicked);
        }

        private void DrawDragGhost()
        {
            if (!EprDrag.Active || EprDrag.Payload == null) return;
            EnsureGhost(EprDrag.Payload, PoolsSnapshot);
            if (ghostDef == null) return;
            var mouse = Event.current.mousePosition;
            Widgets.ThingIcon(new Rect(mouse.x - 16f, mouse.y - 16f, 32f, 32f), ghostDef);
        }

        private void EnsureGhost(string payload, PoolSnapshot? pools)
        {
            if (string.Equals(ghostPayload, payload, System.StringComparison.Ordinal)
                && ReferenceEquals(ghostPools, pools))
                return;

            ghostPayload = payload;
            ghostPools = pools;
            ghostDef = null;
            if (SlotToken.IsPoolRef(EprDrag.Payload!)) // non-null while a drag is active
            {
                int poolId = SlotToken.PoolId(payload);
                if (pools != null
                    && pools.TryGet(poolId, out _, out string? iconDefName, out _)
                    && !string.IsNullOrEmpty(iconDefName))
                    ghostDef = DefDatabase<ThingDef>.GetNamedSilentFail(iconDefName);
            }
            else if (SlotToken.IsPool(payload))
            {
                var members = GameResourceCatalog.Instance.CountedDefsIn(
                    SlotToken.MemberName(payload));
                ghostDef = members.Count > 0
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(members[0])
                    : null;
            }
            else
            {
                ghostDef = DefDatabase<ThingDef>.GetNamedSilentFail(SlotToken.MemberName(payload));
            }
        }
    }
}
