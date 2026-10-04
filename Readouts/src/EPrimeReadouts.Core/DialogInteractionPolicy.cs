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
}
