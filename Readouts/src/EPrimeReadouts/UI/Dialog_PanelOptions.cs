using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Small per-player display options opened from the config dialog's
    /// Options button. Changes persist immediately.
    public class Dialog_PanelOptions : Window
    {
        public Dialog_PanelOptions()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(380f, 190f);

        public override void DoWindowContents(Rect inRect)
        {
            using (new GuiStateScope())
            {
            var settings = EPrimeReadoutsMod.Settings;
            float headerUsed = EprStyle.SectionHeader(inRect.x, inRect.y, inRect.width,
                UiText.Get("EPR.Options"));
            var listing = new Listing_Standard();
            listing.Begin(new Rect(inRect.x, inRect.y + headerUsed + 4f,
                inRect.width, inRect.height - headerUsed - 4f));
            try
            {

            bool showSearch = settings.showSearchFilter;
            listing.CheckboxLabeled(UiText.Get("EPR.ShowSearchFilter"), ref showSearch);
            if (showSearch != settings.showSearchFilter)
            {
                EPrimeReadoutsMod.Persist(s => s.showSearchFilter = showSearch);
                // A hidden filter must not keep filtering the panel.
                if (!showSearch) ReadoutPanel.SearchText = "";
                ReadoutPanel.BumpView();
            }

            bool showName = settings.showModNameWhenNoSearch;
            listing.CheckboxLabeled(UiText.Get("EPR.ShowModName"), ref showName);
            if (showName != settings.showModNameWhenNoSearch)
            {
                EPrimeReadoutsMod.Persist(s => s.showModNameWhenNoSearch = showName);
                ReadoutPanel.BumpView();
            }

            }
            finally
            {
                listing.End();
            }
            }
        }
    }
}
