using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimShared.Tests.HelpShots;
using RimWorld;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

// Implanner help image capture: the scenes and the mod's own recorder hooks.
// The scene walker, the recorder and the game-widget hooks are shared source
// (Shared/Tests/InGame/HelpShots); crops.json names the images cut from the
// frames (Shared/tools/automation/help-shots/crop-help.py). Three runs: the
// default mod set pictures the mod alone, "implanner-help-shots-mods" the
// pages about other implant mods and "implanner-help-shots-quality" the
// options a quality-carrying implant adds (crops.json "variant").
namespace ImplannerHelpShots
{
    public static class Hooks
    {
        public static void Install(Harmony harmony)
        {
            Assembly mod = typeof(ImplannerStore).Assembly;
            // "<prefix>:<Draw method>" for the window's parts and the dialogs.
            foreach (var pair in new Dictionary<string, string>
                     {
                         { "ImplannerView", "view" }, { "Dialog_ExportPlans", "export" }, { "Dialog_ImportPlans", "import" },
                         { "Dialog_AutomationCleanup", "cleanup" },
                     })
                VanillaHooks.PatchDrawMethods(harmony, mod.GetType("Implanner.UI." + pair.Key), pair.Value);
            Type style = mod.GetType("Implanner.UI.PlannerStyle");
            VanillaHooks.Region(harmony, style, "Panel", "panel");
            VanillaHooks.Region(harmony, style, "ShadedPanel", "shaded");
            VanillaHooks.Region(harmony, mod.GetType("RimShared.UiLib.SegmentedControl"), "Row", "segments");
            // The inspect pane's open tab (a bench's bills): "itab:<type>".
            tabRect = AccessTools.Property(typeof(InspectTabBase), "TabRect");
            harmony.Patch(AccessTools.Method(typeof(InspectTabBase), "DoTabGUI"),
                prefix: new HarmonyMethod(typeof(Hooks), nameof(InspectTab)));
        }

        private static PropertyInfo tabRect;

        public static void InspectTab(InspectTabBase __instance) =>
            Recorder.Add("itab:" + __instance.GetType().Name, (Rect)tabRect.GetValue(__instance), screen: true);
    }

    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Assembly Mod = typeof(ImplannerStore).Assembly;
        private static Window dialog;

        static Probe() => HelpShots.Run("eprime.implanner.helpshots", Hooks.Install, Setup);

        private static Scene Add(string name, Action act, string hover = null, string click = null) =>
            HelpShots.Add(name, act, hover, click);

        private static PlannerModel Model => ImplannerStore.Current.Model;

        private static object View => Traverse.Create(dialog).Field("view").GetValue();
        private static Traverse State(string name) => Traverse.Create(View).Field(name);

        private static void Tab(string tab)
        {
            Type view = Mod.GetType("Implanner.UI.ImplannerView");
            FieldInfo field = AccessTools.Field(view, "curTab");
            field.SetValue(null, Enum.Parse(field.FieldType, tab));
            if (dialog == null || !Find.WindowStack.IsOpen(dialog))
            {
                dialog = (Window)Activator.CreateInstance(Mod.GetType("Implanner.UI.Dialog_Implanner"));
                Find.WindowStack.Add(dialog);
            }
        }

