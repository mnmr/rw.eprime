using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The configuration window: Groups on the left, a segmented center panel
    /// switching between group editing and resource pools, and an always-visible
    /// Resources panel on the right.
    /// Every completed action fires a sync command immediately — no Apply/Cancel.
    /// Resizable; size persists.
    public class Dialog_ReadoutConfig : Window
    {
        private const float PanelH = 56f;
        private const float Gap = 10f;
        private const float LeftW = 220f;
        private const float ModeHeaderH = 34f;
        private const float ModeBodyGap = 6f;

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
        private string? ghostPayload;
        private PoolSnapshot? ghostPools;
        private ThingDef? ghostDef;

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
            PoolsSnapshot = null;
            poolsSnapshotStore = null;
            RenderData = null;
            ghostPayload = null;
            ghostPools = null;
            ghostDef = null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;

            using (new GuiStateScope())
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
                // right-to-left: [Restore defaults] [Export] [Import] [Options]
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

                // [Options] — 90px wide, to the left of Import
                var optionsRect = new Rect(importRect.x - BtnGap - 90f, btnY, 90f, 28f);
                if (Widgets.ButtonText(optionsRect, UiText.Get("EPR.Options")))
                    Find.WindowStack.Add(new Dialog_PanelOptions());

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
        }

        public override void OnCancelKeyPressed()
        {
            if (groups.HandleEscape() || editor.HandleEscape()
                || resources.HandleEscape())
                return;
            base.OnCancelKeyPressed();
        }

        internal void SelectGroup(int groupId)
        {
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

        private void DrawModeHeader(Rect rect)
        {
            Widgets.DrawBoxSolidWithOutline(
                rect, EprStyle.PanelBackground, EprStyle.PanelOutline);
            const float Padding = 3f;
            const float SegmentGap = 2f;
            float segmentWidth = Mathf.Max(
                1f, (rect.width - 2f * Padding - SegmentGap) / 2f);
            var groupRect = new Rect(
                rect.x + Padding,
                rect.y + Padding,
                segmentWidth,
                rect.height - 2f * Padding);
            var poolsRect = new Rect(
                groupRect.xMax + SegmentGap,
                groupRect.y,
                segmentWidth,
                groupRect.height);

            if (EprStyle.SegmentedTab(
                groupRect,
                UiText.Get("EPR.GroupEditor"),
                centerMode == ReadoutConfigMode.GroupEditor))
                SetCenterMode(ReadoutConfigMode.GroupEditor);
            if (EprStyle.SegmentedTab(
                poolsRect,
                UiText.Get("EPR.ResourcePoolEditor"),
                centerMode == ReadoutConfigMode.ResourcePools))
                SetCenterMode(ReadoutConfigMode.ResourcePools);
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
