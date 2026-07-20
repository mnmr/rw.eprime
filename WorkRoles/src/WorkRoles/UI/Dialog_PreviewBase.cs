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

        private int previewLanguageRevision = -1;
        private string selectAllLabel;
        private string cancelLabel;
        private string applyLabel;

        protected Dialog_PreviewBase()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        /// Refreshes common translated chrome once per completed language load.
        /// Returns true so derived dialogs can invalidate translated snapshots.
        protected bool ObservePreviewLanguageRevision()
        {
            int current = LanguageChangeCoordinator.Revision;
            if (previewLanguageRevision == current) return false;
            previewLanguageRevision = current;
            selectAllLabel = "WR_SelectAll".Translate();
            cancelLabel = "WR_Cancel".Translate();
            applyLabel = "WR_Apply".Translate();
            return true;
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

        protected float DrawCachedPreviewTitle(Rect inRect, string title)
        {
            ObservePreviewLanguageRevision();
            if (Event.current.type == EventType.Repaint)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y,
                    inRect.width, PreviewTitleHeight), title);
            }
            Text.Font = GameFont.Small;
            return inRect.y + PreviewTitleHeight;
        }

        protected bool DrawCachedPreviewSelectAll(Rect inRect, float y, bool selected)
        {
            ObservePreviewLanguageRevision();
            bool toggled = selected;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, 160f, 24f),
                selectAllLabel, ref toggled);
            return toggled;
        }

        protected static Rect PreviewBodyRect(Rect inRect, float top) =>
            new Rect(inRect.x, top, inRect.width,
                inRect.yMax - top - PreviewButtonHeight - FooterGap);

        /// Draws Cancel and Apply. Cancel closes immediately; true means the
        /// enabled Apply button was pressed and the subclass should apply.
        protected bool DrawPreviewFooter(Rect inRect, bool canApply)
        {
            ObservePreviewLanguageRevision();
            float y = inRect.yMax - PreviewButtonHeight;
            var applyRect = new Rect(inRect.xMax - PreviewButtonWidth, y,
                PreviewButtonWidth, PreviewButtonHeight);
            var cancelRect = new Rect(applyRect.x - FooterGap - PreviewButtonWidth, y,
                PreviewButtonWidth, PreviewButtonHeight);
            if (Widgets.ButtonText(cancelRect, cancelLabel))
            {
                Close();
                return false;
            }
            return Widgets.ButtonText(applyRect, applyLabel, active: canApply)
                && canApply;
        }
    }
}
