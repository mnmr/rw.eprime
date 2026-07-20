using System.Collections.Generic;
using System.Linq;
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
        private class Row
        {
            public Seeding.RestoreItem item;
            public bool included = true;
        }

        private const float RowH = 26f;

        // Dim orange for items that would undo a player change (OptionsTabView's
        // LockedColor family, darkened for body text).
        private static readonly Color WarnColor = new Color(0.9f, 0.6f, 0.25f);

        private readonly List<Row> rows;
        private readonly bool anyUndo;
        private int includedCount;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public Dialog_RestorePreview(List<Seeding.RestoreItem> items)
        {
            rows = items.Select(i => new Row { item = i }).ToList();
            anyUndo = rows.Any(r => r.item.UndoesUserChange);
            includedCount = rows.Count;
        }

        private string titleText;
        private string warnText;
        private float warnH;
        private float warnMeasuredW = -1f;

        public override void DoWindowContents(Rect inRect)
        {
            if (ObservePreviewLanguageRevision())
            {
                titleText = "WR_RestoreDefaultsTitle".Translate();
                warnText = "WR_RestoreOverwriteWarning".Translate();
                warnMeasuredW = -1f;
            }
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
                // Height cached by width: CalcHeight is a full layout pass.
                if (warnMeasuredW != inRect.width)
                {
                    warnH = Text.CalcHeight(warnText, inRect.width);
                    warnMeasuredW = inRect.width;
                }
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = WarnColor;
                    Widgets.Label(new Rect(inRect.x, listTop, inRect.width, warnH), warnText);
                    GUI.color = Color.white;
                }
                listTop += warnH + 4f;
            }

            var listRect = PreviewBodyRect(inRect, listTop);
            float rowW = listRect.width - 16f;
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, rows.Count * RowH));
            var visibleRows = UniformViewportRange.Calculate(
                itemCount: rows.Count,
                itemExtent: RowH,
                contentStart: 0f,
                viewportStart: scroll.y,
                viewportExtent: listRect.height);
            DrawVisibleRows(visibleRows, rowW);
            Widgets.EndScrollView();

            bool canApply = includedCount > 0;
            if (DrawPreviewFooter(inRect, canApply))
            {
                var selected = rows.Where(r => r.included).Select(r => r.item).ToList();
                RoleCommands.RestoreSelected(new RestoreSelection
                {
                    templateDefs = selected.Where(i => i.templateDef != null).Select(i => i.templateDef).ToList(),
                    workTypes = selected.Where(i => i.workType != null).Select(i => i.workType).ToList(),
                    backfillRoleIds = selected.Where(i => i.backfillRoleId != -1).Select(i => i.backfillRoleId).ToList(),
                    pathDefs = selected.Where(i => i.pathDef != null).Select(i => i.pathDef).ToList(),
                    groupRoleIds = selected.Where(i => i.groupRoleId != -1).Select(i => i.groupRoleId).ToList(),
                    colorRoleIds = selected.Where(i => i.colorRoleId != -1).Select(i => i.colorRoleId).ToList(),
                    holderRoleIds = selected.Where(i => i.holderRoleId != -1).Select(i => i.holderRoleId).ToList(),
                    recommendationOrder = selected.Any(i => i.recommendationOrder),
                });
                Close();
            }
        }

        private void DrawVisibleRows(UniformViewportRange visibleRows, float rowW)
        {
            for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)
            {
                Row row = rows[i];
                var rowRect = new Rect(0f, i * RowH, rowW, RowH - 2f);
                if (row.item.UndoesUserChange) GUI.color = WarnColor;
                bool before = row.included;
                Widgets.CheckboxLabeled(rowRect, row.item.label, ref row.included);
                if (before != row.included)
                    includedCount += row.included ? 1 : -1;
                GUI.color = Color.white;
                if (Event.current.type == EventType.Repaint
                    && !row.item.explanation.NullOrEmpty())
                    TooltipHandler.TipRegion(rowRect, row.item.explanation);
            }
        }
    }
}
