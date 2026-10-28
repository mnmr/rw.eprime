using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    internal sealed class Dialog_SetupChoice : Window
    {
        private readonly bool conflict;
        private readonly bool hasDefault;
        private bool autoUpdate;
        private bool answered;
        // Owner: window. Key: UI metric/language revision. Value: resolved text and measured
        // geometry, immutable between refreshes. Dependencies: mode, saved-default presence,
        // fixed width and UI metrics. Refresh: PreOpen/WindowUpdate only. Equality: same key reuses
        // all data. Teardown: released with the window; no external resources.
        private int stamp = -1;
        private string body = "", autoLabel = "";
        private readonly string[] captions = new string[3];
        private float bodyHeight, buttonHeight, rowHeight;
        private const float Width = 560f;

        internal Dialog_SetupChoice(bool conflict)
        {
            this.conflict = conflict;
            hasDefault = SetupDefaults.HasDefault;
            autoUpdate = EPrimeReadoutsMod.Settings.autoUpdateDefault;
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true;
            closeOnAccept = false; closeOnCancel = true;
            Refresh();
        }

        private void Refresh()
        {
            UiRevision.ObserveCurrentMetrics();
            if (stamp == UiRevision.Current) return;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                body = UiText.Get(conflict ? "EPR.DefaultConflictBody" : hasDefault ? "EPR.DefaultSavedBody" : "EPR.DefaultEmptyBody");
                autoLabel = UiText.Get("EPR.DefaultAutoUpdate");
                captions[0] = UiText.Get("EPR.DefaultUseCurrent");
                captions[1] = UiText.Get(conflict ? "EPR.DefaultKeep" : "EPR.DefaultClear");
                captions[2] = UiText.Get(conflict ? "EPR.DefaultStopAuto" : "CancelButton");
                float width = Width - 2f * Margin;
                bodyHeight = Mathf.Ceil(Text.CalcHeight(body, width));
                buttonHeight = Mathf.Max(32f, Mathf.Ceil(Text.LineHeight) + 10f);
                for (int i = 0; i < captions.Length; i++)
                    buttonHeight = Mathf.Max(buttonHeight, Mathf.Ceil(Text.CalcHeight(captions[i], width - 20f)) + 10f);
                rowHeight = Mathf.Max(28f, Mathf.Ceil(Text.CalcHeight(autoLabel, width - 30f)));
            }
            stamp = UiRevision.Current;
        }

        public override Vector2 InitialSize => new Vector2(Width,
            bodyHeight + 24f + 3f * (buttonHeight + 8f) + (conflict ? 0f : rowHeight + 8f) + 2f * Margin);
        public override void WindowUpdate() { base.WindowUpdate(); Refresh(); windowRect.height = InitialSize.y; }

        public override void DoWindowContents(Rect rect)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, 0f, rect.width, bodyHeight), body);
                float y = bodyHeight + 20f;
                if (!conflict)
                {
                    Widgets.CheckboxLabeled(new Rect(0f, y, rect.width, rowHeight), autoLabel, ref autoUpdate);
                    y += rowHeight + 8f;
                }
                for (int i = 0; i < captions.Length; i++)
                {
                    if (Widgets.ButtonText(new Rect(0f, y, rect.width, buttonHeight), captions[i],
                        active: conflict || i != 1 || hasDefault))
                    {
                        answered = true;
                        if (conflict) SetupDefaults.Decide(i == 0, i == 2);
                        else if (i == 0) SetupDefaults.QueueSave(autoUpdate);
                        else if (i == 1) SetupDefaults.QueueClear();
                        Close(); return;
                    }
                    y += buttonHeight + 8f;
                }
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (conflict && !answered) SetupDefaults.Decide(false, false);
        }
    }
}
