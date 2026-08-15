using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shows the export XML before anything is written. Copy it to the clipboard,
    /// or save it as <filename> into a picked location: the mod's folder under the
    /// game data root, Desktop (Windows), the user home, or a custom directory.
    public class Dialog_ExportPreview : Dialog_RoleFilePicker
    {
        private const float TitleH = 38f;

        private readonly string xml;
        private Vector2 scroll;
        // Owner: export dialog. Key: (XML value fixed at construction, exact
        // text-area view width, read-only text-area style). Value: measured text
        // height scalar. Dependencies: XML, font/style, UI scale, and available
        // width. Refresh: immediate inside the width-change cache gate. Equality:
        // unchanged width/style/XML reuse the height without allocation or
        // measurement. Teardown: dialog close releases the single-slot cache.
        private float measuredWidth = -1f;
        private float textHeight;

        // Owner: dialog. Key: LanguageChangeCoordinator.Revision. Value: all
        // static translated labels/messages and the Small-font Copy Path
        // width. Dependencies: language and font. Refresh: immediately on
        // revision change. Equality: matching revision reuses strings/width.
        // Teardown: dialog close releases the instance and cached delegate.
        private int textLanguageRevision = -1;
        private string titleLabel;
        private string copyClipboardLabel;
        private string copiedToClipboardMessage;
        private string exportLocationLabel;
        private string copyPathLabel;
        private float copyPathLabelWidth;
        private string cancelLabel;
        private string saveLabel;
        private readonly System.Action savePendingAction;
        private string pendingSavePath;

        public override Vector2 InitialSize => new Vector2(680f, 660f);

        public Dialog_ExportPreview(string xml)
        {
            this.xml = xml;
            savePendingAction = SavePending;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = new GuiStateScope(capture: true);
            EnsureTextCache();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(
                inRect.x, inRect.y, inRect.width, TitleH), titleLabel);
            Text.Font = GameFont.Small;

            // Copy to Clipboard lives top-right, beside the title: it acts on the
            // XML above it, not on the save controls below.
            var copyRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, ButtonH);
            if (Widgets.ButtonText(copyRect, copyClipboardLabel))
            {
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message(copiedToClipboardMessage,
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }

            // Bottom-up layout: Cancel/Save row, optional custom-dir row,
            // location+filename row, caption/Copy Path link row.
            float btnY = inRect.yMax - ButtonH;
            float customRowY = btnY - 8f - (location == Location.Custom ? RowH : 0f);
            float locRowY = customRowY - RowH;
            float captionRowY = locRowY - CaptionRowH;

            // DevGUI.TextAreaScrollable measures at full width in the label style yet
            // renders scrollbar-narrowed in the text-area style, clipping the tail:
            // measure with the exact render style at the exact render width instead.
            float textTop = inRect.y + TitleH;
            var outRect = new Rect(inRect.x, textTop, inRect.width, captionRowY - 6f - textTop);
            float viewWidth = outRect.width - GenUI.ScrollBarWidth;
            var style = Text.CurTextAreaReadOnlyStyle;
            if (measuredWidth != viewWidth)
            {
                textHeight = style.CalcHeight(new GUIContent(xml), viewWidth);
                measuredWidth = viewWidth;
            }
            var viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(textHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                GUI.TextArea(viewRect, xml, style);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            string path = CachedResolvedPath(out string problem, out _);

            DrawCaption(new Rect(inRect.x, captionRowY, 200f,
                CaptionRowH - 2f), exportLocationLabel);

            // Copy Path: a link (no button chrome), right-aligned over the file
            // name it copies. With nothing to copy it CLEARS the clipboard, so a
            // paste can't insert stale content.
            var linkRect = new Rect(inRect.xMax - copyPathLabelWidth,
                captionRowY, copyPathLabelWidth, CaptionRowH - 4f);
            if (problem != null)
                TooltipHandler.TipRegion(linkRect, problem);
            if (Widgets.ButtonText(linkRect, copyPathLabel, drawBackground: false))
            {
                GUIUtility.systemCopyBuffer = path ?? "";
                if (path != null)
                    Messages.Message(copiedToClipboardMessage,
                        MessageTypeDefOf.PositiveEvent, historical: false);
            }

            DrawLocationRows(inRect, locRowY, customRowY);

            // Bottom row: Cancel escapes on the left, Save commits on the right.
            var cancelRect = new Rect(inRect.x, btnY, ButtonW, ButtonH);
            var saveRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, cancelLabel))
                Close();
            if (problem != null)
                TooltipHandler.TipRegion(saveRect, problem);
            if (Widgets.ButtonText(saveRect, saveLabel, active: path != null)
                && path != null)
            {
                pendingSavePath = path;
                WorkRolesGameComponent.RunOutsideOnGUI(savePendingAction);
            }
        }

        private void EnsureTextCache()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (textLanguageRevision == revision) return;
            textLanguageRevision = revision;
            titleLabel = "WR_ExportTitle".Translate().ToString();
            copyClipboardLabel = "WR_CopyClipboard".Translate().ToString();
            copiedToClipboardMessage =
                "WR_CopiedToClipboard".Translate().ToString();
            exportLocationLabel =
                "WR_ExportLocationLabel".Translate().ToString();
            copyPathLabel = "WR_CopyPath".Translate().ToString();
            cancelLabel = "WR_Cancel".Translate().ToString();
            saveLabel = "WR_Save".Translate().ToString();
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                copyPathLabelWidth = WrText.FitWidth(copyPathLabel) + 8f;
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private void SavePending()
        {
            string path = pendingSavePath;
            pendingSavePath = null;
            if (path == null) return;
            string error = RoleIO.SaveTo(path, xml);
            if (error == null)
            {
                Messages.Message("WR_ExportSaved".Translate(path),
                    MessageTypeDefOf.PositiveEvent, historical: false);
                Close();
            }
            else
            {
                Messages.Message("WR_ExportFailed".Translate(error),
                    MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }
}
