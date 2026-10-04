using System;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared location/file plumbing for the export and import dialogs: a
    /// captioned location dropdown (mod data folder under the game's save
    /// data root, Desktop, user home or a custom directory), a file name
    /// field, and an Enter-path row while Custom is picked, drawn by the
    /// shared location picker. Ported from WorkRoles Dialog_RoleFilePicker.
    public abstract class Dialog_EprFilePicker : Dialog_EprPreviewBase
    {
        protected const float RowH = 30f;
        protected static float CaptionRowH =>
            Mathf.Max(22f, TinyText.LineHeight);

        private const string FileNameControl = "EPR.FileName";
        private const string CustomDirControl = "EPR.CustomDirectory";

        private static readonly ExportFolder Exports = new ExportFolder("EPrimeReadouts");

        // Leaving Custom drops focus from the directory field it hides.
        private static readonly Action<ExportLocation> UnfocusHiddenDirectory =
            static picked =>
            {
                if (picked != ExportLocation.Custom)
                    DialogInputFocus.Unfocus(CustomDirControl);
            };

        private protected readonly ExportLocationPicker picker = new ExportLocationPicker(
            Exports, "Readouts.xml", fileNameControl: FileNameControl,
            customDirControl: CustomDirControl, locationChanged: UnfocusHiddenDirectory);

        // Cache contract:
        // Owner: one picker window.
        // Key: none (single slot).
        // Value: location labels, the Enter-path label with its measured
        //   Small-font width, and the path problem strings.
        // Dependencies: UiRevision.Current (language and UI metrics).
        // Refresh policy: immediate on the next draw after the revision moves.
        // Equality policy: an unchanged revision reuses the strings and width.
        // Teardown: window close releases the instance.
        private readonly ExportPickerLabels pickerLabels = new ExportPickerLabels();
        private int pickerLabelsStamp = -1;

        private ExportPickerLabels PickerLabels()
        {
            UiRevision.ObserveCurrentMetrics();
            if (pickerLabelsStamp == UiRevision.Current) return pickerLabels;
            pickerLabelsStamp = UiRevision.Current;
            pickerLabels.GameData = UiText.Get("EPR.LocGameData");
            pickerLabels.Desktop = UiText.Get("EPR.LocDesktop");
            pickerLabels.UserHome = UiText.Get("EPR.LocUserHome");
            pickerLabels.Custom = UiText.Get("EPR.LocCustom");
            pickerLabels.EnterPath = UiText.Get("EPR.EnterPath");
            pickerLabels.BadFileName = UiText.Get("EPR.BadFileName");
            pickerLabels.BadDirectory = UiText.Get("EPR.BadDirectory");
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                pickerLabels.EnterPathWidth = WrText.FitWidth(pickerLabels.EnterPath) + 6f;
            }
            finally
            {
                Text.Font = previousFont;
            }
            return pickerLabels;
        }

        /// Path state previously sampled by WindowUpdate. This draw-path
        /// accessor never resolves shell folders or touches the filesystem.
        protected string? CachedResolvedPath(out string? problem, out bool exists)
        {
            problem = PickerLabels().Problem(picker.CachedProblem);
            exists = picker.CachedExists;
            return picker.CachedPath;
        }

        /// Tiny grey caption, matching the dialog captions elsewhere.
        protected static void DrawCaption(Rect rect, string text)
        {
            rect.y += TinyText.FallbackCaptionOffsetY;
            rect.height = Mathf.Max(rect.height, TinyText.LineHeight);
            GUI.color = EprStyle.CaptionText;
            Text.Anchor = TextAnchor.LowerLeft;
            TinyText.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// Inset panel behind list content; returns the inner content rect.
        protected static Rect DrawFrame(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, EprStyle.PanelBackground);
            GUI.color = EprStyle.PanelOutline;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
            return rect.ContractedBy(6f);
        }

        /// Location dropdown (+ file name field for export-style dialogs), and
        /// the Enter-path row (with a clear X) while Custom is picked.
        protected void DrawLocationRows(Rect inRect, float locRowY, float customRowY,
            bool includeNameField = true) =>
            picker.DrawLocationRows(inRect, locRowY, customRowY, PickerLabels(),
                includeNameField);

        public override void OnCancelKeyPressed()
        {
            if (DialogInputFocus.TryHandleEscape(
                    FileNameControl, picker.FileName, () => picker.FileName = "")
                || DialogInputFocus.TryHandleEscape(
                    CustomDirControl, picker.CustomDir, () => picker.CustomDir = ""))
                return;
            base.OnCancelKeyPressed();
        }

        protected static void UnfocusPickerInputs()
        {
            DialogInputFocus.Unfocus(FileNameControl);
            DialogInputFocus.Unfocus(CustomDirControl);
        }
    }
}
