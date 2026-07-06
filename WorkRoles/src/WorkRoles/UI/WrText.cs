using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public static class WrText
    {
        /// Medium font scaled down — a header size between GameFont.Small and Medium.
        public static void HeaderLabel(Rect rect, string text, float scale = 0.85f)
        {
            var oldMatrix = GUI.matrix;
            var oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(rect.x, rect.y));
            Widgets.Label(new Rect(rect.x, rect.y, rect.width / scale, rect.height / scale), text);
            GUI.matrix = oldMatrix;
            Text.Font = oldFont;
        }
    }
}
