using System;
using System.IO;
using System.Linq;
using System.Text;
using RimShared.UiLib;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// Translated strings for the plan import/export dialogs, resolved once
    /// per language revision so render passes never translate. Read-only
    /// during drawing. (Local to the picker dialogs; the main window keeps
    /// its own PlannerLabels.)
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: none (single snapshot of all keys).
    // Value: immutable translated strings.
    // Dependencies: UiVersion.LanguageCurrent.
    // Refresh policy: immediate rebuild on next Ensure() after the language
    //   revision moves.
    // Equality policy: unchanged language returns the same strings.
    // Teardown: none needed (bounded static strings; the stamp gate handles
    //   refreshes for the process lifetime).
    internal static class PlanIoLabels
    {
        private static int stamp = -1;

        internal static string ExportTitle = "";
        internal static string ImportTitle = "";
        internal static string CopyClipboard = "";
        internal static string FromClipboard = "";
        internal static string Save = "";
        internal static string Cancel = "";
        internal static string Back = "";
        internal static string Import = "";
        internal static string ImportAddNote = "";
        internal static string LocGameData = "";
        internal static string LocDesktop = "";
        internal static string LocUserHome = "";
        internal static string LocCustom = "";
        internal static string EnterPath = "";
        internal static string NoFiles = "";

        internal static void Ensure()
        {
            if (stamp == UiVersion.LanguageCurrent) return;
            stamp = UiVersion.LanguageCurrent;
            ExportTitle = "IMP_ExportPlansTitle".Translate();
            ImportTitle = "IMP_ImportPlansTitle".Translate();
            CopyClipboard = "IMP_CopyClipboard".Translate();
            FromClipboard = "IMP_FromClipboard".Translate();
            Save = "IMP_Save".Translate();
            Cancel = "IMP_Cancel".Translate();
            Back = "IMP_Back".Translate();
            Import = "IMP_Import".Translate();
            ImportAddNote = "IMP_ImportAddNote".Translate();
            LocGameData = "IMP_LocGameData".Translate();
            LocDesktop = "IMP_LocDesktop".Translate();
            LocUserHome = "IMP_LocUserHome".Translate();
            LocCustom = "IMP_LocCustom".Translate();
            EnterPath = "IMP_EnterPath".Translate();
            NoFiles = "IMP_NoFiles".Translate();
        }
    }

    /// Shared chrome and location/file plumbing for the plan export and
    /// import dialogs: title strip, body/footer geometry, a location dropdown
    /// (mod data folder under the game's save data root, Desktop, user home
    /// or a custom directory), a file name field, and an Enter-path row while
    /// Custom is picked. Adapted from EPrimeReadouts' Dialog_EprPreviewBase
    /// plus Dialog_EprFilePicker.
    public abstract class Dialog_PlanPickerBase : Window
    {
        protected const float TitleH = 38f;
        protected const float FooterH = 32f;
        protected const float FooterGap = 8f;
        protected const float ButtonW = 140f;
        protected const float RowH = 30f;

        protected static float CaptionRowH => Mathf.Max(22f, TinyText.LineHeight);

        protected enum Location { GameData, Desktop, UserHome, Custom }

        protected Location location = Location.GameData;
        protected string fileName = "Plans.xml";
        protected string customDir = "";

        protected Dialog_PlanPickerBase()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            draggable = true;
            forcePause = false;
            closeOnAccept = false;
        }

        private static bool OnWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;

        // ── Chrome ───────────────────────────────────────────────────────────

        /// Draws the (pre-translated) title and returns the Y just below it.
        protected static float DrawTitle(Rect inRect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
            Text.Font = GameFont.Small;
            return inRect.y + TitleH;
        }

        /// Footer Y: top of the footer button row.
        protected static float FooterY(Rect inRect) => inRect.yMax - FooterH;

        /// Tiny grey caption, matching the dialog captions elsewhere.
        protected static void DrawCaption(Rect rect, string text)
        {
            rect.height = Mathf.Max(22f, TinyText.LineHeight);
            GUI.color = PlannerStyle.CaptionText;
            Text.Anchor = TextAnchor.LowerLeft;
            TinyText.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// Inset panel behind list content; returns the inner content rect.
        /// Device-pixel-snapped frame in the shared panel palette — the
        /// vanilla outline helper bleeds past the fill at fractional scales.
        protected static Rect DrawFrame(Rect rect)
        {
            PixelBox.SolidWithOutline(rect,
                SegmentedControl.PanelBackground,
                SegmentedControl.PanelOutline);
            return rect.ContractedBy(6f);
        }

        // ── Location/path plumbing ───────────────────────────────────────────

        private static string LocationLabel(Location l) =>
            l == Location.Desktop ? PlanIoLabels.LocDesktop
            : l == Location.UserHome ? PlanIoLabels.LocUserHome
            : l == Location.Custom ? PlanIoLabels.LocCustom
            : PlanIoLabels.LocGameData;

        protected string ResolvedDir()
        {
            switch (location)
            {
                case Location.Desktop: return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case Location.UserHome: return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case Location.Custom: return customDir.Trim();
                default: return PlansFiles.Folder;
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

        /// Full destination, or null (with a reason) when not usable. Called
        /// only from the WindowUpdate-driven cache refresh, so translating the
        /// problem here never happens in a steady draw pass. The result uses
        /// the platform's directory separator throughout (game paths arrive
        /// with '/', Path.Combine joins with the native one — never mix them).
        protected string? ResolvedPath(out string? problem)
        {
            problem = null;
            string name = fileName.Trim();
            if (name.NullOrEmpty() || name.IndexOfAny(InvalidNameChars) >= 0)
            {
                problem = "IMP_BadFileName".Translate();
                return null;
            }
            string dir = ResolvedDir();
            if (dir.NullOrEmpty() || dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                problem = "IMP_BadDirectory".Translate();
                return null;
            }
            try
            {
                return Path.Combine(dir, name)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch (Exception) { problem = "IMP_BadDirectory".Translate(); return null; }
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
                    options.Add(new FloatMenuOption(LocationLabel(l), () => location = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (includeNameField)
            {
                fileName = Strip(Widgets.TextField(
                    new Rect(locRect.xMax + 8f, locRowY, inRect.width - locRect.width - 8f, RowH - 6f), fileName),
                    InvalidNameChars)!; // non-null for non-null input
            }

            if (location == Location.Custom)
            {
                string enterPath = PlanIoLabels.EnterPath;
                UiVersion.ObserveCurrentMetrics();
                float labelW = WrText.FitWidth(enterPath) + 6f;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, customRowY, labelW, RowH - 6f), enterPath);
                Text.Anchor = TextAnchor.UpperLeft;
                const float ClearW = 24f;
                customDir = Strip(Widgets.TextField(
                    new Rect(inRect.x + labelW, customRowY, inRect.width - labelW - ClearW - 4f, RowH - 6f), customDir),
                    InvalidDirChars)!; // non-null for non-null input
                var clearRect = new Rect(inRect.xMax - ClearW, customRowY + (RowH - 6f - ClearW) / 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                    customDir = "";
            }
        }

        // ── Shared plan preview listing ──────────────────────────────────────

        /// Detached, immutable projection of a plan list for the preview
        /// listings: names plus physical implant counts (each selected slot is
        /// one implant). Snapshot ownership — every field is copied out of the
        /// source plans at capture, so later model mutations can't reach it.
        protected sealed class PlanRows
        {
            public readonly string[] Names;
            public readonly int[] ImplantCounts;
            public readonly int ImplantTotal;

            private PlanRows(string[] names, int[] implantCounts, int implantTotal)
            {
                Names = names;
                ImplantCounts = implantCounts;
                ImplantTotal = implantTotal;
            }

            public int PlanCount => Names.Length;

            public static PlanRows Capture(System.Collections.Generic.IReadOnlyList<Plan> plans)
            {
                var names = new string[plans.Count];
                var counts = new int[plans.Count];
                int total = 0;
                for (int i = 0; i < plans.Count; i++)
                {
                    Plan plan = plans[i];
                    names[i] = plan.Name;
                    int count = 0;
                    for (int g = 0; g < plan.Implants.Count; g++)
                        count += plan.Implants[g].SlotOrdinals.Count;
                    counts[i] = count;
                    total += count;
                }
                return new PlanRows(names, counts, total);
            }
        }

        protected static float PreviewRowH => Mathf.Max(24f, TinyText.LineHeight + 4f);

        private static readonly Color RowStripe = new Color(1f, 1f, 1f, 0.03f);

        /// Simple read-only listing: one row per plan, name left (Small),
        /// pre-built implant-count caption right (Tiny). Bounded indexed
        /// iteration over already-built strings; no model access.
        protected static void DrawPlanListing(
            Rect listRect, PlanRows rows, string[] captions, ref Vector2 scroll)
        {
            if (listRect.height <= 0f || rows.PlanCount == 0) return;

            float rowH = PreviewRowH;
            float totalH = rows.PlanCount * rowH;
            bool needsBar = totalH > listRect.height;
            var viewRect = new Rect(0f, 0f,
                listRect.width - (needsBar ? GenUI.ScrollBarWidth : 0f), totalH);

            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            try
            {
                for (int i = 0; i < rows.PlanCount; i++)
                {
                    var rowRect = new Rect(0f, i * rowH, viewRect.width, rowH);
                    if (i % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, RowStripe);

                    float captionW = rowRect.width * 0.35f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y,
                        rowRect.width - captionW - 12f, rowH), rows.Names[i]);

                    GUI.color = PlannerStyle.CaptionText;
                    Text.Anchor = TextAnchor.MiddleRight;
                    TinyText.Label(new Rect(rowRect.xMax - captionW - 4f, rowRect.y,
                        captionW, rowH), captions[i]);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}
