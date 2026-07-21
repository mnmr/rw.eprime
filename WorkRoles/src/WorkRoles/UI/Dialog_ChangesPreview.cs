using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Preview of pending role changes, grouped per colonist and rendered with role
    /// chips; nothing happens unless the user hits Apply, and individual colonists
    /// can be deselected (plus a select-all toggle). The game keeps running
    /// (MP-friendly): at apply time the plan is recomputed, and if the colony changed
    /// in the meantime the request is dropped with a notification instead of
    /// applying a stale plan.
    public class Dialog_ChangesPreview : Dialog_PreviewBase
    {
        public enum ChipState
        {
            Kept,     // stays assigned: dimmed like an already-assigned chip
            Added,    // new: normal chip
            Removed   // dropped: normal chip struck corner-to-corner
        }

        /// One preview line: chips with per-chip states and reason tooltips.
        public class Line
        {
            public List<(Role role, ChipState state, string tip)> chips =
                new List<(Role role, ChipState state, string tip)>();
            private readonly ParallelIndexGuard<Role, ChipState, string, StructuredTip>
                structuredTips = new ParallelIndexGuard<Role, ChipState, string, StructuredTip>();

            internal void AddChip(Role role, ChipState state, StructuredTip tip)
            {
                string text = tip?.PlainText;
                chips.Add((role, state, text));
                structuredTips.Add(role, state, text, tip);
            }

            internal void InsertChip(int index, Role role, ChipState state, StructuredTip tip)
            {
                string text = tip?.PlainText;
                chips.Insert(index, (role, state, text));
                structuredTips.Insert(index, role, state, text, tip);
            }

            internal StructuredTip StructuredTipAt(int index)
            {
                if (index < 0 || index >= chips.Count) return null;
                var chip = chips[index];
                return structuredTips.TryGet(index, chip.role, chip.state, chip.tip,
                    out StructuredTip tip) ? tip : null;
            }
        }

        public class PawnPreview
        {
            public Pawn pawn;
            public List<Line> lines = new List<Line>();
            public bool included = true;
        }

        private readonly struct ChipLayout
        {
            public ChipLayout(Role role, ChipState state, string tip,
                Line sourceLine, int sourceIndex, Rect rect)
            {
                Role = role;
                State = state;
                Tip = tip;
                SourceLine = sourceLine;
                SourceIndex = sourceIndex;
                Rect = rect;
            }

            public Role Role { get; }
            public ChipState State { get; }
            public string Tip { get; }
            public Line SourceLine { get; }
            public int SourceIndex { get; }
            public Rect Rect { get; }
        }

        private readonly struct EntryLayout
        {
            public EntryLayout(ChipLayout[] chips, float overlayHeight)
            {
                Chips = chips;
                OverlayHeight = overlayHeight;
            }

            public ChipLayout[] Chips { get; }
            public float OverlayHeight { get; }
        }

        private const float PawnRowH = 24f;
        private const float LineGap = 4f;
        private const float GroupGap = 8f;
        private const float ChipGap = 4f;

        private string title;
        private readonly Func<string> titleFactory;
        private readonly List<PawnPreview> entries;
        private readonly Action<HashSet<Pawn>> onApply;
        private readonly Func<List<PawnPreview>> rebuild;
        private readonly object structuredTipOwner = new object();
        private EntryLayout[] rowDescriptors = Array.Empty<EntryLayout>();
        private VariableViewportLayout rowLayout;
        private float rowLayoutWidth = -1f;
        private int rowLayoutStamp = -1;
        private int includedCount;
        private int observedLanguageRevision;
        private string noChangesText;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 620f);

        public Dialog_ChangesPreview(string title, List<PawnPreview> entries,
            Action<HashSet<Pawn>> onApply, Func<List<PawnPreview>> rebuild)
        {
            this.title = title;
            this.entries = entries ?? new List<PawnPreview>();
            this.onApply = onApply;
            this.rebuild = rebuild;
            observedLanguageRevision = LanguageChangeCoordinator.Revision;
            for (int i = 0; i < this.entries.Count; i++)
                if (this.entries[i].included)
                    includedCount++;
        }

        internal Dialog_ChangesPreview(Func<string> titleFactory,
            List<PawnPreview> entries, Action<HashSet<Pawn>> onApply,
            Func<List<PawnPreview>> rebuild)
            : this(titleFactory?.Invoke(), entries, onApply, rebuild)
        {
            this.titleFactory = titleFactory;
        }

        private void RefreshLanguageIfNeeded()
        {
            int current = LanguageChangeCoordinator.Revision;
            if (observedLanguageRevision == current) return;
            observedLanguageRevision = current;

            // Old active handles must disappear before replacement models can
            // activate during this draw's new generation.
            Patches.Patch_ActiveTip_TipRect.ReleaseOwner(structuredTipOwner);
            if (titleFactory != null) title = titleFactory();
            Dictionary<Pawn, bool> selections = IdentitySelectionPreserver.Capture(
                entries,
                entry => entry.pawn,
                entry => entry.included,
                ReferenceIdentityComparer<Pawn>.Instance);
            List<PawnPreview> refreshed = rebuild?.Invoke();
            if (refreshed != null)
            {
                includedCount = IdentitySelectionPreserver.Restore(
                    selections, refreshed,
                    entry => entry.pawn,
                    entry => entry.included,
                    (entry, included) => entry.included = included);
                if (!ReferenceEquals(entries, refreshed))
                {
                    entries.Clear();
                    entries.AddRange(refreshed);
                }
            }

            rowDescriptors = Array.Empty<EntryLayout>();
            rowLayout = null;
            rowLayoutWidth = -1f;
            rowLayoutStamp = -1;
            noChangesText = null;
        }

        private static bool SamePlan(List<PawnPreview> a, List<PawnPreview> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].pawn != b[i].pawn || a[i].lines.Count != b[i].lines.Count) return false;
                for (int j = 0; j < a[i].lines.Count; j++)
                {
                    var la = a[i].lines[j];
                    var lb = b[i].lines[j];
                    if (la.chips.Count != lb.chips.Count) return false;
                    for (int k = 0; k < la.chips.Count; k++)
                        if (la.chips[k].role.id != lb.chips[k].role.id
                            || la.chips[k].state != lb.chips[k].state) return false;
                }
            }
            return true;
        }

        private static void DrawStateChip(Rect rect, Role role, ChipState state,
            string tip, Line sourceLine, int sourceIndex)
        {
            var style = state == ChipState.Kept ? ChipStyle.Subtle : ChipStyle.Normal;
            RoleChipUI.Draw(rect, role, style, showRemove: false, dragSource: null, onClick: null,
                interactive: false);
            if (state == ChipState.Removed)
                RoleChipUI.DrawRemovedOutline(rect);
            if (tip != null && Mouse.IsOver(rect))
            {
                StructuredTip structuredTip = sourceLine?.StructuredTipAt(sourceIndex);
                TooltipHandler.TipRegion(rect, structuredTip?.Activate() ?? tip);
            }
        }

        /// Builds all row/chip geometry once per width/UI revision. UiVersion
        /// covers role label/rule-marker changes that affect RoleChipUI.WidthFor.
        private void EnsureRowLayout(float width)
        {
            int stamp = UiVersion.Current;
            if (rowLayout != null && rowLayoutWidth == width
                && rowLayoutStamp == stamp)
                return;

            var descriptors = new EntryLayout[entries.Count];
            var heights = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                PawnPreview entry = entries[i];
                int chipCount = 0;
                for (int lineIndex = 0; lineIndex < entry.lines.Count; lineIndex++)
                    chipCount += entry.lines[lineIndex].chips.Count;
                var chips = new ChipLayout[chipCount];
                int nextChip = 0;

                float localY = PawnRowH;
                float xMax = width;
                for (int lineIndex = 0; lineIndex < entry.lines.Count; lineIndex++)
                {
                    Line line = entry.lines[lineIndex];
                    float x = 26f;
                    float curY = localY;
                    for (int chipIndex = 0; chipIndex < line.chips.Count; chipIndex++)
                    {
                        var chip = line.chips[chipIndex];
                        float chipWidth = RoleChipUI.WidthFor(chip.role, showRemove: false);
                        // Preserve the old trailing-gap wrap math exactly: x is
                        // advanced by ChipGap after every chip, including the last.
                        if (x + chipWidth > xMax && x > 26f)
                        {
                            x = 26f;
                            curY += RoleChipUI.Height + LineGap;
                        }
                        chips[nextChip++] = new ChipLayout(chip.role, chip.state, chip.tip,
                            line, chipIndex,
                            new Rect(x, curY, chipWidth, RoleChipUI.Height));
                        x += chipWidth + ChipGap;
                    }
                    localY = curY + RoleChipUI.Height + LineGap;
                }

                descriptors[i] = new EntryLayout(chips, localY);
                heights[i] = localY + GroupGap;
            }

            rowDescriptors = descriptors;
            rowLayout = new VariableViewportLayout(heights);
            rowLayoutWidth = width;
            rowLayoutStamp = stamp;
            noChangesText = "WR_PreviewNoChanges".Translate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            RefreshLanguageIfNeeded();
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
                Patches.Patch_ActiveTip_TipRect.BeginGeneration(structuredTipOwner);
            try
            {
                float listTop = DrawCachedPreviewTitle(inRect, title);
                if (entries.Count > 0)
                {
                    // Select-all toggle above the list.
                    bool all = includedCount == entries.Count;
                    bool toggled = DrawCachedPreviewSelectAll(inRect, listTop, all);
                    if (toggled != all)
                    {
                        for (int i = 0; i < entries.Count; i++)
                            entries[i].included = toggled;
                        includedCount = toggled ? entries.Count : 0;
                    }
                    listTop += PreviewSelectRowHeight;
                }

                var listRect = PreviewBodyRect(inRect, listTop);
                float rowW = listRect.width - 16f;
                EnsureRowLayout(rowW);
                float contentH = entries.Count == 0 ? PawnRowH : rowLayout.ContentExtent;

                Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, contentH));
                var visibleRows = rowLayout.Calculate(scroll.y, listRect.height);
                if (entries.Count == 0)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.color = WrStyle.DimText;
                        Widgets.Label(new Rect(0f, 0f, rowW, PawnRowH), noChangesText);
                        GUI.color = Color.white;
                    }
                }
                else
                {
                    DrawVisibleEntries(visibleRows, rowW);
                }
                Widgets.EndScrollView();

                bool canApply = includedCount > 0;
                if (DrawPreviewFooter(inRect, canApply))
                {
                    if (SamePlan(entries, rebuild()))
                    {
                        var selected = new HashSet<Pawn>();
                        for (int i = 0; i < entries.Count; i++)
                            if (entries[i].included)
                                selected.Add(entries[i].pawn);
                        onApply?.Invoke(selected);
                    }
                    else
                        WrToast.Show("WR_PreviewStale".Translate(), MessageTypeDefOf.RejectInput);
                    Close();
                }
            }
            finally
            {
                if (repaint)
                    Patches.Patch_ActiveTip_TipRect.EndGeneration(structuredTipOwner);
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            Patches.Patch_ActiveTip_TipRect.ReleaseOwner(structuredTipOwner);
        }

        private void DrawVisibleEntries(VariableViewportRange visibleRows, float width)
        {
            for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)
            {
                PawnPreview entry = entries[i];
                EntryLayout descriptor = rowDescriptors[i];
                float top = rowLayout.OffsetOf(i);

                bool before = entry.included;
                Widgets.Checkbox(new Vector2(0f, top), ref entry.included, 20f);
                if (before != entry.included)
                    includedCount += entry.included ? 1 : -1;

                if (Event.current.type != EventType.Repaint) continue;
                Widgets.Label(new Rect(26f, top, width - 26f, PawnRowH),
                    entry.pawn.LabelShortCap);
                for (int chipIndex = 0; chipIndex < descriptor.Chips.Length; chipIndex++)
                {
                    ChipLayout chip = descriptor.Chips[chipIndex];
                    Rect rect = chip.Rect;
                    rect.y += top;
                    DrawStateChip(rect, chip.Role, chip.State, chip.Tip,
                        chip.SourceLine, chip.SourceIndex);
                }
                if (!entry.included)
                    Widgets.DrawBoxSolid(new Rect(24f, top, width - 24f,
                        descriptor.OverlayHeight), new Color(0f, 0f, 0f, 0.55f));
            }
        }
    }
}
