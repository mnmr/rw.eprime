using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Swatch-grid picker for a training path's color override: the built-in
    /// swatches plus the filled custom slots (read-only here), or Default
    /// (clears the override; the highest-band role colors the path again).
    public class Dialog_PathColor : Window
    {
        // Grid metrics mirror the role editor's swatch grid.
        private const float SwatchSize = 18f;
        private const float SwatchGap = 2f;
        private const int SwatchCols = 19;
        private const float TitleH = 34f;
        private const float ButtonH = 30f;

        private readonly int pathId;
        private readonly bool hasColor;
        private readonly Color current;
        private readonly List<Color> extras = new List<Color>();
        private readonly List<string> extraNames = new List<string>();

        public Dialog_PathColor(int pathId)
        {
            this.pathId = pathId;
            var store = RoleStore.Current;
            var path = store?.PathById(pathId);
            hasColor = path?.hasCustomColor ?? false;
            current = path?.color ?? Color.white;
            if (store != null)
            {
                store.SyncSwatchNames();
                for (int i = 0; i < store.customSwatches.Count; i++)
                    if (store.customSwatches[i].a >= 0.5f)
                    {
                        extras.Add(store.customSwatches[i]);
                        extraNames.Add(store.customSwatchNames[i]);
                    }
            }
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        // 4 built-in rows (4 x 19 = all swatches) + the filled custom slots.
        private int Rows => 4 + (extras.Count + SwatchCols - 1) / SwatchCols;

        public override Vector2 InitialSize => new Vector2(
            SwatchCols * (SwatchSize + SwatchGap) - SwatchGap + Margin * 2f,
            TitleH + Rows * (SwatchSize + SwatchGap) - SwatchGap + 12f + ButtonH + Margin * 2f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f),
                "WR_PathColorTitle".Translate());
            Text.Font = GameFont.Small;

            var swatches = SwatchPalette.Swatches;
            float gridY = inRect.y + TitleH;
            for (int i = 0; i < swatches.Length + extras.Count; i++)
            {
                bool builtIn = i < swatches.Length;
                var color = builtIn ? swatches[i] : extras[i - swatches.Length];
                var rect = new Rect(
                    inRect.x + i % SwatchCols * (SwatchSize + SwatchGap),
                    gridY + i / SwatchCols * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                Widgets.DrawBoxSolid(rect, color);
                if (hasColor && current.IndistinguishableFrom(color))
                    Widgets.DrawBox(rect.ExpandedBy(2f));
                TooltipHandler.TipRegion(rect,
                    builtIn ? SwatchPalette.Names[i] : extraNames[i - swatches.Length]);
                if (Widgets.ButtonInvisible(rect))
                {
                    RoleCommands.SetTrainingPathColor(pathId, true, color);
                    Close();
                }
            }

            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - ButtonH, 180f, ButtonH),
                    "WR_PathColorDefault".Translate()))
            {
                RoleCommands.SetTrainingPathColor(pathId, false, Color.white);
                Close();
            }
        }
    }
}
