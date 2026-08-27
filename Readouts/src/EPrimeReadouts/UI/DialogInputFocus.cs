using System;
using EPrimeReadouts.Core;
using UnityEngine;

namespace EPrimeReadouts.UI
{
    internal static class DialogInputFocus
    {
        internal static bool TryHandleEscape(string controlName, string value, Action clear)
        {
            if (!string.Equals(GUI.GetNameOfFocusedControl(), controlName,
                StringComparison.Ordinal))
                return false;

            DialogEscapeAction action = DialogInteractionPolicy.Escape(
                inputFocused: true, inputEmpty: string.IsNullOrEmpty(value));
            if (action == DialogEscapeAction.ClearInput)
                clear();
            else
                ClearFocus();
            Event.current.Use();
            return true;
        }

        internal static void Unfocus(string controlName)
        {
            if (string.Equals(GUI.GetNameOfFocusedControl(), controlName,
                StringComparison.Ordinal))
                ClearFocus();
        }

        private static void ClearFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
        }
    }
}
