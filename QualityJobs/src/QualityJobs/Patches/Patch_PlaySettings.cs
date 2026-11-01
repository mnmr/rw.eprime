using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.Patches
{
    /// Toolbar button beside the vanilla view toggles (show room beauty and
    /// friends). Registered the standard mod-compatible way: a postfix on the
    /// same WidgetRow vanilla fills, drawn through the row's own button
    /// primitive (WidgetRow.ButtonIcon / ButtonIconRect). Toolbar-restyling
    /// mods relocate exactly those primitives (UI Rethemed - Main Bar runs
    /// this method inside its own grid and prefixes ToggleableIcon,
    /// ButtonIcon and ButtonIconRect; a bare row.Icon plus an invisible
    /// button was never captured and painted over its first cell, found
    /// 2026-11-01 beside the same Implanner report).
    ///
    /// The button opens the Options and Help window for this mod. A global
    /// settings toggle hides it for players who do not want the shortcut.
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings
    {
        // Tooltip cache — Owner: process. Key: active language. Value: the
        // translated tip string. Dependencies: language change, observed per
        // draw. Refresh: rebuilt when the language object changes. Equality:
        // cache hits preserve the string. Teardown: ResetPresentation on game disposal.
        private static LoadedLanguage? tipLanguage;
        private static string? tip;

        internal static void ResetPresentation()
        {
            tipLanguage = null;
            tip = null;
        }

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

            // ButtonIconRect answers the cell ButtonIcon is about to use
            // without advancing the row (restyling mods answer their own
            // cell), so the hover texture is exact for this pass. The hover
            // variant is a brightened render, so the button's own mouseover
            // tint stays white. ButtonIcon advances by the icon only; the
            // trailing gap keeps the vanilla 28-unit cell for the next mod.
            Rect iconRect = row.ButtonIconRect();
            Texture2D tex = Mouse.IsOver(iconRect)
                ? QualityJobsTex.ToolbarButtonHover
                : QualityJobsTex.ToolbarButton;
            bool clicked = row.ButtonIcon(tex, tip, mouseoverColor: Color.white);
            row.Gap(WidgetRow.DefaultGap);
            if (clicked)
                Find.WindowStack.Add(new Dialog_QualityJobs());
        }
    }
}
