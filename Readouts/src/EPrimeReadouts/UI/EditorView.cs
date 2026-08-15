using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Right panel: the selected group rendered by the layout engine in editor
    /// mode (stacked rows, one per tier). The engine produces the same geometry
    /// as the in-game band so each row looks identical to the readout at that
    /// tier. Below the band — when a slot is selected — an Options section with
    /// show-when-zero and threshold controls.
    public sealed class EditorView
    {
        private const float PencilW = 18f;
        private const float PencilH = 18f;
        private static readonly IReadOnlyDictionary<string, int> emptyCounts =
            new Dictionary<string, int>();

        // Cache contract:
        // Owner: one configuration-dialog EditorView and one ReadoutStore.
        // Key: selected group, exact group/threshold revisions, width,
        // UiVersion, shared pool/count snapshot identities and the
        // storage-only/hide-forbidden count-basis options.
        // Value: detached group snapshot and immutable resolved band DrawModels.
        // Dependencies: only the keys above plus selected-token presentation.
        // Refresh policy: immediate on exact dependency changes.
        // Equality policy: unchanged dependencies preserve band/model identity.
        // Teardown: Reset releases all store, model, snapshot and def references.
        private int builtGroupsVersion = -1;
        private int builtThresholdsVersion = -1;
        private int builtGroupId = -1;
        private int builtUiVersion = -1;
        private float builtWidth = -1f;
        private RenderCountSnapshot builtCounts;
        private PoolSnapshot builtPools;
        private bool builtStorageOnly;
        private bool builtHideForbidden;
        private bool builtShowNegative;

        private ReadoutStore groupOwner;
        private int groupSnapshotVersion = -1;
        private int groupSnapshotId = -1;
        private ReadoutGroup groupSnapshot;

        // Cached draw models: one per tier depth
        private List<(RenderModel model, DrawModel draw)> cachedBands;

        // Cached name width (rebuilt alongside bands or when group changes)
        private float cachedNameWidth = -1f;
        private int cachedNameGroupId = -1;
        private int cachedNameUiVersion = -1;

        // Options fields synchronized against the selected token's stored value.
        private readonly ThresholdEditorState thresholdEditor = new ThresholdEditorState();

        // Pool-backed names refresh when pool data changes; static def/category
        // names keep the value resolved for their selection.
        private readonly SelectedDisplayNameCache selectedDisplayNames = new SelectedDisplayNameCache();
        private static readonly Func<string, string> resolveDisplayName = ResolveDisplayName;

        // Tracks external selection changes (e.g. the resource tree selecting a
        // freshly added token) so buffers/display name re-derive exactly once.
        private string lastSyncedCanonical;

        private int selectionGroupsVersion = -1;
        private int selectionGroupId = -1;
        private string selectionCanonical;
        private bool selectionInGroup;
        private string selectionStoredToken;
        private string optionsDisplayName;
        private string optionsHeader;
        private int optionsLanguageVersion = -1;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            UiVersion.ObserveCurrentMetrics();
            var settings = EPrimeReadoutsMod.Settings;
            var store = ReadoutStore.Current;
            if (store == null) return;

            var group = GetGroupSnapshot(store, owner.selectedGroupId);

            if (owner.selectedCanonical != lastSyncedCanonical)
            {
                if (owner.selectedCanonical != null)
                    Select(owner.selectedCanonical, owner);
                lastSyncedCanonical = owner.selectedCanonical;
            }
            else if (owner.selectedCanonical != null)
            {
                thresholdEditor.Refresh(store.ThresholdsVersion, store.Model.Thresholds);
            }

            EnsureSelection(group, store.GroupsVersion, owner.selectedCanonical);

            // --- Rebuild cached band models and name width when needed ---
            if (group != null && NeedsRebuild(
                store,
                group.Id,
                rect.width,
                owner.PoolsSnapshot,
                owner.RenderData?.Counts))
                Rebuild(store, group, rect.width, owner);

            // --- Section header: group name with rename pencil ---
            // Measure cached name width (update when group changes)
            bool folded = settings.helpEditorFolded;
            string headerLabel = group != null ? group.Name : UiText.Get("EPR.Editor");

            // Cache name width alongside rebuild gate (only per group change, not per frame)
            if (group != null && (cachedNameGroupId != group.Id
                || cachedNameUiVersion != UiVersion.Current
                || cachedNameWidth < 0f))
            {
                Text.Font = GameFont.Small;
                cachedNameWidth = WrText.FitWidth(group.Name);
                cachedNameGroupId = group.Id;
                cachedNameUiVersion = UiVersion.Current;
            }

            // Pencil sits right of the name text; SectionHeader clickable region shrinks to
            // name width + small padding so the pencil does not trigger the fold toggle.
            float pencilX = group != null
                ? Mathf.Min(rect.x + cachedNameWidth + 6f, rect.xMax - PencilW - 2f)
                : rect.xMax; // no pencil
            float clickableW = group != null ? (pencilX - rect.x) : rect.width;

            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                headerLabel, UiText.Get("EPR.HelpEditor"), ref folded, clickableW);
            if (folded != settings.helpEditorFolded)
                EPrimeReadoutsMod.Persist(s => s.helpEditorFolded = folded);

            // Draw rename pencil (handled before the invisible button above so it gets priority)
            if (group != null)
            {
                var pencilRect = new Rect(pencilX, rect.y + 2f, PencilW, PencilH);
                if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
                {
                    int capturedId = group.Id;
                    string capturedName = group.Name;
                    Find.WindowStack.Add(new Dialog_NameInput(
                        "EPR.RenameGroup", capturedName,
                        name => ReadoutCommands.RenameGroup(capturedId, name.Trim())));
                }
            }

            if (group == null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(new Rect(rect.x, rect.y + headerUsed, rect.width, 24f),
                    UiText.Get("EPR.SelectGroupHint"));
                GUI.color = Color.white;
                return;
            }

            float y = rect.y + headerUsed;

            // --- Draw cached band rows ---
            int rowCount = cachedBands != null ? cachedBands.Count : 0;
            float bandY = y;
            for (int t = 0; t < rowCount; t++)
            {
                var (model, dm) = cachedBands[t];
                float bandH = model.TotalHeight;
                var bandRect = new Rect(rect.x, bandY, rect.width, bandH);

                Widgets.BeginGroup(bandRect);
                try
                {
                    CellRenderer.Draw(dm);
                    HandleEditorInput(dm, model, group, store, owner, bandRect);
                }
                finally
                {
                    Widgets.EndGroup();
                }

                bandY += bandH;
                if (t < rowCount - 1) bandY += LayoutMetrics.GroupGap;
            }

            y = bandY + 16f;

            // --- Options section (only when a slot is selected and still in the group) ---
            if (owner.selectedCanonical != null && selectionInGroup)
            {
                string selectedDisplayName = selectedDisplayNames.Get(
                    store,
                    owner.selectedCanonical,
                    store.PoolsVersion,
                    UiVersion.LanguageCurrent,
                    SlotToken.IsPoolRef(owner.selectedCanonical),
                    resolveDisplayName);
                string displayName = selectedDisplayName ?? owner.selectedCanonical;
                if (optionsLanguageVersion != UiVersion.LanguageCurrent
                    || !string.Equals(optionsDisplayName, displayName,
                        StringComparison.Ordinal))
                {
                    optionsDisplayName = displayName;
                    optionsLanguageVersion = UiVersion.LanguageCurrent;
                    optionsHeader = "EPR.OptionsFor".Translate(displayName);
                }
                bool dummy = false;
                float optHeaderUsed = EprStyle.SectionHeader(rect.x, y, rect.width,
                    optionsHeader, null, ref dummy);
                y += optHeaderUsed;
                DrawOptionsBody(new Rect(rect.x, y, rect.width, rect.yMax - y),
                    group, selectionStoredToken, owner);
            }
        }

        private bool NeedsRebuild(
            ReadoutStore store,
            int groupId,
            float width,
            PoolSnapshot pools,
            RenderCountSnapshot counts)
        {
            if (cachedBands == null) return true;
            if (store.GroupsVersion != builtGroupsVersion) return true;
            if (store.ThresholdsVersion != builtThresholdsVersion) return true;
            if (groupId != builtGroupId) return true;
            if (UiVersion.Current != builtUiVersion) return true;
            if (width != builtWidth) return true;
            if (!ReferenceEquals(builtPools, pools)) return true;
            if (!ReferenceEquals(builtCounts, counts)) return true;
            var settings = EPrimeReadoutsMod.Settings;
            if (settings.searchStorageOnly != builtStorageOnly) return true;
            if (settings.searchHideForbidden != builtHideForbidden) return true;
            // Planned-work debt itself arrives with the count snapshot above;
            // only the negative-display choice is an independent input.
            if (settings.showNegativeCounts != builtShowNegative) return true;
            return false;
        }

        private void Rebuild(ReadoutStore store, ReadoutGroup group, float width, Dialog_ReadoutConfig owner)
        {
            if (builtGroupsVersion != store.GroupsVersion)
                cachedNameWidth = -1f;
            var basisSettings = EPrimeReadoutsMod.Settings;
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
            builtGroupId = group.Id;
            builtUiVersion = UiVersion.Current;
            builtWidth = width;
            builtPools = owner.PoolsSnapshot;
            builtCounts = owner.RenderData?.Counts;
            builtStorageOnly = basisSettings.searchStorageOnly;
            builtHideForbidden = basisSettings.searchHideForbidden;
            builtShowNegative = basisSettings.showNegativeCounts;

            IReadOnlyDictionary<string, int> counts = builtCounts != null
                ? builtCounts.Counts
                : emptyCounts;

            // Use the shared pools snapshot from the dialog
            var pools = owner.PoolsSnapshot;

            int rowCount = EditorBand.MaxDepth(group.Tiers);
            cachedBands = new List<(RenderModel, DrawModel)>(rowCount);
            for (int t = 1; t <= rowCount; t++)
            {
                int capturedTier = t;
                var input = new LayoutInput
                {
                    Groups = new List<ReadoutGroup> { group },
                    EditorMode = true,
                    DepthOf = g => capturedTier,
                    Counts = counts,
                    // Editor bands show the same narrowed counts as the
                    // readout so both agree while the options dialog is open.
                    SearchCounts = builtCounts?.SearchCounts,
                    SearchStorageOnly = builtStorageOnly,
                    SearchHideForbidden = builtHideForbidden,
                    Debts = builtCounts?.Debts,
                    AllowNegativeCounts = builtShowNegative,
                    Thresholds = store.Model.Thresholds,
                    Width = width,
                    Catalog = GameResourceCatalog.Instance,
                    Pools = pools,
                    Metrics = PanelCellMetrics.Current,
                };
                var model = ReadoutLayoutEngine.Build(input);
                var dm = DrawModel.Resolve(model, owner.RenderData);
                cachedBands.Add((model, dm));
            }
        }

        private void HandleEditorInput(DrawModel dm, RenderModel model,
            ReadoutGroup group, ReadoutStore store, Dialog_ReadoutConfig owner, Rect bandRect)
        {
            var cells = model.Cells;
            var e = Event.current;

            bool tokenDrag = EprDrag.Active && EprDrag.Payload != null;

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Kind != CellKind.Icon && cell.Kind != CellKind.EmptySlot) continue;

                var cellRect = new Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.W, cell.Rect.H);

                if (cell.Kind == CellKind.Icon)
                {
                    string token = cell.Token;
                    if (token == null) continue;
                    string canonical = SlotToken.Canonical(token);

                    // Hover highlight + tooltip
                    if (Mouse.IsOver(cellRect))
                    {
                        Widgets.DrawHighlight(cellRect);
                        TooltipHandler.TipRegion(cellRect, (TaggedString)dm.Tooltips[i]);
                    }

                    // Selection highlight
                    if (owner.selectedCanonical != null && canonical == owner.selectedCanonical)
                        Widgets.DrawHighlightSelected(cellRect);

                    // Drop target: while a token drag is active, register insert marker
                    if (tokenDrag)
                    {
                        if (Mouse.IsOver(cellRect))
                        {
                            bool rightHalf = e.mousePosition.x > cellRect.x + cellRect.width / 2f;
                            int insertSlot = cell.Slot + (rightHalf ? 1 : 0);
                            // Draw 2px vertical insert marker at left or right edge
                            float markerX = rightHalf ? cellRect.xMax - 1f : cellRect.x - 1f;
                            Widgets.DrawBoxSolid(new Rect(markerX, cellRect.y, 2f, cellRect.height),
                                new Color(1f, 1f, 1f, 0.9f));
                            EprDrag.SetTokenDrop(group.Id, cell.Tier, insertSlot,
                                EprDrag.FromTier >= 0, EprDrag.Payload,
                                EprDrag.FromTier, EprDrag.FromSlot);
                        }
                    }

                    // Slot input
                    int controlId = GUIUtility.GetControlID(FocusType.Passive, cellRect);
                    EprDrag.ObserveSource(controlId, cellRect);

                    if (e.type == EventType.MouseDown && e.button == 0 && Mouse.IsOver(cellRect))
                    {
                        if (e.shift)
                        {
                            // Shift+left: remove
                            int groupId = group.Id;
                            var tiers = TierOps.Clone(group.Tiers);
                            if (TierOps.Remove(tiers, token))
                                ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                            if (owner.selectedCanonical == canonical) owner.selectedCanonical = null;
                            e.Use();
                        }
                        else
                        {
                            // Plain left: drag + click=select
                            string capturedToken = token;
                            string capturedCanonical = canonical;
                            int fromTier = cell.Tier;
                            int fromSlot = cell.Slot;
                            EprDrag.OnPressToken(controlId, capturedToken, fromTier, fromSlot, () =>
                                Select(capturedCanonical, owner));
                            e.Use();
                        }
                    }
                    else if (e.type == EventType.MouseDown && e.button == 1 && Mouse.IsOver(cellRect))
                    {
                        // Right-click: remove
                        int groupId = group.Id;
                        var tiers = TierOps.Clone(group.Tiers);
                        if (TierOps.Remove(tiers, token))
                            ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                        if (owner.selectedCanonical == canonical) owner.selectedCanonical = null;
                        e.Use();
                    }
                }
                else // EmptySlot
                {
                    if (tokenDrag && Mouse.IsOver(cellRect))
                    {
                        Widgets.DrawHighlight(cellRect);
                        EprDrag.SetTokenDrop(group.Id, cell.Tier, cell.Slot,
                            EprDrag.FromTier >= 0, EprDrag.Payload,
                            EprDrag.FromTier, EprDrag.FromSlot);
                    }
                }
            }
        }

        private void EnsureSelection(ReadoutGroup group, int groupsVersion, string canonical)
        {
            int groupId = group?.Id ?? -1;
            if (selectionGroupsVersion == groupsVersion
                && selectionGroupId == groupId
                && string.Equals(selectionCanonical, canonical, StringComparison.Ordinal))
                return;

            selectionGroupsVersion = groupsVersion;
            selectionGroupId = groupId;
            selectionCanonical = canonical;
            selectionInGroup = false;
            selectionStoredToken = null;
            if (group == null || canonical == null) return;
            for (int tier = 0; tier < group.Tiers.Count; tier++)
                for (int slot = 0; slot < group.Tiers[tier].Count; slot++)
                {
                    string token = group.Tiers[tier][slot];
                    if (SlotToken.Canonical(token) != canonical) continue;
                    selectionInGroup = true;
                    selectionStoredToken = token;
                    return;
                }
        }

        private ReadoutGroup GetGroupSnapshot(ReadoutStore store, int groupId)
        {
            if (ReferenceEquals(groupOwner, store)
                && groupSnapshotVersion == store.GroupsVersion
                && groupSnapshotId == groupId)
                return groupSnapshot;

            ReadoutGroup source = store.Model.GroupById(groupId);
            groupSnapshot = source == null ? null : new ReadoutGroup
            {
                Id = source.Id,
                Name = source.Name,
                OrderIndex = source.OrderIndex,
                DefaultEnabled = source.DefaultEnabled,
                Tiers = TierOps.Clone(source.Tiers),
            };
            groupOwner = store;
            groupSnapshotVersion = store.GroupsVersion;
            groupSnapshotId = groupId;
            return groupSnapshot;
        }

        private void Select(string canonical, Dialog_ReadoutConfig owner)
        {
            owner.selectedCanonical = canonical;
            lastSyncedCanonical = canonical;
            var store = ReadoutStore.Current;
            thresholdEditor.Select(
                canonical,
                store != null ? store.ThresholdsVersion : 0,
                store?.Model.Thresholds);

        }

        private static string ResolveDisplayName(string canonical)
        {
            var store = ReadoutStore.Current;
            bool isPoolRef = SlotToken.IsPoolRef(canonical);
            bool isPool = SlotToken.IsPool(canonical);
            if (isPoolRef)
            {
                int poolId = SlotToken.PoolId(canonical);
                var pool = store?.Model.PoolById(poolId);
                return pool != null ? pool.Name : canonical;
            }
            if (isPool)
            {
                string memberName = SlotToken.MemberName(canonical);
                return GameResourceCatalog.Instance.CategoryLabelOf(memberName).CapitalizeFirst();
            }

            string defName = SlotToken.MemberName(canonical);
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null ? (string)def.LabelCap : canonical;
        }

        // Cache contract:
        // Owner: this EditorView instance.
        // Key: none (single value).
        // Value: immutable ThresholdRowLayout.
        // Dependencies: UiVersion.Current (language, UI scale, tiny-text
        // preference — label/button text and the resolved font both follow it).
        // Refresh policy: immediate on UI revision change.
        // Equality policy: value struct; equal rebuilds are identical.
        // Teardown: Reset restores the unset stamp.
        private ThresholdRowLayout thresholdRow;
        private int thresholdRowUiVersion = -1;

        /// Extra width ButtonText needs around its caption.
        private const float ButtonPadX = 16f;

        private ThresholdRowLayout EnsureThresholdRow()
        {
            if (thresholdRowUiVersion == UiVersion.Current) return thresholdRow;
            using (new GuiStateScope())
            {
                // Labels render in Tiny, which RimWorld resolves to Small when
                // tiny text is unavailable; measure whatever it resolves to.
                Text.Font = GameFont.Tiny;
                float lowW = WrText.FitWidth(UiText.Get("EPR.Low"));
                float criticalW = WrText.FitWidth(UiText.Get("EPR.Critical"));
                Text.Font = GameFont.Small;
                float setW = WrText.FitWidth(UiText.Get("EPR.Set")) + ButtonPadX;
                float clearW = WrText.FitWidth(UiText.Get("EPR.Clear")) + ButtonPadX;
                thresholdRow = ThresholdRowLayout.Compute(lowW, criticalW, setW, clearW);
            }
            thresholdRowUiVersion = UiVersion.Current;
            return thresholdRow;
        }

        private void DrawOptionsBody(Rect rect, ReadoutGroup group, string storedToken,
            Dialog_ReadoutConfig owner)
        {
            if (owner.selectedCanonical == null) return;

            float y = rect.y;

            // Line 1: show-when-zero checkbox (width capped at 50% of panel)
            bool showWhenZero = storedToken == null || SlotToken.ShowWhenZero(storedToken);
            bool prevShow = showWhenZero;
            float checkboxW = Mathf.Min(rect.width * 0.5f, rect.width);
            Widgets.CheckboxLabeled(
                new Rect(rect.x, y, checkboxW, 22f),
                UiText.Get("EPR.ShowWhenZero"), ref showWhenZero);
            if (showWhenZero != prevShow && storedToken != null)
            {
                string newToken = SlotToken.WithShowWhenZero(storedToken, showWhenZero);
                string selectedCanonical = owner.selectedCanonical;
                var tiers = TierOps.Clone(group.Tiers);
                foreach (var tier in tiers)
                    for (int i = 0; i < tier.Count; i++)
                        if (SlotToken.Canonical(tier[i]) == selectedCanonical)
                        {
                            tier[i] = newToken;
                            ReadoutCommands.SetGroupLayout(group.Id, TierBlobCodec.Encode(tiers));
                            break;
                        }
            }
            y += 24f;

            // Line 2: threshold caption (Tiny, CaptionText style)
            Text.Font = GameFont.Tiny;
            GUI.color = EprStyle.CaptionText;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                UiText.Get("EPR.ThresholdCaption"));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 24f;

            // Line 3: low/critical/set/clear. Columns start where measured
            // labels end, so substituted fonts and long translations shift
            // the row instead of clipping.
            var row = EnsureThresholdRow();
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, y + 3f, row.LowLabelW, 22f),
                UiText.Get("EPR.Low"));
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(
                new Rect(rect.x + row.LowFieldX, y, ThresholdRowLayout.FieldW, 24f),
                ref thresholdEditor.LowValue, ref thresholdEditor.LowBuffer, 0f, 999999f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + row.CriticalLabelX, y + 3f,
                row.CriticalLabelW, 22f), UiText.Get("EPR.Critical"));
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(
                new Rect(rect.x + row.CriticalFieldX, y, ThresholdRowLayout.FieldW, 24f),
                ref thresholdEditor.CriticalValue, ref thresholdEditor.CriticalBuffer, 0f, 999999f);
            if (Widgets.ButtonText(new Rect(rect.x + row.SetX, y, row.SetW, 24f),
                UiText.Get("EPR.Set")))
                ReadoutCommands.SetThreshold(owner.selectedCanonical,
                    thresholdEditor.LowValue, thresholdEditor.CriticalValue);
            if (Widgets.ButtonText(new Rect(rect.x + row.ClearX, y, row.ClearW, 24f),
                UiText.Get("EPR.Clear")))
            {
                ReadoutCommands.ClearThreshold(owner.selectedCanonical);
                thresholdEditor.LowValue = 0;
                thresholdEditor.CriticalValue = 0;
                thresholdEditor.LowBuffer = "0";
                thresholdEditor.CriticalBuffer = "0";
            }
        }

        internal void Reset()
        {
            cachedBands = null;
            builtGroupsVersion = -1;
            builtThresholdsVersion = -1;
            builtGroupId = -1;
            builtUiVersion = -1;
            builtWidth = -1f;
            builtCounts = null;
            builtPools = null;
            groupOwner = null;
            groupSnapshotVersion = -1;
            groupSnapshotId = -1;
            groupSnapshot = null;
            selectionGroupsVersion = -1;
            selectionGroupId = -1;
            selectionCanonical = null;
            selectionStoredToken = null;
            selectionInGroup = false;
            selectedDisplayNames.Reset();
            optionsDisplayName = null;
            optionsHeader = null;
            optionsLanguageVersion = -1;
            thresholdRow = default;
            thresholdRowUiVersion = -1;
        }
    }
}
