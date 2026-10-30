using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRolesMechanicalVerification
{
    // Disposable managed-run fixture. Native job search, pathing and job
    // drivers consume actual filth; no successful work is mocked.
    [StaticConstructorOnStartup]
    public static partial class Probe
    {
        private static bool started, done;
        private static int elapsed;
        private static Map map;
        private static Pawn[] workers;
        private static Filth[] dirt;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static Probe() => new Harmony("eprime.workroles.mechanicalprobe").Patch(
            AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        private static void Mark(string text) => File.WriteAllText(Path.Combine(Root, "mechanical-stage.txt"), text);
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Log.Message("[MechanicalProbe] PASS " + message);
        }
        private static string Roles(RecommendationPlan plan) => string.Join(";", Enumerable.Range(0, plan.PawnCount)
            .Select(p => string.Join(",", Enumerable.Range(0, plan.RoleCountAt(p)).Select(i => plan.RoleAt(p, i)))));
        private static void Update()
        {
            if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (!started)
                {
                    started = true;
                    if (PhaseMode.StartsWith("reload-")) ResumeFixture(); else Setup();
                    return;
                }
                if (phase == 100) { ContinuePresentation(); return; }
                if (phase != 0) { ContinueLifecycle(); return; }
                if (elapsed > 10000) throw new InvalidOperationException("Work timeout: " + string.Join(";", workers.Select(p => p.LabelShort + ": " + p.CurJob)));
                for (int i = 0; i < 50; i++)
                {
                    Find.TickManager.DoSingleTick(); elapsed++;
                    if (dirt.All(f => f.Destroyed))
                    {
                        for (int w = 0; w < workers.Length; w++) Check(dirt[w].Destroyed, workers[w].def.defName + " consumed real cleaning work through its native scheduler");
                        BeginLifecycle();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                done = true; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Log.Error("[MechanicalProbe] FAILED " + ex); Mark("FAILED " + ex);
            }
        }
        private static void Setup()
        {
            map = Find.CurrentMap; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            RoleStore store = RoleStore.Current;
            foreach (Pawn p in map.mapPawns.AllPawns.ToArray())
                if (p.CurJob != null && p.CurJob.def == null)
                { p.jobs.curJob = null; p.jobs.curDriver = null; p.jobs.ClearQueuedJobs(); p.pather.StopDead(); }
            var humans = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby()).ToList();
            RoleCommands.SetMechanicalWorkersEnabled(true);
            if (PhaseMode == "presentation" && !ModsConfig.BiotechActive)
            { PrepareRobotsWithoutBiotech(); PreparePresentation(); return; }
            Check(!store.mechanicalRecommendationsEnabled, "mechanical recommendation integration defaults off");
            Pawn mechanitor = humans.First(p => !p.Downed);
            if (!mechanitor.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))
                mechanitor.health.AddHediff(HediffDefOf.MechlinkImplant, mechanitor.health.hediffSet.GetBrain());
            foreach (Pawn p in humans) if (p.drafter != null) p.drafter.Drafted = true;
            var positions = new List<IntVec3>();
            for (int i = 0; i < 3; i++) positions.Add(map.AllCells.First(c => c.x > 8 && c.z > 8
                && c.x < map.Size.x - 8 && c.z < map.Size.z - 8 && positions.All(p => p.DistanceTo(c) > 12f)
                && CellRect.CenteredOn(c, 7, 7).Cells.All(x => x.Standable(map) && x.GetEdifice(map) == null)));
            IntVec3 origin = positions[0];
            var factory = AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"), "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) });
            Pawn cleaner = (Pawn)factory.Invoke(null, new object[] { "AIRobot_Cleaner", origin, map });
            Pawn omni = (Pawn)factory.Invoke(null, new object[] { "RPP_Bot_Omni_V", positions[1], map });
            Pawn mech = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Cleansweeper"), Faction.OfPlayer);
            GenSpawn.Spawn(mech, positions[2], map);
            mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            mechanitor.mechanitor.AssignPawnControlGroup(mech, MechWorkModeDefOf.Work);
            workers = new[] { cleaner, omni, mech }; dirt = new Filth[workers.Length];
            var group = mech.GetMechControlGroup();
            Role clean = store.RoleByTemplate("WS_Cleaner");
            Check(clean != null, "default Cleaner role is available");
            for (int i = 0; i < workers.Length; i++)
            {
                Pawn pawn = workers[i];
                map.areaManager.TryMakeNewAllowed(out Area_Allowed area);
                foreach (IntVec3 cell in CellRect.CenteredOn(pawn.Position, 3, 3).Cells)
                { area[cell] = true; map.areaManager.Home[cell] = true; }
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = clean.id } });
                Check(CompiledJobOrders.NormalFor(pawn).Any(g => g.def.defName == "CleanFilth"), pawn.def.defName + " role admits cleaning");
                Check(CompiledJobOrders.NormalFor(pawn).All(g => g.def.workType == WorkTypeDefOf.Cleaning), pawn.def.defName + " role excludes unrelated work");
                IntVec3 target = pawn.Position + IntVec3.East;
                map.terrainGrid.SetTerrain(target, TerrainDefOf.Concrete);
                Check(FilthMaker.TryMakeFilth(target, map, ThingDefOf.Filth_Dirt), "native filth factory creates the cleaning target");
                dirt[i] = target.GetThingList(map).OfType<Filth>().First();
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            Check(mech.GetMechControlGroup() == group && group.WorkMode == MechWorkModeDefOf.Work, "role edits preserve native mech group and mode");
            // Compare after staging using the same human facts so fixture
            // mechlink/drafting changes cannot masquerade as integration drift.
            string staged = Roles(RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, humans), store.recommendationTuning));
            RoleCommands.SetMechanicalWorkersEnabled(false);
            string disabled = Roles(RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, humans), store.recommendationTuning));
            Check(staged == disabled, "showing mechanical workers leaves every human recommendation unchanged");
            Check(workers.All(p => !store.IsManaged(p)), "support off releases work-order ownership");
            RoleCommands.SetMechanicalWorkersEnabled(true);
            Check(workers.All(store.IsManaged), "support re-enable restores dormant assignments");
            VerifyWarmOrders(humans[0]);
            if (PhaseMode == "presentation") { PreparePresentation(); return; }
            Mark("RUNNING native cleaning");
        }
    }
}
