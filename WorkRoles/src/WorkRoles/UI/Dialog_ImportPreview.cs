using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Import preview with selective Palette, Roles, Paths and Order sections;
    /// groups, paths and order remain dependent on importing roles.
    /// The body is flattened into a copy-owned variable-height row snapshot so
    /// idle IMGUI events only visit rows intersecting the scroll viewport.
    public class Dialog_ImportPreview : Dialog_PreviewBase
    {
        private class Row
        {
            public string label;
            public bool included = true;
        }

        private enum Section
        {
            Palette,
            Roles,
            Paths,
        }

        private enum RenderKind
        {
            Gap,
            SectionHeader,
            MergeRow,
            Info,
            OrderHeader,
            BottomPadding,
        }

        private readonly struct RenderRow
        {
            public RenderRow(
                RenderKind kind,
                Section section = Section.Palette,
                int sourceIndex = 0,
                string text = null,
                float textHeight = 0f)
            {
                Kind = kind;
                Section = section;
                SourceIndex = sourceIndex;
                Text = text;
                TextHeight = textHeight;
            }

            public RenderKind Kind { get; }
            public Section Section { get; }
            public int SourceIndex { get; }
            public string Text { get; }
            public float TextHeight { get; }
        }

        private const float RowH = 26f;
        private const float SectionGap = 10f;
        private const float RadioW = 24f;

        private readonly string xml;
        private readonly RoleFileDocument doc;
        private readonly List<RoleIO.PaletteRow> paletteRows;
        private readonly List<RoleIO.RoleRow> roleRows;
        private readonly List<Row> paletteMergeUi;
        private readonly List<Row> roleMergeUi;
        private readonly List<Row> pathMergeUi;
        private readonly List<string> paletteOverwriteInfo = new List<string>();
        private readonly List<string> rolesOverwriteInfo = new List<string>();
        private readonly List<string> pathsOverwriteInfo = new List<string>();

        private bool paletteInclude = true;
        private bool paletteOverwrite;
        private bool rolesInclude = true;
        private bool rolesOverwrite;
        private bool pathsInclude;
        private bool pathsOverwrite;
        private bool orderInclude;
        private int paletteIncludedCount;
        private int roleIncludedCount;
        private int pathIncludedCount;

        private RenderRow[] renderRows = Array.Empty<RenderRow>();
        private VariableViewportLayout rowLayout;
        private float rowLayoutWidth = -1f;
        private int rowLayoutState = -1;
        private int rowLayoutStamp = -1;
        private bool rowLayoutDirty = true;
        private int uiLanguageRevision = -1;

        private string titleText;
        private string paletteTitle;
        private string rolesTitle;
        private string pathsTitle;
        private string orderTitle;
        private string mergeLabel;
        private string overwriteLabel;
        private string nothingToMergeText;
        private string orderInfoText;
        private float mergeControlWidth;
        private float overwriteControlWidth;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 640f);

        public Dialog_ImportPreview(string xml, RoleFileDocument doc)
        {
            this.xml = xml;
            this.doc = doc;
            var store = RoleStore.Current;
            paletteRows = RoleIO.PaletteMergeRows(store, doc);
            roleRows = RoleIO.RoleRows(store, doc);
            paletteInclude = paletteRows.Count > 0 || doc.palette.Count > 0;
            rolesInclude = roleRows.Count > 0;
            pathsInclude = rolesInclude && doc.trainingPaths.Count > 0;
            orderInclude = rolesInclude && doc.recommendationOrder.Count > 0;

            paletteMergeUi = NewRows(paletteRows.Count);
            roleMergeUi = NewRows(roleRows.Count);
            pathMergeUi = NewRows(doc.trainingPaths.Count);
            paletteIncludedCount = paletteMergeUi.Count;
            roleIncludedCount = roleMergeUi.Count;
            pathIncludedCount = pathMergeUi.Count;
        }

        private static List<Row> NewRows(int count)
        {
            var result = new List<Row>(count);
            for (int i = 0; i < count; i++) result.Add(new Row());
            return result;
        }

        private static string ColorHexOf(Color color) =>
            "#" + ColorUtility.ToHtmlStringRGB(color).ToLowerInvariant();

        public override void DoWindowContents(Rect inRect)
        {
            ObservePreviewLanguageRevision();
            EnsureUiText();
            EnforceRoleDependencies();
            float listTop = DrawCachedPreviewTitle(inRect, titleText);
            var listRect = PreviewBodyRect(inRect, listTop);
            float rowW = listRect.width - 16f;
            EnsureRenderRows(rowW);

            Widgets.BeginScrollView(listRect, ref scroll,
                new Rect(0f, 0f, rowW, rowLayout.ContentExtent));
            VariableViewportRange visibleRows = rowLayout.Calculate(scroll.y, listRect.height);
            DrawVisibleRows(visibleRows, rowW);
            Widgets.EndScrollView();

            bool canApply = (paletteInclude && (paletteOverwrite || paletteIncludedCount > 0))
                || (rolesInclude && (rolesOverwrite || roleIncludedCount > 0))
                || (pathsInclude && (pathsOverwrite || pathIncludedCount > 0))
                || (orderInclude && doc.recommendationOrder.Count > 0);
            if (DrawPreviewFooter(inRect, canApply))
            {
                RoleCommands.ApplyImport(new ImportSelection
                {
                    xml = xml,
                    palette = paletteInclude,
                    paletteOverwrite = paletteOverwrite,
                    roles = rolesInclude,
                    rolesOverwrite = rolesOverwrite,
                    paths = pathsInclude,
                    pathsOverwrite = pathsOverwrite,
                    order = orderInclude,
                    paletteRows = SelectedIndices(paletteMergeUi),
                    roleRows = SelectedIndices(roleMergeUi),
                    pathRows = SelectedIndices(pathMergeUi),
                });
                Close();
            }
        }

        private void EnsureUiText()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (uiLanguageRevision == languageRevision) return;
            uiLanguageRevision = languageRevision;

            using (new TextBlock(GameFont.Small))
            {
                titleText = "WR_ImportTitle".Translate();
                paletteTitle = "WR_SectionPalette".Translate();
                rolesTitle = "WR_SectionRoles".Translate();
                pathsTitle = "WR_TrainingSection".Translate();
                orderTitle = "WR_OptRecOrder".Translate();
                mergeLabel = "WR_ModeMerge".Translate();
                overwriteLabel = "WR_ModeOverwrite".Translate();
                nothingToMergeText = "WR_NothingToMerge".Translate();
                orderInfoText = "WR_RecOrderReplaceInfo"
                    .Translate(doc.recommendationOrder.Count);
                mergeControlWidth = WrText.FitWidth(mergeLabel) + 8f + RadioW;
                overwriteControlWidth = WrText.FitWidth(overwriteLabel) + 8f + RadioW;

                for (int i = 0; i < paletteRows.Count; i++)
                {
                    RoleIO.PaletteRow row = paletteRows[i];
                    paletteMergeUi[i].label = row.isNew
                        ? "WR_RowNewColor".Translate(row.name, ColorHexOf(row.color)).ToString()
                        : "WR_RowRecolors".Translate(row.name, ColorHexOf(row.color),
                            row.recolors.Count == 0 ? "-" : row.recolors.ToCommaList()).ToString();
                }
                for (int i = 0; i < roleRows.Count; i++)
                {
                    RoleIO.RoleRow row = roleRows[i];
                    roleMergeUi[i].label = (row.existing == null
                        ? "WR_RowNew".Translate(row.displayLabel)
                        : "WR_RowUpdate".Translate(row.displayLabel)).ToString();
                }
                for (int i = 0; i < doc.trainingPaths.Count; i++)
                    pathMergeUi[i].label = "WR_RowNew"
                        .Translate(doc.trainingPaths[i].name).ToString();

                paletteOverwriteInfo.Clear();
                paletteOverwriteInfo.Add("WR_PaletteOverwriteInfo"
                    .Translate(doc.palette.Count).ToString());

                rolesOverwriteInfo.Clear();
                rolesOverwriteInfo.Add("WR_OverwriteReplaces"
                    .Translate(roleRows.Count).ToString());
                List<Role> deletes = RoleIO.OverwriteDeletes(RoleStore.Current, doc);
                if (deletes.Count > 0)
                {
                    var labels = new List<string>(deletes.Count);
                    for (int i = 0; i < deletes.Count; i++) labels.Add(deletes[i].label);
                    rolesOverwriteInfo.Add("WR_OverwriteDeletes"
                        .Translate(labels.ToCommaList()).ToString());
                }

                pathsOverwriteInfo.Clear();
                pathsOverwriteInfo.Add("WR_PathsOverwriteInfo"
                    .Translate(doc.trainingPaths.Count).ToString());
            }
            rowLayoutDirty = true;
        }

        private void EnsureRenderRows(float width)
        {
            EnsureUiText();
            int state = LayoutState();
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (!rowLayoutDirty && rowLayout != null
                && rowLayoutWidth == width
                && rowLayoutState == state && rowLayoutStamp == languageRevision)
                return;

            var nextRows = new List<RenderRow>();
            var heights = new List<float>();
            AddSection(nextRows, heights, width, Section.Palette,
                paletteInclude, paletteOverwrite, paletteMergeUi, paletteOverwriteInfo);
            AddSimpleRow(nextRows, heights, RenderKind.Gap, SectionGap);
            AddSection(nextRows, heights, width, Section.Roles,
                rolesInclude, rolesOverwrite, roleMergeUi, rolesOverwriteInfo);
            if (pathMergeUi.Count > 0)
            {
                AddSimpleRow(nextRows, heights, RenderKind.Gap, SectionGap);
                AddSection(nextRows, heights, width, Section.Paths,
                    pathsInclude, pathsOverwrite, pathMergeUi, pathsOverwriteInfo);
            }
            if (doc.recommendationOrder.Count > 0)
            {
                AddSimpleRow(nextRows, heights, RenderKind.Gap, SectionGap);
                AddSimpleRow(nextRows, heights, RenderKind.OrderHeader, RowH);
                if (orderInclude) AddInfoRow(nextRows, heights, width, orderInfoText);
            }
            // Preserve MeasureContent's trailing breathing room exactly.
            AddSimpleRow(nextRows, heights, RenderKind.BottomPadding, RowH);

            renderRows = nextRows.ToArray();
            rowLayout = new VariableViewportLayout(heights);
            rowLayoutWidth = width;
            rowLayoutState = state;
            rowLayoutStamp = languageRevision;
            rowLayoutDirty = false;
        }

        private void AddSection(
            List<RenderRow> target,
            List<float> heights,
            float width,
            Section section,
            bool include,
            bool overwrite,
            List<Row> mergeRows,
            List<string> overwriteInfo)
        {
            AddSimpleRow(target, heights, RenderKind.SectionHeader, RowH, section);
            if (!include) return;
            if (!overwrite)
            {
                if (mergeRows.Count == 0)
                {
                    AddInfoRow(target, heights, width, nothingToMergeText);
                    return;
                }
                for (int i = 0; i < mergeRows.Count; i++)
                {
                    target.Add(new RenderRow(RenderKind.MergeRow, section, i));
                    heights.Add(RowH);
                }
                return;
            }
            for (int i = 0; i < overwriteInfo.Count; i++)
                AddInfoRow(target, heights, width, overwriteInfo[i]);
        }

        private static void AddSimpleRow(
            List<RenderRow> target,
            List<float> heights,
            RenderKind kind,
            float height,
            Section section = Section.Palette)
        {
            target.Add(new RenderRow(kind, section));
            heights.Add(height);
        }

        private static void AddInfoRow(
            List<RenderRow> target,
            List<float> heights,
            float width,
            string text)
        {
            float textHeight = Mathf.Max(RowH - 2f, Text.CalcHeight(text, width - 16f));
            target.Add(new RenderRow(RenderKind.Info,
                text: text, textHeight: textHeight));
            heights.Add(textHeight + 2f);
        }

        private void DrawVisibleRows(VariableViewportRange visibleRows, float width)
        {
            for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)
            {
                RenderRow row = renderRows[i];
                float y = rowLayout.OffsetOf(i);
                switch (row.Kind)
                {
                    case RenderKind.SectionHeader:
                        DrawSectionHeader(row.Section, width, y);
                        break;
                    case RenderKind.MergeRow:
                        DrawMergeRow(row.Section, row.SourceIndex, width, y);
                        break;
                    case RenderKind.Info:
                        if (Event.current.type == EventType.Repaint)
                        {
                            GUI.color = new Color(0.7f, 0.7f, 0.7f);
                            Widgets.Label(new Rect(16f, y, width - 16f, row.TextHeight), row.Text);
                            GUI.color = Color.white;
                        }
                        break;
                    case RenderKind.OrderHeader:
                        bool previousEnabled = GUI.enabled;
                        GUI.enabled = previousEnabled && rolesInclude;
                        bool before = orderInclude;
                        Widgets.CheckboxLabeled(new Rect(0f, y, width * 0.4f, RowH - 2f),
                            orderTitle, ref orderInclude);
                        GUI.enabled = previousEnabled;
                        if (before != orderInclude) rowLayoutDirty = true;
                        break;
                }
            }
        }

        private void DrawSectionHeader(Section section, float width, float y)
        {
            switch (section)
            {
                case Section.Palette:
                    DrawSectionHeader(width, y, paletteTitle,
                        ref paletteInclude, ref paletteOverwrite);
                    break;
                case Section.Roles:
                    DrawSectionHeader(width, y, rolesTitle,
                        ref rolesInclude, ref rolesOverwrite);
                    EnforceRoleDependencies();
                    break;
                case Section.Paths:
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = previousEnabled && rolesInclude;
                    DrawSectionHeader(width, y, pathsTitle,
                        ref pathsInclude, ref pathsOverwrite);
                    GUI.enabled = previousEnabled;
                    break;
            }
        }

        private void EnforceRoleDependencies()
        {
            if (rolesInclude || (!pathsInclude && !orderInclude)) return;
            pathsInclude = false;
            orderInclude = false;
            rowLayoutDirty = true;
        }

        private void DrawSectionHeader(
            float width,
            float y,
            string title,
            ref bool include,
            ref bool overwrite)
        {
            bool includeBefore = include;
            bool overwriteBefore = overwrite;
            Widgets.CheckboxLabeled(new Rect(0f, y, width * 0.4f, RowH - 2f),
                title, ref include);
            if (include)
            {
                bool merge = !overwrite;
                var mergeRect = new Rect(width * 0.45f, y,
                    mergeControlWidth, RowH - 2f);
                var overwriteRect = new Rect(mergeRect.xMax + 24f, y,
                    overwriteControlWidth, RowH - 2f);
                if (Widgets.RadioButtonLabeled(mergeRect, mergeLabel, merge))
                    overwrite = false;
                if (Widgets.RadioButtonLabeled(overwriteRect, overwriteLabel, overwrite))
                    overwrite = true;
            }
            if (includeBefore != include || overwriteBefore != overwrite)
                rowLayoutDirty = true;
        }

        private void DrawMergeRow(Section section, int index, float width, float y)
        {
            List<Row> rows = MergeRows(section);
            Row row = rows[index];
            bool before = row.included;
            Widgets.CheckboxLabeled(new Rect(16f, y, width - 16f, RowH - 2f),
                row.label, ref row.included);
            if (before == row.included) return;
            switch (section)
            {
                case Section.Palette:
                    paletteIncludedCount += row.included ? 1 : -1;
                    break;
                case Section.Roles:
                    roleIncludedCount += row.included ? 1 : -1;
                    break;
                case Section.Paths:
                    pathIncludedCount += row.included ? 1 : -1;
                    break;
            }
        }

        private List<Row> MergeRows(Section section)
        {
            switch (section)
            {
                case Section.Palette: return paletteMergeUi;
                case Section.Roles: return roleMergeUi;
                default: return pathMergeUi;
            }
        }

        private int LayoutState()
        {
            int state = 0;
            if (paletteInclude) state |= 1;
            if (paletteOverwrite) state |= 2;
            if (rolesInclude) state |= 4;
            if (rolesOverwrite) state |= 8;
            if (pathsInclude) state |= 16;
            if (pathsOverwrite) state |= 32;
            if (orderInclude) state |= 64;
            return state;
        }

        private static List<int> SelectedIndices(List<Row> rows)
        {
            var result = new List<int>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].included)
                    result.Add(i);
            return result;
        }
    }
}
