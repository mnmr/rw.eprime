using System;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace RobotWorkersVerification
{
    public static partial class Probe
    {
        private static int FactsRevision => (int)AccessTools.Field(typeof(QualityJobsStore), "ExternalPawnFactsRevision").GetValue(store);
        private static void VerifySelectionContracts()
        {
            Check(Dispatcher.FactsFor(robot, recipe, Dispatcher.WorkTypeForRecipe(recipe)).Skill == robot.RaceProps.mechFixedSkillLevel,
                scenario + " candidate ranking uses robot fixed skill");
            Check(Dispatcher.FactsFor(robot, recipe, Dispatcher.WorkTypeForRecipe(recipe)).XpMilli == 0,
                scenario + " mechanical worker has no learning XP rank advantage");
            if (!misc)
            {
                Check(!Dispatcher.FactsFor(builderRobot, recipe, Dispatcher.WorkTypeForRecipe(recipe)).WorkTypeEnabled,
                    scenario + " constructoid is excluded from tailoring candidates");
                return;
            }
            var factory = AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),
                "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) });
            Pawn hauler = (Pawn)factory.Invoke(null, new object[] { "AIRobot_Hauler", origin, map });
            Check(!Dispatcher.FactsFor(hauler, recipe, Dispatcher.WorkTypeForRecipe(recipe)).WorkTypeEnabled
                && !Dispatcher.ConstructionFactsFor(hauler).WorkTypeEnabled,
                scenario + " base Misc Robots hauler cannot become a crafter or builder");
            hauler.Destroy();
            var stationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            ThingDef stationDef = DefDatabase<ThingDef>.AllDefsListForReading
                .First(d => d.thingClass == stationType && d.defName.Contains("Omni") && d.defName.EndsWith("_V"));
            Thing station = ThingMaker.MakeThing(stationDef);
            station.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(station, origin + new IntVec3(-5, 0, 0), map);
            int before = FactsRevision;
            AccessTools.Method(stationType, "AddRobotToContainer").Invoke(station, new object[] { robot });
            Check(!robot.Spawned && FactsRevision != before,
                scenario + " docking robot invalidates pawn facts immediately while paused");
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == null
                && Dispatcher.SelectAutoFinisher(map, null, default) == null,
                scenario + " docked best robot retains the auto-best quality threshold for items and buildings");
            Check(Dispatcher.SelectFinisher(map, recipe, default, false) == colonist,
                scenario + " explicit threshold may select the available human while best robot is docked");
            MinifiedThing packed = station.MakeMinified();
            GenSpawn.Spawn(packed, origin + new IntVec3(-5, 0, 0), map);
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == null,
                "packed recharge station retains its docked robot in the auto-best threshold");
            packed.DeSpawn(); colonist.inventory.innerContainer.TryAdd(packed);
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == null,
                "inventory-packed recharge station retains its docked robot in the auto-best threshold");
            colonist.inventory.innerContainer.Remove(packed); packed.InnerThing = null; packed.Destroy();
            GenSpawn.Spawn(station, origin + new IntVec3(-5, 0, 0), map);
            // Invalidation/no-op behavior is an independent cache contract:
            // a fresh selector result alone cannot prove old UI snapshots were invalidated.
            Type support = typeof(QualityJobsStore).Assembly.GetType("QualityJobs.WorkerCapabilities");
            before = FactsRevision;
            AccessTools.Method(support, "ReleaseMap").Invoke(null, new object[] { map });
            Check(FactsRevision != before, "releasing a map's registered station invalidates pawn facts");
            before = FactsRevision;
            AccessTools.Method(support, "ReleaseMap").Invoke(null, new object[] { map });
            Check(FactsRevision == before, "repeated station map release is a revision-preserving no-op");
            AccessTools.Method(support, "RegisterRobotStation").Invoke(null, new object[] { station });
            before = FactsRevision;
            AccessTools.Method(stationType, "Button_SpawnBot").Invoke(station, null);
            Check(robot.Spawned && FactsRevision != before && Dispatcher.SelectAutoFinisher(map, recipe, default) == robot,
                scenario + " undocking immediately restores eligible best robot");
            before = FactsRevision;
            robot.SetFaction(Faction.OfAncientsHostile);
            Check(FactsRevision != before && Dispatcher.SelectFinisher(map, recipe, default, false) != robot,
                scenario + " faction loss immediately removes robotic finisher eligibility");
            robot.SetFaction(Faction.OfPlayer);
            // Remove only this temporary fixture station without destroying the
            // factory robot used by the actual-work scenario.
            AccessTools.Field(stationType, "robot").SetValue(station, null);
            AccessTools.Field(robot.GetType(), "rechargeStation").SetValue(robot, null);
            station.Destroy(); robot.Position = origin;
            robot.DeSpawn();
            if (!colonist.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))
                colonist.health.AddHediff(HediffDefOf.MechlinkImplant, colonist.health.hediffSet.GetBrain());
            Pawn mentalMech = NewMech("Mech_Fabricor", origin);
            Require(Dispatcher.SelectAutoFinisher(map, recipe, default) == mentalMech,
                "controlled fabricor establishes the auto-best ceiling over the learner");
            bool started = mentalMech.mindState.mentalStateHandler.TryStartMentalState(
                MentalStateDefOf.BerserkMechanoid, forced: true, forceWake: true, transitionSilently: true);
            Require(started && mentalMech.InMentalState, "vanilla mech enters real BerserkMechanoid state");
            Check(Dispatcher.SelectAutoFinisher(map, recipe, default) == null,
                "temporarily berserk best mech retains the auto-best quality threshold");
            mentalMech.MentalState.RecoverFromState();
            VerifyDeathInvalidation(mentalMech);
            GenSpawn.Spawn(robot, origin, map);
        }

        private static void VerifyDeathInvalidation(Pawn bestWorker)
        {
            // A fresh selector would hide a missed invalidation. Exercise the
            // published API and the bill panel's display string while paused.
            var bill = (Bill_ProductionWithUft)BillUtility.MakeNewBill(recipe);
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = 1;
            bench.BillStack.AddBill(bill);
            string id = BillIds.IdOf(bill);
            Commands.SetBillManaged(id, true);
            Commands.SetBillAutoBest(id, true);
            Commands.SetBillRequireInspired(id, false);
            Commands.SetBillRequireSpecialist(id, false);
            try
            {
                store.GameComponentUpdate();
                // Initial publication may normalize vanilla bill pause state
                // and schedule another publication. Drain that setup work.
                store.GameComponentUpdate();
                ManagedBillJob before = QualityJobsApi.GetManagedJobs().Jobs
                    .OfType<ManagedBillJob>().Single(j => j.Bill == bill);
                Require(before.Settings.SkillGate == 10,
                    "published auto-best gate starts at the disposable fabricor's skill 10");
                Type panelType = typeof(QualityJobsStore).Assembly.GetType("QualityJobs.UI.BillQualityPanel");
                object panel = Activator.CreateInstance(panelType,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic, null, new object[] { bill }, null);
                var refresh = AccessTools.Method(panelType, "EnsureAutoBest");
                var label = AccessTools.Field(panelType, "_autoBestCurrentLabel");
                refresh.Invoke(panel, null);
                Require((string)label.GetValue(panel)
                    == "QJ_AutoBestCurrent".Translate(bestWorker.LabelShort).ToString(),
                    "bill panel initially names the disposable fabricor as current best");
                int tick = Find.TickManager.TicksGame;
                bestWorker.Kill(null);
                store.GameComponentUpdate();
                ManagedBillJob after = QualityJobsApi.GetManagedJobs().Jobs
                    .OfType<ManagedBillJob>().Single(j => j.Bill == bill);
                Check(bestWorker.Dead && Find.TickManager.TicksGame == tick
                    && after.Settings.SkillGate == 5,
                    "death publishes the surviving learner's skill 5 auto-best gate while paused"
                    + " (actual " + after.Settings.SkillGate + ")");
                refresh.Invoke(panel, null);
                Check((string)label.GetValue(panel)
                    == "QJ_AutoBestCurrent".Translate(colonist.LabelShort).ToString(),
                    "bill panel replaces the dead current-best worker with the surviving learner while paused"
                    + " (actual " + label.GetValue(panel) + ")");
            }
            finally
            {
                bench.BillStack.Delete(bill);
            }
        }
    }
}
