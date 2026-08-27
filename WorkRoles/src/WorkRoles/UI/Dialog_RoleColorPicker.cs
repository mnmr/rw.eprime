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
        private sealed class RoleColorPickerSizeSnapshot
        {
            internal RoleColorPickerSizeSnapshot(Vector2 size)
            {
                Size = size;
            }

            internal Vector2 Size { get; }

            internal bool ContentEquals(RoleColorPickerSizeSnapshot other) =>
                other != null
                && Size.x == other.Size.x
                && Size.y == other.Size.y;
        }

        private readonly Action<Color> onSave;
        private readonly List<Color> pickable;

        // Owner: this role-color picker dialog instance.
        // Key: dialog identity, immutable pickable-color count, and language
        // revision.
        // Value: immutable initial-window-size snapshot.
        // Dependencies: pickable count, BaseColumns, the translated and
        // capitalized ChooseAColor header, Medium-font wrapped height at 564f,
        // Small-font line height, and LanguageChangeCoordinator.Revision.
        // Refresh: immediately on the next size read after a dependency change.
        // Equality: an equal rebuild preserves snapshot identity.
        // Teardown: closing the dialog releases the instance-owned snapshot.
        private RoleColorPickerSizeSnapshot? sizeSnapshot;
        private int sizePickableCount = -1;
        private int sizeLanguageRevision = -1;

        protected override bool ShowDarklight => false;
        protected override Color DefaultColor => oldColor;
        protected override List<Color> PickableColors => pickable;
        protected override float ForcedColorValue => -1f; // free value channel
        protected override bool ShowColorTemperatureBar => false;

        /// Sized from Dialog_ColorPickerBase's actual row budget — its fixed
        /// 450f height fits ~8 palette rows and RectDivider errors past that.
        public override Vector2 InitialSize => SizeSnapshot().Size;

        private RoleColorPickerSizeSnapshot SizeSnapshot()
        {
            int pickableCount = pickable.Count;
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (sizeSnapshot != null
                && sizePickableCount == pickableCount
                && sizeLanguageRevision == languageRevision)
                return sizeSnapshot;

            int rows = Mathf.CeilToInt((float)pickableCount / BaseColumns);
            float central = Mathf.Max(28f * rows + 26f, 200f);
            string headerText = "ChooseAColor".Translate().CapitalizeFirst();
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            float header;
            float lineHeight;
            try
            {
                Text.Font = GameFont.Medium;
                Text.WordWrap = true;
                header = Text.CalcHeight(headerText, 564f);
                Text.Font = GameFont.Small;
                lineHeight = Text.LineHeight;
            }
            finally
            {
                Text.Font = oldFont;
                Text.WordWrap = oldWordWrap;
            }

            // 192 = the base's fixed rows, RectDivider margins, window
            // margins, and an 8f drift cushion.
            var rebuilt = new RoleColorPickerSizeSnapshot(new Vector2(
                600f, header + central + 2f * lineHeight + 192f));
            if (sizeSnapshot == null || !sizeSnapshot.ContentEquals(rebuilt))
                sizeSnapshot = rebuilt;
            sizePickableCount = pickableCount;
            sizeLanguageRevision = languageRevision;
            return sizeSnapshot;
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
