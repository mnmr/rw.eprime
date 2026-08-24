using System;
using System.IO;
using System.Linq;
using System.Text;
using EPrimeReadouts.Core;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared location/file plumbing for the export and import dialogs: a
    /// captioned location dropdown (mod data folder under the game's save
    /// data root, Desktop, user home or a custom directory), a file name
    /// field, and an Enter-path row while Custom is picked. Ported from
    /// WorkRoles Dialog_RoleFilePicker.
    public abstract class Dialog_EprFilePicker : Dialog_EprPreviewBase
    {
        protected enum Location { GameData, Desktop, UserHome, Custom }

        protected const float RowH = 30f;
        protected static float CaptionRowH =>
            EprStyle.TinyTextMetrics.MinHeight(22f);

        protected Location location = Location.GameData;
        protected string fileName = "Readouts.xml";
        protected string customDir = "";

        private static bool OnWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        private string LocationLabel(Location l) =>
            l == Location.Desktop ? UiText.Get("EPR.LocDesktop")
            : l == Location.UserHome ? UiText.Get("EPR.LocUserHome")
            : l == Location.Custom ? UiText.Get("EPR.LocCustom")
            : UiText.Get("EPR.LocGameData");

        protected string ResolvedDir()
        {
            switch (location)
            {
                case Location.Desktop: return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case Location.UserHome: return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case Location.Custom: return customDir.Trim();
                default: return ReadoutsFiles.Folder;
            }
        }

        // Cache contract:
        // Owner: one file-picker window.
        // Key: location, file name and custom directory.
        // Value: resolved path, validation problem and existence flag.
        // Dependencies: exact key fields and filesystem state sampled by WindowUpdate.
        // Refresh policy: immediate outside OnGUI when an input changes.
        // Equality policy: unchanged inputs preserve strings and avoid syscalls.
        // Teardown: window collection releases all cached strings.
        private Location cachedLocation;
        private string? cachedFileName;
        private string? cachedCustomDir;
        private string? cachedPath;
        private string? cachedProblem;
        private bool cachedExists;
        private bool cacheValid;

        /// Returns path state previously sampled by WindowUpdate. This draw-path
        /// accessor never resolves shell folders or touches the filesystem.
        protected string? CachedResolvedPath(out string? problem, out bool exists)
        {
            problem = cachedProblem;
            exists = cachedExists;
            return cachedPath;
        }

        /// <summary>Refreshes filesystem-backed path state outside OnGUI.</summary>
        protected void RefreshResolvedPathCache()
        {
            if (cacheValid
                && cachedLocation == location
                && string.Equals(cachedFileName, fileName, StringComparison.Ordinal)
                && string.Equals(cachedCustomDir, customDir, StringComparison.Ordinal))
                return;
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
                problem = UiText.Get("EPR.BadFileName");
                return null;
            }
            string dir = ResolvedDir();
            if (dir.NullOrEmpty() || dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                problem = UiText.Get("EPR.BadDirectory");
                return null;
            }
            try
            {
                return Path.Combine(dir, name)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch (Exception) { problem = UiText.Get("EPR.BadDirectory"); return null; }
        }

        // Characters the file system rejects can't be typed at all. A file name
        // additionally never holds separators or a drive colon — Windows'
        // invalid set includes them but Unix's doesn't, so they're explicit.
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '\\', '/', ':' }).Distinct().ToArray();
        private static readonly char[] InvalidDirChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '\\' && c != '/' && c != ':').ToArray();

        private static string? Strip(string? text, char[] invalid)
        {
            if (text == null || text.IndexOfAny(invalid) < 0) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            return sb.ToString();
        }

        /// Tiny grey caption, matching the dialog captions elsewhere.
        protected static void DrawCaption(Rect rect, string text)
        {
            ResolvedTinyTextMetrics metrics = EprStyle.TinyTextMetrics;
            rect.y += metrics.CaptionOffsetY;
            rect.height = metrics.MinHeight(rect.height);
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
            bool includeNameField = true)
        {
            var locRect = new Rect(inRect.x, locRowY, 170f, RowH - 6f);
            if (Widgets.ButtonText(locRect, LocationLabel(location)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var l in new[] { Location.GameData, Location.Desktop, Location.UserHome, Location.Custom })
                {
                    if (l == Location.Desktop && !OnWindows) continue;
                    var captured = l;
                    options.Add(new FloatMenuOption(LocationLabel(l), () =>
                    {
                        location = captured;
                        if (captured != Location.Custom)
                            DialogInputFocus.Unfocus("EPR.CustomDirectory");
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (includeNameField)
            {
                GUI.SetNextControlName("EPR.FileName");
                fileName = Strip(Widgets.TextField(
                    new Rect(locRect.xMax + 8f, locRowY, inRect.width - locRect.width - 8f, RowH - 6f), fileName),
                    InvalidNameChars)!; // non-null for non-null input
            }

            if (location == Location.Custom)
            {
                string enterPath = UiText.Get("EPR.EnterPath");
                UiVersion.ObserveCurrentMetrics();
                float labelW = WrText.FitWidth(enterPath) + 6f;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, customRowY, labelW, RowH - 6f), enterPath);
                Text.Anchor = TextAnchor.UpperLeft;
                const float ClearW = 24f;
                GUI.SetNextControlName("EPR.CustomDirectory");
                customDir = Strip(Widgets.TextField(
                    new Rect(inRect.x + labelW, customRowY, inRect.width - labelW - ClearW - 4f, RowH - 6f), customDir),
                    InvalidDirChars)!; // non-null for non-null input
                var clearRect = new Rect(inRect.xMax - ClearW, customRowY + (RowH - 6f - ClearW) / 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    customDir = "";
            }
        }

        public override void OnCancelKeyPressed()
        {
            if (DialogInputFocus.TryHandleEscape(
                    "EPR.FileName", fileName, () => fileName = "")
                || DialogInputFocus.TryHandleEscape(
                    "EPR.CustomDirectory", customDir, () => customDir = ""))
                return;
            base.OnCancelKeyPressed();
        }

        protected static void UnfocusPickerInputs()
        {
            DialogInputFocus.Unfocus("EPR.FileName");
            DialogInputFocus.Unfocus("EPR.CustomDirectory");
        }
    }
}