        private static Pawn Colonist(string name) =>
            Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p => p.LabelShort == name);

        /// A synced command, as the UI issues it.
        private static void Command(string name, params object[] arguments) =>
            AccessTools.Method(Mod.GetType("Implanner.PlannerCommands"), name).Invoke(null, arguments);

        private static void Search(string query) =>
            State("plans").Field("Search").GetValue<QuickSearchWidget>().filter.Text = query;

        private static void DefaultScenes()
        {
            Plan full = Model.Plans.First(p => p.Name == "Full");
            Add("overview", () =>
            {
                Tab("Overview");
                State("overview").Field("SelectedPawnId").SetValue(Colonist("Twiggy").thingIDNumber);
            });
            Add("plans", () =>
            {
                Tab("Plans");
                State("plans").Field("SelectedPlanId").SetValue(full.Id);
                State("plans").Field("Region").SetValue(0);
            });
            Add("picker-search", () => Search("leg"));
            Add("picker-purchase", () => { Search(""); Command("SetShowPurchaseOnly", true); });
            Add("newplan", () => Command("SetShowPurchaseOnly", false), click: "btn:New plan");
            Add("automation", () => { HelpShots.CloseWindows(dialog.GetType()); Tab("Automation"); });
            Add("reservations", () => Command("SetImplantReserve", "BionicArm", 1));
            Add("options", () => { Command("SetImplantReserve", "BionicArm", 0); Tab("Options"); });
            Add("purchase-tip", () => { }, hover: "check:Show purchase-only implants");

            // An Implanner bench's bills tab.
            Building_WorkTable bench = Find.CurrentMap.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>().First(b => b.def.defName == "FabricationBench");
            Add("bench", () =>
            {
                HelpShots.CloseWindows();
                Command("SetBenchDesignated", bench.thingIDNumber, true);
                // The production pass the next game tick would run, so the
                // queue shows the bills the colony's own plans ask for.
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                ImplannerStore store = ImplannerStore.Current;
                object index = AccessTools.Method(Mod.GetType("Implanner.ColonyIndex"), "Build")
                    .Invoke(null, new object[] { store.Model });
                object pass = Activator.CreateInstance(Mod.GetType("Implanner.ReconcilePass"), all, null,
                    new object[] { store.Model, index, true }, null);
                object change = AccessTools.Method(Mod.GetType("Implanner.PlannerProduction"), "Reconcile")
                    .Invoke(null, new object[] { store, pass });
                AccessTools.Method(typeof(ImplannerStore), "PublishPass").Invoke(store, new[] { change });
                Find.Selector.ClearSelection();
                Find.Selector.Select(bench, playSound: false);
                InspectPaneUtility.OpenTab(typeof(ITab_Bills));
            });
            Add(null, () => { Command("SetBenchDesignated", bench.thingIDNumber, false); Find.Selector.ClearSelection(); });
        }

        private static void Setup()
        {
            if (ImplannerStore.Current == null) throw new InvalidOperationException("no Implanner store");
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.Selector.ClearSelection();
            if (HelpShots.Variant == "implanner-help-shots-mods") ModScenes();
            else if (HelpShots.Variant == "implanner-help-shots-quality") QualityScenes();
            else DefaultScenes();
        }

        private static Plan PlanNamed(string name) => Model.Plans.First(p => p.Name == name);

        private static void Slot(string plan, string implant, string slot) =>
            Command("SetImplantSlot", PlanNamed(plan).Id, implant, slot, true);

        private static void Picker(string plan, int region, int body = -1)
        {
            Tab("Plans");
            State("plans").Field("SelectedPlanId").SetValue(PlanNamed(plan).Id);
            State("plans").Field("Region").SetValue(region);
            State("plans").Field("Body").SetValue(body);
        }

        /// Scrolls the implant picker until a row sits the given number of
        /// rows below the top of the list (the list's own scroll position
        /// is not known to the probe; the recorded row is).
        private static Func<bool> ScrollTo(string row, int rowsBelowTop)
        {
            int calls = 0;
            float last = float.NaN;
            return () =>
            {
                // Recorded regions lag the scroll change by a frame or two.
                if (calls++ % 4 != 0) return false;
                Traverse scroll = Traverse.Create(View).Field("pickerScroll");
                Vector2 position = scroll.GetValue<Vector2>();
                Rect? list = Recorder.Find("label:Everyone@view:DrawPlanEditor");
                Rect? found = Recorder.Find(row);
                if (list == null) return false;
                // Rows are 22 high; the list starts 38 below the plan's name row.
                float wanted = list.Value.yMax + 38f + rowsBelowTop * 22f;
                if (found == null) position.y += 300f;
                // There, or as near as the end of the list lets it come.
                else if (Mathf.Abs(found.Value.y - wanted) < 1f || found.Value.y == last) return true;
                else { last = found.Value.y; position.y += found.Value.y - wanted; }
                scroll.SetValue(position);
                return false;
            };
        }

        /// With a mod whose implants carry a quality: a plan's minimum
        /// quality and the upgrade option.
        private static void QualityScenes()
        {
            Command("CreatePlan", "Arms", 0, "", "");
            Slot("Arms", "BionicArm", "Shoulder#0");
            Slot("Arms", "BionicArm", "Shoulder#1");
            Command("SetPlanMinQuality", PlanNamed("Arms").Id, (int)QualityCategory.Good);
            Command("SetUpgradeByPriority", true);
            Add("min-quality", () => Picker("Arms", 0));
            Add("upgrade", () => Tab("Automation"));
        }

        /// The pages about other mods: two small plans instead of the
        /// colony's own, so the cards and pickers show only what the page
        /// is about.
        private static void ModScenes()
        {
            foreach (HediffDef def in DefDatabase<HediffDef>.AllDefsListForReading)
                if (def.label != null && (def.label.Contains("mechanites") || def.label.Contains("modular")))
                    HelpShots.Write("hediff " + def.defName + " '" + def.label + "'");
            foreach (Plan plan in Model.Plans.ToList()) Command("DeletePlan", plan.Id);
            Command("CreatePlan", "Everyone", 0, "", "");
            Slot("Everyone", "BionicEye", "Eye#0");
            Slot("Everyone", "BionicEye", "Eye#1");
            Slot("Everyone", "BionicLeg", "Leg#0");
            Slot("Everyone", "BionicLeg", "Leg#1");
            Command("CreatePlan", "Androids", 0, "Android4Tier", "");
            Slot("Androids", "ALReceptor", "LeftSightSensor#0");
            Slot("Androids", "ALReceptor", "RightSightSensor#0");
            foreach (string name in new[] { "Madcat", "Takeo", "Twiggy" })
                Command("AssignPlan", Colonist(name).thingIDNumber, PlanNamed("Everyone").Id);
            var bodies = (List<BodyDef>)AccessTools.Method(Mod.GetType("Implanner.Catalogs"), "PlannableBodies").Invoke(null, null);
            int android = bodies.FindIndex(b => b.defName == "Android4Tier");
            HelpShots.Write("bodies " + string.Join(",", bodies.Select(b => b.defName)));

            Add("plans-body", () => Picker("Everyone", 0));
            Add("body-filter", () => Picker("Everyone", 0, android));
            Add("newplan-body", () =>
            {
                var prompt = (Window)Activator.CreateInstance(Mod.GetType("Implanner.UI.Dialog_NewPlan"));
                Find.WindowStack.Add(prompt);
                AccessTools.Method(prompt.GetType(), "SetBody").Invoke(prompt, new object[] { "Android4Tier", "" });
            });
            // The human body's slots only: Android Tiers adds its own parts.
            Add("torso", () =>
            {
                HelpShots.CloseWindows(dialog.GetType());
                Slot("Everyone", "FSFImplantMechanitesArmor", "Torso#0");
                Picker("Everyone", 1, 0);
            }).Until = ScrollTo("label:Armor-enhancing mechanites", 6);
            Add("torso-2", null).Until = ScrollTo("label:Sense-enhancing mechanites", 4);
            Add("head", () =>
            {
                Slot("Everyone", "LTS_ModularBionicEye", "Eye#0");
                Picker("Everyone", 2, 0);
            }).Until = ScrollTo("label:Bionic eye (modular) (left eye)", 5);
            Add("options", () => Tab("Options"));
            Add("bladder-tip", () => { }, hover: "check:Allow multiple bladder implants");
        }
    }
}
