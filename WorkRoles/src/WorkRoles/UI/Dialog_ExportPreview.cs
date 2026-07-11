using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shows the export XML before anything is written. Copy it to the clipboard,
    /// or save it as <filename> into a picked location: the mod's folder under the
    /// game data root, Desktop (Windows), the user home, or a custom directory.
    public class Dialog_ExportPreview : Window
    {
        private enum Location { GameData, Desktop, UserHome, Custom }

        private const float TitleH = 38f;
        private const float RowH = 30f;
        private const float ButtonW = 150f;
        private const float ButtonH = 32f;

        private readonly string xml;
        private string fileName = RoleIO.DefaultFileName;
        private Location location = Location.GameData;
        private string customDir = "";
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(680f, 660f);

        public Dialog_ExportPreview(string xml)
        {
            this.xml = xml;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

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

        /// Full destination, or null (with a reason) when not saveable.
        private string ResolvedPath(out string problem)
        {
            problem = null;
            string name = fileName.Trim();
            if (name.NullOrEmpty() || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
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
            try { return Path.Combine(dir, name); }
            catch (Exception) { problem = "WR_BadDirectory".Translate(); return null; }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_ExportTitle".Translate());
            Text.Font = GameFont.Small;

            // Bottom-up layout: buttons, optional custom-dir row, location row.
            float btnY = inRect.yMax - ButtonH;
            float customRowY = btnY - 8f - (location == Location.Custom ? RowH : 0f);
            float locRowY = customRowY - RowH;

            float textTop = inRect.y + TitleH;
            DevGUI.TextAreaScrollable(new Rect(inRect.x, textTop, inRect.width, locRowY - 6f - textTop),
                xml, ref scroll, readOnly: true);

            // Location dropdown + filename + Copy Path.
            var locRect = new Rect(inRect.x, locRowY, 170f, RowH - 6f);
            if (Widgets.ButtonText(locRect, LocationLabel(location)))
            {
                var options = new List<FloatMenuOption>();
                foreach (var l in new[] { Location.GameData, Location.Desktop, Location.UserHome, Location.Custom })
                {
                    if (l == Location.Desktop && !OnWindows) continue;
                    var captured = l;
                    options.Add(new FloatMenuOption(LocationLabel(l), () => location = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            fileName = Widgets.TextField(new Rect(locRect.xMax + 8f, locRowY, inRect.width - locRect.width - 8f - 110f - 8f, RowH - 6f), fileName);
            var copyPathRect = new Rect(inRect.xMax - 110f, locRowY, 110f, RowH - 6f);
            string path = ResolvedPath(out string problem);
            if (Widgets.ButtonText(copyPathRect, "WR_CopyPath".Translate()) && path != null)
            {
                GUIUtility.systemCopyBuffer = path;
                Messages.Message("WR_CopiedToClipboard".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
            }

            if (location == Location.Custom)
                customDir = Widgets.TextField(new Rect(inRect.x, customRowY, inRect.width, RowH - 6f), customDir);

            var saveRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            var copyRect = new Rect(saveRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            var cancelRect = new Rect(copyRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            if (Widgets.ButtonText(copyRect, "WR_CopyClipboard".Translate()))
            {
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("WR_CopiedToClipboard".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
            }
            if (problem != null)
                TooltipHandler.TipRegion(saveRect, problem);
            if (Widgets.ButtonText(saveRect, "WR_Save".Translate(), active: path != null) && path != null)
            {
                string error = RoleIO.SaveTo(path, xml);
                if (error == null)
                {
                    Messages.Message("WR_ExportSaved".Translate(path), MessageTypeDefOf.PositiveEvent, historical: false);
                    Close();
                }
                else
                {
                    Messages.Message("WR_ExportFailed".Translate(error), MessageTypeDefOf.RejectInput, historical: false);
                }
            }
        }
    }
}
