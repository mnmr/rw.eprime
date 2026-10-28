using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The Options tab: per-player display options in two columns, one
    /// section per option domain. Behavior on the left; display and geometry
    /// on the right. Geometry previews immediately and persists on window close.
    internal sealed class OptionsTabView
    {
        private const float ColumnGap = 20f;
        private const float Indent = 16f;

        private readonly Listing_Standard left = new Listing_Standard { maxOneColumn = true };
        private readonly Listing_Standard right = new Listing_Standard { maxOneColumn = true };
        private const float SectionGap = 8f;
        private const float SliderHeight = 22f;

        // Owner/key: this window / UiRevision.Current. Value: scalar geometry
        // and translated segment labels, immutable between refreshes.
        // Dependencies: Small font metrics and all option-label translations.
        // Refresh: immediately at a metric revision; no preference-dependent
        // height (reserve both conditional rows). Equality: unchanged key reuses
        // values. Teardown: Reset on window close; no external resources.
        private int metricStamp = -1;
        private float lineHeight, rowHeight, headerHeight, buttonHeight, selectorLabelWidth;
        private Vector2 minimumContentSize;
        private readonly string[] tierLayoutLabels = new string[2];
        private readonly string[] counterFontLabels = new string[2];
        private static readonly string[] LabelKeys =
        {
            "EPR.CountOptions", "EPR.PlannedWorkOptions", "EPR.ClickOptions",
            "EPR.SearchOptions", "EPR.HoverOptions", "EPR.DisplayOptions",
            "EPR.PositionAndSizeOptions", "EPR.SearchStorageOnly", "EPR.SearchHideForbidden",
            "EPR.ReserveForBills", "EPR.ReserveForBuildables", "EPR.ShowNegativeCounts",
            "EPR.QualityJobsRework", "EPR.SelectJumpCamera", "EPR.ShowSearchFilter",
            "EPR.ShowModName", "EPR.SearchHideZero", "EPR.ExpandOnHover",
            "EPR.CollapseWhenIdle", "EPR.RenderCountersWithTinyFont",
            "EPR.KeepReadoutToggle", "EPR.UseVanilla", "EPR.ResetPositionAndSize",
            "EPR.OffsetX", "EPR.OffsetY", "EPR.PanelWidth", "EPR.BottomMargin"
        };

        internal Vector2 MinimumContentSize { get { EnsureMetrics(); return minimumContentSize; } }

        private void EnsureMetrics()
        {
            if (metricStamp == UiRevision.Current) return;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                lineHeight = Mathf.Ceil(Text.LineHeight);
                rowHeight = Mathf.Max(24f, lineHeight);
                headerHeight = lineHeight + 6f;
                buttonHeight = Mathf.Max(28f, lineHeight + 6f);
                float columnWidth = 0f;
                for (int i = 0; i < LabelKeys.Length; i++)
                    columnWidth = Mathf.Max(columnWidth,
                        WrText.FitWidth(UiText.Get(LabelKeys[i])) + Indent + 36f);
                tierLayoutLabels[0] = UiText.Get("EPR.TierLayoutHorizontal");
                tierLayoutLabels[1] = UiText.Get("EPR.TierLayoutVertical");
                counterFontLabels[0] = UiText.Get("EPR.CounterFontTiny");
                counterFontLabels[1] = UiText.Get("EPR.CounterFontSmall");
                selectorLabelWidth = Mathf.Max(WrText.FitWidth(UiText.Get("EPR.TierLayoutOptions")),
                    WrText.FitWidth(UiText.Get("EPR.RenderCountersWithTinyFont"))) + RuleGapX;
                float segmentWidth = Mathf.Max(
                    Mathf.Max(WrText.FitWidth(tierLayoutLabels[0]), WrText.FitWidth(tierLayoutLabels[1])),
                    Mathf.Max(WrText.FitWidth(counterFontLabels[0]), WrText.FitWidth(counterFontLabels[1]))) + 24f;
                columnWidth = Mathf.Max(columnWidth, selectorLabelWidth + 2f * segmentWidth);
                float behaviorHeight = 5f * (SectionGap + headerHeight)
                    + 12f * (rowHeight + left.verticalSpacing);
                float displayHeight = 2f * (SectionGap + headerHeight)
                    + 2f * (rowHeight + right.verticalSpacing)
                    + 3f * (buttonHeight + right.verticalSpacing)
                    + 4f * (lineHeight + SliderHeight + right.verticalSpacing);
                minimumContentSize = new Vector2(2f * Mathf.Ceil(columnWidth) + ColumnGap,
                    Mathf.Max(behaviorHeight, displayHeight));
            }
            metricStamp = UiRevision.Current;
        }

        // Owner/key: this window / language + displayed integer slider values.
        // Value: immutable formatted labels. Dependencies: those exact inputs.
        // Refresh: immediate after a change. Equality: unchanged inputs preserve
        // string references. Teardown: Reset releases all labels on close.
        private int positionLanguageStamp = -1;
        private int textOffsetX, textOffsetY, textPanelWidth, textBottomMargin;
        private string offsetXLabel = "", offsetYLabel = "", panelWidthLabel = "", bottomMarginLabel = "";

        internal void Reset()
        {
            metricStamp = positionLanguageStamp = -1;
            tierLayoutLabels[0] = tierLayoutLabels[1] = "";
            counterFontLabels[0] = counterFontLabels[1] = "";
            offsetXLabel = offsetYLabel = panelWidthLabel = bottomMarginLabel = "";
        }

        internal void Draw(Rect rect)
        {
            EnsureMetrics();
            ReadoutSettings settings = EPrimeReadoutsMod.Settings;
            float columnW = Mathf.Floor((rect.width - ColumnGap) / 2f);
            var leftRect = new Rect(rect.x, rect.y, columnW, rect.height);
            var rightRect = new Rect(leftRect.xMax + ColumnGap, rect.y,
                columnW, rect.height);

            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.UpperLeft;
                left.ColumnWidth = right.ColumnWidth = columnW;
                left.Begin(leftRect);
                try
                {
                    DrawCountOptions(left, settings);
                    DrawPlannedWorkOptions(left, settings);
                    DrawClickOptions(left, settings);
                    DrawSearchOptions(left, settings);
                    DrawHoverOptions(left, settings);
                }
                finally
                {
                    left.End();
                }

                right.Begin(rightRect);
                try
                {
                    DrawDisplayOptions(right, settings);
                    DrawPositionOptions(right, settings);
                }
                finally
                {
                    right.End();
                }
            }
        }

        private void DrawCountOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.CountOptions");

            bool storageOnly = settings.searchStorageOnly;
            if (CheckboxRow(listing, "EPR.SearchStorageOnly",
                    "EPR.SearchStorageOnlyTip", ref storageOnly))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.searchStorageOnly = value, storageOnly);
                ReadoutPanel.BumpView();
            }

            bool hideForbidden = settings.searchHideForbidden;
            if (CheckboxRow(listing, "EPR.SearchHideForbidden", null, ref hideForbidden))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.searchHideForbidden = value, hideForbidden);
                ReadoutPanel.BumpView();
            }
        }

        private void DrawPlannedWorkOptions(
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
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.reserveForBills = value, reserveBills);
                ReadoutPanel.BumpView();
            }

            bool reserveBuildables = settings.reserveForBuildables;
            if (CheckboxRow(listing, "EPR.ReserveForBuildables",
                    "EPR.ReserveForBuildablesTip", ref reserveBuildables))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.reserveForBuildables = value, reserveBuildables);
                ReadoutPanel.BumpView();
            }

            bool showNegative = settings.showNegativeCounts;
            if (CheckboxRow(listing, "EPR.ShowNegativeCounts",
                    "EPR.ShowNegativeCountsTip", ref showNegative))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.showNegativeCounts = value, showNegative);
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
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.qualityJobsRework = value, qualityRework);
                ReadoutPanel.BumpView();
            }
        }

        private void DrawDisplayOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.DisplayOptions");

            DrawCounterFontRow(listing, settings);
            DrawTierLayoutRow(listing, settings);

            // The panel observes the game's categorized-readout preference
            // every draw, so the kept toggle takes effect at once.
            bool keepToggle = settings.keepReadoutToggle;
            if (CheckboxRow(listing, "EPR.KeepReadoutToggle",
                    "EPR.KeepReadoutToggleTip", ref keepToggle))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.keepReadoutToggle = value, keepToggle);
                ReadoutPanel.BumpView();
            }

            bool vanilla = settings.useVanillaReadout;
            if (CheckboxRow(listing, "EPR.UseVanilla", null, ref vanilla))
            {
                if (vanilla)
                    Find.WindowStack.Add(new CompactConfirmDialog(
                        UiText.Get("EPR.UseVanillaConfirm"), EnableVanilla,
                        UiText.Get("EPR.UseVanillaConfirmButton"), UiText.Get("CancelButton"),
                        UiRevision.Current));
                else
                    EPrimeReadoutsMod.PersistSetup(static s => s.useVanillaReadout = false);
            }
        }

        // Only accepting the confirmation changes the preference; dismissing
        // it leaves both the checkbox and the custom readout unchanged.
        private static readonly System.Action EnableVanilla =
            static () => EPrimeReadoutsMod.PersistSetup(static s => s.useVanillaReadout = true);

        /// Gap between the font/tier label and its selector.
        private const float RuleGapX = 8f;

        private void DrawCounterFontRow(Listing_Standard listing, ReadoutSettings settings)
        {
            int active = settings.renderCountersWithTinyFont ? 0 : 1;
            int clicked = DrawSelectorRow(listing, "EPR.RenderCountersWithTinyFont",
                "EPR.RenderCountersWithTinyFontTip", counterFontLabels, active);
            if (clicked >= 0 && clicked != active)
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.renderCountersWithTinyFont = value,
                    clicked == 0);
                ReadoutPanel.BumpView();
            }
        }

        /// One Display Options row: "Tier layout" label on the left, the
        /// Horizontal / Vertical selector filling the rest of the row.
        private void DrawTierLayoutRow(
            Listing_Standard listing, ReadoutSettings settings)
        {
            int active = settings.verticalTiers ? 1 : 0;
            int clicked = DrawSelectorRow(listing, "EPR.TierLayoutOptions",
                "EPR.TierLayoutTip", tierLayoutLabels, active);
            if (clicked >= 0 && clicked != active)
            {
                bool vertical = clicked == 1;
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.verticalTiers = value, vertical);
                ReadoutPanel.BumpView();
            }
        }

        private int DrawSelectorRow(Listing_Standard listing, string labelKey,
            string tooltipKey, string[] labels, int active)
        {
            Rect rowRect = listing.GetRect(buttonHeight);
            WrTips.Key(tooltipKey).Region(rowRect);
            using (GuiStateScope.Capture())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rowRect.x, rowRect.y, selectorLabelWidth, rowRect.height),
                    UiText.Get(labelKey));
            }
            var selectorRect = new Rect(rowRect.x + selectorLabelWidth, rowRect.y,
                Mathf.Max(1f, rowRect.width - selectorLabelWidth), rowRect.height);
            int clicked = SegmentedControl.Row(selectorRect, labels, active);
            listing.Gap(listing.verticalSpacing);
            return clicked;
        }

        private void DrawClickOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.ClickOptions");

            bool jumpCamera = settings.selectJumpCamera;
            if (CheckboxRow(listing, "EPR.SelectJumpCamera", null, ref jumpCamera))
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.selectJumpCamera = value, jumpCamera);
        }

        private void DrawSearchOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.SearchOptions");

            bool showSearch = settings.showSearchFilter;
            if (CheckboxRow(listing, "EPR.ShowSearchFilter", null, ref showSearch))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.showSearchFilter = value, showSearch);
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
                if (CheckboxRow(listing, "EPR.ShowModName", null, ref showName, indented: true))
                {
                    EPrimeReadoutsMod.PersistSetup(static (s, value) => s.showModNameWhenNoSearch = value, showName);
                    ReadoutPanel.BumpView();
                }
            }

            bool hideZero = settings.searchHideZero;
            if (CheckboxRow(listing, "EPR.SearchHideZero", null, ref hideZero))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.searchHideZero = value, hideZero);
                ReadoutPanel.BumpView();
            }
        }

        private void DrawHoverOptions(
            Listing_Standard listing, ReadoutSettings settings)
        {
            SectionHeader(listing, "EPR.HoverOptions");

            bool expandOnHover = settings.expandOnHover;
            if (CheckboxRow(listing, "EPR.ExpandOnHover", null, ref expandOnHover))
            {
                EPrimeReadoutsMod.PersistSetup(static (s, value) => s.expandOnHover = value, expandOnHover);
                ReadoutPanel.BumpView();
            }

            // Sub-option: only meaningful (and only shown) while the master
            // hover toggle is on.
            if (settings.expandOnHover)
            {
                bool collapseIdle = settings.collapseWhenIdle;
                if (CheckboxRow(listing, "EPR.CollapseWhenIdle", null, ref collapseIdle, indented: true))
                {
                    EPrimeReadoutsMod.PersistSetup(static (s, value) => s.collapseWhenIdle = value, collapseIdle);
                    ReadoutPanel.BumpView();
                }
            }
        }

        private void DrawPositionOptions(Listing_Standard listing, ReadoutSettings settings)
        {
            float oldX = settings.offsetX, oldY = settings.offsetY, oldWidth = settings.panelWidth, oldBottom = settings.bottomMargin;
            SectionHeader(listing, "EPR.PositionAndSizeOptions");
            EnsurePositionLabels(settings);
            settings.offsetX = SliderRow(listing, offsetXLabel, settings.offsetX, 0f, 200f);
            settings.offsetY = SliderRow(listing, offsetYLabel, settings.offsetY, 0f, 200f);
            settings.panelWidth = SliderRow(listing, panelWidthLabel, settings.panelWidth, 80f, 400f);
            settings.bottomMargin = SliderRow(listing, bottomMarginLabel, settings.bottomMargin, 0f, 500f);
            if (Widgets.ButtonText(listing.GetRect(buttonHeight), UiText.Get("EPR.ResetPositionAndSize")))
            {
                settings.offsetX = settings.offsetY = 7f;
                settings.panelWidth = 140f;
                settings.bottomMargin = 200f;
            }
            if (settings.offsetX != oldX || settings.offsetY != oldY || settings.panelWidth != oldWidth || settings.bottomMargin != oldBottom)
                PlayerSetupBridge.Changed();
            listing.Gap(listing.verticalSpacing);
        }

        private float SliderRow(Listing_Standard listing, string label, float value, float min, float max)
        {
            Widgets.Label(listing.GetRect(lineHeight), label);
            float result = Widgets.HorizontalSlider(listing.GetRect(SliderHeight), value, min, max);
            listing.Gap(listing.verticalSpacing);
            return result;
        }

        private void EnsurePositionLabels(ReadoutSettings settings)
        {
            int x = (int)settings.offsetX, y = (int)settings.offsetY;
            int width = (int)settings.panelWidth, bottom = (int)settings.bottomMargin;
            if (positionLanguageStamp == UiRevision.LanguageCurrent
                && textOffsetX == x && textOffsetY == y
                && textPanelWidth == width && textBottomMargin == bottom) return;
            offsetXLabel = UiText.Get("EPR.OffsetX") + ": " + x;
            offsetYLabel = UiText.Get("EPR.OffsetY") + ": " + y;
            panelWidthLabel = UiText.Get("EPR.PanelWidth") + ": " + width;
            bottomMarginLabel = UiText.Get("EPR.BottomMargin") + ": " + bottom;
            textOffsetX = x; textOffsetY = y; textPanelWidth = width; textBottomMargin = bottom;
            positionLanguageStamp = UiRevision.LanguageCurrent;
        }

        private void SectionHeader(Listing_Standard listing, string key)
        {
            listing.Gap(SectionGap);
            var rect = listing.GetRect(headerHeight);
            using (GuiStateScope.Capture())
            {
                GUI.color = EprStyle.HeaderText;
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, lineHeight), UiText.Get(key));
                GUI.color = EprStyle.HeaderRule;
                WrText.LineHorizontal(rect.x, rect.y + lineHeight + 2f, rect.width);
            }
        }

        /// One option row with an optional tooltip. Every row shares the same
        /// single-line height so the checkboxes align exactly down the section.
        /// A disabled row still draws and still explains itself on hover, but
        /// swallows clicks and greys its label. Returns true when the player
        /// changed the value.
        private bool CheckboxRow(
            Listing_Standard listing, string labelKey, string? tooltipKey,
            ref bool value, bool disabled = false, bool indented = false)
        {
            Rect rect = listing.GetRect(rowHeight);
            if (indented) { rect.xMin += Indent; }
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            if (tooltipKey != null) WrTips.Key(tooltipKey).Region(rect);
            bool before = value;
            using (GuiStateScope.Capture())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                if (disabled) GUI.color = EprStyle.CaptionText;
                Widgets.CheckboxLabeled(rect, UiText.Get(labelKey), ref value, disabled);
            }
            listing.Gap(listing.verticalSpacing);
            return value != before;
        }
    }
}
