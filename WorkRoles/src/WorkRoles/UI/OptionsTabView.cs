using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Options tab: per-save toggles. These live here (not in Mod Settings)
    /// because both are per-savegame state, synced in MP.
    public class OptionsTabView
    {
        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;

            float y = rect.y + 12f;
            float rowW = Mathf.Min(rect.width - 32f, 560f);
            float rowX = rect.x + 16f;

            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            var numericRect = new Rect(rowX, y, rowW, 28f);
            TooltipHandler.TipRegion(numericRect, "WR_OptNumericTip".Translate());
            bool numericNew = numeric;
            Widgets.CheckboxLabeled(numericRect, "WR_OptNumeric".Translate(), ref numericNew);
            if (numericNew != numeric)
                RoleCommands.SetUseWorkPriorities(numericNew);
            y += 34f;

            bool vanillaRange = store.reportVanillaPriorities;
            var rangeRect = new Rect(rowX, y, rowW, 28f);
            TooltipHandler.TipRegion(rangeRect, "WR_OptVanillaRangeTip".Translate());
            bool vanillaNew = vanillaRange;
            Widgets.CheckboxLabeled(rangeRect, "WR_OptVanillaRange".Translate(), ref vanillaNew);
            if (vanillaNew != vanillaRange)
                RoleCommands.SetReportVanillaPriorities(vanillaNew);
        }
    }
}
