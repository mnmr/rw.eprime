using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.Patches
{
    /// Fix 1: Suppress the vanilla Dialog_ModSettings heading label for our mod only.
    ///
    /// Vanilla Dialog_ModSettings.DoWindowContents (Decompiled\RimWorld\Dialog_ModSettings.cs):
    ///   Text.Font = GameFont.Medium;
    ///   Widgets.Label(new Rect(0f, 0f, inRect.width - 150f - 17f, 35f), mod.SettingsCategory());
    ///   Text.Font = GameFont.Small;
    ///   Rect inRect2 = new Rect(0f, 40f, inRect.width, inRect.height - 40f - Window.CloseButSize.y);
    ///   mod.DoSettingsWindowContents(inRect2);
    ///
    /// Our DoSettingsWindowContents already draws a full header panel with the mod
    /// title, so the vanilla label produces a duplicate. We replace DoWindowContents
    /// for our mod only: skip the Widgets.Label call, set Small font, and delegate
    /// directly to our DoSettingsWindowContents with the same inRect2 geometry.
    ///
    /// Window-only UI: no save, MP, or lifecycle impact.
    [HarmonyPatch(typeof(Dialog_ModSettings), nameof(Dialog_ModSettings.DoWindowContents))]
    public static class Patch_ModSettingsHeading
    {
        // Cached field accessor (no per-frame allocation — this prefix runs
        // every OnGUI pass while ANY mod's settings window is open). Field
        // name verified at Decompiled\RimWorld\Dialog_ModSettings.cs line 8.
        private static readonly AccessTools.FieldRef<Dialog_ModSettings, Mod> ModRef =
            AccessTools.FieldRefAccess<Dialog_ModSettings, Mod>("mod");

        public static bool Prefix(Dialog_ModSettings __instance, Rect inRect)
        {
            if (ModRef(__instance) is not QualityJobsMod ourMod) return true; // original for other mods

            // Replicate vanilla layout without the heading label.
            // Window.InnerWindowOnGUI (Window.cs line 249) does:
            //   Rect rect3 = rect.ContractedBy(Margin)  [Margin = 18f]
            // then passes rect3.AtZero() to DoWindowContents. So the inRect
            // we receive here already has 18f contracted on all sides and is
            // zeroed. Vanilla then further offsets by (0, 40) for the heading —
            // we skip that, so we use (0, 0) to keep the top margin equal to
            // the 18f side margins baked in by Window.
            Text.Font = GameFont.Small;
            Rect inRect2 = new Rect(0f, 0f, inRect.width,
                inRect.height - Window.CloseButSize.y);
            ourMod.DoSettingsWindowContents(inRect2);
            return false; // skip the original method
        }
    }
}
