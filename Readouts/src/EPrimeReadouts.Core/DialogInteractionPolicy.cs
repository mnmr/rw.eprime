namespace EPrimeReadouts.Core
{
    public enum DialogEscapeAction
    {
        ClearInput,
        UnfocusInput,
        CloseDialog,
    }

    public static class DialogInteractionPolicy
    {
        public static DialogEscapeAction Escape(bool inputFocused, bool inputEmpty)
        {
            if (!inputFocused) return DialogEscapeAction.CloseDialog;
            return inputEmpty ? DialogEscapeAction.UnfocusInput : DialogEscapeAction.ClearInput;
        }
    }

    public static class CompactDialogLayout
    {
        public static float Height(float bodyHeight, float chromeHeight, float minHeight, float maxHeight)
        {
            float natural = bodyHeight + chromeHeight;
            if (natural < minHeight) return minHeight;
            if (natural > maxHeight) return maxHeight;
            return natural;
        }
    }
}
