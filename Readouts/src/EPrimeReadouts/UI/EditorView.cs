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
    /// tier. Below the band: rename row, then — when a slot is selected — an
    /// Options section with show-when-zero and threshold controls.
    public sealed class EditorView
    {
        private const float NameRowH = 28f;

        private string nameBuffer;
        private int nameForGroupId = -1;
        private int lowValue;
        private string lowBuffer = "0";
        private int criticalValue;
        private string criticalBuffer = "0";

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var settings = EPrimeReadoutsMod.Settings;

            // Section header with fold toggle
            bool folded = settings.helpEditorFolded;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                "EPR.Editor".Translate(), "EPR.HelpEditor".Translate(), ref folded);
            if (folded != settings.helpEditorFolded)
                EPrimeReadoutsMod.Persist(s => s.helpEditorFolded = folded);

            var group = owner.SelectedGroup;
            if (group == null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(new Rect(rect.x, rect.y + headerUsed, rect.width, 24f),
                    "EPR.SelectGroupHint".Translate());
                GUI.color = Color.white;
                return;
            }

            float y = rect.y + headerUsed;

            // Rename row
            DrawNameRow(new Rect(rect.x, y, rect.width, NameRowH), group, owner);
            y += NameRowH + 6f;

            // --- Engine-rendered band rows (one per tier) ---
            var store = ReadoutStore.Current;
            if (store == null) return;

            float availableW = rect.width;
            var counts = Find.CurrentMap != null
                ? GameCounts.Snapshot(Find.CurrentMap)
                : new Dictionary<string, int>();

            int rowCount = EditorBand.MaxDepth(group.Tiers);
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
                    Width = availableW,
                    Catalog = GameResourceCatalog.Instance,
                };
                var model = ReadoutLayoutEngine.Build(input);
                var dm = DrawModel.Resolve(model);

                float bandH = model.TotalHeight;
                var bandRect = new Rect(rect.x, y, availableW, bandH);

                Widgets.BeginGroup(bandRect);
                CellRenderer.Draw(dm);
                HandleEditorInput(dm, model, group, store, owner, bandRect);
                Widgets.EndGroup();

                y += bandH;
                if (t < rowCount) y += LayoutMetrics.GroupGap;
            }

            y += 16f;

            // --- Options section (only when a slot is selected and still in the group) ---
            if (owner.selectedCanonical != null && IsStillInGroup(owner.selectedCanonical, group))
            {
                bool dummy = false;
                float optHeaderUsed = EprStyle.SectionHeader(rect.x, y, rect.width,
                    "EPR.Options".Translate(), null, ref dummy);
                y += optHeaderUsed;
                DrawThresholdRow(new Rect(rect.x, y, rect.width, rect.yMax - y), group, owner);
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
                        // Determine label for tooltip
                        string tipLabel;
                        if (SlotToken.IsPool(token))
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

        private void DrawNameRow(Rect rect, ReadoutGroup group, Dialog_ReadoutConfig owner)
        {
            if (nameForGroupId != group.Id)
            {
                nameBuffer = group.Name;
                nameForGroupId = group.Id;
                owner.selectedCanonical = null;
            }
            nameBuffer = Widgets.TextField(new Rect(rect.x, rect.y, rect.width - 84f, 24f), nameBuffer);
            if (nameBuffer.Trim() != group.Name
                && Widgets.ButtonText(new Rect(rect.xMax - 80f, rect.y, 80f, 24f),
                    "EPR.Rename".Translate()))
                ReadoutCommands.RenameGroup(group.Id, nameBuffer.Trim());
        }

        private void Select(string canonical, Dialog_ReadoutConfig owner)
        {
            owner.selectedCanonical = canonical;
            var store = ReadoutStore.Current;
            if (store != null && store.Model.Thresholds.TryGetValue(canonical, out var spec))
            {
                lowValue = spec.Low;
                criticalValue = spec.Critical;
            }
            else
            {
                lowValue = 0;
                criticalValue = 0;
            }
            lowBuffer = lowValue.ToString();
            criticalBuffer = criticalValue.ToString();
        }

        private void DrawThresholdRow(Rect rect, ReadoutGroup group, Dialog_ReadoutConfig owner)
        {
            if (owner.selectedCanonical == null) return;
            bool isPool = SlotToken.IsPool(owner.selectedCanonical);
            string memberName = SlotToken.MemberName(owner.selectedCanonical);
            string label;
            if (isPool)
            {
                label = GameResourceCatalog.Instance.CategoryLabelOf(memberName).CapitalizeFirst();
            }
            else
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(memberName);
                if (def == null) return;
                label = def.LabelCap;
            }

            float y = rect.y;

            // Line 1: label
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                "EPR.Thresholds".Translate() + ": " + label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += 24f;

            // Line 2: show-when-zero checkbox
            string storedToken = null;
            if (group != null)
            {
                foreach (var tier in group.Tiers)
                    foreach (var t in tier)
                        if (SlotToken.Canonical(t) == owner.selectedCanonical) { storedToken = t; break; }
            }
            bool showWhenZero = storedToken == null || SlotToken.ShowWhenZero(storedToken);
            bool prevShow = showWhenZero;
            Widgets.CheckboxLabeled(
                new Rect(rect.x, y, rect.width, 22f),
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

            // Line 3: low/critical/set/clear
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, y + 3f, 34f, 22f), "EPR.Low".Translate());
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(new Rect(rect.x + 38f, y, 60f, 24f),
                ref lowValue, ref lowBuffer, 0f, 999999f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 106f, y + 3f, 56f, 22f), "EPR.Critical".Translate());
            Text.Font = GameFont.Small;
            Widgets.TextFieldNumeric(new Rect(rect.x + 166f, y, 60f, 24f),
                ref criticalValue, ref criticalBuffer, 0f, 999999f);
            if (Widgets.ButtonText(new Rect(rect.x + 234f, y, 50f, 24f), "EPR.Set".Translate()))
                ReadoutCommands.SetThreshold(owner.selectedCanonical, lowValue, criticalValue);
            if (Widgets.ButtonText(new Rect(rect.x + 288f, y, 56f, 24f), "EPR.Clear".Translate()))
                ReadoutCommands.ClearThreshold(owner.selectedCanonical);
        }
    }
}
