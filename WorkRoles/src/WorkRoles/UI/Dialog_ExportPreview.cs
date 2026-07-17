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
        private float measuredWidth = -1f;
        private float textHeight;

        public override Vector2 InitialSize => new Vector2(680f, 660f);

        public Dialog_ExportPreview(string xml)
        {
            this.xml = xml;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), "WR_ExportTitle".Translate());
            Text.Font = GameFont.Small;

            // Copy to Clipboard lives top-right, beside the title: it acts on the
            // XML above it, not on the save controls below.
            var copyRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, ButtonH);
            if (Widgets.ButtonText(copyRect, "WR_CopyClipboard".Translate()))
            {
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("WR_CopiedToClipboard".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
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
            GUI.TextArea(viewRect, xml, style);
            Widgets.EndScrollView();

            string path = ResolvedPath(out string problem);

            DrawCaption(new Rect(inRect.x, captionRowY, 200f, CaptionRowH - 2f), "WR_ExportLocationLabel".Translate());

            // Copy Path: a link (no button chrome), right-aligned over the file
            // name it copies. With nothing to copy it CLEARS the clipboard, so a
            // paste can't insert stale content.
            string copyPathLabel = "WR_CopyPath".Translate();
            float linkW = Text.CalcSize(copyPathLabel).x + 8f;
            var linkRect = new Rect(inRect.xMax - linkW, captionRowY, linkW, CaptionRowH - 4f);
            if (problem != null)
                TooltipHandler.TipRegion(linkRect, problem);
            if (Widgets.ButtonText(linkRect, copyPathLabel, drawBackground: false))
            {
                GUIUtility.systemCopyBuffer = path ?? "";
                if (path != null)
                    Messages.Message("WR_CopiedToClipboard".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
            }

            DrawLocationRows(inRect, locRowY, customRowY);

            // Bottom row: Cancel escapes on the left, Save commits on the right.
            var cancelRect = new Rect(inRect.x, btnY, ButtonW, ButtonH);
            var saveRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
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
