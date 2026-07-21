using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Vanilla HSV color picker (wheel + RGB/hex fields + palette) for defining a
    /// custom role-editor swatch. The palette offers the Tailwind swatches.
    public class Dialog_RoleColorPicker : Dialog_ColorPickerBase
    {
        private readonly Action<Color> onSave;
        private readonly List<Color> pickable;

        protected override bool ShowDarklight => false;
        protected override Color DefaultColor => oldColor;
        protected override List<Color> PickableColors => pickable;
        protected override float ForcedColorValue => -1f; // free value channel
        protected override bool ShowColorTemperatureBar => false;

        /// Sized from Dialog_ColorPickerBase's actual row budget — its fixed
        /// 450f height fits ~8 palette rows and RectDivider errors past that.
        public override Vector2 InitialSize
        {
            get
            {
                int rows = Mathf.CeilToInt((float)pickable.Count / BaseColumns);
                float central = Mathf.Max(28f * rows + 26f, 200f);  // palette vs wheel/textfields
                float header, lineH;
                using (new TextBlock(GameFont.Medium))
                    header = Text.CalcHeight("ChooseAColor".Translate().CapitalizeFirst(), 564f);
                using (new TextBlock(GameFont.Small))
                    lineH = Text.LineHeight;
                // 192 = the base's fixed rows, RectDivider margins, window
                // margins, and an 8f drift cushion.
                return new Vector2(600f, header + central + 2f * lineH + 192f);
            }
        }

        /// The base palette wraps at 9 fixed columns (250px, private layout),
        /// while our grid is 19 families wide — fed raw, families smear across
        /// rows. Reordered into 9-family blocks, each block renders as shade
        /// rows with families vertically aligned, like our own grid.
        private const int BaseColumns = 9;

        public Dialog_RoleColorPicker(Color current, Action<Color> onSave)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
        {
            this.onSave = onSave;
            // Our grid is shade-major: 4 shade rows of 19 families each.
            var swatches = SwatchPalette.Swatches;
            const int ShadeCount = 4;
            int familyCount = swatches.Length / ShadeCount;
            pickable = new List<Color>(swatches.Length);
            for (int block = 0; block < familyCount; block += BaseColumns)
                for (int shade = 0; shade < ShadeCount; shade++)
                    for (int f = block; f < Mathf.Min(block + BaseColumns, familyCount); f++)
                        pickable.Add(swatches[shade * familyCount + f]);
            color = current;
            oldColor = current;
        }

        protected override void SaveColor(Color color) => onSave(color);
    }
}
