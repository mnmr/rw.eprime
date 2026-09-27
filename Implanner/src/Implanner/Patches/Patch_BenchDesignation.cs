using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.Patches
{
    /// Bill-tab controls for benches the player hands to Implanner: the
    /// toggle itself, and guards that keep a designated bench Implanner's
    /// (no new bills, no pasting, no suspending or resuming). A guard only
    /// swallows the click before the vanilla control sees it and says why:
    /// nothing here mutates game state, and in multiplayer only the synced
    /// toggle command travels. Bills that reach a designated bench another
    /// way are suspended by the next reconcile pass (PlannerBenches).
    internal static class BenchLock
    {
        // Label cache — Owner: process. Key: active language. Value: the
        // translated strings. Dependencies: language change, observed on
        // use. Refresh: rebuilt when the language object changes. Equality:
        // an unchanged language reuses the strings. Teardown: Reset on
        // world teardown.
        private static LoadedLanguage? language;
        internal static string Toggle = "";
        internal static string ToggleTip = "";
        internal static string LockedTip = "";
        internal static string NoNewBills = "";

        internal static void EnsureLabels()
        {
            if (language == LanguageDatabase.activeLanguage) return;
            language = LanguageDatabase.activeLanguage;
            Toggle = "IMP_BenchToggle".Translate();
            ToggleTip = "IMP_BenchToggleTip".Translate();
            LockedTip = "IMP_BenchLockedTip".Translate();
            NoNewBills = "IMP_BenchNoNewBills".Translate();
        }

        internal static void Reset() => language = null;

        /// Swallows clicks on the vanilla control that is drawn at rect
        /// after this call, and shows why while hovered.
        internal static void Guard(Rect rect)
        {
            if (!Mouse.IsOver(rect)) return;
            EventType type = Event.current.type;
            if (type == EventType.MouseDown || type == EventType.MouseUp)
                Event.current.Use();
            EnsureLabels();
            TooltipHandler.TipRegion(rect, LockedTip);
        }
    }

    /// The Implanner bench toggle beside Add bill, offered on benches that
    /// can craft implants (and on any designated bench, so it can always be
    /// released). Drawn before vanilla so its click is never taken by a
    /// bill row scrolled under it; the paste button is guarded the same way.
    [HarmonyPatch(typeof(ITab_Bills), "FillTab")]
    public static class Patch_BillsTabBench
    {
        // ITab_Bills layout (tab-local): Add bill at (10, 10, 150, 29), the
        // paste button at (WinSize.x - PasteX, PasteY, PasteSize) with the
        // TweakValue defaults 420 - 48, 3, 24.
        private static readonly Rect ToggleRect = new Rect(170f, 10f, 190f, 29f);
        private static readonly Rect PasteRect = new Rect(372f, 3f, 24f, 24f);

        public static void Prefix()
        {
            if (!(Find.Selector.SingleSelectedThing is Building_WorkTable bench)
                || bench.Faction == null || !bench.Faction.IsPlayer)
                return;
            bool designated = BenchDesignations.Contains(bench);
            if (!designated && !(PlannerAutomation.Available
                    && PlannerProduction.CraftsImplants(bench.def)))
                return;

            BenchLock.EnsureLabels();
            if (designated && BillUtility.Clipboard != null)
                BenchLock.Guard(PasteRect);

            GameFont font = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                bool now = designated;
                Widgets.CheckboxLabeled(ToggleRect, BenchLock.Toggle, ref now);
                if (Mouse.IsOver(ToggleRect))
                    TooltipHandler.TipRegion(ToggleRect, BenchLock.ToggleTip);
                if (now != designated)
                    PlannerCommands.SetBenchDesignated(bench.thingIDNumber, now);
            }
            finally
            {
                Text.Font = font;
            }
        }
    }

    /// Add bill on a designated bench opens a single disabled entry
    /// explaining why, instead of the recipe list.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.DoListing))]
    public static class Patch_BenchAddBill
    {
        private static readonly Func<List<FloatMenuOption>> LockedOptions = static () =>
        {
            BenchLock.EnsureLabels();
            return new List<FloatMenuOption> { new FloatMenuOption(BenchLock.NoNewBills, null) };
        };

        public static void Prefix(BillStack __instance,
            ref Func<List<FloatMenuOption>> recipeOptionsMaker)
        {
            if (BenchDesignations.Contains(__instance.billGiver))
                recipeOptionsMaker = LockedOptions;
        }
    }

    /// The suspend toggle of a bill row on a designated bench.
    [HarmonyPatch(typeof(Bill), nameof(Bill.DoInterface))]
    public static class Patch_BenchBillRow
    {
        // Bill.CanCopy is protected and virtual; the delegate dispatches
        // virtually and is created once.
        private static readonly Func<Bill, bool> CanCopy =
            AccessTools.MethodDelegate<Func<Bill, bool>>(
                AccessTools.PropertyGetter(typeof(Bill), "CanCopy"));

        public static void Prefix(Bill __instance, float x, float y, float width)
        {
            if (!BenchDesignations.Contains(__instance.billStack?.billGiver)) return;
            // Vanilla lays the row's top-right buttons out right to left:
            // delete (24), copy (24 + 4 gap, when copyable), then suspend.
            float suspendX = x + width - 48f - (CanCopy(__instance) ? 28f : 0f);
            BenchLock.Guard(new Rect(suspendX, y, 24f, 24f));
        }
    }

    /// The Suspended / Not suspended button of the bill details dialog for
    /// a bill on a designated bench: the first row of the left column.
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    public static class Patch_BenchBillConfig
    {
        public static void Prefix(Bill_Production ___bill, Rect inRect)
        {
            if (!BenchDesignations.Contains(___bill?.billStack?.billGiver)) return;
            float width = (int)((inRect.width - 34f) / 3f);
            BenchLock.Guard(new Rect(0f, 80f, width, 30f));
        }
    }
}
