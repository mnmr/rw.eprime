#nullable enable
using System;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// A small name-prompt window with the standard keyboard contract:
    /// Enter confirms (via OnAcceptKeyPressed, so it works regardless of
    /// focus), ESC cancels (the Window default), and the input field is
    /// focused on open. Subclasses may inject an extra row between the title
    /// and the field (ExtraHeight/DrawExtra), refuse a name (CanConfirm),
    /// change the length cap (MaxNameLength) and override Confirm to consume
    /// additional state gathered there.
    public class NameDialog : Window
    {
        private readonly Action<string>? onConfirm;
        private readonly string title;
        private readonly string okLabel;
        private readonly string cancelLabel;
        private string name;
        private bool focused;

        /// Control name of the input field, for subclasses that act on its focus.
        protected const string FieldControlName = "RimShared_NameDialogField";
        private const float FieldHeight = 30f;
        private const float ButtonHeight = 32f;
        private const float RowGap = 4f;

        /// Longest name the field accepts; longer input is cut to this length.
        protected virtual int MaxNameLength => 40;

        public NameDialog(string title, string initial, Action<string>? onConfirm = null)
        {
            this.title = title;
            this.onConfirm = onConfirm;
            name = initial ?? "";
            // Resolved once: the language cannot change while a dialog is open.
            okLabel = "OK".Translate();
            cancelLabel = "CancelButton".Translate();
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;  // Enter routes through OnAcceptKeyPressed
            closeOnCancel = true;  // ESC cancels
        }

        public override Vector2 InitialSize =>
            new Vector2(360f, 24f + RowGap + ExtraHeight + FieldHeight
                + RowGap + ButtonHeight + Margin * 2f + 12f);

        /// The untrimmed text in the input field.
        protected string EnteredName
        {
            get => name;
            set => name = value ?? "";
        }

        /// Height of the optional row drawn between the title and the input
        /// field; 0 draws nothing.
        protected virtual float ExtraHeight => 0f;

        protected virtual void DrawExtra(Rect rect) { }

        /// Called with the trimmed, non-empty name on Enter or OK before
        /// Confirm; false keeps the dialog open (the subclass says why).
        protected virtual bool CanConfirm(string trimmedName) => true;

        /// Called with the trimmed, non-empty name on Enter or OK.
        protected virtual void Confirm(string trimmedName) =>
            onConfirm?.Invoke(trimmedName);

        private bool TryApply()
        {
            string trimmed = name.Trim();
            if (trimmed.Length == 0 || !CanConfirm(trimmed)) return false;
            Confirm(trimmed);
            return true;
        }

        /// Enter with an empty or refused name keeps the dialog open instead
        /// of silently discarding the intent.
        public override void OnAcceptKeyPressed()
        {
            if (!TryApply()) return;
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                float y = 0f;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), title);
                y += 24f + RowGap;

                float extra = ExtraHeight;
                if (extra > 0f)
                {
                    DrawExtra(new Rect(0f, y, inRect.width, extra));
                    y += extra;
                }

                GUI.SetNextControlName(FieldControlName);
                name = Widgets.TextField(new Rect(0f, y, inRect.width, FieldHeight), name);
                if (name.Length > MaxNameLength)
                    name = name.Substring(0, MaxNameLength);
                if (!focused)
                {
                    Verse.UI.FocusControl(FieldControlName, this);
                    focused = true;
                }

                var okRect = new Rect(0f, inRect.height - ButtonHeight,
                    inRect.width / 2f - 4f, ButtonHeight);
                var cancelRect = new Rect(inRect.width / 2f + 4f,
                    inRect.height - ButtonHeight,
                    inRect.width / 2f - 4f, ButtonHeight);
                if (Widgets.ButtonText(okRect, okLabel) && TryApply())
                    Close();
                if (Widgets.ButtonText(cancelRect, cancelLabel))
                    Close();
            }
        }
    }
}
