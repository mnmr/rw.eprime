using System;
using System.IO;
using System.Linq;
using System.Text;
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

        private static bool OnWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        private string LocationLabel(Location l) =>
            l == Location.Desktop ? "WR_LocDesktop".Translate().ToString()
            : l == Location.UserHome ? "WR_LocUserHome".Translate().ToString()
            : l == Location.Custom ? "WR_LocCustom".Translate().ToString()
            : "WR_LocGameData".Translate().ToString();

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

        /// Full destination, or null (with a reason) when not usable. The result
        /// uses the platform's directory separator throughout (game paths arrive
        /// with '/', Path.Combine joins with the native one — never mix them).
        protected string ResolvedPath(out string problem)
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
            if (text == null || text.IndexOfAny(invalid) < 0) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            return sb.ToString();
        }

        /// Tiny grey caption, matching the filter-row captions.
        protected static void DrawCaption(Rect rect, string text)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.60f, 0.62f, 0.64f);
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// Location dropdown + file name field, and the Enter-path row (with a
        /// clear X) while Custom is picked.
        protected void DrawLocationRows(Rect inRect, float locRowY, float customRowY)
        {
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
                string enterPath = "WR_EnterPath".Translate();
                float labelW = Text.CalcSize(enterPath).x + 8f;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, customRowY, labelW, RowH - 6f), enterPath);
                Text.Anchor = TextAnchor.UpperLeft;
                const float ClearW = 24f;
                customDir = Strip(Widgets.TextField(
                    new Rect(inRect.x + labelW, customRowY, inRect.width - labelW - ClearW - 4f, RowH - 6f), customDir),
                    InvalidDirChars);
                var clearRect = new Rect(inRect.xMax - ClearW, customRowY + (RowH - 6f - ClearW) / 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    customDir = "";
            }
        }
    }
}
