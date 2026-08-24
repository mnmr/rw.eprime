using System;
using System.IO;
using System.Linq;
using System.Text;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shared location/file plumbing for the export and import dialogs: a
    /// captioned location dropdown (game data folder, Desktop, user home or a
    /// custom directory), a file name field, and an Enter-path row while
    /// Custom is picked. Both dialogs lay these rows out bottom-up.
    public abstract class Dialog_RoleFilePicker : Window
    {
        protected enum Location { GameData, Desktop, UserHome, Custom }

        protected const float RowH = 30f;
        protected const float ButtonW = 150f;
        protected const float ButtonH = 32f;
        protected const float CaptionRowH = 22f;

        protected Location location = Location.GameData;
        protected string fileName = RoleIO.DefaultFileName;
        protected string customDir = "";

        // Owner: dialog. Key: LanguageChangeCoordinator.Revision. Value:
        // translated location/enter-path labels and the Small-font enter-path
        // width. Dependencies: language and font. Refresh: immediately on
        // language revision. Equality: matching revision reuses strings/width.
        // Teardown: dialog close releases the instance and its owned array.
        private readonly string[] locationLabels = new string[4];
        private int textLanguageRevision = -1;
        // Assigned by EnsureTextCache before first use.
        private string enterPathLabel = null!;
        private float enterPathLabelWidth;

        private static bool OnWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        private string LocationLabel(Location location)
        {
            EnsureTextCache();
            return locationLabels[(int)location];
        }

        private void EnsureTextCache()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (textLanguageRevision == revision) return;
            textLanguageRevision = revision;
            locationLabels[(int)Location.GameData] =
                "WR_LocGameData".Translate().ToString();
            locationLabels[(int)Location.Desktop] =
                "WR_LocDesktop".Translate().ToString();
            locationLabels[(int)Location.UserHome] =
                "WR_LocUserHome".Translate().ToString();
            locationLabels[(int)Location.Custom] =
                "WR_LocCustom".Translate().ToString();
            enterPathLabel = "WR_EnterPath".Translate().ToString();
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                enterPathLabelWidth = WrText.FitWidth(enterPathLabel) + 8f;
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private string ResolvedDir()
        {
            switch (location)
            {
                case Location.Desktop: return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case Location.UserHome: return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case Location.Custom: return customDir.Trim();
                default: return RoleIO.GameDataDir;
            }
        }

        // Owner: dialog. Key: (location, file-name text, custom-directory text).
        // Value: immutable resolved path/problem strings and existence scalar.
        // Dependencies: picker inputs, platform special folders, filesystem
        // existence, and translated validation messages. Refresh: queued once in
        // GameComponentUpdate after an input-key miss, never from OnGUI. Equality:
        // ordinally equal input keys reuse the complete result. Teardown: dialog
        // close and completion of any queued cached delegate release the instance.
        private Location cachedLocation;
        private string? cachedFileName;
        private string? cachedCustomDir;
        private string? cachedPath;
        private string? cachedProblem;
        private bool cachedExists;
        private bool cacheValid;
        private bool pathRefreshPending;
        private readonly Action refreshPathAction;

        protected Dialog_RoleFilePicker()
        {
            refreshPathAction = RefreshPathOutsideOnGUI;
        }

        /// Path + existence, recomputed outside OnGUI when the input key changes.
        /// Idle passes only compare the cached key; File.Exists and special-folder
        /// resolution execute at most once for each queued refresh.
        protected string? CachedResolvedPath(out string? problem, out bool exists)
        {
            bool current = cacheValid
                && cachedLocation == location
                && string.Equals(cachedFileName, fileName,
                    StringComparison.Ordinal)
                && string.Equals(cachedCustomDir, customDir,
                    StringComparison.Ordinal);
            if (!current && !pathRefreshPending)
            {
                cacheValid = false;
                pathRefreshPending = true;
                WorkRolesGameComponent.RunOutsideOnGUI(refreshPathAction);
            }
            problem = current ? cachedProblem : null;
            exists = current && cachedExists;
            return current ? cachedPath : null;
        }

        private void RefreshPathOutsideOnGUI()
        {
            pathRefreshPending = false;
            cachedLocation = location;
            cachedFileName = fileName;
            cachedCustomDir = customDir;
            cachedPath = ResolvedPath(out cachedProblem);
            cachedExists = cachedPath != null && File.Exists(cachedPath);
            cacheValid = true;
        }

        /// Full destination, or null (with a reason) when not usable. The result
        /// uses the platform's directory separator throughout (game paths arrive
        /// with '/', Path.Combine joins with the native one — never mix them).
        protected string? ResolvedPath(out string? problem)
        {
            problem = null;
            string name = fileName.Trim();
            if (name.NullOrEmpty() || name.IndexOfAny(InvalidNameChars) >= 0)
            {
                problem = "WR_BadFileName".Translate();
                return null;
            }
            string dir = ResolvedDir();
            if (dir.NullOrEmpty() || dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                problem = "WR_BadDirectory".Translate();
                return null;
            }
            try
            {
                return Path.Combine(dir, name)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch (Exception) { problem = "WR_BadDirectory".Translate(); return null; }
        }

        // Characters the file system rejects can't be typed at all. A file name
        // additionally never holds separators or a drive colon — Windows'
        // invalid set includes them but Unix's doesn't, so they're explicit.
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '\\', '/', ':' }).Distinct().ToArray();
        private static readonly char[] InvalidDirChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '\\' && c != '/' && c != ':').ToArray();

        private static string Strip(string text, char[] invalid)
        {
            // Callers pass Widgets.TextField results, which are never null.
            if (text == null || text.IndexOfAny(invalid) < 0) return text!;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            return sb.ToString();
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
            var locRect = new Rect(inRect.x, locRowY, 170f, RowH - 6f);
            if (Widgets.ButtonText(locRect, LocationLabel(location)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var l in new[] { Location.GameData, Location.Desktop, Location.UserHome, Location.Custom })
                {
                    if (l == Location.Desktop && !OnWindows) continue;
                    var captured = l;
                    options.Add(new FloatMenuOption(LocationLabel(l), () => location = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            fileName = Strip(Widgets.TextField(
                new Rect(locRect.xMax + 8f, locRowY, inRect.width - locRect.width - 8f, RowH - 6f), fileName),
                InvalidNameChars);

            if (location == Location.Custom)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, customRowY,
                    enterPathLabelWidth, RowH - 6f), enterPathLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                const float ClearW = 24f;
                customDir = Strip(Widgets.TextField(
                    new Rect(inRect.x + enterPathLabelWidth, customRowY,
                        inRect.width - enterPathLabelWidth - ClearW - 4f,
                        RowH - 6f), customDir),
                    InvalidDirChars);
                var clearRect = new Rect(inRect.xMax - ClearW, customRowY + (RowH - 6f - ClearW) / 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    customDir = "";
            }
        }
    }
}
