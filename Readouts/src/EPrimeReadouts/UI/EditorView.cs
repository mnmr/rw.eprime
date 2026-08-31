using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
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
        private int builtCountRulesVersion = -1;
        private int builtGroupId = -1;
        private int builtUiVersion = -1;
        private float builtWidth = -1f;
        private RenderCountSnapshot? builtCounts;
        private PoolSnapshot? builtPools;
        private bool builtStorageOnly;
        private bool builtHideForbidden;
        private bool builtShowNegative;

        private ReadoutStore? groupOwner;
        private int groupSnapshotVersion = -1;
        private int groupSnapshotId = -1;
        private ReadoutGroup? groupSnapshot;

        // Cached draw models: one per tier depth
        private List<(RenderModel model, DrawModel draw)>? cachedBands;

        // Owner: this EditorView. Key: selected group identity,
        // GroupsVersion, and UiVersion.Current. Value: the Medium-font name
        // width plus coupled row/icon geometry. Dependencies: the group
        // group name and active Medium GUI style. Refresh: immediate on a key
        // change, before drawing the row. Equality: exact key hits reuse all
        // measurements. Teardown: Reset releases the key and measurements.
        private float cachedNameWidth = -1f;
        private float cachedNameLineHeight = -1f;
        private float cachedRenameSize = -1f;
        private float cachedNameRowHeight = -1f;
        private int cachedNameGroupId = -1;
        private int cachedNameGroupsVersion = -1;
        private int cachedNameUiVersion = -1;

        // Options fields synchronized against the selected token's stored value.
        private readonly ThresholdEditorState thresholdEditor = new ThresholdEditorState();

        // Pool-backed names refresh when pool data changes; static def/category
        // names keep the value resolved for their selection.
        private readonly SelectedDisplayNameCache selectedDisplayNames = new SelectedDisplayNameCache();
        private static readonly Func<string, string> resolveDisplayName = ResolveDisplayName;

        // Tracks external selection changes (e.g. the resource tree selecting a
        // freshly added token) so buffers/display name re-derive exactly once.
        private string? lastSyncedCanonical;

        private int selectionGroupsVersion = -1;
        private int selectionGroupId = -1;
        private string? selectionCanonical;
        private bool selectionInGroup;
        private string? selectionStoredToken;
        private string? optionsDisplayName;
        private string? optionsHeader;
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

            // --- Help, followed by the selected group name ---
            bool folded = settings.helpEditorFolded;

            // Establish the font first, then cache every piece of geometry
            // coupled to it. The rename icon follows the font size while the
            // row follows the larger of icon and measured line height.
            if (group != null && (cachedNameGroupId != group.Id
                || cachedNameGroupsVersion != store.GroupsVersion
                || cachedNameUiVersion != UiVersion.Current
                || cachedNameWidth < 0f))
            {
                using (GuiStateScope.Capture())
                {
                    Text.Font = GameFont.Medium;
                    cachedNameWidth = WrText.FitWidth(group.Name);
                    cachedNameLineHeight = Mathf.Ceil(Text.LineHeight);
                    cachedRenameSize = Mathf.Ceil(
                        Text.CurFontStyle.fontSize);
                    if (cachedRenameSize <= 0f)
                        cachedRenameSize = cachedNameLineHeight;
                    cachedNameRowHeight = Mathf.Max(
                        cachedNameLineHeight, cachedRenameSize) + 4f;
                }
                cachedNameGroupId = group.Id;
                cachedNameGroupsVersion = store.GroupsVersion;
                cachedNameUiVersion = UiVersion.Current;
            }

            float headerUsed = EprStyle.HelpGroup(
                rect.x,
                rect.y,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpEditor"),
                ref folded);
            if (folded != settings.helpEditorFolded)
                EPrimeReadoutsMod.Persist(s => s.helpEditorFolded = folded);

            // The selected name is a stronger white row below Help; the rename
            // pencil follows the measured name rather than staying in the
            // section header above it. HelpGroup's trailing margin is shared
            // evenly above and below this visual row without changing the
            // logical advance to the editor body.
            if (group != null)
            {
                float helpBottomMargin = folded
                    ? EprStyle.HelpCollapsedBottomMargin
                    : EprStyle.HelpExpandedBottomMargin;
                float groupNameY = rect.y + headerUsed
                    - helpBottomMargin / 2f;
                float pencilX = Mathf.Min(
                    rect.x + cachedNameWidth + 6f,
                    rect.xMax - cachedRenameSize - 2f);
                float labelWidth = Mathf.Max(
                    0f, pencilX - rect.x - 6f);
                var labelRect = new Rect(rect.x, groupNameY,
                    labelWidth, cachedNameRowHeight);
                var pencilRect = new Rect(pencilX,
                    groupNameY
                        + (cachedNameRowHeight - cachedRenameSize) / 2f,
                    cachedRenameSize, cachedRenameSize);
                using (GuiStateScope.Capture())
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.WordWrap = false;
                    GUI.color = Color.white;
                    Widgets.Label(labelRect, group.Name);
                    if (cachedNameWidth > labelWidth)
                        TooltipHandler.TipRegion(labelRect, group.Name);
                    if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
                    {
                        int capturedId = group.Id;
                        string capturedName = group.Name;
                        Find.WindowStack.Add(new Dialog_NameInput(
                            "EPR.RenameGroup", capturedName,
                            name => ReadoutCommands.RenameGroup(
                                capturedId, name.Trim())));
                    }
                }
                headerUsed += cachedNameRowHeight;
            }

            if (group == null)
            {
                UnfocusThresholdInputs();
                GUI.color = EprStyle.SelectionTint;
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
                var (model, dm) = cachedBands![t];
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
                    optionsHeader!, null, ref dummy); // set by the block above
                y += optHeaderUsed;
                DrawOptionsBody(new Rect(rect.x, y, rect.width, rect.yMax - y),
                    group, selectionStoredToken, owner, store);
            }
            else
            {
                UnfocusThresholdInputs();
            }
        }

        private bool NeedsRebuild(
            ReadoutStore store,
            int groupId,
            float width,
            PoolSnapshot? pools,
            RenderCountSnapshot? counts)
        {
            if (cachedBands == null) return true;
            if (store.GroupsVersion != builtGroupsVersion) return true;
            if (store.ThresholdsVersion != builtThresholdsVersion) return true;
            if (store.CountRulesVersion != builtCountRulesVersion) return true;
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
            ReleaseCachedBands();
            var basisSettings = EPrimeReadoutsMod.Settings;
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
            builtCountRulesVersion = store.CountRulesVersion;
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
                    CountRules = store.Model.CountRules,
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
                    string? token = cell.Token;
                    if (token == null) continue;
                    string canonical = SlotToken.Canonical(token);

                    // Hover highlight + tooltip
                    if (Mouse.IsOver(cellRect))
                    {
                        Widgets.DrawHighlight(cellRect);
                        WrTips.Text("EPR.EditorTip", token, dm.Tooltips[i])
                            .Region(cellRect);
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
                                EprDrag.FromTier >= 0, EprDrag.Payload!, // tokenDrag implies a payload
                                EprDrag.FromTier, EprDrag.FromSlot);
                        }
                    }

                    // Slot input
                    int controlId = GUIUtility.GetControlID(FocusType.Passive, cellRect);
                    EprDrag.ObserveSource(controlId, cellRect);

                    if (e.type == EventType.MouseDown && e.button == 0 && Mouse.IsOver(cellRect))
                    {
                        // Left: drag + click=select. Shift has no alternate
                        // removal behavior; right-click remains the shortcut.
                        string capturedToken = token;
                        string capturedCanonical = canonical;
                        int fromTier = cell.Tier;
                        int fromSlot = cell.Slot;
                        EprDrag.OnPressToken(controlId, capturedToken, fromTier, fromSlot, () =>
                            Select(capturedCanonical, owner));
                        e.Use();
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
                            EprDrag.FromTier >= 0, EprDrag.Payload!, // tokenDrag implies a payload
                            EprDrag.FromTier, EprDrag.FromSlot);
                    }
                }
            }

            // A left press on the band background clears the slot selection.
            // Slot icons consumed their press above; each icon/empty cell's
            // full column (icon plus counter) stays slot territory so a click
            // on a counter remains inert instead of deselecting.
            if (e.type == EventType.MouseDown && e.button == 0
                && owner.selectedCanonical != null
                && e.mousePosition.x >= 0f && e.mousePosition.x <= bandRect.width
                && e.mousePosition.y >= 0f && e.mousePosition.y <= bandRect.height)
            {
                float cellW = PanelCellMetrics.Current.CellW;
                float halfPad = (cellW - LayoutMetrics.IconSize) / 2f;
                bool onSlotColumn = false;
                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    if (cell.Kind != CellKind.Icon && cell.Kind != CellKind.EmptySlot)
                        continue;
                    float colX = cell.Rect.X - halfPad;
                    if (e.mousePosition.x >= colX && e.mousePosition.x <= colX + cellW)
                    {
                        onSlotColumn = true;
                        break;
                    }
                }
                if (!onSlotColumn)
                {
                    owner.selectedCanonical = null;
                    e.Use();
                }
            }
        }

        private void EnsureSelection(ReadoutGroup? group, int groupsVersion, string? canonical)
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

        private ReadoutGroup? GetGroupSnapshot(ReadoutStore store, int groupId)
        {
            if (ReferenceEquals(groupOwner, store)
                && groupSnapshotVersion == store.GroupsVersion
                && groupSnapshotId == groupId)
                return groupSnapshot;

            ReadoutGroup? source = store.Model.GroupById(groupId);
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
                store?.Model.Thresholds!); // Select tolerates a null dictionary

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

        // Cache contract:
        // Owner: this EditorView instance.
        // Key: none (single value).
        // Value: resolved count-rule row captions, the stable segment-label
        // array handed to SegmentedControl, and measured widths.
        // Dependencies: UiVersion.Current (language, UI scale — labels and
        // segments both render Small).
        // Refresh policy: immediate on UI revision change.
        // Equality policy: equal rebuilds are identical.
        // Teardown: Reset restores the unset stamp.
        private int ruleRowUiVersion = -1;
        private string? ruleCaption;
        private string? ruleStorageLabel;
        private string? ruleForbiddenLabel;
        private readonly string[] ruleStateLabels = new string[3];
        private float ruleLabelW;
        private float ruleRowW;

        /// Horizontal padding inside one segment around its caption.
        private const float SegmentPadX = 12f;

        /// Vertical padding above each section caption in the options body.
        private const float CaptionPadTop = 16f;

        private void EnsureRuleRow()
        {
            if (ruleRowUiVersion == UiVersion.Current) return;
            using (GuiStateScope.Capture())
            {
                ruleCaption = UiText.Get("EPR.CountRuleCaption");
                ruleStorageLabel = UiText.Get("EPR.SearchStorageOnly");
                ruleForbiddenLabel = UiText.Get("EPR.SearchHideForbidden");
                ruleStateLabels[(int)BasisOverride.Inherit] = UiText.Get("EPR.RuleDefault");
                ruleStateLabels[(int)BasisOverride.ForceOn] = UiText.Get("EPR.RuleAlwaysOn");
                ruleStateLabels[(int)BasisOverride.ForceOff] = UiText.Get("EPR.RuleAlwaysOff");
                Text.Font = GameFont.Small;
                ruleLabelW = Mathf.Max(
                    WrText.FitWidth(ruleStorageLabel),
                    WrText.FitWidth(ruleForbiddenLabel));
                // Segments share one width, so the row is sized from the
                // widest translated caption — every language fits.
                float stateW = 0f;
                for (int i = 0; i < ruleStateLabels.Length; i++)
                    stateW = Mathf.Max(stateW, WrText.FitWidth(ruleStateLabels[i]));
                ruleRowW = ruleStateLabels.Length * (stateW + 2f * SegmentPadX)
                    + 2f * 2f + (ruleStateLabels.Length - 1);
            }
            ruleRowUiVersion = UiVersion.Current;
        }

        /// One override row: Small label on the left, a right-aligned
        /// three-segment selector (Default / Always On / Always Off).
        /// Segment indices are BasisOverride ordinals. Returns the
        /// (possibly changed) state.
        private BasisOverride DrawRuleRow(
            Rect rect, ref float y, string label, BasisOverride state)
        {
            const float ControlH = 24f;
            TextAnchor anchor = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, y, ruleLabelW, ControlH), label);
            Text.Anchor = anchor;
            int clicked = SegmentedControl.Row(
                new Rect(rect.xMax - ruleRowW, y, ruleRowW, ControlH),
                ruleStateLabels, (int)state);
            if (clicked >= 0) state = (BasisOverride)clicked;
            y += ControlH + 2f;
            return state;
        }

        private ThresholdRowLayout EnsureThresholdRow()
        {
            if (thresholdRowUiVersion == UiVersion.Current) return thresholdRow;
            using (GuiStateScope.Capture())
            {
                // Labels render in Tiny, which RimWorld resolves to Small when
                // tiny text is unavailable; measure whatever it resolves to.
                float lowW = WrText.FitTinyWidth(UiText.Get("EPR.Low"));
                float criticalW = WrText.FitTinyWidth(
                    UiText.Get("EPR.Critical"));
                Text.Font = GameFont.Small;
                float setW = WrText.FitWidth(UiText.Get("EPR.Set")) + ButtonPadX;
                float clearW = WrText.FitWidth(UiText.Get("EPR.Clear")) + ButtonPadX;
                thresholdRow = ThresholdRowLayout.Compute(lowW, criticalW, setW, clearW);
            }
            thresholdRowUiVersion = UiVersion.Current;
            return thresholdRow;
        }

        private void DrawOptionsBody(Rect rect, ReadoutGroup group, string? storedToken,
            Dialog_ReadoutConfig owner, ReadoutStore store)
        {
            if (owner.selectedCanonical == null) return;

            float y = rect.y;
            ResolvedTinyTextMetrics tinyMetrics = EprStyle.TinyTextMetrics;

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
            y += 22f + CaptionPadTop;

            // Line 2: threshold caption (Tiny, CaptionText style)
            float captionH = tinyMetrics.MinHeight(22f);
            GUI.color = EprStyle.CaptionText;
            TinyText.Caption(new Rect(
                    rect.x,
                    y,
                    rect.width,
                    captionH),
                UiText.Get("EPR.ThresholdCaption"));
            GUI.color = Color.white;
            y += captionH + 2f;

            // Line 3: low/critical/set/clear. Columns start where measured
            // labels end, so substituted fonts and long translations shift
            // the row instead of clipping.
            var row = EnsureThresholdRow();
            const float ControlH = 24f;
            float thresholdRowH = Mathf.Max(ControlH, tinyMetrics.LineHeight);
            float controlY = y + Mathf.Floor((thresholdRowH - ControlH) / 2f);
            float labelY = y
                + Mathf.Floor((thresholdRowH - tinyMetrics.LineHeight) / 2f)
                + tinyMetrics.CaptionOffsetY;
            TinyText.Label(new Rect(
                    rect.x,
                    labelY,
                    row.LowLabelW,
                    tinyMetrics.LineHeight),
                UiText.Get("EPR.Low"));
            DrawThresholdField(
                new Rect(rect.x + row.LowFieldX, controlY,
                    ThresholdRowLayout.FieldW, ControlH),
                "EPR.LowThreshold", ref thresholdEditor.LowValue,
                ref thresholdEditor.LowBuffer);
            TinyText.Label(new Rect(
                    rect.x + row.CriticalLabelX,
                    labelY,
                    row.CriticalLabelW,
                    tinyMetrics.LineHeight),
                UiText.Get("EPR.Critical"));
            DrawThresholdField(
                new Rect(rect.x + row.CriticalFieldX, controlY,
                    ThresholdRowLayout.FieldW, ControlH),
                "EPR.CriticalThreshold", ref thresholdEditor.CriticalValue,
                ref thresholdEditor.CriticalBuffer);
            if (Widgets.ButtonText(new Rect(
                    rect.x + row.SetX, controlY, row.SetW, ControlH),
                UiText.Get("EPR.Set")))
                ReadoutCommands.SetThreshold(owner.selectedCanonical,
                    thresholdEditor.LowValue, thresholdEditor.CriticalValue);
            if (Widgets.ButtonText(new Rect(
                    rect.x + row.ClearX, controlY, row.ClearW, ControlH),
                UiText.Get("EPR.Clear")))
            {
                ReadoutCommands.ClearThreshold(owner.selectedCanonical);
                thresholdEditor.LowValue = 0;
                thresholdEditor.CriticalValue = 0;
                thresholdEditor.LowBuffer = "0";
                thresholdEditor.CriticalBuffer = "0";
            }
            y += thresholdRowH + CaptionPadTop;

            // Line 4: count-rule caption (Tiny, CaptionText style)
            EnsureRuleRow();
            GUI.color = EprStyle.CaptionText;
            TinyText.Caption(new Rect(
                    rect.x, y, rect.width, captionH),
                ruleCaption!);
            GUI.color = Color.white;
            y += captionH + 2f;

            // Lines 5-6: count-basis overrides. Rules are keyed by canonical
            // token and shared across every slot showing it, so this edits
            // authoritative state through a synced command.
            store.Model.CountRules.TryGetValue(
                owner.selectedCanonical, out CountRule rule);
            BasisOverride storage = DrawRuleRow(rect, ref y,
                ruleStorageLabel!, rule.StorageOnly);
            if (storage != rule.StorageOnly)
                ReadoutCommands.SetCountRule(owner.selectedCanonical,
                    (int)storage, (int)rule.HideForbidden);
            BasisOverride forbidden = DrawRuleRow(rect, ref y,
                ruleForbiddenLabel!, rule.HideForbidden);
            if (forbidden != rule.HideForbidden)
                ReadoutCommands.SetCountRule(owner.selectedCanonical,
                    (int)rule.StorageOnly, (int)forbidden);
        }

        internal void Reset()
        {
            ReleaseCachedBands();
            builtGroupsVersion = -1;
            builtThresholdsVersion = -1;
            builtCountRulesVersion = -1;
            builtGroupId = -1;
            builtUiVersion = -1;
            builtWidth = -1f;
            builtCounts = null;
            builtPools = null;
            groupOwner = null;
            groupSnapshotVersion = -1;
            groupSnapshotId = -1;
            groupSnapshot = null;
            cachedNameWidth = -1f;
            cachedNameLineHeight = -1f;
            cachedRenameSize = -1f;
            cachedNameRowHeight = -1f;
            cachedNameGroupId = -1;
            cachedNameGroupsVersion = -1;
            cachedNameUiVersion = -1;
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
            ruleRowUiVersion = -1;
            ruleCaption = null;
            ruleStorageLabel = null;
            ruleForbiddenLabel = null;
            ruleStateLabels[0] = ruleStateLabels[1] = ruleStateLabels[2] = null!;
            ruleLabelW = 0f;
            ruleRowW = 0f;
        }

        internal bool HandleEscape()
        {
            if (DialogInputFocus.TryHandleEscape(
                "EPR.LowThreshold", thresholdEditor.LowBuffer,
                () => thresholdEditor.LowBuffer = ""))
                return true;
            return DialogInputFocus.TryHandleEscape(
                "EPR.CriticalThreshold", thresholdEditor.CriticalBuffer,
                () => thresholdEditor.CriticalBuffer = "");
        }

        internal void Unfocus() => UnfocusThresholdInputs();

        private static void DrawThresholdField(Rect rect, string controlName,
            ref int value, ref string buffer)
        {
            GUI.SetNextControlName(controlName);
            string edited = Widgets.TextField(rect, buffer);
            if (string.Equals(edited, buffer, StringComparison.Ordinal)) return;
            if (edited.Length == 0)
            {
                buffer = "";
                return;
            }
            for (int i = 0; i < edited.Length; i++)
                if (edited[i] < '0' || edited[i] > '9')
                    return;
            if (!int.TryParse(edited, out int parsed)) return;
            if (parsed < 0 || parsed > 999999) return;
            buffer = edited;
            value = parsed;
        }

        private static void UnfocusThresholdInputs()
        {
            DialogInputFocus.Unfocus("EPR.LowThreshold");
            DialogInputFocus.Unfocus("EPR.CriticalThreshold");
        }

        private void ReleaseCachedBands()
        {
            if (cachedBands == null) return;
            cachedBands = null;
        }
    }
}
