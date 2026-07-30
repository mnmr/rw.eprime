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

        // Caching fields (NeedsRebuild pattern)
        private int builtGroupsVersion = -1;
        private int builtThresholdsVersion = -1;
        private int builtGroupId = -1;
        private float builtWidth = -1f;
        private RenderCountSnapshot builtCounts;
        private PoolSnapshot builtPools;

        // Cached draw models: one per tier depth
        private List<(RenderModel model, DrawModel draw)> cachedBands;
        private float cachedBandsHeight; // total height of all tier bands + gaps

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

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            UiVersion.ObserveCurrentMetrics();
            var settings = EPrimeReadoutsMod.Settings;
            var store = ReadoutStore.Current;
            if (store == null) return;

            var group = owner.SelectedGroup;

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
            string headerLabel = group != null ? group.Name : "EPR.Editor".Translate();

            // Cache name width alongside rebuild gate (only per group change, not per frame)
            if (group != null && (cachedNameGroupId != group.Id
                || cachedNameUiVersion != UiVersion.Current
                || cachedNameWidth < 0f))
            {
                Text.Font = GameFont.Small;
                cachedNameWidth = Text.CalcSize(group.Name).x;
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
                headerLabel, "EPR.HelpEditor".Translate(), ref folded, clickableW);
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
                    "EPR.SelectGroupHint".Translate());
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
                CellRenderer.Draw(dm);
                HandleEditorInput(dm, model, group, store, owner, bandRect);
                Widgets.EndGroup();

                bandY += bandH;
                if (t < rowCount - 1) bandY += LayoutMetrics.GroupGap;
            }

            y = bandY + 16f;

            // --- Options section (only when a slot is selected and still in the group) ---
            if (owner.selectedCanonical != null && IsStillInGroup(owner.selectedCanonical, group))
            {
                string selectedDisplayName = selectedDisplayNames.Get(
                    owner.selectedCanonical,
                    store.PoolsVersion,
                    SlotToken.IsPoolRef(owner.selectedCanonical),
                    resolveDisplayName);
                string optHeader = "EPR.OptionsFor".Translate(selectedDisplayName ?? owner.selectedCanonical);
                bool dummy = false;
                float optHeaderUsed = EprStyle.SectionHeader(rect.x, y, rect.width,
                    optHeader, null, ref dummy);
                y += optHeaderUsed;
                DrawOptionsBody(new Rect(rect.x, y, rect.width, rect.yMax - y), group, owner);
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
            if (width != builtWidth) return true;
            if (!ReferenceEquals(builtPools, pools)) return true;
            if (!ReferenceEquals(builtCounts, counts)) return true;
            return false;
        }

        private void Rebuild(ReadoutStore store, ReadoutGroup group, float width, Dialog_ReadoutConfig owner)
        {
            if (builtGroupsVersion != store.GroupsVersion)
                cachedNameWidth = -1f;
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
            builtGroupId = group.Id;
            builtWidth = width;
            builtPools = owner.PoolsSnapshot;
            builtCounts = owner.RenderData?.Counts;

            IReadOnlyDictionary<string, int> counts = builtCounts != null
                ? builtCounts.Counts
                : new Dictionary<string, int>();

            // Use the shared pools snapshot from the dialog
            var pools = owner.PoolsSnapshot;

            int rowCount = EditorBand.MaxDepth(group.Tiers);
            cachedBands = new List<(RenderModel, DrawModel)>(rowCount);
            float totalH = 0f;
            for (int t = 1; t <= rowCount; t++)
            {
                int capturedTier = t;
                var input = new LayoutInput
                {
                    Groups = new List<ReadoutGroup> { group },
                    EditorMode = true,
                    DepthOf = g => capturedTier,
                    Counts = counts,
                    Thresholds = store.Model.Thresholds,
                    Width = width,
                    Catalog = GameResourceCatalog.Instance,
                    Pools = pools,
                };
                var model = ReadoutLayoutEngine.Build(input);
                var dm = DrawModel.Resolve(model, owner.RenderData);
                cachedBands.Add((model, dm));
                totalH += model.TotalHeight;
                if (t < rowCount) totalH += LayoutMetrics.GroupGap;
            }
            cachedBandsHeight = totalH;
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
                        // Determine label for tooltip
                        string tipLabel;
                        if (SlotToken.IsPoolRef(token))
                        {
                            int poolId = SlotToken.PoolId(token);
                            var pool = store.Model.PoolById(poolId);
                            tipLabel = pool != null ? pool.Name : token;
                        }
                        else if (SlotToken.IsPool(token))
                            tipLabel = GameResourceCatalog.Instance.CategoryLabelOf(
                                SlotToken.MemberName(token)).CapitalizeFirst();
                        else
                        {
                            var def = DefDatabase<ThingDef>.GetNamedSilentFail(cell.DefName);
                            tipLabel = def != null ? def.LabelCap : cell.DefName;
                        }
                        TooltipHandler.TipRegion(cellRect, (TaggedString)tipLabel);
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
                            int groupId = group.Id;
                            int toTier = cell.Tier;
                            int toSlot = insertSlot;
                            bool bandSourced = EprDrag.FromTier >= 0;
                            string dragToken = EprDrag.Payload;
                            int fromTier = EprDrag.FromTier;
                            int fromSlot = EprDrag.FromSlot;
                            EprDrag.HoverDropAction = () =>
                            {
                                var g = ReadoutStore.Current?.Model.GroupById(groupId);
                                if (g == null) return;
                                var tiers = TierOps.Clone(g.Tiers);
                                bool changed = bandSourced
                                    ? TierOps.Move(tiers, fromTier, fromSlot, toTier, toSlot)
                                    : TierOps.Add(tiers, dragToken, toTier, toSlot);
                                if (changed)
                                    ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                            };
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
                        int groupId = group.Id;
                        int toTier = cell.Tier;
                        int toSlot = cell.Slot;
                        bool bandSourced = EprDrag.FromTier >= 0;
                        string dragToken = EprDrag.Payload;
                        int fromTier = EprDrag.FromTier;
                        int fromSlot = EprDrag.FromSlot;
                        EprDrag.HoverDropAction = () =>
                        {
                            var g = ReadoutStore.Current?.Model.GroupById(groupId);
                            if (g == null) return;
                            var tiers = TierOps.Clone(g.Tiers);
                            bool changed = bandSourced
                                ? TierOps.Move(tiers, fromTier, fromSlot, toTier, toSlot)
                                : TierOps.Add(tiers, dragToken, toTier, toSlot);
                            if (changed)
                                ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                        };
                    }
                }
            }
        }

        private static bool IsStillInGroup(string canonical, ReadoutGroup group)
        {
            foreach (var tier in group.Tiers)
                foreach (var t in tier)
                    if (SlotToken.Canonical(t) == canonical) return true;
            return false;
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

        private void DrawOptionsBody(Rect rect, ReadoutGroup group, Dialog_ReadoutConfig owner)
        {
            if (owner.selectedCanonical == null) return;

            float y = rect.y;

            // Line 1: show-when-zero checkbox (width capped at 50% of panel)
            string storedToken = null;
            if (group != null)
            {
                foreach (var tier in group.Tiers)
                    foreach (var t in tier)
                        if (SlotToken.Canonical(t) == owner.selectedCanonical) { storedToken = t; break; }
            }
            bool showWhenZero = storedToken == null || SlotToken.ShowWhenZero(storedToken);
            bool prevShow = showWhenZero;
            float checkboxW = Mathf.Min(rect.width * 0.5f, rect.width);
            Widgets.CheckboxLabeled(
                new Rect(rect.x, y, checkboxW, 22f),
                "EPR.ShowWhenZero".Translate(), ref showWhenZero);
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
                "EPR.ThresholdCaption".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 24f;

            // Line 3: low/critical/set/clear (unchanged column alignment)
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, y + 3f, 34f, 22f), "EPR.Low".Translate());
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(new Rect(rect.x + 38f, y, 60f, 24f),
                ref thresholdEditor.LowValue, ref thresholdEditor.LowBuffer, 0f, 999999f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 106f, y + 3f, 56f, 22f), "EPR.Critical".Translate());
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(new Rect(rect.x + 166f, y, 60f, 24f),
                ref thresholdEditor.CriticalValue, ref thresholdEditor.CriticalBuffer, 0f, 999999f);
            if (Widgets.ButtonText(new Rect(rect.x + 234f, y, 50f, 24f), "EPR.Set".Translate()))
                ReadoutCommands.SetThreshold(owner.selectedCanonical,
                    thresholdEditor.LowValue, thresholdEditor.CriticalValue);
            if (Widgets.ButtonText(new Rect(rect.x + 288f, y, 56f, 24f), "EPR.Clear".Translate()))
            {
                ReadoutCommands.ClearThreshold(owner.selectedCanonical);
                thresholdEditor.LowValue = 0;
                thresholdEditor.CriticalValue = 0;
                thresholdEditor.LowBuffer = "0";
                thresholdEditor.CriticalBuffer = "0";
            }
        }
    }
}
