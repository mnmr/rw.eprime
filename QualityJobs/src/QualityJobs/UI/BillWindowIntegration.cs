using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    internal static class BillWindowIntegration
    {
        // Owner: window, partitioned by window reference (weak keys). Value:
        // window-owned panel and its revision-gated immutable presentation data.
        // Dependencies: bill identity; panel dependencies documented in
        // BillQualityPanel. Refresh: factory/open lifecycle, never during drawing.
        // Equality: repeated attachment retains the same panel. Teardown: successful
        // WindowStack removal removes the entry; game disposal replaces the table;
        // abandoned factory results are collectible without an explicit close.
        private static ConditionalWeakTable<Window, BillQualityPanel> panels =
            new ConditionalWeakTable<Window, BillQualityPanel>();
        private static readonly AccessTools.FieldRef<Dialog_BillConfig, Bill_Production> ReadBill =
            AccessTools.FieldRefAccess<Dialog_BillConfig, Bill_Production>("bill");

        internal static void Attach(Window? window, Bill_Production? bill)
        {
            if (window == null || QualityJobsStore.Active == null
                || bill is not Bill_ProductionWithUft uft
                || !ManagedRecipes.IsManagedRecipe(bill.recipe)
                || bill.billStack?.billGiver is not Thing
                || panels.TryGetValue(window, out _)) return;
            panels.Add(window, new BillQualityPanel(uft));
        }

        internal static void Opened(Window window)
        {
            if (window is Dialog_BillConfig dialog) Attach(window, ReadBill(dialog));
            Resize(window);
        }

        internal static void Resize(Window window)
        {
            if (!panels.TryGetValue(window, out _)) return;
            Rect rect = window.windowRect;
            float center = rect.center.x;
            rect.width = Mathf.Min(rect.width + BillQualityPanel.PanelWidth, Verse.UI.screenWidth);
            rect.x = Mathf.Clamp(center - rect.width / 2f, 0f, Verse.UI.screenWidth - rect.width);
            window.windowRect = rect.Rounded();
        }

        internal static void Closed(Window window) => panels.Remove(window);
        internal static void Reset() => panels = new ConditionalWeakTable<Window, BillQualityPanel>();

        // This replaces only Window's virtual contents CALL, not the contents
        // implementation. The original virtual dispatch and every other mod's
        // patches still run on the same window. Vanilla's proportional columns
        // share the available width on compact screens; the QJ column keeps its
        // measured width. This geometry changes only on open/resolution events.
        internal static void DrawContents(Window window, Rect rect)
        {
            if (!panels.TryGetValue(window, out BillQualityPanel? panel))
            { window.DoWindowContents(rect); return; }
            Rect original = rect;
            original.width -= BillQualityPanel.PanelWidth;
            window.DoWindowContents(original);
            using (GuiStateScope.Capture()) panel.Draw(rect);
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class Patch_BillWindowOpen
    {
        private static void Postfix(Window window) => BillWindowIntegration.Opened(window);
    }

    [HarmonyPatch(typeof(Window), nameof(Window.Notify_ResolutionChanged))]
    internal static class Patch_BillWindowResolution
    {
        private static void Postfix(Window __instance) => BillWindowIntegration.Resize(__instance);
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) })]
    internal static class Patch_BillWindowClose
    {
        private static void Postfix(Window window, bool __result)
        { if (__result) BillWindowIntegration.Closed(window); }
    }

    [HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
    internal static class Patch_BillWindowContents
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(Window), nameof(Window.DoWindowContents));
            var draw = AccessTools.Method(typeof(BillWindowIntegration), nameof(BillWindowIntegration.DrawContents));
            var result = new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (!result[i].Calls(original)) continue;
                result[i].opcode = OpCodes.Call;
                result[i].operand = draw;
                matches++;
            }
            if (matches != 1)
                throw new System.InvalidOperationException("Quality Jobs could not locate the bill-window content call.");
            return result;
        }
    }
}
