using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Slim dialog chrome shared by the export and import preview dialogs:
    /// title strip (Medium font), body region, footer button row.
    /// Ported from WorkRoles Dialog_PreviewBase, adapted to EPR naming.
    public abstract class Dialog_EprPreviewBase : Window
    {
        protected const float TitleH        = 38f;
        protected const float FooterH       = 32f;
        protected const float FooterGap     = 8f;
        protected const float ButtonW       = 140f;

        protected Dialog_EprPreviewBase()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside   = false;
            doCloseX                = true;
            draggable               = true;
            forcePause              = false;
        }

        /// Draws the title and returns the Y coordinate just below it.
        protected float DrawTitle(Rect inRect, string titleKey)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH),
                UiText.Get(titleKey));
            Text.Font = GameFont.Small;
            return inRect.y + TitleH;
        }

        /// Body rect: between <paramref name="bodyTop"/> and the top of the footer.
        protected Rect BodyRect(Rect inRect, float bodyTop) =>
            new Rect(inRect.x, bodyTop, inRect.width,
                inRect.yMax - bodyTop - FooterH - FooterGap);

        /// Footer Y: top of the footer button row.
        protected float FooterY(Rect inRect) => inRect.yMax - FooterH;
    }
}
