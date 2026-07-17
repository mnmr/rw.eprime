using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Composer for Training Path chips: name + delete X; selection recolors
    /// the chip's own border (the chip-highlight idiom, see DrawRemovedOutline)
    /// — never a second ring outside it.
    public static class PathChipUI
    {
        private static readonly Color Outline = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedOutline = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.95f);
        // Metrics mirror RoleChipUI (PadX 8, X icon 16+2).
        private const float PadX = 8f;
        private const float TailW = 18f;

        public static float WidthFor(string name)
        {
            Text.Font = GameFont.Small;
            return WrText.FitWidth(name) + PadX * 2f + TailW;
        }

        public static void Draw(Rect rect, string name, Color color, bool selected)
        {
            var spec = new ChipSpec
            {
                Bg = color,
                Outline = selected ? SelectedOutline : Outline,
                LabelColor = LabelColor,
                Label = name,
                ShowRemove = true,
                LabelInsetLeft = PadX,
                LabelInsetRight = TailW,
            };
            ChipUI.Draw(rect, in spec);
        }
    }
}
