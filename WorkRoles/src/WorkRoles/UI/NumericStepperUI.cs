using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shared "[-] value [+]" integer input row used by the Recommendations
    /// tuning panels and the role editor. One in-flight edit for the whole UI:
    /// only one text field can hold keyboard focus, so a single buffer is safe.
    internal static class NumericStepperUI
    {
        // Focused control name, its text buffer, and an owner token (role id,
        // 0 for global options) so a mid-edit owner switch drops the edit
        // rather than committing it against the new owner.
        private static string editControl;
        private static string editBuffer;
        private static int editOwner;

        /// Modifier-accelerated stepping: plain click = 1 step, Shift ×5,
        /// Ctrl ×10, Ctrl+Shift jumps to the bound (every commit path clamps,
        /// so the oversized step lands exactly on min/max).
        internal static int StepSize(int step)
        {
            Event e = Event.current;
            if (e == null) return step;
            if (e.control && e.shift) return step * 1000000;
            if (e.control) return step * 10;
            if (e.shift) return step * 5;
            return step;
        }

        /// One input row: dim caption left, then [-] editable value [+] with
        /// modifier-accelerated steps; a unit suffix ("%") renders as a dim
        /// glyph right of the field. Returns the requested value when the user
        /// steps or commits a typed edit; the caller's command clamps it.
        internal static int? DrawRow(Rect rect, string caption,
            string valueLabel, int value, string controlName, int owner,
            string unitSuffix = null)
        {
            int? requested = null;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.DimText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 116f,
                rect.height), caption);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            float controlsX = rect.xMax - 108f;
            if (Widgets.ButtonText(
                    new Rect(controlsX, rect.y + 1f, 26f, 26f), "−"))
                requested = value - StepSize(1);
            float fieldW = unitSuffix == null ? 48f : 36f;
            int? committed = DrawNumericField(
                new Rect(controlsX + 30f, rect.y + 1f, fieldW, 26f),
                controlName, owner, valueLabel);
            if (committed.HasValue) requested = committed;
            if (unitSuffix != null)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = WrStyle.DimText;
                Widgets.Label(new Rect(controlsX + 30f + fieldW + 2f,
                    rect.y + 1f, 14f, 26f), unitSuffix);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (Widgets.ButtonText(
                    new Rect(controlsX + 82f, rect.y + 1f, 26f, 26f), "+"))
                requested = value + StepSize(1);
            return requested;
        }

        /// Editable int field for a [-] value [+] row. Shows the snapshot's
        /// value text until focused; typing edits a local buffer that commits
        /// on Enter or focus loss. Returns the committed value, if any.
        internal static int? DrawNumericField(Rect rect, string controlName,
            int owner, string shownValue)
        {
            bool editing = editControl == controlName && editOwner == owner;
            int? committed = null;
            Event e = Event.current;
            if (editing && e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == controlName)
            {
                committed = TakeNumericEdit();
                GUIUtility.keyboardControl = 0;
                e.Use();
                editing = false;
            }
            GUI.SetNextControlName(controlName);
            string text = editing ? editBuffer : shownValue;
            string typed = Widgets.TextField(rect, text);
            if (GUI.GetNameOfFocusedControl() == controlName)
            {
                if (!editing)
                {
                    editControl = controlName;
                    editOwner = owner;
                    editBuffer = shownValue;
                }
                if (typed != text) editBuffer = typed;
            }
            else if (editing)
            {
                committed = TakeNumericEdit();
            }
            return committed;
        }

        private static int? TakeNumericEdit()
        {
            string buffer = editBuffer;
            editControl = null;
            editBuffer = null;
            return int.TryParse(buffer, out int value) ? value : (int?)null;
        }
    }
}
