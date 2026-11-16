using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// way are suspended as they are added (Patch_BenchStrayBill) or by
    /// the next reconcile pass (PlannerBenches).
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
    /// can craft implants. Drawn before vanilla so its click is never taken
    /// by a bill row scrolled under it. A designated bench shows Implanner's
    /// own production queue (UI.BenchQueue) instead of the vanilla tab, at
    /// its own size; the toggle sits in that tab's header, so the bench can
    /// always be released.
    [HarmonyPatch(typeof(ITab_Bills), "FillTab")]
    public static class Patch_BillsTabBench
    {
        // ITab_Bills layout (tab-local): Add bill at (10, 10, 150, 29), the
        // paste button at (WinSize.x - PasteX, PasteY, PasteSize) with the
        // TweakValue defaults 420 - 48, 3, 24. WinSize is read live: Nice
        // Bill Tab rewrites it on game versions that allow that.
        internal static readonly Rect ToggleRect = new Rect(170f, 10f, 190f, 29f);
        internal static readonly AccessTools.FieldRef<Vector2> WinSize =
            AccessTools.StaticFieldRefAccess<Vector2>(
                AccessTools.Field(typeof(ITab_Bills), "WinSize"));

        private static readonly AccessTools.FieldRef<InspectTabBase, Vector2> Size =
            AccessTools.FieldRefAccess<InspectTabBase, Vector2>("size");

        public static bool Prefix(ITab_Bills __instance)
        {
            bool designated = false;
            Building_WorkTable? bench = Find.Selector.SingleSelectedThing as Building_WorkTable;
            bool offered = bench != null && Offered(bench, out designated);
            // Under Nice Bill Tab the queue needs its prefix out of the way
            // (Patch_NiceBillTabYield); without that the old placement stays.
            if (designated && !(NiceBillTab.ReplacesTab && NiceBillTab.TabPrefix == null)
                && UI.BenchQueue.Draw(bench!))
            {
                Size(__instance) = UI.BenchQueue.TabSize;
                return false;
            }
            // Back on a bench vanilla draws: its layout is sized by WinSize.
            if (!NiceBillTab.ReplacesTab && Size(__instance) == UI.BenchQueue.TabSize)
                Size(__instance) = WinSize();
            if (!offered) return true;
            // Nice Bill Tab draws its own tab instead; the toggle goes into
            // its left pane (Patch_NiceBillTabLeftPane).
            if (NiceBillTab.ReplacesTab && !(designated && NiceBillTab.TabPrefix != null))
                return true;

            BenchLock.EnsureLabels();
            if (designated && BillUtility.Clipboard != null)
                BenchLock.Guard(new Rect(WinSize().x - 48f, 3f, 24f, 24f));
            DrawToggle(ToggleRect, bench!, designated);
            return true;
        }

        internal static bool Offered(Building_WorkTable bench, out bool designated)
        {
            designated = false;
            if (bench.Faction == null || !bench.Faction.IsPlayer) return false;
            designated = BenchDesignations.Contains(bench);
            return designated || PlannerProduction.CraftsImplants(bench.def);
        }

        internal static void DrawToggle(Rect rect, Building_WorkTable bench, bool designated)
        {
            BenchLock.EnsureLabels();
            GameFont font = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                bool now = designated;
                Widgets.CheckboxLabeled(rect, BenchLock.Toggle, ref now);
                if (Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, BenchLock.ToggleTip);
                if (now != designated)
                    PlannerCommands.SetBenchDesignated(bench.thingIDNumber, now);
            }
            finally
            {
                Text.Font = font;
            }
        }
    }

    /// Nice Bill Tab (Andromeda.NiceBillTab) replaces the whole bills tab
    /// while its own switch is on, so the vanilla toggle position lands on
    /// its search row and none of the bench guards are drawn. Resolved by
    /// name; each absent member disables only the part that needs it.
    internal static class NiceBillTab
    {
        // Cache contract — Owner: process. Key: none. Value: Nice Bill
        // Tab's left-pane draw method, its FillTab prefix, its
        // closest-thing lookup, and accessors
        // to its static EnabledMod switch and list-refresh flag, or null
        // when the mod is not loaded (immutable). Dependencies: loaded assembly set (static per
        // session). Refresh: resolved once at patch time. Equality: never
        // changes. Teardown: none; retains no game object.
        internal static readonly MethodInfo? DrawLeftPart;
        internal static readonly MethodInfo? TabPrefix;
        internal static readonly MethodInfo? FindClosest;
        internal static readonly AccessTools.FieldRef<bool>? RefreshList;
        private static readonly AccessTools.FieldRef<bool>? enabled;

        static NiceBillTab()
        {
            Type? drawer = GenTypes.GetTypeInAnyAssembly("NiceBillTab.TabBillsDrawer");
            FieldInfo? field = GenTypes.GetTypeInAnyAssembly("NiceBillTab.Settings")
                ?.GetField("EnabledMod", BindingFlags.Public | BindingFlags.Static);
            MethodInfo? method = drawer?.GetMethod("DrawLeftPart",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Rect), typeof(Building_WorkTable) }, null);
            if (method == null || field == null || field.FieldType != typeof(bool)) return;
            DrawLeftPart = method;
            enabled = AccessTools.StaticFieldRefAccess<bool>(field);

            MethodInfo? prefix = GenTypes.GetTypeInAnyAssembly("NiceBillTab.ITab_Bills_FillTab_Patch")
                ?.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(ITab_Bills) }, null);
            if (prefix != null && prefix.ReturnType == typeof(bool)) TabPrefix = prefix;
            FieldInfo? refresh = drawer!.GetField("shouldRefreshFilter",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (refresh != null && refresh.FieldType == typeof(bool))
                RefreshList = AccessTools.StaticFieldRefAccess<bool>(refresh);
            MethodInfo? closest = GenTypes.GetTypeInAnyAssembly("NiceBillTab.Utils")
                ?.GetMethod("FindClosestOnMap", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(ThingDef) }, null);
            if (closest != null && closest.ReturnType == typeof(Thing)) FindClosest = closest;
        }

        /// True while Nice Bill Tab draws the bills tab in place of vanilla.
        internal static bool ReplacesTab => enabled != null && enabled();
    }

    /// Under Nice Bill Tab the toggle takes a row at the top of its left
    /// pane, which moves down to make room, so nothing overlaps.
    [HarmonyPatch]
    public static class Patch_NiceBillTabLeftPane
    {
        private const float Band = 30f;

        public static bool Prepare() => NiceBillTab.DrawLeftPart != null;

        public static MethodBase TargetMethod() => NiceBillTab.DrawLeftPart!;

        public static void Prefix(ref Rect __0, Building_WorkTable __1)
        {
            if (__1 == null || !Patch_BillsTabBench.Offered(__1, out bool designated))
                return;
            Rect toggle = new Rect(__0.x, __0.y,
                Mathf.Min(Patch_BillsTabBench.ToggleRect.width, __0.width),
                Patch_BillsTabBench.ToggleRect.height);
            Patch_BillsTabBench.DrawToggle(toggle, __1, designated);
            __0.yMin += Band;
        }
    }

    /// A designated bench shows Implanner's production queue under Nice
    /// Bill Tab too: Nice Bill Tab's own FillTab prefix is skipped for that
    /// bench, so Patch_BillsTabBench draws. Its setting is not touched, and
    /// other benches keep its layout.
    [HarmonyPatch]
    public static class Patch_NiceBillTabYield
    {
        public static bool Prepare() => NiceBillTab.TabPrefix != null;

        public static MethodBase TargetMethod() => NiceBillTab.TabPrefix!;

        public static bool Prefix(ref bool __result)
        {
            if (!NiceBillTab.ReplacesTab
                || !BenchDesignations.Contains(Find.Selector.SingleSelectedThing as IBillGiver))
                return true;
            // Its bill list is cached per bench and goes stale meanwhile.
            if (NiceBillTab.RefreshList != null) NiceBillTab.RefreshList() = true;
            __result = true;
            return false;
        }
    }

    /// Nice Bill Tab's "create task on available workbench" takes the closest
    /// bench of each type that can make the item. A designated bench is
    /// passed over for the closest other bench of that type, or for none,
    /// so Nice Bill Tab tries the next bench type and the task is crafted.
    /// The lookup also finds ingredient items; those are left alone.
    [HarmonyPatch]
    public static class Patch_NiceBillTabBenchPick
    {
        public static bool Prepare() => NiceBillTab.FindClosest != null;

        public static MethodBase TargetMethod() => NiceBillTab.FindClosest!;

        public static void Postfix(ThingDef __0, ref Thing __result)
        {
            Thing picked = __result;
            if (picked?.Map == null || !BenchDesignations.Contains(picked as IBillGiver)) return;
            List<Thing> things = picked.Map.listerThings.ThingsOfDef(__0);
            Thing? best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (BenchDesignations.Contains(thing as IBillGiver)) continue;
                int distance = thing.Position.DistanceToSquared(picked.Position);
                if (distance >= bestDistance) continue;
                best = thing;
                bestDistance = distance;
            }
            __result = best!;
        }
    }

    /// A bill added to a designated bench by anything but Implanner (another
    /// mod's tab, paste or automation) is suspended at once, as the
    /// reconcile pass would do later. The caller keeps a valid bill. Reads
    /// only the published designations, so every multiplayer client agrees.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    public static class Patch_BenchStrayBill
    {
        public static void Postfix(BillStack __instance, Bill bill)
        {
            if (!BenchDesignations.AddingOwnBill && bill != null
                && BenchDesignations.Contains(__instance.billGiver))
                bill.suspended = true;
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
