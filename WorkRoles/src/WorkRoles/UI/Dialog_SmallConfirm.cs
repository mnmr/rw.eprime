using System;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Compact confirmation sized to its text — vanilla's Dialog_MessageBox
    /// reserves far more space than a one-liner needs.
    public class Dialog_SmallConfirm : Window
    {
        private sealed class DialogChromeSnapshot
        {
            internal DialogChromeSnapshot(string cancelLabel, string okLabel)
            {
                CancelLabel = cancelLabel;
                OkLabel = okLabel;
            }

            internal string CancelLabel { get; }
            internal string OkLabel { get; }

            internal bool ContentEquals(DialogChromeSnapshot other) =>
                other != null
                && CancelLabel == other.CancelLabel
                && OkLabel == other.OkLabel;
        }

        private const float ContentW = 344f;
        private const float ButtonH = 30f;

        private readonly string text;
        private readonly Action onConfirm;
        private readonly Vector2 initialSize;

        // Owner: this confirmation dialog instance.
        // Key: the dialog instance and observed language revision.
        // Value: immutable localized chrome plus the constructor-measured
        // initial size for the immutable supplied body text.
        // Dependencies: body text, ContentW, ButtonH, Margin, GameFont.Small
        // metrics, and LanguageChangeCoordinator.Revision for button labels.
        // Refresh: size is built once at construction; localized chrome
        // refreshes immediately when the language revision changes.
        // Equality: equal localized chrome retains snapshot identity.
        // Teardown: closing the dialog releases all instance-owned values.
        // Assigned by EnsureChrome, called from the constructor.
        private DialogChromeSnapshot chromeSnapshot = null!;
        private int chromeLanguageRevision = -1;

        public Dialog_SmallConfirm(string text, Action onConfirm)
        {
            this.text = text;
            this.onConfirm = onConfirm;
            initialSize = MeasureInitialSize(text);
            EnsureChrome();
            absorbInputAroundWindow = true;
            closeOnAccept = true;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize => initialSize;

        private Vector2 MeasureInitialSize(string bodyText)
        {
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                return new Vector2(ContentW + Margin * 2f,
                    Text.CalcHeight(bodyText, ContentW)
                        + 14f + ButtonH + Margin * 2f);
            }
            finally
            {
                Text.Font = oldFont;
                Text.WordWrap = oldWordWrap;
            }
        }

        private void EnsureChrome()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (chromeSnapshot != null
                && chromeLanguageRevision == languageRevision)
                return;
            var rebuilt = new DialogChromeSnapshot(
                "WR_Cancel".Translate(), "WR_OK".Translate());
            if (chromeSnapshot == null || !chromeSnapshot.ContentEquals(rebuilt))
                chromeSnapshot = rebuilt;
            chromeLanguageRevision = languageRevision;
        }

        public override void OnAcceptKeyPressed()
        {
            onConfirm();
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = GuiStateScope.Capture();
            EnsureChrome();
            DialogChromeSnapshot chrome = chromeSnapshot;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width,
                    inRect.height - ButtonH - 8f), text);
                float btnY = inRect.yMax - ButtonH;
                if (Widgets.ButtonText(
                        new Rect(inRect.x, btnY, 120f, ButtonH),
                        chrome.CancelLabel))
                    Close();
                if (Widgets.ButtonText(
                        new Rect(inRect.xMax - 120f, btnY, 120f, ButtonH),
                        chrome.OkLabel))
                {
                    onConfirm();
                    Close();
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
                GUI.color = oldColor;
            }
        }
    }
}
