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

        /// The base dialog's fixed height fits 8 palette rows (9 columns, 28px
        /// per row); our swatch grid needs more. The extra 16f absorbs the
        /// header/text rows varying with UI scale — exact fits keep breaking.
        public override Vector2 InitialSize
        {
            get
            {
                var size = base.InitialSize;
                int paletteRows = Mathf.CeilToInt(pickable.Count / 9f);
                if (paletteRows > 8)
                    size.y += 28f * (paletteRows - 8) + 16f;
                return size;
            }
        }

        public Dialog_RoleColorPicker(Color current, Action<Color> onSave)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
        {
            this.onSave = onSave;
            pickable = RolesTabView.Swatches.ToList();
            color = current;
            oldColor = current;
        }

        protected override void SaveColor(Color color) => onSave(color);
    }
}
