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

        // Owner: import dialog. Key: explicit open/mouse-down clipboard refresh.
        // Value: immutable clipboard string plus a scalar format precheck.
        // Dependencies: the external OS clipboard. Refresh: event-driven outside
        // OnGUI, never per repaint. Equality: equal clipboard text preserves the
        // published string identity and usability result. Teardown: dialog close
        // releases the snapshot and cached refresh delegate.
        private string clip;
        private bool clipUsable;
        private readonly System.Action refreshClipboardAction;

        // Owner: import dialog. Key: one outstanding explicit import action.
        // Value: immutable XML/path command inputs and a cached noncapturing
        // delegate. Dependencies: the clicked source and completion state.
        // Refresh: event-driven; exactly one command is queued outside OnGUI.
        // Equality: duplicate clicks while pending reuse the existing command.
        // Teardown: completion clears inputs; dialog close releases the instance.
        private readonly System.Action importPendingAction;
        private string pendingImportPath;
        private string pendingImportXml;
        private bool importPending;
        // Owner: import dialog. Key: LanguageChangeCoordinator.Revision. Value:
        // immutable translated labels. Dependencies: language only. Refresh:
        // immediately on the next draw after revision change. Equality: matching
        // revision preserves all string identities. Teardown: dialog close
        // releases the single-slot label cache.
        private int textLanguageRevision = -1;
        private string titleLabel;
        private string clipboardLabel;
        private string locationCaption;
        private string cancelLabel;
        private string importLabel;

        public Dialog_ImportSource()
        {
            refreshClipboardAction = RefreshClipboard;
            importPendingAction = ImportPending;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshClipboard();
        }

        private void RefreshClipboard()
        {
            string current = GUIUtility.systemCopyBuffer;
            if (string.Equals(clip, current, System.StringComparison.Ordinal))
                return;
            clip = current;
            clipUsable = !clip.NullOrEmpty() && clip.Contains("<WorkRoles");
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = new GuiStateScope(capture: true);
            EnsureTextCache();
            if (Event.current.type == EventType.MouseDown)
                WorkRolesGameComponent.RunOutsideOnGUI(
                    refreshClipboardAction);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y,
                inRect.width, TitleH), titleLabel);
            Text.Font = GameFont.Small;

            // From Clipboard top-right, mirroring export's Copy to Clipboard.
            // A quick sniff (the root element's name) gates it — parsing every
            // frame would be waste, and arbitrary clipboard text stays out.
            var clipRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, ButtonH);
            if (!clipUsable)
                WrTips.Key("WR_ImportClipboardInvalid").Region(clipRect);
            if (Widgets.ButtonText(clipRect, clipboardLabel,
                    active: clipUsable && !importPending)
                && clipUsable)
                QueueImport(xml: clip, path: null);

            // Bottom-up: Cancel/Import row, optional custom-dir row, location row.
            float btnY = inRect.yMax - ButtonH;
            float customRowY = btnY - 8f - (location == Location.Custom ? RowH : 0f);
            float locRowY = customRowY - RowH;
            float captionRowY = locRowY - CaptionRowH;

            DrawCaption(new Rect(inRect.x, captionRowY, 200f,
                CaptionRowH - 2f), locationCaption);
            DrawLocationRows(inRect, locRowY, customRowY);

            string path = CachedResolvedPath(out string problem, out bool exists);

            var cancelRect = new Rect(inRect.x, btnY, ButtonW, ButtonH);
            var importRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, cancelLabel))
                Close();
            if (problem != null)
                TooltipHandler.TipRegion(importRect, problem);
            else if (!exists && Mouse.IsOver(importRect))
                WrTips.Key("WR_ImportFileMissing", path).Region(importRect);
            if (Widgets.ButtonText(importRect, importLabel,
                    active: path != null && !importPending)
                && path != null)
                QueueImport(xml: null, path: path);
        }

        private void EnsureTextCache()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (textLanguageRevision == revision) return;
            textLanguageRevision = revision;
            titleLabel = "WR_ImportTitle".Translate().ToString();
            clipboardLabel = "WR_ImportFromClipboard".Translate().ToString();
            locationCaption = "WR_ImportLocationLabel".Translate().ToString();
            cancelLabel = "WR_Cancel".Translate().ToString();
            importLabel = "WR_Import".Translate().ToString();
        }

        private void QueueImport(string xml, string path)
        {
            if (importPending) return;
            importPending = true;
            pendingImportXml = xml;
            pendingImportPath = path;
            WorkRolesGameComponent.RunOutsideOnGUI(importPendingAction);
        }

        private void ImportPending()
        {
            string xml = pendingImportXml;
            string path = pendingImportPath;
            pendingImportXml = null;
            pendingImportPath = null;
            importPending = false;
            if (xml == null)
            {
                try { xml = File.ReadAllText(path); }
                catch (System.Exception error)
                {
                    Messages.Message(
                        "WR_ImportParseFailed".Translate(error.Message),
                        MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }
            if (TryOpenPreview(xml)) Close();
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
