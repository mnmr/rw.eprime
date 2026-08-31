using System;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Minimal name-input dialog: label + text field + OK/Cancel buttons.
    /// Enter accepts. Escape clears, then unfocuses, then closes according to
    /// the shared dialog-input policy.
    public sealed class Dialog_NameInput : Window
    {
        private readonly string titleKey;
        private readonly Action<string> onAccept;
        private readonly Func<string, string?>? validate;
        private string value;
        private bool requestInitialFocus = true;

        public override Vector2 InitialSize => new Vector2(320f, 130f);

        public Dialog_NameInput(string titleKey, string initialValue,
            Action<string> onAccept, Func<string, string?>? validate = null)
        {
            this.titleKey = titleKey;
            this.onAccept = onAccept;
            this.validate = validate;
            value = initialValue ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            closeOnClickedOutside = false;
            closeOnAccept = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
            float y = inRect.y;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f),
                UiText.Get("EPR.NameLabel"));
            Text.Anchor = TextAnchor.UpperLeft;
            y += 28f;

            GUI.SetNextControlName("NameInputField");
            value = Widgets.TextField(new Rect(inRect.x, y, inRect.width, 24f), value);
            if (requestInitialFocus)
            {
                GUI.FocusControl("NameInputField");
                requestInitialFocus = false;
            }
            y += 32f;

            float btnW = 80f;
            float btnGap = 8f;
            float totalBtnW = btnW * 2f + btnGap;
            float btnX = inRect.x + (inRect.width - totalBtnW) / 2f;

            bool accepted = Widgets.ButtonText(new Rect(btnX, y, btnW, 24f), UiText.Get("EPR.OK"));
            bool cancelled = Widgets.ButtonText(new Rect(btnX + btnW + btnGap, y, btnW, 24f),
                UiText.Get("EPR.Cancel"));

            if (accepted && TryAccept())
            {
                Close();
            }
            else if (cancelled)
            {
                Close();
            }
            }
        }

        public override void OnAcceptKeyPressed()
        {
            if (TryAccept())
                base.OnAcceptKeyPressed();
        }

        public override void OnCancelKeyPressed()
        {
            if (DialogInputFocus.TryHandleEscape(
                "NameInputField", value, () => value = ""))
                return;
            base.OnCancelKeyPressed();
        }

        private bool TryAccept()
        {
            string trimmed = value.Trim();
            if (trimmed.NullOrEmpty()) return false;
            string? problem = validate?.Invoke(trimmed);
            if (!problem.NullOrEmpty())
            {
                Messages.Message(
                    problem, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            onAccept(trimmed);
            return true;
        }
    }
}
