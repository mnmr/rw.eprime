using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Small per-player display options opened from the config dialog's
    /// Options button. Changes persist immediately. One medium-font title,
    /// then one section per option domain: counts, clicks, search, hover.
    public class Dialog_PanelOptions : Window
    {
        public Dialog_PanelOptions()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(380f, 470f);

        public override void DoWindowContents(Rect inRect)
        {
            using (new GuiStateScope())
            {
            var settings = EPrimeReadoutsMod.Settings;
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            try
            {

            var titleRect = listing.GetRect(32f);
            Text.Font = GameFont.Medium;
            GUI.color = EprStyle.HeaderText;
            Widgets.Label(titleRect, UiText.Get("EPR.Options"));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            SectionHeader(listing, "EPR.CountOptions");

            bool storageOnly = settings.searchStorageOnly;
            listing.CheckboxLabeled(UiText.Get("EPR.SearchStorageOnly"), ref storageOnly);
            if (storageOnly != settings.searchStorageOnly)
            {
                EPrimeReadoutsMod.Persist(s => s.searchStorageOnly = storageOnly);
                ReadoutPanel.BumpView();
            }

            bool hideForbidden = settings.searchHideForbidden;
            listing.CheckboxLabeled(UiText.Get("EPR.SearchHideForbidden"), ref hideForbidden);
            if (hideForbidden != settings.searchHideForbidden)
            {
                EPrimeReadoutsMod.Persist(s => s.searchHideForbidden = hideForbidden);
                ReadoutPanel.BumpView();
            }

            SectionHeader(listing, "EPR.ClickOptions");

            bool jumpCamera = settings.selectJumpCamera;
            listing.CheckboxLabeled(UiText.Get("EPR.SelectJumpCamera"), ref jumpCamera);
            if (jumpCamera != settings.selectJumpCamera)
                EPrimeReadoutsMod.Persist(s => s.selectJumpCamera = jumpCamera);

            SectionHeader(listing, "EPR.SearchOptions");

            bool showSearch = settings.showSearchFilter;
            listing.CheckboxLabeled(UiText.Get("EPR.ShowSearchFilter"), ref showSearch);
            if (showSearch != settings.showSearchFilter)
            {
                EPrimeReadoutsMod.Persist(s => s.showSearchFilter = showSearch);
                // A hidden filter must not keep filtering the panel.
                if (!showSearch) ReadoutPanel.SearchText = "";
                ReadoutPanel.BumpView();
            }

            // Nested sub-option directly below its parent: only meaningful
            // (and only shown) while the search field is hidden — the name
            // renders in the field's place.
            if (!settings.showSearchFilter)
            {
                bool showName = settings.showModNameWhenNoSearch;
                listing.Indent(16f);
                listing.ColumnWidth -= 16f;
                listing.CheckboxLabeled(UiText.Get("EPR.ShowModName"), ref showName);
                listing.ColumnWidth += 16f;
                listing.Outdent(16f);
                if (showName != settings.showModNameWhenNoSearch)
                {
                    EPrimeReadoutsMod.Persist(s => s.showModNameWhenNoSearch = showName);
                    ReadoutPanel.BumpView();
                }
            }

            bool hideZero = settings.searchHideZero;
            listing.CheckboxLabeled(UiText.Get("EPR.SearchHideZero"), ref hideZero);
            if (hideZero != settings.searchHideZero)
            {
                EPrimeReadoutsMod.Persist(s => s.searchHideZero = hideZero);
                ReadoutPanel.BumpView();
            }

            SectionHeader(listing, "EPR.HoverOptions");

            bool expandOnHover = settings.expandOnHover;
            listing.CheckboxLabeled(UiText.Get("EPR.ExpandOnHover"), ref expandOnHover);
            if (expandOnHover != settings.expandOnHover)
            {
                EPrimeReadoutsMod.Persist(s => s.expandOnHover = expandOnHover);
                ReadoutPanel.BumpView();
            }

            // Sub-option: only meaningful (and only shown) while the master
            // hover toggle is on.
            if (settings.expandOnHover)
            {
                bool collapseIdle = settings.collapseWhenIdle;
                listing.Indent(16f);
                listing.ColumnWidth -= 16f;
                listing.CheckboxLabeled(UiText.Get("EPR.CollapseWhenIdle"), ref collapseIdle);
                listing.ColumnWidth += 16f;
                listing.Outdent(16f);
                if (collapseIdle != settings.collapseWhenIdle)
                {
                    EPrimeReadoutsMod.Persist(s => s.collapseWhenIdle = collapseIdle);
                    ReadoutPanel.BumpView();
                }
            }

            }
            finally
            {
                listing.End();
            }
            }
        }

        private static void SectionHeader(Listing_Standard listing, string key)
        {
            listing.Gap(8f);
            var rect = listing.GetRect(28f);
            EprStyle.SectionHeader(rect.x, rect.y, rect.width, UiText.Get(key));
        }
    }
}
