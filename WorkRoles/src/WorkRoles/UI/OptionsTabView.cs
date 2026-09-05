using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Options tab: per-save compatibility toggles (synced in MP) plus
    /// client-side display preferences (ModSettings, never synced). All
    /// recommendation configuration lives on the Recommendations tab.
    public class OptionsTabView
    {
        private readonly OptionsTabState state = new OptionsTabState();

        /// Opens the auto-optimize enable confirmation; wired by the window to
        /// the Colonists tab, which owns the fix-plan machinery. Disabling
        /// needs no confirmation and issues the command directly.
        internal System.Action? showAutoOptimizeEnablePreview;

        public void Reset() => state.Reset();

        internal void ReleaseWindowData() => Reset();

        internal void InvalidateLanguageCaches() => state.InvalidateLanguageCaches();

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            OptionsRenderSnapshot snapshot = state.Snapshot(store);

            float flowX = rect.x + 16f;
            float flowW = Mathf.Min(rect.width - 32f, 640f);
            float y = rect.y + 12f;
            var compatHeader = new Rect(flowX, y, flowW, 28f);
            y += 32f;
            var numericRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var rangeRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var emergencyRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var automationHeader = new Rect(flowX, y + 8f, flowW, 28f);
            y += 8f + 32f;
            var autoOptimizeRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var displayHeader = new Rect(flowX, y + 8f, flowW, 28f);
            y += 8f + 32f;

            WrText.HeaderLabel(compatHeader, snapshot.CompatibilityHeader);

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

            // Per-save automation: the hourly auto-optimize schedule is shared
            // world state (AutoOptimizer runs in the synced simulation), so
            // the edit travels through a synced command like the toggles above.
            WrText.HeaderLabel(automationHeader, snapshot.AutomationHeader);
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

            // Client-side display preferences: chip caches key on these values
            // directly, so a write here is picked up on the next draw pass.
            WrText.HeaderLabel(displayHeader, snapshot.DisplayHeader);
            bool? changed = DisplayToggle(new Rect(flowX, y, flowW, 28f),
                snapshot.SkillCaptionsLabel, "WR_OptSkillCaptionsTip",
                snapshot.SkillCaptions);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.ColonistSkillCaptions,
                    changed.Value);
            y += 34f;
            changed = DisplayToggle(new Rect(flowX, y, flowW, 28f),
                snapshot.ColonistVerdictsLabel, "WR_OptVerdictsColonistsTip",
                snapshot.ColonistVerdicts);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.VerdictsOnColonistChips,
                    changed.Value);
            y += 34f;
            changed = DisplayToggle(new Rect(flowX, y, flowW, 28f),
                snapshot.PaletteVerdictsLabel, "WR_OptVerdictsPaletteTip",
                snapshot.PaletteVerdicts);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.VerdictsInPalette,
                    changed.Value);
            y += 34f;
            changed = DisplayToggle(new Rect(flowX, y, flowW, 28f),
                snapshot.RecommendationVerdictsLabel,
                "WR_OptVerdictsRecommendationsTip",
                snapshot.RecommendationVerdicts);
            if (changed.HasValue)
                SetDisplayPreference(
                    OptionsDisplayPreference.VerdictsOnRecommendationChips,
                    changed.Value);
            y += 34f;

            // Palette grouping: a right-aligned Skills/Groups segmented pair
            // (the retired in-palette cycle button's replacement).
            var paletteRow = new Rect(flowX, y, flowW, 28f);
            WrTips.Key("WR_PaletteModeTip").Region(paletteRow);
            float controlW = snapshot.PaletteGroupingControlWidth;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(flowX, y, flowW - controlW - 8f, 28f),
                    snapshot.PaletteGroupingLabel);
            }
            finally
            {
                Text.Anchor = oldAnchor;
            }
            int clicked = SegmentedControl.Row(
                new Rect(flowX + flowW - controlW, y + 2f, controlW, 24f),
                snapshot.PaletteGroupingOptions,
                snapshot.PaletteGroupingIndex);
            if (clicked >= 0 && clicked != snapshot.PaletteGroupingIndex)
            {
                WorkRolesSettings settings = WorkRolesMod.Settings;
                if (settings != null)
                {
                    settings.paletteMode = clicked == 1
                        ? PaletteMode.Groups : PaletteMode.Skills;
                    WorkRolesGameComponent.RequestSettingsWrite();
                }
            }
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
