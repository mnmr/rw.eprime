using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Small per-player display options opened from the config dialog's
    /// Options button. Changes persist immediately. One medium-font title,
    /// then one section per option domain: counts, clicks, search, hover.
    public class Dialog_PanelOptions : Window
    {
        private readonly Listing_Standard listing = new Listing_Standard();

        public Dialog_PanelOptions()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(380f, 660f);

        public override void DoWindowContents(Rect inRect)
        {
            using (new GuiStateScope())
            {
            var settings = EPrimeReadoutsMod.Settings;
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
            if (CheckboxRow(listing, "EPR.SearchStorageOnly",
                    "EPR.SearchStorageOnlyTip", ref storageOnly))
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

            SectionHeader(listing, "EPR.PlannedWorkOptions");

            // Every reservation narrows the counters the same way the count
            // options do; the snapshot rebuilds at once so a toggle is visible
            // even while the game is paused.
            bool reserveBills = settings.reserveForBills;
            if (CheckboxRow(listing, "EPR.ReserveForBills",
                "EPR.ReserveForBillsTip", ref reserveBills))
            {
                EPrimeReadoutsMod.Persist(s => s.reserveForBills = reserveBills);
                ReadoutPanel.BumpView();
            }

            bool reserveBuildables = settings.reserveForBuildables;
            if (CheckboxRow(listing, "EPR.ReserveForBuildables",
                "EPR.ReserveForBuildablesTip", ref reserveBuildables))
            {
                EPrimeReadoutsMod.Persist(s => s.reserveForBuildables = reserveBuildables);
                ReadoutPanel.BumpView();
            }

            bool showNegative = settings.showNegativeCounts;
            if (CheckboxRow(listing, "EPR.ShowNegativeCounts",
                "EPR.ShowNegativeCountsTip", ref showNegative))
            {
                EPrimeReadoutsMod.Persist(s => s.showNegativeCounts = showNegative);
                ReadoutPanel.BumpView();
            }

            // Without a working Quality Jobs integration there is no quality
            // target to rework for, so the row is inert and says why on hover
            // rather than disappearing. The two failure modes read differently:
            // the mod is absent, or it is present but too old to answer.
            bool qualityReady = QualityJobsBridge.Available;
            string qualityTip = qualityReady
                ? "EPR.QualityJobsReworkTip"
                : QualityJobsBridge.Installed
                    ? "EPR.QualityJobsOutdatedTip"
                    : "EPR.QualityJobsMissingTip";
            bool qualityRework = settings.qualityJobsRework;
            if (CheckboxRow(listing, "EPR.QualityJobsRework", qualityTip,
                    ref qualityRework, disabled: !qualityReady))
            {
                EPrimeReadoutsMod.Persist(s => s.qualityJobsRework = qualityRework);
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

        /// One always-tooltipped option row. Every row here shares the same
        /// single-line height so the checkboxes align exactly down the section.
        /// A disabled row still draws and still explains itself on hover, but
        /// swallows clicks and greys its label. Returns true when the player
        /// changed the value.
        private static bool CheckboxRow(
            Listing_Standard listing, string labelKey, string tooltipKey,
            ref bool value, bool disabled = false)
        {
            Rect rect = listing.GetRect(Text.LineHeight);
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            WrTips.Key(tooltipKey).Region(rect);
            bool before = value;
            if (disabled) GUI.color = EprStyle.CaptionText;
            Widgets.CheckboxLabeled(rect, UiText.Get(labelKey), ref value, disabled);
            if (disabled) GUI.color = Color.white;
            listing.Gap(listing.verticalSpacing);
            return value != before;
        }
    }
}
