using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RobotWorkersVerification
{
    public static partial class Probe
    {
        private static int reloadPhase, reloadRobot, reloadHuman, reloadBench, reloadUft, reloadDestination;
        private static string reloadSourceBill, reloadDestinationBill;
        private static bool wroteCheckbox;
        private static int savedPackedStation;
        private static void BeginProvenanceReload(Building_WorkTable destination, Bill_ProductionWithUft bill)
        {
            CreatePackedSaveFixture();
            reloadRobot = robot.thingIDNumber; reloadHuman = colonist.thingIDNumber;
            reloadBench = bench.thingIDNumber; reloadUft = prepared.thingIDNumber;
            reloadDestination = destination.thingIDNumber;
            reloadSourceBill = BillIds.IdOf(sourceBill); reloadDestinationBill = BillIds.IdOf(bill);
            reloadPhase = 1;
            GameDataSaveLoader.SaveGame("RobotWorkerPreparationOn");
            GameDataSaveLoader.LoadGame("RobotWorkerPreparationOn");
        }

        private static void ResumeProvenanceReload()
        {
            if (map == Find.CurrentMap) return;
            map = Find.CurrentMap; store = QualityJobsStore.Active;
            robot = map.mapPawns.AllPawnsSpawned.First(p => p.thingIDNumber == reloadRobot);
            builderRobot = robot;
            colonist = map.mapPawns.AllPawnsSpawned.First(p => p.thingIDNumber == reloadHuman);
            bench = (Building_WorkTable)map.listerThings.AllThings.First(t => t.thingIDNumber == reloadBench);
            prepared = map.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == reloadUft) as UnfinishedThing
                ?? robot.carryTracker.CarriedThing as UnfinishedThing;
            Require(prepared != null && prepared.thingIDNumber == reloadUft,
                "saved unfinished item survives as spawned or carried work");
            var destination = (Building_WorkTable)map.listerThings.AllThings.First(t => t.thingIDNumber == reloadDestination);
            sourceBill = (Bill_ProductionWithUft)bench.BillStack.Bills.First(b => BillIds.IdOf(b) == reloadSourceBill);
            var bill = (Bill_ProductionWithUft)destination.BillStack.Bills.First(b => BillIds.IdOf(b) == reloadDestinationBill);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (reloadPhase == 1)
            {
                Check((bool)AccessTools.Field(typeof(QualityJobsStore), "allowRobotPreparation").GetValue(store),
                    "full save/reload preserves preparation ON");
                SetPreparation(false);
                Check(robot.CurJob?.def != JobDefOf.DoBill,
                    "OFF after full reload stops the active shared-source preparation job");
                if (!prepared.Spawned) robot.carryTracker.TryDropCarriedThing(origin, ThingPlaceMode.Near, out _);
                bill.nextTickToSearchForIngredients = 0;
                Job blocked = billGiver.JobOnThing(robot, destination, true);
                Check(!Uses(blocked, prepared), "full save/reload preserves managed-source provenance against unmanaged resume");
                if (blocked != null) JobMaker.ReturnToPool(blocked);
                reloadPhase = 2;
                GameDataSaveLoader.SaveGame("RobotWorkerPreparationOff");
                GameDataSaveLoader.LoadGame("RobotWorkerPreparationOff");
                return;
            }
            Check(!(bool)AccessTools.Field(typeof(QualityJobsStore), "allowRobotPreparation").GetValue(store),
                "full save/reload preserves preparation OFF");
            VerifyPackedSaveFixture();
            bill.nextTickToSearchForIngredients = 0;
            Job denied = billGiver.JobOnThing(robot, destination, true);
            Check(!Uses(denied, prepared), "reloaded OFF setting still denies managed-source item through unmanaged destination");
            if (denied != null) JobMaker.ReturnToPool(denied);
            prepared.BoundBill = null; destination.Destroy(); SetPreparation(true); reloadPhase = 0;
        }

        private static void CreatePackedSaveFixture()
        {
            Type stationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            ThingDef stationDef = DefDatabase<ThingDef>.AllDefsListForReading
                .First(d => d.thingClass == stationType && d.defName.Contains("Omni") && d.defName.EndsWith("_III"));
            Thing station = ThingMaker.MakeThing(stationDef); station.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(station, origin + new IntVec3(-5, 0, 0), map);
            Pawn docked = (Pawn)AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),
                "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) })
                .Invoke(null, new object[] { "RPP_Bot_Omni_III", origin, map });
            AccessTools.Field(docked.GetType(), "rechargeStation").SetValue(docked, station);
            AccessTools.Field(stationType, "robotSpawnedOnce").SetValue(station, true);
            AccessTools.Method(stationType, "AddRobotToContainer").Invoke(station, new object[] { docked });
            // The native station otherwise tries to deploy its fully charged bot while packed after load.
            AccessTools.Method(stationType, "Notify_CallBotForShutdown").Invoke(station, null);
            MinifiedThing packed = station.MakeMinified();
            colonist.inventory.innerContainer.TryAdd(packed); savedPackedStation = packed.thingIDNumber;
        }

        private static void VerifyPackedSaveFixture()
        {
            var packed = (MinifiedThing)colonist.inventory.innerContainer.First(t => t.thingIDNumber == savedPackedStation);
            robot.SetFaction(Faction.OfAncientsHostile);
            var container = (System.Collections.IList)AccessTools.Field(packed.InnerThing.GetType(), "container").GetValue(packed.InnerThing);
            Pawn docked = (Pawn)container[0];
            var nativeGivers = (System.Collections.IList)AccessTools.Method(docked.GetType(), "GetWorkGivers").Invoke(docked, new object[] { false });
            Log.Message("[RobotWorkersProbe] FACT loaded docked robot=" + docked + " map=" + packed.InnerThing.MapHeld
                + " nativeGivers=" + nativeGivers.Count + " enabled=" + docked.workSettings.WorkIsActive(Dispatcher.WorkTypeForRecipe(recipe))
                + " disabled=" + docked.WorkTypeIsDisabled(Dispatcher.WorkTypeForRecipe(recipe))
                + " factsEnabled=" + Dispatcher.FactsFor(docked, recipe, Dispatcher.WorkTypeForRecipe(recipe)).WorkTypeEnabled
                + " auto=" + Dispatcher.SelectAutoFinisher(map, recipe, default));
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == null,
                "full save/reload re-registers the robot in an inventory-packed recharge station");
            colonist.inventory.innerContainer.Remove(packed); packed.Destroy();
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == colonist,
                "destroyed packed station releases its threshold contribution");
            robot.SetFaction(Faction.OfPlayer);
        }

        private static void OpenOptions()
        {
            SetPreparation(false);
            var property = AccessTools.Property(typeof(QualityJobsStore), "SettingsPresentation");
            object snapshot = property.GetValue(store);
            int billRevision = (int)AccessTools.Field(typeof(QualityJobsStore), "BillStatusRevision").GetValue(store);
            int planRevision = (int)AccessTools.Field(typeof(QualityJobsStore), "PlanStatusRevision").GetValue(store);
            SetPreparation(false);
            Check(ReferenceEquals(snapshot, property.GetValue(store))
                && billRevision == (int)AccessTools.Field(typeof(QualityJobsStore), "BillStatusRevision").GetValue(store)
                && planRevision == (int)AccessTools.Field(typeof(QualityJobsStore), "PlanStatusRevision").GetValue(store),
                "no-op preparation command preserves settings identity and unrelated revisions");
            new Harmony("eprime.qualityjobs.robotworkersprobe.ui").Patch(
                AccessTools.GetDeclaredMethods(typeof(Widgets)).First(m => m.Name == "CheckboxLabeled"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(ObserveCheckbox)));
            new Harmony("eprime.qualityjobs.robotworkersprobe.ui").Patch(
                AccessTools.Method(typeof(Commands), "SetAllowRobotPreparation"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(PreparationChanged)));
            Find.WindowStack.Add(new Dialog_QualityJobs());
        }

        private static void ObserveCheckbox(Rect rect, string label)
        {
            if (wroteCheckbox || Event.current.type != EventType.Repaint
                || label != "QJ_SettingsAllowRobotPreparation".Translate()) return;
            wroteCheckbox = true;
            Vector2 point = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax - 12, rect.center.y));
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            File.WriteAllText(Path.Combine(Root, "robot-workers-checkbox.txt"),
                ((int)point.x) + " " + ((int)point.y));
        }
        private static void PreparationChanged(bool value) => File.WriteAllText(
            Path.Combine(Root, "robot-workers-setting.txt"), value.ToString());
    }
}
