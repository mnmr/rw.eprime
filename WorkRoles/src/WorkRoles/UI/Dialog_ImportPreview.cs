using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Import preview with two independent sections — Palette and Roles — each
    /// with an include toggle and its own Merge/Overwrite mode. Merge lists
    /// selectable rows (with the existing roles each change affects); Overwrite
    /// shows an informational consequence list, since partial overwrite is merge.
    public class Dialog_ImportPreview : Window
    {
        private class Row
        {
            public string label;
            public bool included = true;
        }

        private const float TitleH = 38f;
        private const float RowH = 26f;
        private const float SectionGap = 10f;
        private const float ButtonW = 120f;
        private const float ButtonH = 32f;

        private readonly string xml;
        private readonly WorkRoles.Core.RoleFileDocument doc;
        private readonly List<RoleIO.PaletteRow> paletteRows;
        private readonly List<RoleIO.RoleRow> roleRows;

        private bool paletteInclude = true;
        private bool paletteOverwrite;
        private bool rolesInclude = true;
        private bool rolesOverwrite;
        private readonly List<Row> paletteMergeUi;
        private readonly List<Row> roleMergeUi;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 640f);

        public Dialog_ImportPreview(string xml, WorkRoles.Core.RoleFileDocument doc)
        {
            this.xml = xml;
            this.doc = doc;
            var store = RoleStore.Current;
            paletteRows = RoleIO.PaletteMergeRows(store, doc);
            roleRows = RoleIO.RoleRows(store, doc);
            paletteInclude = paletteRows.Count > 0 || doc.palette.Count > 0;
            rolesInclude = roleRows.Count > 0;
            paletteMergeUi = paletteRows.Select(r => new Row
            {
                label = r.isNew
                    ? "WR_RowNewColor".Translate(r.name, ColorHexOf(r.color)).ToString()
                    : "WR_RowRecolors".Translate(r.name, ColorHexOf(r.color),
                        r.recolors.Count == 0 ? "-" : r.recolors.ToCommaList()).ToString(),
            }).ToList();
            roleMergeUi = roleRows.Select(r => new Row
            {
                label = (r.existing == null
                    ? "WR_RowNew".Translate(r.role.label)
                    : "WR_RowUpdate".Translate(r.role.label)).ToString(),
            }).ToList();
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        private static string ColorHexOf(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant();

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_ImportTitle".Translate());
            Text.Font = GameFont.Small;

            var listRect = new Rect(inRect.x, inRect.y + TitleH, inRect.width,
                inRect.yMax - inRect.y - TitleH - ButtonH - 8f);
            float rowW = listRect.width - 16f;
            float contentH = MeasureContent(rowW);
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, contentH));
            float y = 0f;
            DrawSection(rowW, ref y, "WR_SectionPalette".Translate(),
                ref paletteInclude, ref paletteOverwrite, paletteMergeUi, PaletteOverwriteInfo());
            y += SectionGap;
            DrawSection(rowW, ref y, "WR_SectionRoles".Translate(),
                ref rolesInclude, ref rolesOverwrite, roleMergeUi, RolesOverwriteInfo());
            Widgets.EndScrollView();

            float btnY = inRect.yMax - ButtonH;
            var applyRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            var cancelRect = new Rect(applyRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            bool canApply = (paletteInclude && (paletteOverwrite || paletteMergeUi.Any(r => r.included)))
                || (rolesInclude && (rolesOverwrite || roleMergeUi.Any(r => r.included)));
            if (Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply) && canApply)
            {
                RoleCommands.ApplyImport(xml,
                    paletteInclude, paletteOverwrite, SelectedIndices(paletteMergeUi),
                    rolesInclude, rolesOverwrite, SelectedIndices(roleMergeUi));
                Close();
            }
        }

        private static List<int> SelectedIndices(List<Row> rows) =>
            rows.Select((row, index) => (row, index)).Where(t => t.row.included).Select(t => t.index).ToList();

        private List<string> PaletteOverwriteInfo()
        {
            var lines = new List<string> { "WR_PaletteOverwriteInfo".Translate(doc.palette.Count).ToString() };
            return lines;
        }

        private List<string> RolesOverwriteInfo()
        {
            var lines = new List<string> { "WR_OverwriteReplaces".Translate(roleRows.Count).ToString() };
            var deletes = RoleIO.OverwriteDeletes(RoleStore.Current, doc);
            if (deletes.Count > 0)
                lines.Add("WR_OverwriteDeletes".Translate(deletes.Select(r => r.label).ToCommaList()).ToString());
            return lines;
        }

        /// Draws one section; when measure-only callers need the height they call
        /// MeasureContent instead (same math).
        private void DrawSection(float width, ref float y, string title,
            ref bool include, ref bool overwrite, List<Row> mergeRows, List<string> overwriteInfo)
        {
            Widgets.CheckboxLabeled(new Rect(0f, y, width * 0.4f, RowH - 2f), title, ref include);
            if (include)
            {
                bool merge = !overwrite;
                // Tight rects: RadioButtonLabeled pins the circle to the rect's
                // right edge, so a wide rect strands the label far from it.
                const float RadioW = 24f;
                string mergeLabel = "WR_ModeMerge".Translate();
                string overwriteLabel = "WR_ModeOverwrite".Translate();
                var mergeRect = new Rect(width * 0.45f, y,
                    WrText.FitWidth(mergeLabel) + 8f + RadioW, RowH - 2f);
                var overwriteRect = new Rect(mergeRect.xMax + 24f, y,
                    WrText.FitWidth(overwriteLabel) + 8f + RadioW, RowH - 2f);
                if (Widgets.RadioButtonLabeled(mergeRect, mergeLabel, merge)) overwrite = false;
                if (Widgets.RadioButtonLabeled(overwriteRect, overwriteLabel, overwrite)) overwrite = true;
            }
            y += RowH;
            if (!include) return;
            if (!overwrite)
            {
                if (mergeRows.Count == 0)
                {
                    DrawInfo(width, ref y, "WR_NothingToMerge".Translate());
                    return;
                }
                foreach (var row in mergeRows)
                {
                    Widgets.CheckboxLabeled(new Rect(16f, y, width - 16f, RowH - 2f), row.label, ref row.included);
                    y += RowH;
                }
            }
            else
            {
                foreach (var line in overwriteInfo)
                    DrawInfo(width, ref y, line);
            }
        }

        /// Info text sizes to its wrapped height — deletion lists and multi-
        /// sentence explanations span lines instead of clipping to one row.
        private static void DrawInfo(float width, ref float y, string text)
        {
            float height = InfoHeight(width, text);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(16f, y, width - 16f, height), text);
            GUI.color = Color.white;
            y += height + 2f;
        }

        private static float InfoHeight(float width, string text)
            => Mathf.Max(RowH - 2f, Text.CalcHeight(text, width - 16f));

        private float MeasureContent(float width)
        {
            float y = 0f;
            y += RowH; // palette header
            if (paletteInclude)
                y += paletteOverwrite
                    ? PaletteOverwriteInfo().Sum(line => InfoHeight(width, line) + 2f)
                    : paletteMergeUi.Count == 0
                        ? InfoHeight(width, "WR_NothingToMerge".Translate()) + 2f
                        : RowH * paletteMergeUi.Count;
            y += SectionGap + RowH; // roles header
            if (rolesInclude)
                y += rolesOverwrite
                    ? RolesOverwriteInfo().Sum(line => InfoHeight(width, line) + 2f)
                    : roleMergeUi.Count == 0
                        ? InfoHeight(width, "WR_NothingToMerge".Translate()) + 2f
                        : RowH * roleMergeUi.Count;
            return y + RowH;
        }
    }
}
