using System;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Content-sized confirmation with a bounded scrolling body for unusually
    /// long messages: vanilla's Dialog_MessageBox reserves far more space than
    /// a one-liner needs. Enter confirms (once) and Escape cancels. Callers
    /// pass translated button labels and their UI metric revision, so the
    /// draw pass never translates or measures.
    internal sealed class CompactConfirmDialog : Window
    {
        private const float ContentW = 384f;
        private const float ButtonW = 120f;
        private const float ButtonH = 30f;
        private const float BodyGap = 14f;
        private const float MinWindowH = 140f;
        private const float MaxWindowH = 420f;
        private const float BodyTextW = ContentW - 16f;
        private static readonly Color DestructiveTint = new Color(1f, 0.48f, 0.42f);

        private struct MeasureState
        {
            internal string Body;
        }

        private static readonly Func<MeasureState, float> measureBody =
            static state => Text.CalcHeight(state.Body, BodyTextW);

        // Cache contract:
        // Owner: process, one per mod assembly (shared source compiles into
        //   each mod, so mods never share entries).
        // Key: body text, Small font and BodyTextW.
        // Value: wrapped body height used by dialog sizing and scrolling; an
        //   immutable float.
        // Dependencies: the key plus the caller's UI metric revision
        //   (UiRevision.Current).
        // Refresh policy: immediate; an entry stamped with another revision
        //   re-measures on its next read.
        // Equality policy: an equal key and revision reuses the float.
        // Teardown: Reset clears every entry; mods that open the dialog call
        //   it from their world teardown (Readouts RuntimeTeardown.ResetAll,
        //   WorkRoles Patch_MemoryUtility_ClearAllMapsAndWorld); mods that
        //   never open one keep an empty copy.
        private static readonly TextHeightCache bodyHeights = new TextHeightCache();

        private readonly string body;
        private readonly Action confirm;
        private readonly string okLabel;
        private readonly string cancelLabel;
        private readonly bool destructive;
        private readonly float bodyHeight;
        private readonly Vector2 initialSize;
        private Vector2 scroll;
        private bool committed;

        public CompactConfirmDialog(string body, Action confirm, string okLabel,
            string cancelLabel, int uiMetricRevision, bool destructive = false)
        {
            this.body = body ?? "";
            this.confirm = confirm;
            this.okLabel = okLabel;
            this.cancelLabel = cancelLabel;
            this.destructive = destructive;
            // Callers may construct this outside OnGUI (WorkRoles' export
            // save runs from the game update), so only the Text state the
            // measurement needs is touched.
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                bodyHeight = bodyHeights.Get(this.body, (int)GameFont.Small,
                    BodyTextW, uiMetricRevision,
                    new MeasureState { Body = this.body }, measureBody);
            }
            finally
            {
                Text.Font = previousFont;
                Text.WordWrap = previousWrap;
            }
            float chromeHeight = BodyGap + ButtonH + Margin * 2f;
            initialSize = new Vector2(ContentW + Margin * 2f,
                Mathf.Clamp(bodyHeight + chromeHeight, MinWindowH, MaxWindowH));

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
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
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
                    new Rect(inRect.x, buttonY, ButtonW, ButtonH), cancelLabel))
                    Close();

                if (destructive) GUI.color = DestructiveTint;
                bool accepted = Widgets.ButtonText(
                    new Rect(inRect.xMax - ButtonW, buttonY, ButtonW, ButtonH), okLabel);
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

        public static void Reset() => bodyHeights.Reset();
    }
}
