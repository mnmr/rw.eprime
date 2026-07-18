using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Selectable preview for Restore Defaults: one checkbox row per restorable
    /// item (missing role or training path, uncovered work type, moved-jobs
    /// recovery, group or color drift, or the recommendation-order reset), with a select-all toggle —
    /// mirroring the Fix My Colony preview's selection model. Application
    /// self-guards against staleness per item.
    public class Dialog_RestorePreview : Window
    {
        private class Row
        {
            public Seeding.RestoreItem item;
            public bool included = true;
        }

        private const float TitleH = 38f;
        private const float SelectRowH = 26f;
        private const float RowH = 26f;
        private const float ButtonW = 120f;
        private const float ButtonH = 32f;

        // Dim orange for items that would undo a player change (OptionsTabView's
        // LockedColor family, darkened for body text).
        private static readonly Color WarnColor = new Color(0.9f, 0.6f, 0.25f);

        private readonly List<Row> rows;
        private readonly bool anyUndo;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public Dialog_RestorePreview(List<Seeding.RestoreItem> items)
        {
            rows = items.Select(i => new Row { item = i }).ToList();
            anyUndo = rows.Any(r => r.item.UndoesUserChange);
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_RestoreDefaultsTitle".Translate());
            Text.Font = GameFont.Small;

            float listTop = inRect.y + TitleH;
            bool all = rows.All(r => r.included);
            bool toggled = all;
            Widgets.CheckboxLabeled(new Rect(inRect.x, listTop, 160f, 24f), "WR_SelectAll".Translate(), ref toggled);
            if (toggled != all)
                foreach (var row in rows) row.included = toggled;
            listTop += SelectRowH;

            if (anyUndo)
            {
                string warning = "WR_RestoreOverwriteWarning".Translate();
                float warnH = Text.CalcHeight(warning, inRect.width);
                GUI.color = WarnColor;
                Widgets.Label(new Rect(inRect.x, listTop, inRect.width, warnH), warning);
                GUI.color = Color.white;
                listTop += warnH + 4f;
            }

            var listRect = new Rect(inRect.x, listTop, inRect.width, inRect.yMax - listTop - ButtonH - 8f);
            float rowW = listRect.width - 16f;
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, rows.Count * RowH));
            float y = 0f;
            foreach (var row in rows)
            {
                var rowRect = new Rect(0f, y, rowW, RowH - 2f);
                if (row.item.UndoesUserChange) GUI.color = WarnColor;
                Widgets.CheckboxLabeled(rowRect, row.item.label, ref row.included);
                GUI.color = Color.white;
                if (!row.item.explanation.NullOrEmpty())
                    TooltipHandler.TipRegion(rowRect, row.item.explanation);
                y += RowH;
            }
            Widgets.EndScrollView();

            float btnY = inRect.yMax - ButtonH;
            var applyRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            var cancelRect = new Rect(applyRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            bool canApply = rows.Any(r => r.included);
            if (Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply) && canApply)
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
    }
}
