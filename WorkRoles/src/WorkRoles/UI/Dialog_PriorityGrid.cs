using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Read-only vanilla-style priority grid for a set of pawns: vanilla work
    /// boxes (skill-shaded backgrounds, passion overlays, check/number) under
    /// 45-degree column labels. Managed pawns can show raw ranks or the vanilla
    /// 0-4 projection — a view-only toggle seeded from the Options setting;
    /// unmanaged pawns always show their real vanilla priorities.
    public class Dialog_PriorityGrid : Window
    {
        private readonly List<Pawn> pawns;
        private readonly List<WorkTypeDef> workTypes = new List<WorkTypeDef>();
        private readonly RevisionPairGate columnCacheRevisions = new RevisionPairGate();
        private float headerH;
        // Pawn names are fixed for the dialog lifetime. Definition and translated
        // column geometry is replaceable at its authoritative revision boundary.
        private readonly string[] pawnNames;
        private string[] columnLabels;
        private Vector2[] columnLabelSizes;
        private Vector2 phantomLabelSize;
        private string[] columnTips;
        private string titleLabel;
        private string rawModeLabel;
        private string vanillaModeLabel;
        private float modeToggleW;
        private Vector2 scroll;
        /// Local view state only — never written back to the synced setting.
        private bool showVanilla;

        private const float TitleH = 38f;
        private const float NameW = 170f;
        private const float ColW = 26f;   // vanilla work box (25) + gap
        private const float RowH = 27f;
        private const float LabelAngle = 45f;
        private const float HeaderRunOutPadding = 20f;

        public Dialog_PriorityGrid(List<Pawn> pawns)
        {
            this.pawns = pawns;
            showVanilla = RoleStore.Current?.reportVanillaPriorities == true;
            using (new TextBlock(GameFont.Small))
            {
                pawnNames = new string[pawns.Count];
                for (int r = 0; r < pawns.Count; r++)
                    pawnNames[r] = pawns[r].LabelShortCap.Truncate(NameW - 6f);
            }
            EnsureColumnCache();
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                EnsureColumnCache();
                // The last label rises past its column; reserve its run-out.
                float labelRunOut = HeaderHorizontalRunOut(headerH);
                float w = Mathf.Min(NameW + workTypes.Count * ColW + labelRunOut + 36f + 20f,
                    Verse.UI.screenWidth * 0.95f);
                float h = Mathf.Min(TitleH + headerH + pawns.Count * RowH + 36f + 24f,
                    Verse.UI.screenHeight * 0.9f);
                return new Vector2(w, h);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            EnsureColumnCache();
            if (Event.current.type == EventType.Repaint)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), titleLabel);
            }
            Text.Font = GameFont.Small;

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;

            if (numeric)
            {
                var toggleRect = new Rect(inRect.xMax - modeToggleW - 26f, inRect.y + 2f,
                    modeToggleW, 28f);
                if (Widgets.ButtonText(toggleRect, showVanilla ? vanillaModeLabel : rawModeLabel))
                    showVanilla = !showVanilla;
            }

            var outRect = new Rect(inRect.x, inRect.y + TitleH, inRect.width, inRect.height - TitleH);
            var viewRect = new Rect(0f, 0f,
                NameW + workTypes.Count * ColW + HeaderHorizontalProjection(headerH),
                headerH + pawns.Count * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);

            var scrollViewport = new Rect(scroll.x, scroll.y, outRect.width, outRect.height);
            var visibleBodyColumns = UniformViewportRange.Calculate(
                itemCount: workTypes.Count,
                itemExtent: ColW,
                contentStart: NameW,
                viewportStart: scrollViewport.x,
                viewportExtent: scrollViewport.width);
            // Inclined labels project to the right of their base columns. Expand
            // the viewport left by that exact run-out so the last label remains
            // visible while scrolling through the reserved trailing header area.
            float headerRunOut = HeaderHorizontalRunOut(headerH);
            var visibleHeaderColumns = UniformViewportRange.Calculate(
                itemCount: workTypes.Count,
                itemExtent: ColW,
                contentStart: NameW,
                viewportStart: scrollViewport.x - headerRunOut,
                viewportExtent: scrollViewport.width + headerRunOut);
            var visibleRows = UniformViewportRange.Calculate(
                itemCount: pawns.Count,
                itemExtent: RowH,
                contentStart: headerH,
                viewportStart: scrollViewport.y,
                viewportExtent: scrollViewport.height);
            bool headerVisible = scrollViewport.y < headerH && scrollViewport.yMax > 0f;

            if (Event.current.type == EventType.Repaint)
            {
                if (headerVisible)
                    DrawVisibleHeaderLabels(visibleHeaderColumns);
                DrawVisibleColumnChrome(visibleBodyColumns, pawns.Count * RowH);
                DrawVisibleRows(visibleRows, visibleBodyColumns, viewRect.width, numeric,
                    scrollViewport.x < NameW);
            }
            Widgets.EndScrollView();
        }

        private void EnsureColumnCache()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (!columnCacheRevisions.ShouldRefresh(languageRevision, definitionRevision)) return;

            workTypes.Clear();
            foreach (WorkTypeDef workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
                if (workType.visible)
                    workTypes.Add(workType);

            columnLabels = new string[workTypes.Count];
            columnLabelSizes = new Vector2[workTypes.Count];
            columnTips = new string[workTypes.Count];
            titleLabel = "WR_PriorityGridTitle".Translate();
            rawModeLabel = "WR_GridModeRaw".Translate();
            vanillaModeLabel = "WR_GridModeVanilla".Translate();

            // Inclined labels need diagonal headroom: width*sin + height*cos.
            float maxLabel = 0f;
            using (new TextBlock(GameFont.Small))
            {
                phantomLabelSize = Text.CalcSize("");
                modeToggleW = Mathf.Max(WrText.FitWidth(rawModeLabel),
                    WrText.FitWidth(vanillaModeLabel)) + 24f;
                for (int c = 0; c < workTypes.Count; c++)
                {
                    columnLabels[c] = workTypes[c].labelShort.CapitalizeFirst();
                    // Vanilla's Work tab shows the type description; match it.
                    columnTips[c] = workTypes[c].gerundLabel.CapitalizeFirst()
                        + (workTypes[c].description.NullOrEmpty()
                            ? "" : "\n" + workTypes[c].description);
                    columnLabelSizes[c] = Text.CalcSize(columnLabels[c]);
                    Vector2 size = columnLabelSizes[c];
                    maxLabel = Mathf.Max(maxLabel,
                        size.x * Mathf.Sin(Mathf.Deg2Rad * LabelAngle)
                        + size.y * Mathf.Cos(Mathf.Deg2Rad * LabelAngle));
                }
            }
            headerH = Mathf.Clamp(maxLabel + 8f, 40f, 140f);
        }

        private void DrawVisibleHeaderLabels(UniformViewportRange visibleHeaderColumns)
        {
            // Each label draws only its own trailing 45° line; the first label
            // needs the line BEFORE it drawn separately (an empty phantom label
            // one column to the left).
            WrText.InclinedLabel(new Rect(NameW - ColW, 0f, ColW, headerH), "",
                phantomLabelSize, LabelAngle);
            for (int c = visibleHeaderColumns.Start; c < visibleHeaderColumns.EndExclusive; c++)
            {
                float x = NameW + c * ColW;
                var headRect = new Rect(x, 0f, ColW, headerH);
                WrText.InclinedLabel(headRect, columnLabels[c], columnLabelSizes[c], LabelAngle);
            }
        }

        private void DrawVisibleColumnChrome(
            UniformViewportRange visibleBodyColumns,
            float bodyH)
        {
            for (int c = visibleBodyColumns.Start; c < visibleBodyColumns.EndExclusive; c++)
            {
                float x = NameW + c * ColW;
                TooltipHandler.TipRegion(new Rect(x, 0f, ColW, headerH + bodyH),
                    columnTips[c]);
                // Column separator, vanilla Work-tab style (pixel-snapped).
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                WrText.LineVertical(x, headerH - 2f, bodyH + 2f);
                GUI.color = Color.white;
            }
        }

        private void DrawVisibleRows(
            UniformViewportRange visibleRows,
            UniformViewportRange visibleBodyColumns,
            float viewWidth,
            bool numeric,
            bool pawnNamesVisible)
        {
            var store = RoleStore.Current;
            for (int r = visibleRows.Start; r < visibleRows.EndExclusive; r++)
            {
                var pawn = pawns[r];
                float y = headerH + r * RowH;
                if (r % 2 == 0)
                    Widgets.DrawBoxSolid(new Rect(0f, y, viewWidth, RowH),
                        new Color(1f, 1f, 1f, 0.04f));

                if (pawnNamesVisible)
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(2f, y, NameW - 6f, RowH), pawnNames[r]);
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                bool managed = store != null && store.IsManaged(pawn);
                for (int c = visibleBodyColumns.Start; c < visibleBodyColumns.EndExclusive; c++)
                {
                    var wt = workTypes[c];
                    if (pawn.WorkTypeIsDisabled(wt)) continue; // vanilla leaves these blank
                    // Floored centering: (ColW - 25) / 2 is 0.5, and a half-pixel
                    // x smears the box textures at every UI scale.
                    var box = new Rect(NameW + c * ColW + Mathf.Floor((ColW - 25f) / 2f), y + (RowH - 25f) / 2f, 25f, 25f);
                    DrawWorkBoxBackground(box, pawn, wt);
                    int priority = managed
                        ? (showVanilla
                            ? CompiledJobOrders.VanillaPriorityFor(pawn, wt)
                            : CompiledJobOrders.PriorityFor(pawn, wt))
                        : pawn.workSettings?.GetPriority(wt) ?? 0;
                    if (priority <= 0) continue;
                    if (!numeric)
                    {
                        GUI.DrawTexture(box, WidgetsWork.WorkBoxCheckTex);
                    }
                    else
                    {
                        // Vanilla-equivalent value keys the color even when raw
                        // ranks are shown, so the familiar palette holds.
                        int colorKey = managed
                            ? (showVanilla
                                ? priority
                                : CompiledJobOrders.VanillaPriorityFor(pawn, wt))
                            : Mathf.Clamp(priority, 0, 4);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = WidgetsWork.ColorOfPriority(colorKey);
                        Widgets.Label(box.ContractedBy(-3f), priority.ToStringCached());
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
        }

        private static float HeaderHorizontalProjection(float headerHeight) =>
            headerHeight / Mathf.Tan(Mathf.Deg2Rad * LabelAngle);

        private static float HeaderHorizontalRunOut(float headerHeight) =>
            HeaderHorizontalProjection(headerHeight) + HeaderRunOutPadding;

        /// Vanilla's WidgetsWork.DrawWorkBoxBackground is private; this is its
        /// look-alike (skill-shaded background lerp + passion overlay), minus
        /// the warning overlays and null-safe on skills.
        private static void DrawWorkBoxBackground(Rect rect, Pawn pawn, WorkTypeDef workType)
        {
            float skill = pawn.skills != null ? pawn.skills.AverageOfRelevantSkillsFor(workType) : 10f;
            Texture2D baseTex;
            Texture2D blendTex;
            float blend;
            if (skill < 4f)
            {
                baseTex = WidgetsWork.WorkBoxBGTex_Awful;
                blendTex = WidgetsWork.WorkBoxBGTex_Bad;
                blend = skill / 4f;
            }
            else if (skill <= 14f)
            {
                baseTex = WidgetsWork.WorkBoxBGTex_Bad;
                blendTex = WidgetsWork.WorkBoxBGTex_Mid;
                blend = (skill - 4f) / 10f;
            }
            else
            {
                baseTex = WidgetsWork.WorkBoxBGTex_Mid;
                blendTex = WidgetsWork.WorkBoxBGTex_Excellent;
                blend = (skill - 14f) / 6f;
            }
            GUI.DrawTexture(rect, baseTex);
            GUI.color = new Color(1f, 1f, 1f, blend);
            GUI.DrawTexture(rect, blendTex);
            GUI.color = Color.white;

            var passion = pawn.skills?.MaxPassionOfRelevantSkillsFor(workType) ?? Passion.None;
            if (passion > Passion.None)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                var half = rect;
                half.xMin = rect.center.x;
                half.yMin = rect.center.y;
                GUI.DrawTexture(half, passion == Passion.Major
                    ? WidgetsWork.PassionWorkboxMajorIcon
                    : WidgetsWork.PassionWorkboxMinorIcon);
                GUI.color = Color.white;
            }
        }
    }
}
