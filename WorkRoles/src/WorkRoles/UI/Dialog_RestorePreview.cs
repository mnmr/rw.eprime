using System;
using System.Collections.Generic;
using System.Linq;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Selectable preview for Restore Defaults: one checkbox row per restorable
    /// item (missing role or training path, uncovered work type, moved-jobs
    /// recovery, group or color drift, or the recommendation-order reset), with a select-all toggle —
    /// mirroring the Fix My Colony preview's selection model. Application
    /// self-guards against staleness per item.
    public class Dialog_RestorePreview : Dialog_PreviewBase
    {
        private sealed class RestoreWarningSnapshot
        {
            internal RestoreWarningSnapshot(string text, float height)
            {
                Text = text;
                Height = height;
            }

            internal string Text { get; }
            internal float Height { get; }

            internal bool ContentEquals(RestoreWarningSnapshot other) =>
                other != null
                && Text == other.Text
                && Height == other.Height;
        }

        private class Row
        {
            public Seeding.RestoreItem item = null!; // set at construction site
            public bool included = true;
            public RestoreItemKey Key => new RestoreItemKey(item);
        }

        private readonly struct RestoreItemKey : IEquatable<RestoreItemKey>
        {
            private readonly string? templateDef;
            private readonly string? workType;
            private readonly int backfillRoleId;
            private readonly int groupRoleId;
            private readonly int colorRoleId;
            private readonly int holderRoleId;
            private readonly int entriesRoleId;
            private readonly int paletteSnapRoleId;
            private readonly string? pathDef;
            private readonly bool recommendationOrder;

            internal RestoreItemKey(Seeding.RestoreItem item)
            {
                templateDef = item.templateDef;
                workType = item.workType;
                backfillRoleId = item.backfillRoleId;
                groupRoleId = item.groupRoleId;
                colorRoleId = item.colorRoleId;
                holderRoleId = item.holderRoleId;
                entriesRoleId = item.entriesRoleId;
                paletteSnapRoleId = item.paletteSnapRoleId;
                pathDef = item.pathDef;
                recommendationOrder = item.recommendationOrder;
            }

            public bool Equals(RestoreItemKey other) =>
                templateDef == other.templateDef
                && workType == other.workType
                && backfillRoleId == other.backfillRoleId
                && groupRoleId == other.groupRoleId
                && colorRoleId == other.colorRoleId
                && holderRoleId == other.holderRoleId
                && entriesRoleId == other.entriesRoleId
                && paletteSnapRoleId == other.paletteSnapRoleId
                && pathDef == other.pathDef
                && recommendationOrder == other.recommendationOrder;

            public override bool Equals(object obj) =>
                obj is RestoreItemKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (templateDef?.GetHashCode() ?? 0);
                    hash = hash * 31 + (workType?.GetHashCode() ?? 0);
                    hash = hash * 31 + backfillRoleId;
                    hash = hash * 31 + groupRoleId;
                    hash = hash * 31 + colorRoleId;
                    hash = hash * 31 + holderRoleId;
                    hash = hash * 31 + entriesRoleId;
                    hash = hash * 31 + paletteSnapRoleId;
                    hash = hash * 31 + (pathDef?.GetHashCode() ?? 0);
                    return hash * 31 + (recommendationOrder ? 1 : 0);
                }
            }
        }

        private const float RowH = 26f;

        // Dim orange for items that would undo a player change (OptionsTabView's
        // LockedColor family, darkened for body text).
        private static readonly Color WarnColor = new Color(0.9f, 0.6f, 0.25f);

        // Owner: this dialog. Key: RoleStore identity, UiVersion, definition
        // revision, and language revision. Value: private detached restore rows.
        // Dependencies: current store drift, seeded defs, and translated labels.
        // Refresh: immediate at the next DoWindowContents call. Equality: equal
        // rows preserve list identity; stable item keys preserve user selections
        // within the same store. Teardown: PostClose clears rows and owner refs.
        private List<Row> rows = null!; // published by PublishRows in the ctor
        private bool anyUndo;
        private RoleStore? rowsOwner;
        private int rowsUiRevision = -1;
        private int rowsDefinitionRevision = -1;
        private int rowsLanguageRevision = -1;
        private int includedCount;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public Dialog_RestorePreview(List<Seeding.RestoreItem> items)
        {
            PublishRows(items, preserveSelections: false);
            rowsOwner = RoleStore.Current;
            rowsUiRevision = UiVersion.Current;
            rowsDefinitionRevision = DefinitionReloadCoordinator.Revision;
            rowsLanguageRevision = LanguageChangeCoordinator.Revision;
        }

        private void EnsureRowsCurrent()
        {
            RoleStore? owner = RoleStore.Current;
            int uiRevision = UiVersion.Current;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (ReferenceEquals(rowsOwner, owner)
                && rowsUiRevision == uiRevision
                && rowsDefinitionRevision == definitionRevision
                && rowsLanguageRevision == languageRevision)
                return;

            bool sameOwner = ReferenceEquals(rowsOwner, owner);
            PublishRows(Seeding.ComputeRestoreItems(), sameOwner);
            rowsOwner = owner;
            rowsUiRevision = uiRevision;
            rowsDefinitionRevision = definitionRevision;
            rowsLanguageRevision = languageRevision;
        }

        private void PublishRows(List<Seeding.RestoreItem> items,
            bool preserveSelections)
        {
            Dictionary<RestoreItemKey, bool>? selections = null;
            if (preserveSelections && rows != null)
            {
                selections = new Dictionary<RestoreItemKey, bool>(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                    selections[rows[i].Key] = rows[i].included;
            }

            var rebuilt = new List<Row>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var row = new Row { item = items[i] };
                if (selections != null
                    && selections.TryGetValue(row.Key, out bool included))
                    row.included = included;
                rebuilt.Add(row);
            }

            if (rows == null || !RowsContentEquals(rows, rebuilt))
                rows = rebuilt;
            includedCount = 0;
            anyUndo = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].included) includedCount++;
                if (rows[i].item.UndoesUserChange) anyUndo = true;
            }
        }

        private static bool RowsContentEquals(List<Row> left, List<Row> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!left[i].Key.Equals(right[i].Key)
                    || left[i].included != right[i].included
                    || left[i].item.label != right[i].item.label
                    || left[i].item.explanation != right[i].item.explanation)
                    return false;
            return true;
        }

        private string? titleText;

        // Owner: this Restore Defaults preview dialog instance.
        // Key: dialog identity, exact available width, and language revision.
        // Value: immutable detached warning text and its measured height.
        // Dependencies: WR_RestoreOverwriteWarning translation,
        // LanguageChangeCoordinator.Revision, GameFont.Small metrics,
        // enabled word wrapping, and the exact available width.
        // Refresh: immediately when the language revision or width changes.
        // Equality: an equal rebuild preserves snapshot identity.
        // Teardown: closing the dialog releases the instance-owned snapshot.
        private RestoreWarningSnapshot? warningSnapshot;
        private float warningWidth = -1f;
        private int warningLanguageRevision = -1;

        private RestoreWarningSnapshot WarningSnapshot(float width)
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (warningSnapshot != null
                && warningWidth == width
                && warningLanguageRevision == languageRevision)
                return warningSnapshot;

            string warningText = "WR_RestoreOverwriteWarning".Translate();
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            float height;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                height = Text.CalcHeight(warningText, width);
            }
            finally
            {
                Text.Font = oldFont;
                Text.WordWrap = oldWordWrap;
            }

            var rebuilt = new RestoreWarningSnapshot(warningText, height);
            if (warningSnapshot == null || !warningSnapshot.ContentEquals(rebuilt))
                warningSnapshot = rebuilt;
            warningWidth = width;
            warningLanguageRevision = languageRevision;
            return warningSnapshot;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = GuiStateScope.Capture();
            EnsureRowsCurrent();
            if (ObservePreviewLanguageRevision())
                titleText = "WR_RestoreDefaultsTitle".Translate();
            float listTop = DrawCachedPreviewTitle(inRect, titleText);
            bool all = includedCount == rows.Count;
            bool toggled = DrawCachedPreviewSelectAll(inRect, listTop, all);
            if (toggled != all)
            {
                for (int i = 0; i < rows.Count; i++)
                    rows[i].included = toggled;
                includedCount = toggled ? rows.Count : 0;
            }
            listTop += PreviewSelectRowHeight;

            if (anyUndo)
            {
                RestoreWarningSnapshot warning = WarningSnapshot(inRect.width);
                if (Event.current.type == EventType.Repaint)
                {
                    Color oldColor = GUI.color;
                    try
                    {
                        GUI.color = WarnColor;
                        Widgets.Label(new Rect(inRect.x, listTop, inRect.width,
                            warning.Height), warning.Text);
                    }
                    finally
                    {
                        GUI.color = oldColor;
                    }
                }
                listTop += warning.Height + 4f;
            }

            var listRect = PreviewBodyRect(inRect, listTop);
            float rowW = listRect.width - 16f;
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, rows.Count * RowH));
            try
            {
                var visibleRows = UniformViewportRange.Calculate(
                    itemCount: rows.Count,
                    itemExtent: RowH,
                    contentStart: 0f,
                    viewportStart: scroll.y,
                    viewportExtent: listRect.height);
                DrawVisibleRows(visibleRows, rowW);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            bool canApply = includedCount > 0;
            if (DrawPreviewFooter(inRect, canApply))
            {
                var selected = rows.Where(r => r.included).Select(r => r.item).ToList();
                RoleCommands.RestoreSelected(new RestoreSelection
                {
                    templateDefs = selected.Select(i => i.templateDef).OfType<string>().ToList(),
                    workTypes = selected.Select(i => i.workType).OfType<string>().ToList(),
                    backfillRoleIds = selected.Where(i => i.backfillRoleId != -1).Select(i => i.backfillRoleId).ToList(),
                    pathDefs = selected.Select(i => i.pathDef).OfType<string>().ToList(),
                    groupRoleIds = selected.Where(i => i.groupRoleId != -1).Select(i => i.groupRoleId).ToList(),
                    colorRoleIds = selected.Where(i => i.colorRoleId != -1).Select(i => i.colorRoleId).ToList(),
                    holderRoleIds = selected.Where(i => i.holderRoleId != -1).Select(i => i.holderRoleId).ToList(),
                    entriesRoleIds = selected.Where(i => i.entriesRoleId != -1).Select(i => i.entriesRoleId).ToList(),
                    paletteSnapRoleIds = selected.Where(i => i.paletteSnapRoleId != -1).Select(i => i.paletteSnapRoleId).ToList(),
                    recommendationOrder = selected.Any(i => i.recommendationOrder),
                });
                Close();
            }
        }

        public override void PostClose()
        {
            rows?.Clear();
            rows = null!; // released at teardown; the dialog never draws after close
            rowsOwner = null;
            rowsUiRevision = -1;
            rowsDefinitionRevision = -1;
            rowsLanguageRevision = -1;
            titleText = null;
            warningSnapshot = null;
            warningWidth = -1f;
            warningLanguageRevision = -1;
            base.PostClose();
        }

        private void DrawVisibleRows(UniformViewportRange visibleRows, float rowW)
        {
            Color oldColor = GUI.color;
            try
            {
                for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)
                {
                    Row row = rows[i];
                    var rowRect = new Rect(0f, i * RowH, rowW, RowH - 2f);
                    GUI.color = row.item.UndoesUserChange ? WarnColor : oldColor;
                    bool before = row.included;
                    Widgets.CheckboxLabeled(rowRect, row.item.label, ref row.included);
                    if (before != row.included)
                        includedCount += row.included ? 1 : -1;
                    GUI.color = oldColor;
                    if (Event.current.type == EventType.Repaint
                        && !row.item.explanation.NullOrEmpty())
                        TooltipHandler.TipRegion(rowRect, row.item.explanation);
                }
            }
            finally
            {
                GUI.color = oldColor;
            }
        }
    }
}
