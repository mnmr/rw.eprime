using System;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Patches;
using QualityJobs.UI;
using RimShared.Tests.HelpShots;
using RimWorld;
using UnityEngine;
using Verse;

// Quality Jobs help image capture: the scenes and the mod's own recorder
// hooks. The scene walker, the recorder and the game-widget hooks are shared
// source (Shared/Tests/InGame/HelpShots); crops.json names the images cut
// from the frames (Shared/tools/automation/help-shots/crop-help.py).
namespace QualityJobsHelpShots
{
    public static class Hooks
    {
        public static void Install(Harmony harmony)
        {
            // "<prefix>:<Draw method>" for the dialogs and the bill panel.
            VanillaHooks.PatchDrawMethods(harmony, typeof(Dialog_ConstructionPlanConfig), "plan");
            VanillaHooks.PatchDrawMethods(harmony, AccessTools.TypeByName("QualityJobs.UI.Dialog_QualityJobs"), "options");
            VanillaHooks.PatchDrawMethods(harmony, AccessTools.TypeByName("QualityJobs.UI.BillQualityPanel"), "billpanel");
            harmony.Patch(AccessTools.Method(typeof(Patch_PlaySettings), "Postfix"),
                postfix: new HarmonyMethod(typeof(Hooks), nameof(Toolbar)));
            harmony.Patch(AccessTools.Method(typeof(Command_QualityJob), "GizmoOnGUI"),
                postfix: new HarmonyMethod(typeof(Hooks), nameof(Gizmo)));
        }

        /// The toolbar icon: drawn through the row (ButtonIcon, then a gap),
        /// so after the patch the row's cursor sits one gap past the icon's
        /// left edge in a row that grows leftwards.
        public static void Toolbar(WidgetRow row)
        {
            if (row != null) Recorder.Add("toolbar",
                new Rect(row.FinalX + WidgetRow.DefaultGap, row.FinalY, WidgetRow.IconSize, WidgetRow.IconSize));
        }

        public static void Gizmo(Vector2 topLeft) => Recorder.Add("gizmo", new Rect(topLeft.x, topLeft.y, 75f, 75f));
    }

    [StaticConstructorOnStartup]
    public static class Probe
    {
        static Probe() => HelpShots.Run("tests.qualityjobs.helpshots", Hooks.Install, Setup);

        private static Scene Add(string name, Action act, string hover = null, string click = null) =>
            HelpShots.Add(name, act, hover, click);

        private static Map Map => Find.CurrentMap;

        private static void Select(Thing thing)
        {
            HelpShots.CloseWindows();
            Find.Selector.ClearSelection();
            Find.Selector.Select(thing, playSound: false);
        }

        private static void Setup()
        {
            if (QualityJobsStore.Active == null) throw new InvalidOperationException("no Quality Jobs store");
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.Selector.ClearSelection();

            Add("toolbar", () => { });
            Add("options", () => Find.WindowStack.Add(
                (Window)Activator.CreateInstance(AccessTools.TypeByName("QualityJobs.UI.Dialog_QualityJobs"))));

            // A managed bill for a quality item, its panel beside the bill's own controls.
            Add("crafting", () =>
            {
                HelpShots.CloseWindows();
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("Make_Apparel_CowboyHat");
                if (!ManagedRecipes.IsManagedRecipe(recipe)) throw new InvalidOperationException("cowboy hat is not a managed recipe");
                Building_WorkTable bench = Map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                    .First(b => b.def.AllRecipes.Contains(recipe));
                var bill = (Bill_Production)BillUtility.MakeNewBill(recipe);
                bench.BillStack.AddBill(bill);
                Commands.SetBillManaged(bill.GetUniqueLoadID(), true);
                Commands.SetBillAutoBest(bill.GetUniqueLoadID(), true);
                Commands.SetBillTargetQuality(bill.GetUniqueLoadID(), 3);
                Find.WindowStack.Add((Window)AccessTools.Method(typeof(Bill_Production), "GetBillDialog").Invoke(bill, null));
            });

            // A blueprint: right-click its Quality Job command for the controls.
            Building_Bed bed = Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
                .First(b => b.TryGetComp<CompQuality>() != null);
            Add("construction", () =>
            {
                // The nearest free cell to that bed, the same in every run.
                IntVec3 cell = GenRadial.RadialCellsAround(bed.Position, 30f, false)
                    .First(c => c.InBounds(Map) && c.Standable(Map) && c.GetEdifice(Map) == null && c.GetFirstItem(Map) == null);
                Thing blueprint = GenConstruct.PlaceBlueprintForBuild(DefDatabase<ThingDef>.GetNamed("Armchair"),
                    cell, Map, Rot4.North, Faction.OfPlayer, ThingDefOf.Cloth);
                Commands.SetPlanMinSkill(blueprint.thingIDNumber, 15);
                Commands.SetPlanMinQuality(blueprint.thingIDNumber, 4);
                Select(blueprint);
            }, click: "right gizmo");
            // A completed building: the same command opens the rebuild panel.
            Add("rebuild", () => Select(bed), click: "gizmo");
        }
    }
}
