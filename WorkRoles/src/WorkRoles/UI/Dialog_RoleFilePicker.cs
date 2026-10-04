using System;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shared location/file plumbing for the export and import dialogs: a
    /// captioned location dropdown (game data folder, Desktop, user home or a
    /// custom directory), a file name field, and an Enter-path row while
    /// Custom is picked, drawn by the shared location picker. Both dialogs
    /// lay these rows out bottom-up.
    public abstract class Dialog_RoleFilePicker : Window
    {
        protected const float RowH = 30f;
        protected const float ButtonW = 150f;
        protected const float ButtonH = 32f;
        protected const float CaptionRowH = 22f;

        private protected readonly ExportLocationPicker picker;

        // Owner: dialog. Key: LanguageChangeCoordinator.Revision. Value:
        // translated location/enter-path labels, path problem messages and the
        // Small-font enter-path width. Dependencies: language and font.
        // Refresh: immediately on language revision. Equality: matching
        // revision reuses strings/width. Teardown: dialog close releases the
        // instance and its owned labels.
        private readonly ExportPickerLabels labels = new ExportPickerLabels();
        private int textLanguageRevision = -1;

        // Owner: dialog. The picker's path state (resolved path, problem and
        // existence for the location, file-name and custom-directory text) is
        // refreshed queued once in GameComponentUpdate after an input-key
        // miss, never from OnGUI. Equality: ordinally equal input keys reuse
        // the complete result. Teardown: dialog close and completion of any
        // queued cached delegate release the instance.
        private bool pathRefreshPending;
        private readonly Action refreshPathAction;

        /// Import passes acceptExactTypedName to also find a file saved under
        /// the exact typed name when no ".xml" file exists.
        protected Dialog_RoleFilePicker(bool acceptExactTypedName = false)
        {
            picker = new ExportLocationPicker(RoleIO.Exports, RoleIO.DefaultFileName,
                acceptExactTypedName);
            refreshPathAction = RefreshPathOutsideOnGUI;
        }

        private void EnsureTextCache()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (textLanguageRevision == revision) return;
            textLanguageRevision = revision;
            labels.GameData = "WR_LocGameData".Translate().ToString();
            labels.Desktop = "WR_LocDesktop".Translate().ToString();
            labels.UserHome = "WR_LocUserHome".Translate().ToString();
            labels.Custom = "WR_LocCustom".Translate().ToString();
            labels.EnterPath = "WR_EnterPath".Translate().ToString();
            labels.BadFileName = "WR_BadFileName".Translate().ToString();
            labels.BadDirectory = "WR_BadDirectory".Translate().ToString();
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                labels.EnterPathWidth = WrText.FitWidth(labels.EnterPath) + 8f;
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        /// Path + existence, recomputed outside OnGUI when the input key changes.
        /// Idle passes only compare the cached key; File.Exists and special-folder
        /// resolution execute at most once for each queued refresh.
        protected string? CachedResolvedPath(out string? problem, out bool exists)
        {
            bool current = picker.IsCurrent;
            if (!current && !pathRefreshPending)
            {
                picker.Invalidate();
                pathRefreshPending = true;
                WorkRolesGameComponent.RunOutsideOnGUI(refreshPathAction);
            }
            if (!current)
            {
                problem = null;
                exists = false;
                return null;
            }
            EnsureTextCache();
            problem = labels.Problem(picker.CachedProblem);
            exists = picker.CachedExists;
            return picker.CachedPath;
        }

        private void RefreshPathOutsideOnGUI()
        {
            pathRefreshPending = false;
            picker.Refresh();
        }

        /// Tiny grey caption, matching the filter-row captions.
        protected static void DrawCaption(Rect rect, string text)
        {
            GUI.color = WrStyle.CaptionText;
            Text.Anchor = TextAnchor.LowerLeft;
            float visualH = Mathf.Max(rect.height, TinyText.LineHeight);
            TinyText.Label(new Rect(rect.x, rect.yMax - visualH,
                rect.width, visualH), text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// Location dropdown + file name field, and the Enter-path row (with a
        /// clear X) while Custom is picked.
        protected void DrawLocationRows(Rect inRect, float locRowY, float customRowY)
        {
            EnsureTextCache();
            picker.DrawLocationRows(inRect, locRowY, customRowY, labels);
        }
    }
}
