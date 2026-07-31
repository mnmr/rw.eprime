using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The configuration window: inset top panel, then (left) Groups panel,
    /// (right) a vertical split — left half EditorView (full height), right half
    /// toggles between ResourceTreeView and Pools UI (PoolListView + PoolEditorView).
    /// Every completed action fires a sync command immediately — no Apply/Cancel.
    /// Resizable; size persists.
    public class Dialog_ReadoutConfig : Window
    {
        private const float PanelH = 56f;
        private const float Gap = 10f;
        private const float LeftW = 220f;
        private const float ToggleBtnW = 110f;
        private const float ToggleBtnH = 24f;
        private const float PoolEditorMinH = 120f;

        /// Currently selected group id; -1 = none.
        public int selectedGroupId = -1;

        /// Currently selected pool id; -1 = none.
        public int selectedPoolId = -1;

        /// Canonical token of the currently selected slot (e.g. "Steel" or "#3").
        /// Set by the editor view; may be null. ResourceTreeView reads this.
        public string selectedCanonical;

        // Shared per-frame-safe pools snapshot — rebuilt only for pool edits.
        public PoolSnapshot PoolsSnapshot { get; private set; }
        public int poolsSnapshotVersion = -1;
        private ReadoutStore poolsSnapshotStore;
        internal RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> RenderData { get; private set; }

        /// Session state: false = show Resources tree, true = show Pools UI.
        private bool showPools;

        private readonly GroupListView groups = new GroupListView();
        private readonly ResourceTreeView tree = new ResourceTreeView();
        private readonly EditorView editor = new EditorView();
        private readonly PoolListView poolList = new PoolListView();
        private readonly PoolEditorView poolEditor = new PoolEditorView();
        private string ghostPayload;
        private PoolSnapshot ghostPools;
        private ThingDef ghostDef;

        public Dialog_ReadoutConfig()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            closeOnClickedOutside = false;
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
            tree.Reset();
            editor.Reset();
            poolList.Reset();
            poolEditor.Reset();
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
                // right-to-left: [Restore defaults] [Import] [Export]
                float btnY = panelRect.y + (PanelH - 28f) / 2f;
                const float BtnGap = 8f;

                // [Restore defaults] — 130px wide, 8px from right edge
                var restoreRect = new Rect(panelRect.xMax - 138f, btnY, 130f, 28f);
                if (Widgets.ButtonText(restoreRect, UiText.Get("EPR.RestoreDefaults")))
                {
                    string restorePayload = DefaultGroups.GetRestorePayload();
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "EPR.RestoreConfirm".Translate(),
                        () => ReadoutCommands.RestoreDefaults(restorePayload), destructive: true));
                }

                // [Import] — 90px wide, to the left of Restore
                var importRect = new Rect(restoreRect.x - BtnGap - 90f, btnY, 90f, 28f);
                if (Widgets.ButtonText(importRect, UiText.Get("EPR.Import")))
                    Find.WindowStack.Add(new Dialog_ImportReadouts());

                // [Options] — 90px wide, to the left of Export
                var exportRect = new Rect(importRect.x - BtnGap - 90f, btnY, 90f, 28f);
                var optionsRect = new Rect(exportRect.x - BtnGap - 90f, btnY, 90f, 28f);
                if (Widgets.ButtonText(optionsRect, UiText.Get("EPR.Options")))
                    Find.WindowStack.Add(new Dialog_PanelOptions());

                // [Export] — 90px wide, to the left of Import
                if (Widgets.ButtonText(exportRect, UiText.Get("EPR.Export")))
                    Find.WindowStack.Add(new Dialog_ExportReadouts());

                // --- Content area (below top panel) ---
                var content = new Rect(inRect.x, inRect.y + PanelH + Gap,
                    inRect.width, inRect.height - PanelH - Gap);

                // Left column: Groups panel (fixed 220px, full height)
                var leftRect = new Rect(content.x, content.y, LeftW, content.height);

                // Remaining area to the right — split into left half (editor) and right half
                float rightX = leftRect.xMax + Gap;
                float rightW = content.xMax - rightX;
                float halfW = (rightW - Gap) / 2f;

                var editorRect = new Rect(rightX, content.y, halfW, content.height);
                var rightHalf = new Rect(rightX + halfW + Gap, content.y, halfW, content.height);

                // Toggle button FIRST: IMGUI gives the click to the earliest
                // drawn control, and the views' section headers lay an
                // invisible full-width fold toggle over this same strip.
                string toggleLabel = showPools
                    ? UiText.Get("EPR.ShowResources")
                    : UiText.Get("EPR.ShowPools");
                var toggleRect = new Rect(rightHalf.xMax - ToggleBtnW, rightHalf.y - 2f,
                    ToggleBtnW, ToggleBtnH);
                if (Widgets.ButtonText(toggleRect, toggleLabel))
                    showPools = !showPools;
                tree.HeaderReservedRight = ToggleBtnW + 8f;
                poolList.HeaderReservedRight = ToggleBtnW + 8f;

                // --- Right half: Resources or Pools depending on showPools toggle ---
                Rect poolListRect, poolEditorRect;
                if (showPools)
                {
                    // Dynamic height: desired height clamped so pool editor gets at least 120px
                    float desiredListH = poolList.DesiredHeight(rightHalf.width, this);
                    float maxListH = selectedPoolId >= 0
                        ? Mathf.Max(0f, rightHalf.height - PoolEditorMinH - Gap)
                        : rightHalf.height;
                    float poolListH = Mathf.Min(desiredListH, maxListH);

                    poolListRect = new Rect(rightHalf.x, rightHalf.y, rightHalf.width, poolListH);
                    if (selectedPoolId >= 0)
                    {
                        float poolEditorH = rightHalf.height - poolListH - Gap;
                        poolEditorRect = new Rect(rightHalf.x, rightHalf.y + poolListH + Gap,
                            rightHalf.width, poolEditorH);
                    }
                    else
                    {
                        poolEditorRect = default(Rect);
                    }
                }
                else
                {
                    poolListRect = default(Rect);
                    poolEditorRect = default(Rect);
                }

                // Draw all panels
                groups.Draw(leftRect, this);
                editor.Draw(editorRect, this);

                if (showPools)
                {
                    poolList.Draw(poolListRect, this);
                    if (selectedPoolId >= 0)
                        poolEditor.Draw(poolEditorRect, this);
                }
                else
                {
                    tree.Draw(rightHalf, this);
                }

                DrawDragGhost();
                EprDrag.ResolveMouseUp();
            }
        }

        private void DrawDragGhost()
        {
            if (!EprDrag.Active || EprDrag.Payload == null) return;
            EnsureGhost(EprDrag.Payload, PoolsSnapshot);
            if (ghostDef == null) return;
            var mouse = Event.current.mousePosition;
            Widgets.ThingIcon(new Rect(mouse.x - 16f, mouse.y - 16f, 32f, 32f), ghostDef);
        }

        private void EnsureGhost(string payload, PoolSnapshot pools)
        {
            if (string.Equals(ghostPayload, payload, System.StringComparison.Ordinal)
                && ReferenceEquals(ghostPools, pools))
                return;

            ghostPayload = payload;
            ghostPools = pools;
            ghostDef = null;
            if (SlotToken.IsPoolRef(EprDrag.Payload))
            {
                int poolId = SlotToken.PoolId(payload);
                if (pools != null
                    && pools.TryGet(poolId, out _, out string iconDefName, out _)
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
