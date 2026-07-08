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
