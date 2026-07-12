using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Where to import roles from: the export dialog's location picker in
    /// reverse (location + file name, or a custom path), plus the clipboard —
    /// enabled only while it plausibly holds a WorkRoles export.
    public class Dialog_ImportSource : Dialog_RoleFilePicker
    {
        private const float TitleH = 38f;

        public override Vector2 InitialSize => new Vector2(560f, 250f);

        public Dialog_ImportSource()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_ImportTitle".Translate());
            Text.Font = GameFont.Small;

            // From Clipboard top-right, mirroring export's Copy to Clipboard.
            // A quick sniff (the root element's name) gates it — parsing every
            // frame would be waste, and arbitrary clipboard text stays out.
            string clip = GUIUtility.systemCopyBuffer;
            bool clipUsable = !clip.NullOrEmpty() && clip.Contains("<WorkRoles");
            var clipRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, ButtonH);
            if (!clipUsable)
                TooltipHandler.TipRegion(clipRect, "WR_ImportClipboardInvalid".Translate());
            if (Widgets.ButtonText(clipRect, "WR_ImportFromClipboard".Translate(), active: clipUsable)
                && clipUsable && TryOpenPreview(clip))
                Close();

            // Bottom-up: Cancel/Import row, optional custom-dir row, location row.
            float btnY = inRect.yMax - ButtonH;
            float customRowY = btnY - 8f - (location == Location.Custom ? RowH : 0f);
            float locRowY = customRowY - RowH;
            float captionRowY = locRowY - CaptionRowH;

            DrawCaption(new Rect(inRect.x, captionRowY, 200f, CaptionRowH - 2f), "WR_ImportLocationLabel".Translate());
            DrawLocationRows(inRect, locRowY, customRowY);

            string path = ResolvedPath(out string problem);
            bool exists = path != null && File.Exists(path);

            var cancelRect = new Rect(inRect.x, btnY, ButtonW, ButtonH);
            var importRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            if (problem != null)
                TooltipHandler.TipRegion(importRect, problem);
            else if (!exists)
                TooltipHandler.TipRegion(importRect, "WR_ImportFileMissing".Translate(path));
            if (Widgets.ButtonText(importRect, "WR_Import".Translate(), active: exists) && exists)
            {
                string xml;
                try { xml = File.ReadAllText(path); }
                catch (System.Exception e)
                {
                    Messages.Message("WR_ImportParseFailed".Translate(e.Message),
                        MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                if (TryOpenPreview(xml))
                    Close();
            }
        }

        /// Parses and opens the merge/overwrite preview; false (with a message)
        /// when the text isn't a usable export.
        private static bool TryOpenPreview(string xml)
        {
            var doc = RoleIO.Parse(xml);
            if (doc.error != null)
            {
                Messages.Message("WR_ImportParseFailed".Translate(doc.error),
                    MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            Find.WindowStack.Add(new Dialog_ImportPreview(xml, doc));
            return true;
        }
    }
}
