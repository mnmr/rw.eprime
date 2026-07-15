using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Compact confirmation sized to its text — vanilla's Dialog_MessageBox
    /// reserves far more space than a one-liner needs.
    public class Dialog_SmallConfirm : Window
    {
        private const float ContentW = 344f;
        private const float ButtonH = 30f;

        private readonly string text;
        private readonly Action onConfirm;

        public Dialog_SmallConfirm(string text, Action onConfirm)
        {
            this.text = text;
            this.onConfirm = onConfirm;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                Text.Font = GameFont.Small;
                return new Vector2(ContentW + Margin * 2f,
                    Text.CalcHeight(text, ContentW) + 14f + ButtonH + Margin * 2f);
            }
        }

        public override void OnAcceptKeyPressed()
        {
            onConfirm();
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width,
                inRect.height - ButtonH - 8f), text);
            float btnY = inRect.yMax - ButtonH;
            if (Widgets.ButtonText(new Rect(inRect.x, btnY, 120f, ButtonH), "WR_Cancel".Translate()))
                Close();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, btnY, 120f, ButtonH), "WR_OK".Translate()))
            {
                onConfirm();
                Close();
            }
        }
    }
}
