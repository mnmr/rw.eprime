using Implanner.Core;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// A mod-owned integer text field: the game's own numeric field names
    /// its control from the rect (three string allocations per pass) and
    /// so overwrites any caller-chosen name, which breaks Tab cycling.
    /// This one draws the text field under the CALLER's control name and
    /// applies the game's typing rules through Core's NumericInput.
    internal static class NumericField
    {
        /// Draws the field; returns true when the buffer or value changed
        /// (the caller persists the buffer and issues its command).
        internal static bool Draw(Rect rect, string controlName,
            ref int value, ref string buffer, int min, int max)
        {
            GUI.SetNextControlName(controlName);
            string typed = Widgets.TextField(rect, buffer);
            return NumericInput.Apply(typed, min, max, ref value, ref buffer);
        }

        /// The field with right-aligned text, reading like a number column.
        /// The shared text-field style is global GUI state, so the
        /// alignment is restored through try/finally.
        internal static bool DrawRightAligned(Rect rect, string controlName,
            ref int value, ref string buffer, int min, int max)
        {
            GUIStyle style = Text.CurTextFieldStyle;
            TextAnchor alignment = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            try
            {
                return Draw(rect, controlName, ref value, ref buffer, min, max);
            }
            finally
            {
                style.alignment = alignment;
            }
        }
    }
}
