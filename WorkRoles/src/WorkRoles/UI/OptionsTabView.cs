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

        public void Reset() => state.Reset();

        internal void ReleaseWindowData() => Reset();

        internal void InvalidateLanguageCaches() => state.InvalidateLanguageCaches();

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;

            state.EnsureTips();

            float flowX = rect.x + 16f;
            float flowW = Mathf.Min(rect.width - 32f, 640f);
            float y = rect.y + 12f;
            var compatHeader = new Rect(flowX, y, flowW, 28f);
            y += 32f;
            var numericRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var rangeRect = new Rect(flowX, y, flowW, 28f);
            y += 34f;
            var displayHeader = new Rect(flowX, y + 8f, flowW, 28f);
            y += 8f + 32f;

            WrText.HeaderLabel(compatHeader, "WR_CompatSection".Translate());

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            StructuredTipPresenter.TipRegion(numericRect, state.NumericTip);
            bool numericNew = numeric;
            Widgets.CheckboxLabeled(numericRect, "WR_OptNumeric".Translate(), ref numericNew);
            if (numericNew != numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);

            bool vanillaRange = store.reportVanillaPriorities;
            StructuredTipPresenter.TipRegion(rangeRect, state.RangeTip);
            bool vanillaNew = vanillaRange;
            Widgets.CheckboxLabeled(rangeRect, "WR_OptVanillaRange".Translate(), ref vanillaNew);
            if (vanillaNew != vanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);

            // Client-side display preferences: chip caches key on these values
            // directly, so a write here is picked up on the next draw pass.
            WrText.HeaderLabel(displayHeader, "WR_DisplaySection".Translate());
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            VerdictToggle(new Rect(flowX, y, flowW, 28f),
                "WR_OptVerdictsColonists", ref settings.verdictsOnColonistChips);
            y += 34f;
            VerdictToggle(new Rect(flowX, y, flowW, 28f),
                "WR_OptVerdictsPalette", ref settings.verdictsInPalette);
            y += 34f;
            VerdictToggle(new Rect(flowX, y, flowW, 28f),
                "WR_OptVerdictsRecommendations",
                ref settings.verdictsOnRecommendationChips);
        }

        private static void VerdictToggle(Rect rect, string key, ref bool value)
        {
            WrTips.Key(key + "Tip").Region(rect);
            bool edited = value;
            Widgets.CheckboxLabeled(rect, key.Translate(), ref edited);
            if (edited == value) return;
            value = edited;
            WorkRolesMod.Settings.Write();
        }
    }
}
