using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.Patches
{
    /// Toolbar button beside the vanilla view toggles (show room beauty and
    /// friends). Registered the standard mod-compatible way: a postfix on the
    /// same WidgetRow vanilla fills, so toolbar-restyling mods that patch this
    /// method compose with ours instead of hiding it. The row does the actual
    /// painting; we contribute one button.
    ///
    /// The button opens the vanilla mod-settings window for this mod. A global
    /// settings toggle hides it for players who do not want the shortcut.
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings
    {
        // Tooltip cache — Owner: process. Key: active language. Value: the
        // translated tip string. Dependencies: language change, observed per
        // draw. Refresh: rebuilt when the language object changes. Equality:
        // n/a (single value). Teardown: none (process-static string).
        private static LoadedLanguage? tipLanguage;
        private static string? tip;

        // Last drawn rect, used to pick the hover texture on the NEXT pass:
        // Icon() only reveals its rect after drawing, and toolbar-restyling
        // mods may relocate it, so predicting the rect up front is unreliable.
        // One frame of hover lag is imperceptible.
        private static Rect lastIconRect;

        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            if (!QualityJobsMod.Settings.showToolbarButton) return;

            if (LanguageDatabase.activeLanguage != tipLanguage)
            {
                tipLanguage = LanguageDatabase.activeLanguage;
                tip = "QJ_ToolbarButtonTip".Translate();
            }

            // The row is a shared cursor: vanilla toggles consume exactly 28
            // units each (24 icon + 4 gap), forming clean columns across the
            // wrapped rows. Other mods' postfixes can append elements whose
            // width is NOT a multiple of 28 (RimHUD's HUD label, for one),
            // which knocks the cursor off the column grid for everything
            // drawn after them. Re-synchronize: pad the cursor forward to the
            // next 28-unit column boundary, measured from the row origin
            // (GlobalControlsUtility.DoPlaySettings inits the row at
            // UI.screenWidth). This also hands the NEXT mod a clean phase.
            const float CellPitch = WidgetRow.IconSize + WidgetRow.DefaultGap; // 28
            float phase = ((float)Verse.UI.screenWidth - row.FinalX) % CellPitch;
            if (phase > 0.01f && phase < CellPitch - 0.01f)
                row.Gap(CellPitch - phase);

            // Drawn through row.Icon, the same primitive the vanilla toggles
            // use, consuming the standard cell (icon + gap). Icon returns the
            // rect it actually drew in; an invisible button over that rect
            // supplies the click, and the hover texture is picked from the
            // previous pass's rect (Icon only reveals its rect after drawing).
            Texture2D tex = Mouse.IsOver(lastIconRect)
                ? QualityJobsTex.ToolbarButtonHover
                : QualityJobsTex.ToolbarButton;
            Rect iconRect = row.Icon(tex, tip);
            lastIconRect = iconRect;
            if (Widgets.ButtonInvisible(iconRect))
                Find.WindowStack.Add(new Dialog_ModSettings(QualityJobsMod.Instance));
        }
    }
}
