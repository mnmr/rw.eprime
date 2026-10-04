using System;
using System.Collections.Generic;
using System.IO;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Where an export/import picker looks: the mod's folder under the game's
    /// save data root, Desktop (Windows only), the user home, or a typed path.
    internal enum ExportLocation { GameData, Desktop, UserHome, Custom }

    /// Translated strings and measurements a mod hands to
    /// <see cref="ExportLocationPicker"/>. The mod owns the instance and
    /// refreshes it behind its own language/UI-metric revision gates; the
    /// picker only reads it.
    internal sealed class ExportPickerLabels
    {
        public string GameData { get; set; } = "";
        public string Desktop { get; set; } = "";
        public string UserHome { get; set; } = "";
        public string Custom { get; set; } = "";

        /// Label of the custom directory row and its pre-measured width
        /// (Small font, including the mod's padding).
        public string EnterPath { get; set; } = "";
        public float EnterPathWidth { get; set; }

        public string BadFileName { get; set; } = "";
        public string BadDirectory { get; set; } = "";

        public string LocationLabel(ExportLocation location) =>
            location == ExportLocation.Desktop ? Desktop
            : location == ExportLocation.UserHome ? UserHome
            : location == ExportLocation.Custom ? Custom
            : GameData;

        /// Translated text for a path problem, or null for none.
        public string? Problem(ExportPathProblem problem) =>
            problem == ExportPathProblem.BadFileName ? BadFileName
            : problem == ExportPathProblem.BadDirectory ? BadDirectory
            : null;
    }

    /// Location state of an export/import file picker, composed by each mod's
    /// dialogs: the picked location, typed file name and custom directory,
    /// the resolved path sampled outside OnGUI, and the location rows (a
    /// location dropdown, an optional file name field, and an Enter-path row
    /// with a clear X while Custom is picked).
    internal sealed class ExportLocationPicker
    {
        private const float RowH = 30f;
        private const float ClearW = 24f;

        public ExportLocation Location = ExportLocation.GameData;
        public string FileName;
        public string CustomDir = "";

        private readonly ExportFolder folder;
        private readonly bool acceptExactTypedName;
        private readonly string? fileNameControl;
        private readonly string? customDirControl;
        private readonly Action<ExportLocation>? locationChanged;

        /// acceptExactTypedName: when no ".xml" file exists, also accept a file
        /// saved under the exact typed name (exports written before ".xml" was
        /// added). The control names for the two text fields and the callback
        /// after the dropdown changes the location are optional hooks for a
        /// mod's own focus and Escape handling.
        public ExportLocationPicker(ExportFolder folder, string defaultFileName,
            bool acceptExactTypedName = false, string? fileNameControl = null,
            string? customDirControl = null, Action<ExportLocation>? locationChanged = null)
        {
            this.folder = folder;
            FileName = defaultFileName;
            this.acceptExactTypedName = acceptExactTypedName;
            this.fileNameControl = fileNameControl;
            this.customDirControl = customDirControl;
            this.locationChanged = locationChanged;
        }

        private static bool OnWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        /// The picked directory. Resolves shell folders, so call it outside
        /// steady draw passes only.
        public string ResolvedDir()
        {
            switch (Location)
            {
                case ExportLocation.Desktop: return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case ExportLocation.UserHome: return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case ExportLocation.Custom: return CustomDir.Trim();
                default: return folder.Folder;
            }
        }

        // Cache contract:
        // Owner: the dialog composing this picker.
        // Key: location, file name and custom directory (ordinal).
        // Value: resolved path, problem code and existence flag.
        // Dependencies: the key fields, platform special folders and
        //   filesystem existence sampled by Refresh.
        // Refresh policy: Refresh, called by the owner outside OnGUI (each
        //   WindowUpdate, or a queued update) once the key moved.
        // Equality policy: an unchanged key preserves the strings and avoids
        //   syscalls.
        // Teardown: dialog close releases the picker and its strings.
        private ExportLocation cachedLocation;
        private string? cachedFileName;
        private string? cachedCustomDir;
        private string? cachedPath;
        private ExportPathProblem cachedProblem;
        private bool cachedExists;
        private bool cacheValid;

        /// True while the sampled path state matches the current inputs.
        public bool IsCurrent =>
            cacheValid
            && cachedLocation == Location
            && string.Equals(cachedFileName, FileName, StringComparison.Ordinal)
            && string.Equals(cachedCustomDir, CustomDir, StringComparison.Ordinal);

        /// Path state from the last Refresh. Never resolves shell folders or
        /// touches the filesystem, so draw passes may read it.
        public string? CachedPath => cachedPath;
        public ExportPathProblem CachedProblem => cachedProblem;
        public bool CachedExists => cachedExists;

        /// Drops the sampled state; the next Refresh resamples it.
        public void Invalidate() => cacheValid = false;

        /// Resamples path state outside OnGUI when the inputs moved.
        public void Refresh()
        {
            if (IsCurrent) return;
            cachedLocation = Location;
            cachedFileName = FileName;
            cachedCustomDir = CustomDir;
            string dir = ResolvedDir();
            cachedPath = ExportPathRules.Compose(dir, FileName, addExtension: true,
                out cachedProblem);
            cachedExists = cachedPath != null && File.Exists(cachedPath);
            if (!cachedExists && acceptExactTypedName)
            {
                string? exact = ExportPathRules.Compose(dir, FileName, addExtension: false,
                    out _);
                if (exact != null && File.Exists(exact))
                {
                    cachedPath = exact;
                    cachedExists = true;
                }
            }
            cacheValid = true;
        }

        /// Location dropdown (+ file name field for export-style dialogs), and
        /// the Enter-path row (with a clear X) while Custom is picked.
        public void DrawLocationRows(Rect inRect, float locRowY, float customRowY,
            ExportPickerLabels labels, bool includeNameField = true)
        {
            var locRect = new Rect(inRect.x, locRowY, 170f, RowH - 6f);
            if (Widgets.ButtonText(locRect, labels.LocationLabel(Location)))
                OpenLocationMenu(labels);
            if (includeNameField)
            {
                if (fileNameControl != null) GUI.SetNextControlName(fileNameControl);
                FileName = ExportPathRules.StripFileName(Widgets.TextField(
                    new Rect(locRect.xMax + 8f, locRowY, inRect.width - locRect.width - 8f, RowH - 6f),
                    FileName));
            }

            if (Location == ExportLocation.Custom)
            {
                float labelW = labels.EnterPathWidth;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, customRowY, labelW, RowH - 6f), labels.EnterPath);
                Text.Anchor = TextAnchor.UpperLeft;
                if (customDirControl != null) GUI.SetNextControlName(customDirControl);
                CustomDir = ExportPathRules.StripDirectory(Widgets.TextField(
                    new Rect(inRect.x + labelW, customRowY, inRect.width - labelW - ClearW - 4f, RowH - 6f),
                    CustomDir));
                var clearRect = new Rect(inRect.xMax - ClearW,
                    customRowY + (RowH - 6f - ClearW) / 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    CustomDir = "";
            }
        }

        /// Built on click only, never in a steady pass.
        private void OpenLocationMenu(ExportPickerLabels labels)
        {
            var options = new List<FloatMenuOption>();
            foreach (var l in new[] { ExportLocation.GameData, ExportLocation.Desktop,
                ExportLocation.UserHome, ExportLocation.Custom })
            {
                if (l == ExportLocation.Desktop && !OnWindows) continue;
                var captured = l;
                options.Add(new FloatMenuOption(labels.LocationLabel(l), () =>
                {
                    Location = captured;
                    locationChanged?.Invoke(captured);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
