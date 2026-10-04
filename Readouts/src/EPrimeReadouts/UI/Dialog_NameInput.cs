using System;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The shared name prompt with Readouts' input rules: a refused name is
    /// reported as a message and keeps the dialog open; typed keys never reach
    /// game bindings; Escape clears, then unfocuses, then closes according to
    /// the shared dialog-input policy.
    public sealed class Dialog_NameInput : NameDialog
    {
        private readonly Func<string, string?>? validate;

        public Dialog_NameInput(string initialValue, Action<string> onAccept,
            Func<string, string?>? validate = null)
            : base(UiText.Get("EPR.NameLabel"), initialValue, onAccept)
        {
            this.validate = validate;
        }

        // Readouts never capped pool or group names.
        protected override int MaxNameLength => int.MaxValue;

        public override void DoWindowContents(Rect inRect)
        {
            TextInputCapture.Observe();
            base.DoWindowContents(inRect);
        }

        public override void OnCancelKeyPressed()
        {
            if (DialogInputFocus.TryHandleEscape(
                FieldControlName, EnteredName, () => EnteredName = ""))
                return;
            base.OnCancelKeyPressed();
        }

        protected override bool CanConfirm(string trimmedName)
        {
            string? problem = validate?.Invoke(trimmedName);
            if (problem.NullOrEmpty()) return true;
            Messages.Message(problem, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}
