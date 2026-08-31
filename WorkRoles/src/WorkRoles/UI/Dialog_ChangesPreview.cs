using System;
using System.Collections.Generic;
using RimShared.Common;
using RimShared.UiLib;
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

        internal readonly struct PreviewChipSource
        {
            internal PreviewChipSource(Role role, ChipState state, string? tip)
            {
                RoleId = role.id;
                RenderData = RoleChipRenderData.From(role);
                State = state;
                Tip = tip;
            }

            internal int RoleId { get; }
            internal RoleChipRenderData RenderData { get; }
            internal ChipState State { get; }
            internal string? Tip { get; }
        }

        /// One preview line: chips with per-chip states, reason tooltips, and
        /// suitability verdicts (default = no badge).
        public class Line
        {
            private readonly List<PreviewChipSource> chipSources =
                new List<PreviewChipSource>();
            private readonly List<RoleChipVerdict> verdicts = new List<RoleChipVerdict>();
            private readonly List<StructuredTip?> structuredTips =
                new List<StructuredTip?>();

            internal int ChipCount => chipSources.Count;
            internal PreviewChipSource ChipAt(int index) => chipSources[index];

            internal void AddChip(Role role, ChipState state, StructuredTip? tip,
                RoleChipVerdict verdict = default)
            {
                string? text = tip?.PlainText;
                var source = new PreviewChipSource(role, state, text);
                chipSources.Add(source);
                verdicts.Add(verdict);
                structuredTips.Add(tip);
            }

            internal void InsertChip(int index, Role role, ChipState state, StructuredTip? tip,
                RoleChipVerdict verdict = default)
            {
                string? text = tip?.PlainText;
                var source = new PreviewChipSource(role, state, text);
                chipSources.Insert(index, source);
                verdicts.Insert(index, verdict);
                structuredTips.Insert(index, tip);
            }

            internal RoleChipVerdict VerdictAt(int index) =>
                index >= 0 && index < verdicts.Count ? verdicts[index] : default;

            internal StructuredTip? StructuredTipAt(int index)
            {
                return index >= 0 && index < structuredTips.Count
                    ? structuredTips[index] : null;
            }
        }

        public class PawnPreview
        {
            public Pawn pawn = null!; // assigned by every preview builder
            public List<Line> lines = new List<Line>();
            public bool included = true;
        }

        private readonly struct ChipLayout
        {
            public ChipLayout(RoleChipRenderData renderData, ChipState state,
                string? tip, StructuredTip? structuredTip, Rect rect,
                RoleChipVerdict verdict)
            {
                RenderData = renderData;
                State = state;
                Tip = tip;
                StructuredTip = structuredTip;
                Rect = rect;
                Verdict = verdict;
            }

            internal RoleChipRenderData RenderData { get; }
            internal ChipState State { get; }
            internal string? Tip { get; }
            internal StructuredTip? StructuredTip { get; }
            internal Rect Rect { get; }
            internal RoleChipVerdict Verdict { get; }

            public bool ContentEquals(ChipLayout other) =>
                RenderData.ContentEquals(other.RenderData)
                && State == other.State
                && string.Equals(Tip, other.Tip, StringComparison.Ordinal)
                && (ReferenceEquals(StructuredTip, other.StructuredTip)
                    || (StructuredTip != null && other.StructuredTip != null
                        && StructuredTip.ContentEquals(other.StructuredTip)))
                && Rect.x == other.Rect.x && Rect.y == other.Rect.y
                && Rect.width == other.Rect.width
                && Rect.height == other.Rect.height
                && VerdictEquals(Verdict, other.Verdict);

            private static bool VerdictEquals(RoleChipVerdict left,
                RoleChipVerdict right) => left.Shown == right.Shown
                && ColorEquals(left.Bottom, right.Bottom)
                && ColorEquals(left.Top, right.Top);

            private static bool ColorEquals(Color left, Color right) =>
                left.r == right.r && left.g == right.g
                && left.b == right.b && left.a == right.a;
        }

        private readonly struct EntryLayout
        {
            private readonly ChipLayout[] chips;

            public EntryLayout(string pawnLabel, ChipLayout[] chips,
                float overlayHeight)
            {
                PawnLabel = pawnLabel;
                this.chips = chips;
                OverlayHeight = overlayHeight;
            }

            internal string PawnLabel { get; }
            internal int ChipCount => chips.Length;
            internal ChipLayout ChipAt(int index) => chips[index];
            internal float OverlayHeight { get; }

            internal bool ContentEquals(EntryLayout other)
            {
                if (!string.Equals(PawnLabel, other.PawnLabel,
                        StringComparison.Ordinal)
                    || OverlayHeight != other.OverlayHeight
                    || chips.Length != other.chips.Length)
                    return false;
                for (int i = 0; i < chips.Length; i++)
                    if (!chips[i].ContentEquals(other.chips[i])) return false;
                return true;
            }
        }

        private sealed class ChangesPreviewRenderSnapshot
        {
            private readonly EntryLayout[] entries;

            internal ChangesPreviewRenderSnapshot(EntryLayout[] entries,
                VariableViewportLayout layout, string noChangesText)
            {
                this.entries = entries;
                Layout = layout;
                NoChangesText = noChangesText;
            }

            internal VariableViewportLayout Layout { get; }
            internal string NoChangesText { get; }
            internal EntryLayout EntryAt(int index) => entries[index];

            internal bool ContentEquals(ChangesPreviewRenderSnapshot other)
            {
                if (other == null || entries.Length != other.entries.Length
                    || !string.Equals(NoChangesText, other.NoChangesText,
                        StringComparison.Ordinal))
                    return false;
                for (int i = 0; i < entries.Length; i++)
                    if (!entries[i].ContentEquals(other.entries[i])) return false;
                return true;
            }
        }

        private const float PawnRowH = 24f;
        private const float LineGap = 4f;
        private const float GroupGap = 8f;
        private const float ChipGap = 4f;

        private string? title;
        private readonly Func<string>? titleFactory;
        private readonly List<PawnPreview> entries;
        private readonly Action<HashSet<Pawn>> onApply;
        private readonly Func<List<PawnPreview>> rebuild;
        /// False renders a purely informational plan (no select-all row, no
        /// per-pawn checkboxes) and keeps Apply enabled even with no changes —
        /// the auto-optimize enable confirmation applies the whole plan.
        private readonly bool selectable;
        private int includedCount;
        private int observedLanguageRevision;
        private int entriesGeneration;

        // Owner: this dialog instance. Key: the banner factories' language
        // revision and the available banner width. Value: the translated info
        // and warning texts plus their measured wrapped heights (immutable
        // between key changes; a null/empty warning result renders nothing).
        // Dependencies: LanguageChangeCoordinator.Revision, banner width,
        // GameFont.Small metrics (the dialog is fixed-size, so width moves
        // only with UI metrics, which rebuild the whole window layout). The
        // warning's condition (e.g. colony count) is deliberately frozen for
        // the display session and re-evaluated on language refresh or reopen.
        // Refresh: immediate on the next draw after a key change. Equality:
        // matching keys reuse texts and heights. Teardown: PostClose resets
        // the stamps; the strings follow the dialog instance.
        private readonly Func<string>? infoFactory;
        private readonly Func<string?>? warningFactory;
        private string? infoText;
        private string? warningText;
        private int infoLanguageRevision = int.MinValue;
        private float infoMeasuredWidth = -1f;
        private float infoHeight;
        private float warningHeight;

        // Owner: changes-preview dialog. Key: RoleStore identity, UiVersion,
        // language revision, available row width, entry generation, and each
        // preview pawn's ExternalPawnFacts revision. Value: one immutable-by-
        // publication snapshot containing detached pawn labels, role chip
        // render data, tooltips, verdicts, geometry, and viewport layout.
        // Dependencies: current role presentation, pawn names, translated empty
        // text, preview source rows, and width. Refresh: immediate on a key
        // change. Equality: equal contents retain identity within one RoleStore.
        // Teardown: PostClose releases the snapshot, owner, and revision stamps.
        private ChangesPreviewRenderSnapshot? renderSnapshot;
        private RoleStore? renderOwner;
        private int renderUiRevision = int.MinValue;
        private int renderLanguageRevision = int.MinValue;
        private int renderEntriesGeneration = int.MinValue;
        private int renderFactsCurrent = int.MinValue;
        private float renderWidth = -1f;
        private int[] renderPawnFactRevisions = Array.Empty<int>();
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 620f);

        public Dialog_ChangesPreview(string? title, List<PawnPreview> entries,
            Action<HashSet<Pawn>> onApply, Func<List<PawnPreview>> rebuild)
        {
            selectable = true;
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
            Func<List<PawnPreview>> rebuild,
            Func<string>? infoFactory = null,
            Func<string?>? warningFactory = null, bool selectable = true)
            : this(titleFactory?.Invoke(), entries, onApply, rebuild)
        {
            this.titleFactory = titleFactory;
            this.infoFactory = infoFactory;
            this.warningFactory = warningFactory;
            this.selectable = selectable;
        }

        private void RefreshLanguageIfNeeded()
        {
            int current = LanguageChangeCoordinator.Revision;
            if (observedLanguageRevision == current) return;
            observedLanguageRevision = current;

            StructuredTipPresenter.Reset();
            if (titleFactory != null) title = titleFactory();
            Dictionary<Pawn, bool> selections = IdentitySelectionPreserver.Capture(
                entries,
                entry => entry.pawn,
                entry => entry.included,
                ReferenceIdentityComparer<Pawn>.Instance);
            List<PawnPreview>? refreshed = rebuild?.Invoke();
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

            unchecked { entriesGeneration++; }
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
                    if (la.ChipCount != lb.ChipCount) return false;
                    for (int k = 0; k < la.ChipCount; k++)
                        if (la.ChipAt(k).RoleId != lb.ChipAt(k).RoleId
                            || la.ChipAt(k).State != lb.ChipAt(k).State) return false;
                }
            }
            return true;
        }

        private static void DrawStateChip(Rect rect, RoleChipRenderData role,
            ChipState state, string? tip, StructuredTip? structuredTip,
            RoleChipVerdict verdict)
        {
            var style = state == ChipState.Kept ? ChipStyle.Subtle : ChipStyle.Normal;
            RoleChipUI.Draw(rect, role, style, showRemove: false, dragSource: null!, onClick: null!,
                interactive: false, verdict: verdict);
            if (state == ChipState.Removed)
                RoleChipUI.DrawRemovedOutline(rect);
            if (tip != null && Mouse.IsOver(rect))
            {
                if (structuredTip != null)
                    StructuredTipPresenter.TipRegion(rect, structuredTip);
                else
                    TooltipHandler.TipRegion(rect, tip);
            }
        }

        /// Measurement-gated banner build: CalcHeight runs only when the
        /// language revision or available width changes.
        private void EnsureInfoBanner(float width)
        {
            if (infoFactory == null) return;
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (infoText != null && infoLanguageRevision == languageRevision
                && infoMeasuredWidth == width)
                return;
            infoText = infoFactory();
            string? warning = warningFactory?.Invoke();
            warningText = string.IsNullOrEmpty(warning) ? null : warning;
            GameFont previousFont = Text.Font;
            bool previousWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                infoHeight = Text.CalcHeight(infoText, width);
                warningHeight = warningText == null
                    ? 0f : Text.CalcHeight(warningText, width);
            }
            finally
            {
                Text.Font = previousFont;
                Text.WordWrap = previousWordWrap;
            }
            infoLanguageRevision = languageRevision;
            infoMeasuredWidth = width;
        }

        private void EnsureRenderSnapshot(float width)
        {
            RoleStore? store = RoleStore.Current;
            int uiRevision = UiVersion.Current;
            int languageRevision = LanguageChangeCoordinator.Revision;
            int factsCurrent = ExternalPawnFacts.Revisions.Current;
            bool ownerChanged = !ReferenceEquals(renderOwner, store);
            bool keyChanged = renderSnapshot == null || ownerChanged
                || renderUiRevision != uiRevision
                || renderLanguageRevision != languageRevision
                || renderEntriesGeneration != entriesGeneration
                || renderWidth != width;

            bool pawnFactsChanged = renderSnapshot == null
                || renderPawnFactRevisions.Length != entries.Count;
            if (!pawnFactsChanged && renderFactsCurrent != factsCurrent)
                for (int i = 0; i < entries.Count; i++)
                    if (renderPawnFactRevisions[i]
                        != ExternalPawnFacts.Revisions.RevisionOf(entries[i].pawn))
                    {
                        pawnFactsChanged = true;
                        break;
                    }

            if (!keyChanged && !pawnFactsChanged)
            {
                renderFactsCurrent = factsCurrent;
                return;
            }

            var descriptors = new EntryLayout[entries.Count];
            var heights = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                PawnPreview entry = entries[i];
                int chipCount = 0;
                for (int lineIndex = 0; lineIndex < entry.lines.Count; lineIndex++)
                    chipCount += entry.lines[lineIndex].ChipCount;
                var chips = new ChipLayout[chipCount];
                int nextChip = 0;

                float localY = PawnRowH;
                float xMax = width;
                for (int lineIndex = 0; lineIndex < entry.lines.Count; lineIndex++)
                {
                    Line line = entry.lines[lineIndex];
                    float x = 26f;
                    float curY = localY;
                    for (int chipIndex = 0; chipIndex < line.ChipCount; chipIndex++)
                    {
                        var chip = line.ChipAt(chipIndex);
                        Role? currentRole = store?.RoleById(chip.RoleId);
                        RoleChipRenderData renderData = currentRole == null
                            ? chip.RenderData
                            : RoleChipRenderData.From(currentRole);
                        RoleChipVerdict verdict = line.VerdictAt(chipIndex);
                        float chipWidth = RoleChipUI.WidthFor(renderData, showRemove: false,
                            verdictSlot: verdict.Shown);
                        // Preserve the old trailing-gap wrap math exactly: x is
                        // advanced by ChipGap after every chip, including the last.
                        if (x + chipWidth > xMax && x > 26f)
                        {
                            x = 26f;
                            curY += RoleChipUI.Height + LineGap;
                        }
                        chips[nextChip++] = new ChipLayout(renderData,
                            chip.State, chip.Tip,
                            line.StructuredTipAt(chipIndex),
                            new Rect(x, curY, chipWidth, RoleChipUI.Height),
                            verdict);
                        x += chipWidth + ChipGap;
                    }
                    localY = curY + RoleChipUI.Height + LineGap;
                }

                descriptors[i] = new EntryLayout(
                    entry.pawn?.LabelShortCap ?? string.Empty, chips, localY);
                heights[i] = localY + GroupGap;
            }

            var rebuilt = new ChangesPreviewRenderSnapshot(descriptors,
                new VariableViewportLayout(heights),
                "WR_PreviewNoChanges".Translate());
            if (ownerChanged || renderSnapshot == null
                || !renderSnapshot.ContentEquals(rebuilt))
                renderSnapshot = rebuilt;

            renderOwner = store;
            renderUiRevision = uiRevision;
            renderLanguageRevision = languageRevision;
            renderEntriesGeneration = entriesGeneration;
            renderFactsCurrent = factsCurrent;
            renderWidth = width;
            if (renderPawnFactRevisions.Length != entries.Count)
                renderPawnFactRevisions = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                renderPawnFactRevisions[i] =
                    ExternalPawnFacts.Revisions.RevisionOf(entries[i].pawn);
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = GuiStateScope.Capture();
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                DrawContents(inRect);
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
        }

        private void DrawContents(Rect inRect)
        {
            RefreshLanguageIfNeeded();
            float listTop = DrawCachedPreviewTitle(inRect, title);
            if (infoFactory != null)
            {
                EnsureInfoBanner(inRect.width);
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = WrStyle.DimText;
                    Widgets.Label(new Rect(inRect.x, listTop, inRect.width,
                        infoHeight), infoText);
                    GUI.color = Color.white;
                }
                listTop += infoHeight;
                if (warningText != null)
                {
                    listTop += 4f;
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.color = TipText.WarningColor;
                        Widgets.Label(new Rect(inRect.x, listTop,
                            inRect.width, warningHeight), warningText);
                        GUI.color = Color.white;
                    }
                    listTop += warningHeight;
                }
                listTop += 8f;
            }
            if (selectable && entries.Count > 0)
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
            EnsureRenderSnapshot(rowW);
            ChangesPreviewRenderSnapshot snapshot =
                renderSnapshot!; // published by EnsureRenderSnapshot above
            float contentH = entries.Count == 0
                ? PawnRowH : snapshot.Layout.ContentExtent;

            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, contentH));
            try
            {
                var visibleRows = snapshot.Layout.Calculate(scroll.y,
                    listRect.height);
                if (entries.Count == 0)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.color = WrStyle.DimText;
                        Widgets.Label(new Rect(0f, 0f, rowW, PawnRowH),
                            snapshot.NoChangesText);
                        GUI.color = Color.white;
                    }
                }
                else
                {
                    DrawVisibleEntries(snapshot, visibleRows, rowW);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            bool canApply = !selectable || includedCount > 0;
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

        public override void PostClose()
        {
            renderSnapshot = null;
            renderOwner = null;
            renderPawnFactRevisions = Array.Empty<int>();
            renderUiRevision = int.MinValue;
            renderLanguageRevision = int.MinValue;
            renderEntriesGeneration = int.MinValue;
            renderFactsCurrent = int.MinValue;
            renderWidth = -1f;
            infoText = null;
            warningText = null;
            infoLanguageRevision = int.MinValue;
            infoMeasuredWidth = -1f;
            infoHeight = 0f;
            warningHeight = 0f;
            base.PostClose();
            StructuredTipPresenter.Reset();
        }

        private void DrawVisibleEntries(ChangesPreviewRenderSnapshot snapshot,
            VariableViewportRange visibleRows, float width)
        {
            for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)
            {
                PawnPreview entry = entries[i];
                EntryLayout descriptor = snapshot.EntryAt(i);
                float top = snapshot.Layout.OffsetOf(i);

                if (selectable)
                {
                    bool before = entry.included;
                    Widgets.Checkbox(new Vector2(0f, top), ref entry.included, 20f);
                    if (before != entry.included)
                        includedCount += entry.included ? 1 : -1;
                }

                if (Event.current.type != EventType.Repaint) continue;
                Widgets.Label(new Rect(26f, top, width - 26f, PawnRowH),
                    descriptor.PawnLabel);
                for (int chipIndex = 0; chipIndex < descriptor.ChipCount; chipIndex++)
                {
                    ChipLayout chip = descriptor.ChipAt(chipIndex);
                    Rect rect = chip.Rect;
                    rect.y += top;
                    DrawStateChip(rect, chip.RenderData, chip.State, chip.Tip,
                        chip.StructuredTip, chip.Verdict);
                }
                if (!entry.included)
                    Widgets.DrawBoxSolid(new Rect(24f, top, width - 24f,
                        descriptor.OverlayHeight), new Color(0f, 0f, 0f, 0.55f));
            }
        }
    }
}
