using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The Options tab: per-player display options in two columns, one
    /// section per option domain. Counts and planned work on the left;
    /// clicks, search, and hover on the right. Changes persist immediately.
    internal sealed class OptionsTabView
    {
        private const float ColumnGap = 20f;
        private const float Indent = 16f;

        private readonly Listing_Standard left = new Listing_Standard();
        private readonly Listing_Standard right = new Listing_Standard();

        internal void Draw(Rect rect)
        {
            ReadoutSettings settings = EPrimeReadoutsMod.Settings;
            float columnW = Mathf.Floor((rect.width - ColumnGap) / 2f);
            var leftRect = new Rect(rect.x, rect.y, columnW, rect.height);
            var rightRect = new Rect(leftRect.xMax + ColumnGap, rect.y,
                columnW, rect.height);

            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                left.Begin(leftRect);
                try
                {
                    DrawCountOptions(left, settings);
                    DrawPlannedWorkOptions(left, settings);
                    DrawCompatibilityOptions(left, settings);
                }
                finally
                {
                    left.End();
                }

                right.Begin(rightRect);
                try
                {
                    DrawDisplayOptions(right, settings);
                    DrawClickOptions(right, settings);
                    DrawSearchOptions(right, settings);
                    DrawHoverOptions(right, settings);
                }
                finally
                {
                    right.End();
                }
            }
        }

        private static void DrawCountOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
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
        }

        private static void DrawPlannedWorkOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
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
        }

        private static void DrawDisplayOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.DisplayOptions");

            // The panel observes the game's categorized-readout preference
            // every draw, so the kept toggle takes effect at once.
            bool keepToggle = settings.keepReadoutToggle;
            if (CheckboxRow(listing, "EPR.KeepReadoutToggle",
                    "EPR.KeepReadoutToggleTip", ref keepToggle))
            {
                EPrimeReadoutsMod.Persist(s => s.keepReadoutToggle = keepToggle);
                ReadoutPanel.BumpView();
            }

            DrawTierLayoutRow(listing, settings);
        }

        private static void DrawCompatibilityOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.CompatibilityOptions");

            // The panel observes the switch on its next draw: off releases
            // the cached surfaces and draws directly, on rebuilds them.
            bool buffered = settings.bufferedRendering;
            if (CheckboxRow(listing, "EPR.BufferedRendering",
                    "EPR.BufferedRenderingTip", ref buffered))
                EPrimeReadoutsMod.Persist(s => s.bufferedRendering = buffered);
        }

        /// Reused label slots for the tier layout segmented row, so the
        /// render pass hands SegmentedControl a stable array of cached
        /// translated strings.
        private static readonly string[] tierLayoutLabels = new string[2];
        private const float SegmentRowH = 28f;
        /// Gap between the tier layout label and its selector.
        private const float RuleGapX = 8f;

        /// One Display Options row: "Tier layout" label on the left, the
        /// Horizontal / Vertical selector filling the rest of the row.
        private static void DrawTierLayoutRow(
            Listing_Standard listing, ReadoutSettings settings)
        {
            tierLayoutLabels[0] = UiText.Get("EPR.TierLayoutHorizontal");
            tierLayoutLabels[1] = UiText.Get("EPR.TierLayoutVertical");
            string label = UiText.Get("EPR.TierLayoutOptions");
            Rect rowRect = listing.GetRect(SegmentRowH);
            WrTips.Key("EPR.TierLayoutTip").Region(rowRect);
            float labelW = WrText.FitWidth(label) + RuleGapX;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rowRect.x, rowRect.y, labelW, rowRect.height), label);
            Text.Anchor = anchor;
            var selectorRect = new Rect(rowRect.x + labelW, rowRect.y,
                Mathf.Max(1f, rowRect.width - labelW), rowRect.height);
            int active = settings.verticalTiers ? 1 : 0;
            int clicked = SegmentedControl.Row(selectorRect, tierLayoutLabels, active);
            if (clicked >= 0 && clicked != active)
            {
                bool vertical = clicked == 1;
                EPrimeReadoutsMod.Persist(s => s.verticalTiers = vertical);
                ReadoutPanel.BumpView();
            }
            listing.Gap(listing.verticalSpacing);
        }

        private static void DrawClickOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.ClickOptions");

            bool jumpCamera = settings.selectJumpCamera;
            listing.CheckboxLabeled(UiText.Get("EPR.SelectJumpCamera"), ref jumpCamera);
            if (jumpCamera != settings.selectJumpCamera)
                EPrimeReadoutsMod.Persist(s => s.selectJumpCamera = jumpCamera);
        }

        private static void DrawSearchOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
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
            // (and only shown) while the search field is hidden, because the
            // name renders in the field's place.
            if (!settings.showSearchFilter)
            {
                bool showName = settings.showModNameWhenNoSearch;
                listing.Indent(Indent);
                listing.ColumnWidth -= Indent;
                listing.CheckboxLabeled(UiText.Get("EPR.ShowModName"), ref showName);
                listing.ColumnWidth += Indent;
                listing.Outdent(Indent);
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
        }

        private static void DrawHoverOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
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
                listing.Indent(Indent);
                listing.ColumnWidth -= Indent;
                listing.CheckboxLabeled(UiText.Get("EPR.CollapseWhenIdle"), ref collapseIdle);
                listing.ColumnWidth += Indent;
                listing.Outdent(Indent);
                if (collapseIdle != settings.collapseWhenIdle)
                {
                    EPrimeReadoutsMod.Persist(s => s.collapseWhenIdle = collapseIdle);
                    ReadoutPanel.BumpView();
                }
            }
        }

        private static void SectionHeader(Listing_Standard listing, string key)
        {
            listing.Gap(8f);
            var rect = listing.GetRect(EprStyle.SectionHeaderHeight);
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
