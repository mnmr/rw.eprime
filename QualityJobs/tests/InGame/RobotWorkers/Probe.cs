using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace RobotWorkersVerification
{
    // Main-thread test fixture in a disposable managed run. Production work,
    // quality generation and game job consumption are never replaced.
    [StaticConstructorOnStartup]
    public static partial class Probe
    {
        private static bool ran;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static Map map;
        private static QualityJobsStore store;
        private static Pawn robot, colonist;
        private static RecipeDef recipe;
        private static int failures;
        static Probe()
        {
            new Harmony("eprime.qualityjobs.robotworkersprobe").Patch(
                AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        }
        private static void Check(bool pass, string message)
        {
            Log.Message("[RobotWorkersProbe] " + (pass ? "PASS " : "FAIL ") + message);
            if (!pass) failures++;
        }
        private static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            if (ran) { ContinueScenario(); return; }
            ran = true;
            try
            {
                map = Find.CurrentMap; store = QualityJobsStore.Active;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                foreach (Pawn p in map.mapPawns.AllPawns.ToArray())
                    if (p.CurJob != null && p.CurJob.def == null)
                    { p.jobs.curJob = null; p.jobs.curDriver = null; p.jobs.ClearQueuedJobs(); p.pather.StopDead(); }
                colonist = map.mapPawns.FreeColonistsSpawned.First(p => !p.Downed && !p.Dead
                    && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    && !p.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Tailoring")));
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned) AssignWorkRoles(pawn, false);
                recipe = DefDatabase<RecipeDef>.GetNamed("Make_Apparel_CowboyHat");
                var factory = AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),
                    "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) });
                robot = (Pawn)factory.Invoke(null, new object[] { "RPP_Bot_Omni_V", colonist.Position, map });
                Check(robot.Spawned && robot.Faction == Faction.OfPlayer && robot.RaceProps.IsMechanoid,
                    "Misc Robots factory creates a spawned player robot");
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToArray())
                {
                    p.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Tailoring"), 0);
                    p.workSettings.SetPriority(WorkTypeDefOf.Construction, 0);
                }
                Log.Message("[RobotWorkersProbe] FACT robot=" + robot.def.defName + " colonist=" + robot.IsColonist
                    + " tailoring=" + robot.workSettings.WorkIsActive(DefDatabase<WorkTypeDef>.GetNamed("Tailoring"))
                    + " construction=" + robot.workSettings.WorkIsActive(WorkTypeDefOf.Construction)
                    + " fixedSkill=" + robot.RaceProps.mechFixedSkillLevel);
                Check(Dispatcher.SelectFinisher(map, recipe, default, false) == robot,
                    "capable robot is selected to finish a quality bill when no human is enabled");
                Check(Dispatcher.SelectConstructionFinisher(map, default) == robot,
                    "capable robot is selected to finish a quality building when no human is enabled");
                var setting = AccessTools.Field(typeof(QualityJobsStore), "allowRobotPreparation");
                Check(setting != null && (bool)setting.GetValue(store),
                    "robot preparation setting exists and defaults on for an older save");
                var bill = (Bill_ProductionWithUft)BillUtility.MakeNewBill(recipe);
                Log.Message("[RobotWorkersProbe] FACT baseline ordinary bill preparation permitted="
                    + bill.PawnAllowedToStartAnew(robot));
                if (setting == null) Mark(failures == 0 ? "SUCCESS" : "FAILED " + failures + " assertions");
                else SetupScenario();
            }
            catch (Exception ex) { Log.Error("[RobotWorkersProbe] FAILED " + ex); Mark("FAILED " + ex); }
        }
        private static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "robot-workers-stage.txt"), value);
    }
}
