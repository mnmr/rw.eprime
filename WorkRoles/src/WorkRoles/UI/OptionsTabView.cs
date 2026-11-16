using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Options tab: client-side display preferences (ModSettings, never
    /// synced) with live previews on the left; per-save automation and
    /// compatibility toggles (synced in MP) on the right. All
    /// recommendation configuration lives on the Recommendations tab.
    public class OptionsTabView
    {
        private readonly OptionsTabState state = new OptionsTabState();
        private readonly OptionsPreviewState previews = new OptionsPreviewState();

        /// Opens the auto-optimize enable confirmation; wired by the window to
        /// the Colonists tab, which owns the fix-plan machinery. Disabling
        /// needs no confirmation and issues the command directly.
        internal System.Action? showAutoOptimizeEnablePreview;

        /// The Colonists tab's shared role catalog (palette clusters and
        /// chip data), wired by the window; the previews consume it
        /// instead of building their own.
        internal System.Func<RoleStore, ColonistsRosterCatalogSnapshot>? catalogProvider;

        private const float ColumnX = 16f;
        private const float ColumnGap = 32f;
        private const float MaxColumnWidth = 640f;
        private const float RowAdvance = 34f;
        private const float CompatibilityGapAbove = 20f;
        private const float PreviewGapAbove = 4f;
        private const float PreviewGapBelow = 12f;
        private Vector2 scroll;

        public void Reset()
        {
            scroll = Vector2.zero;
            state.Reset();
            previews.Reset();
        }

        internal void ReleaseWindowData() => Reset();

        internal void InvalidateLanguageCaches() => state.InvalidateLanguageCaches();

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            float viewW = rect.width - 16f;
            float columnW = Mathf.Min(
                (viewW - 2f * ColumnX - ColumnGap) / 2f, MaxColumnWidth);
            OptionsRenderSnapshot snapshot = state.Snapshot(store, columnW);
            ColonistsRosterCatalogSnapshot? catalog = catalogProvider?.Invoke(store);
            OptionsPreviewSnapshot? preview = catalog == null ? null
                : previews.Snapshot(catalog, columnW, snapshot);

            float leftH = 12f + 32f + 7f * RowAdvance
                + PreviewBlock(preview?.Row) + PreviewBlock(preview?.Palette)
                + PreviewBlock(preview?.Panel);
            float rightH = 12f + 32f + RowAdvance + CompatibilityGapAbove + 32f
                + 3f * RowAdvance
                + snapshot.MechanicalHeight + 6f
                + snapshot.MechanicalRecommendationsHeight + 6f;
            Widgets.BeginScrollView(rect, ref scroll,
                new Rect(0f, 0f, viewW, Mathf.Max(leftH, rightH) + 12f));
            try
            {
                DrawDisplay(snapshot, preview, ColumnX, columnW);
                DrawSaveOptions(snapshot, ColumnX + columnW + ColumnGap, columnW);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float PreviewBlock(OptionsPreviewStrip? strip) =>
            strip == null ? 0f
                : PreviewGapAbove + strip.Height + PreviewGapBelow;

        private static float DrawPreview(OptionsPreviewStrip? strip, float x,
            float y, float width)
        {
            if (strip == null) return y;
            OptionsPreviewState.Draw(new Rect(x, y + PreviewGapAbove, width,
                strip.Height), strip);
            return y + PreviewBlock(strip);
        }

        /// Client-side display preferences: chip caches key on these values
        /// directly, so a write here is picked up on the next draw pass.
        /// Each set of options is followed by the preview it changes.
        private static void DrawDisplay(OptionsRenderSnapshot snapshot,
            OptionsPreviewSnapshot? preview, float x, float width)
        {
            float y = 12f;
            WrLabels.HeaderLabel(new Rect(x, y, width, 28f), snapshot.DisplayHeader);
            y += 32f;

            // Colonist rows and their chips.
            bool? changed = DisplayToggle(new Rect(x, y, width, 28f),
                snapshot.ColonistVerdictsLabel, "WR_OptVerdictsColonistsTip",
                snapshot.ColonistVerdicts);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.VerdictsOnColonistChips,
                    changed.Value);
            y += RowAdvance;
            // Chip markers: Dots / Icons / Off. Chip width caches key on the
            // value (RoleChipUI.MetricRevision), so the write shows at once.
            int clicked = SegmentRow(x, y, width, snapshot.ChipMarkersLabel,
                "WR_OptChipMarkersTip", snapshot.ChipMarkersOptions,
                snapshot.ChipMarkersIndex, snapshot.ChipMarkersControlWidth);
            if (clicked >= 0)
            {
                WorkRolesSettings settings = WorkRolesMod.Settings;
                if (settings != null)
                {
                    settings.chipMarkers = (ChipMarkers)clicked;
                    WorkRolesGameComponent.RequestSettingsWrite();
                }
            }
            y += RowAdvance;
            changed = DisplayToggle(new Rect(x, y, width, 28f),
                snapshot.RemoveIconLabel, "WR_OptRemoveIconTip",
                snapshot.RemoveIcon);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.RemoveRoleIcon, changed.Value);
            y += RowAdvance;
            changed = DisplayToggle(new Rect(x, y, width, 28f),
                snapshot.SkillCaptionsLabel, "WR_OptSkillCaptionsTip",
                snapshot.SkillCaptions);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.ColonistSkillCaptions,
                    changed.Value);
            y += RowAdvance;
            y = DrawPreview(preview?.Row, x, y, width);

            // Role palette.
            clicked = SegmentRow(x, y, width, snapshot.PaletteGroupingLabel,
                "WR_PaletteModeTip", snapshot.PaletteGroupingOptions,
                snapshot.PaletteGroupingIndex,
                snapshot.PaletteGroupingControlWidth);
            if (clicked >= 0)
            {
                WorkRolesSettings settings = WorkRolesMod.Settings;
                if (settings != null)
                {
                    settings.paletteMode = clicked == 1
                        ? PaletteMode.Groups : PaletteMode.Skills;
                    WorkRolesGameComponent.RequestSettingsWrite();
                }
            }
            y += RowAdvance;
            changed = DisplayToggle(new Rect(x, y, width, 28f),
                snapshot.PaletteVerdictsLabel, "WR_OptVerdictsPaletteTip",
                snapshot.PaletteVerdicts);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.VerdictsInPalette,
                    changed.Value);
            y += RowAdvance;
            y = DrawPreview(preview?.Palette, x, y, width);

            // Selected colonist panel.
            changed = DisplayToggle(new Rect(x, y, width, 28f),
                snapshot.RecommendationsLabel, "WR_OptRecommendationsTip",
                snapshot.Recommendations);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.RecommendationsEnabled,
                    changed.Value);
            y += RowAdvance;
            DrawPreview(preview?.Panel, x, y, width);
        }

        /// Per-save options (shared world state, edited through synced
        /// commands): automation first, then compatibility.
        private void DrawSaveOptions(OptionsRenderSnapshot snapshot, float x,
            float width)
        {
            float y = 12f;
            // The hourly auto-optimize schedule runs in the synced
            // simulation (AutoOptimizer).
            WrLabels.HeaderLabel(new Rect(x, y, width, 28f),
                snapshot.AutomationHeader);
            y += 32f;
            var autoOptimizeRect = new Rect(x, y, width, 28f);
            StructuredTipPresenter.TipRegion(
                autoOptimizeRect, snapshot.AutoOptimizeTip);
            bool autoOptimizeNew = snapshot.AutoOptimize;
            Widgets.CheckboxLabeled(
                autoOptimizeRect, snapshot.AutoOptimizeLabel, ref autoOptimizeNew);
            if (autoOptimizeNew != snapshot.AutoOptimize)
            {
                // Enabling opens a confirmation preview; the checkbox stays
                // unchecked until the dialog's apply issues the command.
                if (autoOptimizeNew) showAutoOptimizeEnablePreview?.Invoke();
                else RoleCommands.SetAutoOptimize(false);
            }
            y += RowAdvance;

            WrLabels.HeaderLabel(
                new Rect(x, y + CompatibilityGapAbove, width, 28f),
                snapshot.CompatibilityHeader);
            y += CompatibilityGapAbove + 32f;
            var numericRect = new Rect(x, y, width, 28f);
            y += RowAdvance;
            var rangeRect = new Rect(x, y, width, 28f);
            y += RowAdvance;
            var emergencyRect = new Rect(x, y, width, 28f);
            y += RowAdvance;
            var mechanicalRect = new Rect(x, y, width, snapshot.MechanicalHeight);
            y += snapshot.MechanicalHeight + 6f;
            var mechanicalRecommendationsRect = new Rect(x, y, width,
                snapshot.MechanicalRecommendationsHeight);

            StructuredTipPresenter.TipRegion(numericRect, snapshot.NumericTip);
            bool numericNew = snapshot.Numeric;
            Widgets.CheckboxLabeled(
                numericRect, snapshot.NumericLabel, ref numericNew);
            if (numericNew != snapshot.Numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);

            StructuredTipPresenter.TipRegion(rangeRect, snapshot.RangeTip);
            bool vanillaNew = snapshot.VanillaRange;
            Widgets.CheckboxLabeled(
                rangeRect, snapshot.RangeLabel, ref vanillaNew);
            if (vanillaNew != snapshot.VanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);

            StructuredTipPresenter.TipRegion(emergencyRect, snapshot.EmergencyRuleTip);
            bool emergencyNew = snapshot.RolePriorityEmergencyRule;
            Widgets.CheckboxLabeled(
                emergencyRect, snapshot.EmergencyRuleLabel, ref emergencyNew);
            if (emergencyNew != snapshot.RolePriorityEmergencyRule)
                RoleCommands.SetRolePriorityEmergencyRule(emergencyNew);

            bool mechanicalNew = snapshot.MechanicalEnabled;
            WrTips.Key("WR_OptMechanicalWorkersTip").Region(mechanicalRect);
            Widgets.CheckboxLabeled(mechanicalRect, snapshot.MechanicalLabel, ref mechanicalNew);
            if (mechanicalNew != snapshot.MechanicalEnabled)
                RoleCommands.SetMechanicalWorkersEnabled(mechanicalNew);
            bool recommendationsNew = snapshot.MechanicalRecommendationsEnabled;
            WrTips.Key("WR_OptMechanicalRecommendationsTip").Region(mechanicalRecommendationsRect);
            Widgets.CheckboxLabeled(mechanicalRecommendationsRect,
                snapshot.MechanicalRecommendationsLabel, ref recommendationsNew);
            if (recommendationsNew != snapshot.MechanicalRecommendationsEnabled)
                RoleCommands.SetMechanicalRecommendationsEnabled(recommendationsNew);
        }

        /// Label with a right-aligned segmented control; returns the newly
        /// clicked segment or -1.
        private static int SegmentRow(float x, float y, float width,
            string label, string tipKey, string[] options, int index,
            float controlWidth)
        {
            WrTips.Key(tipKey).Region(new Rect(x, y, width, 28f));
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x, y, width - controlWidth - 8f, 28f),
                    label);
            }
            finally
            {
                Text.Anchor = oldAnchor;
            }
            int clicked = SegmentedControl.Row(
                new Rect(x + width - controlWidth, y + 2f, controlWidth, 24f),
                options, index);
            return clicked >= 0 && clicked != index ? clicked : -1;
        }

        private static bool? DisplayToggle(
            Rect rect, string label, string tipKey, bool value)
        {
            WrTips.Key(tipKey).Region(rect);
            bool edited = value;
            Widgets.CheckboxLabeled(rect, label, ref edited);
            return edited == value ? (bool?)null : edited;
        }

        private static void SetDisplayPreference(
            OptionsDisplayPreference preference, bool value)
        {
            WorkRolesSettings settings = WorkRolesMod.Settings;
            if (settings == null
                || !settings.SetDisplayPreference(preference, value))
                return;
            WorkRolesGameComponent.RequestSettingsWrite();
        }
    }
}
