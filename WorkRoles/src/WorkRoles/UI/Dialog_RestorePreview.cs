using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Selectable preview for Restore Roles: one checkbox row per restorable item
    /// (missing role, uncovered work type, or moved-jobs recovery), with a
    /// select-all toggle — mirroring the Fix My Colony preview's selection model.
    /// Application self-guards against staleness per item.
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

        private readonly List<Row> rows;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public Dialog_RestorePreview(List<Seeding.RestoreItem> items)
        {
            rows = items.Select(i => new Row { item = i }).ToList();
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_RestoreRolesTitle".Translate());
            Text.Font = GameFont.Small;

            float listTop = inRect.y + TitleH;
            bool all = rows.All(r => r.included);
            bool toggled = all;
            Widgets.CheckboxLabeled(new Rect(inRect.x, listTop, 160f, 24f), "WR_SelectAll".Translate(), ref toggled);
            if (toggled != all)
                foreach (var row in rows) row.included = toggled;
            listTop += SelectRowH;

            var listRect = new Rect(inRect.x, listTop, inRect.width, inRect.yMax - listTop - ButtonH - 8f);
            float rowW = listRect.width - 16f;
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, rows.Count * RowH));
            float y = 0f;
            foreach (var row in rows)
            {
                Widgets.CheckboxLabeled(new Rect(0f, y, rowW, RowH - 2f), row.item.label, ref row.included);
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
                RoleCommands.RestoreSelected(
                    selected.Where(i => i.templateDef != null).Select(i => i.templateDef).ToList(),
                    selected.Where(i => i.workType != null).Select(i => i.workType).ToList(),
                    selected.Where(i => i.backfillRoleId != -1).Select(i => i.backfillRoleId).ToList(),
                    selected.Any(i => i.oddJobs));
                Close();
            }
        }
    }
}
