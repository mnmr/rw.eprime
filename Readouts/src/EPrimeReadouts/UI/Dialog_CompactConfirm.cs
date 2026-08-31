using System;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Content-sized confirmation with a bounded scrolling body for unusually
    /// long messages. Enter confirms and Escape cancels.
    public sealed class Dialog_CompactConfirm : Window
    {
        private const float ContentW = 384f;
        private const float ButtonW = 120f;
        private const float ButtonH = 30f;
        private const float BodyGap = 14f;
        private const float MinWindowH = 140f;
        private const float MaxWindowH = 420f;
        private const float BodyTextW = ContentW - 16f;

        private struct MeasureState
        {
            internal string Text;
        }

        private static readonly Func<MeasureState, float> measureBody =
            state => Text.CalcHeight(state.Text, BodyTextW);

        // Cache contract:
        // Owner: process/current UI presentation.
        // Key: immutable body text, Small font, BodyTextW and UiVersion.Current.
        // Value: wrapped body height used by dialog sizing and scrolling.
        // Dependencies: the complete measurement key above.
        // Refresh policy: immediate when a key component changes.
        // Equality policy: equal keys reuse the measured height.
        // Teardown: Reset releases measurements during global runtime teardown.
        private static readonly TextHeightCache bodyHeights = new TextHeightCache();

        private readonly string body;
        private readonly Action confirm;
        private readonly bool destructive;
        private readonly float bodyHeight;
        private readonly Vector2 initialSize;
        private Vector2 scroll;
        private bool committed;

        public Dialog_CompactConfirm(string body, Action confirm, bool destructive = false)
        {
            this.body = body ?? "";
            this.confirm = confirm;
            this.destructive = destructive;
            UiVersion.ObserveCurrentMetrics();
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                bodyHeight = bodyHeights.Get(
                    this.body,
                    (int)GameFont.Small,
                    BodyTextW,
                    UiVersion.Current,
                    new MeasureState { Text = this.body },
                    measureBody);
            }
            finally
            {
                Text.Font = previousFont;
                Text.WordWrap = previousWrap;
            }
            float chromeHeight = BodyGap + ButtonH + Margin * 2f;
            initialSize = new Vector2(
                ContentW + Margin * 2f,
                CompactDialogLayout.Height(
                    bodyHeight, chromeHeight, MinWindowH, MaxWindowH));

            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = true;
            closeOnCancel = true;
            doCloseX = true;
        }

        public override Vector2 InitialSize => initialSize;

        public override void OnAcceptKeyPressed()
        {
            Commit();
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                float buttonY = inRect.yMax - ButtonH;
                var bodyRect = new Rect(
                    inRect.x, inRect.y, inRect.width, buttonY - BodyGap - inRect.y);
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                if (bodyHeight <= bodyRect.height)
                {
                    Widgets.Label(new Rect(bodyRect.x, bodyRect.y, BodyTextW, bodyHeight), body);
                }
                else
                {
                    var viewRect = new Rect(0f, 0f, BodyTextW, bodyHeight);
                    Widgets.BeginScrollView(bodyRect, ref scroll, viewRect);
                    try
                    {
                        Widgets.Label(new Rect(0f, 0f, viewRect.width, bodyHeight), body);
                    }
                    finally
                    {
                        Widgets.EndScrollView();
                    }
                }

                if (Widgets.ButtonText(
                    new Rect(inRect.x, buttonY, ButtonW, ButtonH), UiText.Get("EPR.Cancel")))
                    Close();

                if (destructive) GUI.color = new Color(1f, 0.48f, 0.42f);
                bool accepted = Widgets.ButtonText(
                    new Rect(inRect.xMax - ButtonW, buttonY, ButtonW, ButtonH),
                    UiText.Get("EPR.OK"));
                GUI.color = Color.white;
                if (accepted)
                {
                    Commit();
                    Close();
                }
            }
        }

        private void Commit()
        {
            if (committed) return;
            committed = true;
            confirm();
        }

        internal static void Reset() => bodyHeights.Reset();
    }
}
