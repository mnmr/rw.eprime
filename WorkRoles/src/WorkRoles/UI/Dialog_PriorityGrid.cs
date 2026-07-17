using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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
        private readonly string title;
        private readonly List<WorkTypeDef> workTypes;
        private readonly float headerH;
        // Precomputed per-open: Truncate and CapitalizeFirst allocate per call,
        // and the pawn/column lists are fixed for the dialog's lifetime.
        private readonly string[] pawnNames;
        private readonly string[] columnLabels;
        private readonly string[] columnTips;
        private Vector2 scroll;
        /// Local view state only — never written back to the synced setting.
        private bool showVanilla;

        private const float TitleH = 38f;
        private const float NameW = 170f;
        private const float ColW = 26f;   // vanilla work box (25) + gap
        private const float RowH = 27f;
        private const float LabelAngle = 45f;

        public Dialog_PriorityGrid(List<Pawn> pawns, string title)
        {
            this.pawns = pawns;
            this.title = title;
            showVanilla = RoleStore.Current?.reportVanillaPriorities == true;
            workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder.Where(w => w.visible).ToList();
            columnLabels = new string[workTypes.Count];
            columnTips = new string[workTypes.Count];
            // Inclined labels need diagonal headroom: width*sin + height*cos.
            float maxLabel = 0f;
            using (new TextBlock(GameFont.Small))
            {
                for (int c = 0; c < workTypes.Count; c++)
                {
                    columnLabels[c] = workTypes[c].labelShort.CapitalizeFirst();
                    // Vanilla's Work tab shows the type description; match it.
                    columnTips[c] = workTypes[c].gerundLabel.CapitalizeFirst()
                        + (workTypes[c].description.NullOrEmpty() ? "" : "\n" + workTypes[c].description);
                    var size = Text.CalcSize(columnLabels[c]);
                    maxLabel = Mathf.Max(maxLabel,
                        size.x * Mathf.Sin(Mathf.Deg2Rad * LabelAngle)
                        + size.y * Mathf.Cos(Mathf.Deg2Rad * LabelAngle));
                }
                pawnNames = new string[pawns.Count];
                for (int r = 0; r < pawns.Count; r++)
                    pawnNames[r] = pawns[r].LabelShortCap.Truncate(NameW - 6f);
            }
            headerH = Mathf.Clamp(maxLabel + 8f, 40f, 140f);
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                // The last label rises past its column; reserve its run-out.
                float labelRunOut = headerH / Mathf.Tan(Mathf.Deg2Rad * LabelAngle) + 20f;
                float w = Mathf.Min(NameW + workTypes.Count * ColW + labelRunOut + 36f + 20f,
                    Verse.UI.screenWidth * 0.95f);
                float h = Mathf.Min(TitleH + headerH + pawns.Count * RowH + 36f + 24f,
                    Verse.UI.screenHeight * 0.9f);
                return new Vector2(w, h);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH),
                "WR_PriorityGridTitle".Translate(title));
            Text.Font = GameFont.Small;

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            var store = RoleStore.Current;

            if (numeric)
            {
                string rawLabel = "WR_GridModeRaw".Translate();
                string vanillaLabel = "WR_GridModeVanilla".Translate();
                float toggleW = Mathf.Max(WrText.FitWidth(rawLabel), WrText.FitWidth(vanillaLabel)) + 24f;
                var toggleRect = new Rect(inRect.xMax - toggleW - 26f, inRect.y + 2f, toggleW, 28f);
                if (Widgets.ButtonText(toggleRect, showVanilla ? vanillaLabel : rawLabel))
                    showVanilla = !showVanilla;
            }

            var outRect = new Rect(inRect.x, inRect.y + TitleH, inRect.width, inRect.height - TitleH);
            var viewRect = new Rect(0f, 0f,
                NameW + workTypes.Count * ColW + headerH / Mathf.Tan(Mathf.Deg2Rad * LabelAngle),
                headerH + pawns.Count * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);

            float bodyH = pawns.Count * RowH;
            // Each label draws only its own trailing 45° line; the first label
            // needs the line BEFORE it drawn separately (an empty phantom label
            // one column to the left).
            WrText.InclinedLabel(new Rect(NameW - ColW, 0f, ColW, headerH), "", LabelAngle);
            for (int c = 0; c < workTypes.Count; c++)
            {
                float x = NameW + c * ColW;
                var headRect = new Rect(x, 0f, ColW, headerH);
                WrText.InclinedLabel(headRect, columnLabels[c], LabelAngle);
                TooltipHandler.TipRegion(new Rect(x, 0f, ColW, headerH + bodyH),
                    columnTips[c]);
                // Column separator, vanilla Work-tab style (pixel-snapped).
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                WrText.LineVertical(x, headerH - 2f, bodyH + 2f);
                GUI.color = Color.white;
            }

            // Cull to the scroll viewport: big groups drew every row off-screen.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt((scroll.y - headerH) / RowH));
            int lastRow = Mathf.Min(pawns.Count - 1,
                Mathf.CeilToInt((scroll.y + outRect.height - headerH) / RowH));
            for (int r = firstRow; r <= lastRow; r++)
            {
                var pawn = pawns[r];
                float y = headerH + r * RowH;
                if (r % 2 == 0)
                    Widgets.DrawBoxSolid(new Rect(0f, y, viewRect.width, RowH), new Color(1f, 1f, 1f, 0.04f));

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(2f, y, NameW - 6f, RowH), pawnNames[r]);
                Text.Anchor = TextAnchor.UpperLeft;

                bool managed = store != null && store.IsManaged(pawn);
                for (int c = 0; c < workTypes.Count; c++)
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
                            ? CompiledJobOrders.VanillaPriorityFor(pawn, wt)
                            : Mathf.Clamp(priority, 0, 4);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = WidgetsWork.ColorOfPriority(colorKey);
                        Widgets.Label(box.ContractedBy(-3f), priority.ToStringCached());
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
            Widgets.EndScrollView();
        }

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
