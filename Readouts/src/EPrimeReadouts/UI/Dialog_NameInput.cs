using System;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Minimal name-input dialog: label + text field + OK/Cancel buttons.
    /// Enter key accepts; Escape cancels (via base Window handling).
    public sealed class Dialog_NameInput : Window
    {
        private readonly string titleKey;
        private readonly Action<string> onAccept;
        private string value;

        public override Vector2 InitialSize => new Vector2(320f, 130f);

        public Dialog_NameInput(string titleKey, string initialValue, Action<string> onAccept)
        {
            this.titleKey = titleKey;
            this.onAccept = onAccept;
            value = initialValue ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f),
                "EPR.NameLabel".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            y += 28f;

            GUI.SetNextControlName("NameInputField");
            value = Widgets.TextField(new Rect(inRect.x, y, inRect.width, 24f), value);
            GUI.FocusControl("NameInputField");
            y += 32f;

            float btnW = 80f;
            float btnGap = 8f;
            float totalBtnW = btnW * 2f + btnGap;
            float btnX = inRect.x + (inRect.width - totalBtnW) / 2f;

            bool accepted = Widgets.ButtonText(new Rect(btnX, y, btnW, 24f), "EPR.OK".Translate());
            bool cancelled = Widgets.ButtonText(new Rect(btnX + btnW + btnGap, y, btnW, 24f),
                "EPR.Cancel".Translate());

            // Enter key accepts
            if (Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return
                && !value.NullOrEmpty())
            {
                accepted = true;
                Event.current.Use();
            }

            if (accepted && !value.NullOrEmpty())
            {
                onAccept(value.Trim());
                Close();
            }
            else if (cancelled)
            {
                Close();
            }
        }
    }
}
