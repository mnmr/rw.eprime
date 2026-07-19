using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Shared chrome for selectable preview dialogs. Subclasses retain their
    /// own body, selection model, staleness checks and apply operation.
    public abstract class Dialog_PreviewBase : Window
    {
        protected const float PreviewTitleHeight = 38f;
        protected const float PreviewSelectRowHeight = 26f;
        protected const float PreviewButtonWidth = 120f;
        protected const float PreviewButtonHeight = 32f;
        private const float FooterGap = 8f;

        protected Dialog_PreviewBase()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        protected static float DrawPreviewTitle(Rect inRect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y,
                inRect.width, PreviewTitleHeight), title);
            Text.Font = GameFont.Small;
            return inRect.y + PreviewTitleHeight;
        }

        protected static bool DrawPreviewSelectAll(Rect inRect, float y, bool selected)
        {
            bool toggled = selected;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, 160f, 24f),
                "WR_SelectAll".Translate(), ref toggled);
            return toggled;
        }

        protected static Rect PreviewBodyRect(Rect inRect, float top) =>
            new Rect(inRect.x, top, inRect.width,
                inRect.yMax - top - PreviewButtonHeight - FooterGap);

        /// Draws Cancel and Apply. Cancel closes immediately; true means the
        /// enabled Apply button was pressed and the subclass should apply.
        protected bool DrawPreviewFooter(Rect inRect, bool canApply)
        {
            float y = inRect.yMax - PreviewButtonHeight;
            var applyRect = new Rect(inRect.xMax - PreviewButtonWidth, y,
                PreviewButtonWidth, PreviewButtonHeight);
            var cancelRect = new Rect(applyRect.x - FooterGap - PreviewButtonWidth, y,
                PreviewButtonWidth, PreviewButtonHeight);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
            {
                Close();
                return false;
            }
            return Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply)
                && canApply;
        }
    }
}
