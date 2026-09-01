using HarmonyLib;
using Implanner.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.Patches
{
    /// Toolbar button beside the vanilla view toggles, registered the
    /// standard mod-compatible way: a postfix on the same WidgetRow vanilla
    /// fills, so toolbar-restyling mods compose with ours instead of hiding
    /// it. The row does the actual painting; we contribute one button that
    /// toggles the main Implanner dialog.
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings
    {
        // Tooltip cache — Owner: process. Key: active language. Value: the
        // translated tip string. Dependencies: language change, observed per
        // draw. Refresh: rebuilt when the language object changes. Equality:
        // cache hits preserve the string. Teardown: ResetPresentation on game
        // disposal.
        private static LoadedLanguage? tipLanguage;
        private static string? tip;

        // Last drawn rect, used to pick the hover texture on the NEXT pass:
        // Icon() only reveals its rect after drawing, and toolbar-restyling
        // mods may relocate it, so predicting the rect up front is
        // unreliable. One frame of hover lag is imperceptible.
        private static Rect lastIconRect;

        internal static void ResetPresentation()
        {
            tipLanguage = null;
            tip = null;
            lastIconRect = default;
        }

        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            if (!ImplannerMod.Settings.showToolbarButton) return;

            if (LanguageDatabase.activeLanguage != tipLanguage)
            {
                tipLanguage = LanguageDatabase.activeLanguage;
                tip = "IMP_ToolbarButtonTip".Translate();
            }

            // The row is a shared cursor: vanilla toggles consume exactly 28
            // units each (24 icon + 4 gap), forming clean columns across the
            // wrapped rows. Other mods' postfixes can append elements whose
            // width is NOT a multiple of 28, which knocks the cursor off the
            // column grid for everything drawn after them. Re-synchronize:
            // pad the cursor forward to the next 28-unit column boundary,
            // measured from the row origin (GlobalControlsUtility inits the
            // row at UI.screenWidth). This also hands the NEXT mod a clean
            // phase.
            const float CellPitch = WidgetRow.IconSize + WidgetRow.DefaultGap; // 28
            float phase = ((float)Verse.UI.screenWidth - row.FinalX) % CellPitch;
            if (phase > 0.01f && phase < CellPitch - 0.01f)
                row.Gap(CellPitch - phase);

            // Drawn through row.Icon, the same primitive the vanilla toggles
            // use, consuming the standard cell (icon + gap). The hover
            // texture is picked from the previous pass's rect.
            Texture2D tex = Mouse.IsOver(lastIconRect)
                ? ImplannerTex.ToolbarButtonHover
                : ImplannerTex.ToolbarButton;
            Rect iconRect = row.Icon(tex, tip);
            lastIconRect = iconRect;
            if (Widgets.ButtonInvisible(iconRect))
            {
                Dialog_Implanner? open =
                    Find.WindowStack.WindowOfType<Dialog_Implanner>();
                if (open != null)
                    open.Close();
                else
                    Find.WindowStack.Add(new Dialog_Implanner());
            }
        }
    }

    /// Mod textures resolved once on the main thread at startup.
    [StaticConstructorOnStartup]
    internal static class ImplannerTex
    {
        /// Toolbar button: the preview's upgrade machine, exported by
        /// assets/export-assets.ps1 (128x128, drawn by WidgetRow in the
        /// standard 24x24 virtual cell). The hover variant is a uniformly
        /// brightened render swapped in by Patch_PlaySettings on mouse-over.
        internal static readonly Texture2D ToolbarButton =
            ContentFinder<Texture2D>.Get("EPrimeImplanner/ToolbarButton");

        internal static readonly Texture2D ToolbarButtonHover =
            ContentFinder<Texture2D>.Get("EPrimeImplanner/ToolbarButtonHover");

        /// Solid circle used as the gear icon's backdrop in the table.
        internal static readonly Texture2D CircleFill =
            ContentFinder<Texture2D>.Get("UI/Overlays/Circle75Solid");

        /// The vanilla tab atlas (TabRecord keeps its copy private); drawn by
        /// PlannerTabs, never mutated.
        internal static readonly Texture2D TabAtlas =
            ContentFinder<Texture2D>.Get("UI/Widgets/TabAtlas");
    }
}
