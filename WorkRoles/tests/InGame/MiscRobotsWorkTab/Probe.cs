using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.UI;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace MiscRobotsWorkTabProbe;
/// User report (2026-11-02): with MiscRobots WorkTab support, Misc. Robots++
/// Crafter/Omni bots research and operate scanners and every model fights
/// fires, yet WorkRoles marks Researcher/Firefighter red and never recommends
/// them. Phase "before" only records; phase "after" also asserts the fix and
/// watches the game consume the roles (a research job, an extinguished fire).
/// Without WorkTab support (2026-11-05 save), WorkRoles' Researcher role must
/// still drive Crafter/Omni research (every robotWorkTypes entry counts) and
/// every model with the vanilla beat-fire verb fights fires through its role.
[StaticConstructorOnStartup]
public static class Probe
{
 static bool started, done, outcomeRunning; static int elapsed;
 static Map map; static string phase; static bool native;
 static readonly List<Pawn> workers=new(); static readonly List<IntVec3> origins=new();
 static readonly Dictionary<Pawn,Thing> targets=new(); static readonly Dictionary<Pawn,HashSet<string>> jobs=new();
 static ResearchProjectDef project; static float progressBefore;
 static readonly string[] ResearchBots={"RPP_Bot_Crafter_III","RPP_Bot_Omni_III"};
 static readonly string[] FireBots={"AIRobot_Hauler_II","RPP_Bot_Builder_I","AIRobot_Cleaner"};
 static string Root => GenFilePaths.SaveDataFolderPath;
 static void Record(string s) => File.AppendAllText(Path.Combine(Root,"worktab-results.txt"),s+Environment.NewLine);
 static void Stage(string s) => File.WriteAllText(Path.Combine(Root,"worktab-stage.txt"),s);
 static object Field(object o,string n) => AccessTools.Field(o.GetType(),n).GetValue(o);
 static object Prop(object o,string n) => AccessTools.Property(o.GetType(),n).GetValue(o);
 static object Call(object o,string n,params object[] a) => AccessTools.GetDeclaredMethods(o.GetType()).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(o,a);
 static object Static(string type,string n,params object[] a) => AccessTools.GetDeclaredMethods(AccessTools.TypeByName(type)).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(null,a);
 static void Expect(bool ok,string what){ if(phase=="after"&&!ok)throw new Exception("EXPECT "+what); Record((ok?"PASS ":"FAIL ")+what); }
 static Probe() { var h=new Harmony("eprime.workroles.miscrobotsworktab");h.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));Log.Message("[MiscRobotsWorkTabProbe] initialized"); }
 static void Update()
 {
  if(done || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting)return;
  try {
   if(!started){started=true;Stage("SETUP");Setup();return;}
   if(outcomeRunning){StepOutcome();return;}
   if(File.Exists(Path.Combine(Root,"captured-chips"))){foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);BeginOutcome();}
  } catch(Exception ex){done=true;Record("ERROR "+ex);Stage("FAILED "+ex);}
 }
 static void Setup()
 {
  map=Find.CurrentMap; Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
  phase=File.ReadAllText(Path.Combine(Root,"worktab-phase.txt")).Trim();
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"wantsToOpen").SetValue(null,false);
  foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);
  RoleCommands.SetAutoOptimize(false);RoleCommands.SetMechanicalWorkersEnabled(false);
  var store=RoleStore.Current;
  Record("GAME "+VersionControl.CurrentVersionString+" phase="+phase);
  foreach(var mod in LoadedModManager.RunningModsListForReading)Record("MOD "+mod.PackageId+" root="+mod.RootDir);
  // native: no WorkTab support, WorkRoles alone picks managed robots' work.
  native=!(bool)Static("WorkRoles.MiscRobotSupport","get_WorkTabControlled");
  Record("WORKTAB_CONTROLLED "+!native);
  foreach(var p in map.mapPawns.AllPawnsSpawned.ToArray()) {
   if(p.CurJob!=null&&p.CurJob.def==null){p.jobs.curJob=null;p.jobs.curDriver=null;p.jobs.ClearQueuedJobs();p.pather.StopDead();}
   if(p.RaceProps.Humanlike&&p.drafter!=null)p.drafter.Drafted=true; else if(!p.RaceProps.Humanlike)p.DeSpawn();
  }
  var overseers=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed&&!p.DevelopmentalStage.Baby()).Take(6).ToArray();
  var parent=(MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);parent.SetFaction(Faction.OfPlayer);parent.Tile=TileFinder.RandomStartingTile();Find.WorldObjects.Add(parent);
  map=MapGenerator.GenerateMap(new IntVec3(100,1,100),parent,new MapGeneratorDef{defName="WorkTabProbeGround",genSteps=new List<GenStepDef>()},extraInitBeforeContentGen:m=>{foreach(var cell in m.AllCells)m.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);});
  for(int i=0;i<overseers.Length;i++){overseers[i].DeSpawn();GenSpawn.Spawn(overseers[i],new IntVec3(4+i,0,4),map);overseers[i].drafter.Drafted=true;} // despawn undrafts; idle colonists would take the bots' benches
  Current.Game.CurrentMap=map;Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
  // Think tree proof: WorkTab support swaps AIRobot's work node for vanilla JobGiver_Work.
  var tree=DefDatabase<ThinkTreeDef>.GetNamedSilentFail("X2_AIRobot");
  Record("THINKTREE "+(tree==null?"missing":string.Join(",",Descendants(tree.thinkRoot).Select(n=>n.GetType().FullName).Where(n=>n.Contains("JobGiver_Work")))));
  var factory=AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),"CreateRobot",new[]{typeof(string),typeof(IntVec3),typeof(Map)});
  foreach(var defName in ResearchBots.Concat(FireBots)) {
   var cell=map.AllCells.First(c=>c.x>10&&c.z>10&&c.x<map.Size.x-10&&c.z<map.Size.z-10&&origins.All(p=>p.DistanceTo(c)>9)&&CellRect.CenteredOn(c,7,7).Cells.All(x=>x.Standable(map)&&x.GetEdifice(map)==null));
   origins.Add(cell);workers.Add((Pawn)factory.Invoke(null,new object[]{defName,cell,map}));
  }
  PrepareRooms();
  var research=DefDatabase<WorkTypeDef>.GetNamed("Research"); var fire=WorkTypeDefOf.Firefighter;
  foreach(var p in workers)
   Record("NATIVE "+p.def.defName+" researchDisabled="+p.WorkTypeIsDisabled(research)+" fireDisabled="+p.WorkTypeIsDisabled(fire)+" workTabResearch="+(p.workSettings?.GetPriority(research)??-1)+" workTabFire="+(p.workSettings?.GetPriority(fire)??-1)+" disabledTags="+p.CombinedDisabledWorkTags+" disabledTypes="+string.Join(",",p.GetDisabledWorkTypes().Select(t=>t.defName)));
  RoleCommands.SetMechanicalWorkersEnabled(true);
  var colony=RecsAdapter.BuildColonyView(store,new List<Pawn>());
  var researcher=store.RoleByTemplate("WS_Researcher"); var firefighter=store.RoleByTemplate("WS_Firefighter");
  if(researcher==null||firefighter==null)throw new Exception("Fixture lacks Researcher/Firefighter roles");
  foreach(var p in workers) {
   var worker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",p,store);
   var recommendedIds=MechanicalDutyPlan.Recommend(colony,worker);
   var recommended=recommendedIds.Select(id=>store.RoleById(id).templateDefName).ToList();
   // A role is skipped when an earlier recommendation already covers its work (Core covers fires for Omni bots).
   var covered=new HashSet<string>(recommendedIds.SelectMany(id=>colony.Roles.Single(r=>r.Id==id).Coverage));
   Record("WR "+p.def.defName+" executable="+string.Join(",",worker.ExecutableGivers.Where(g=>g=="Research"||g=="LongRangeScan"||g=="GroundPenetratingScan"||g=="FightFires"))+" recommended="+string.Join(",",recommended));
   bool researchBot=ResearchBots.Contains(p.def.defName);
   if(researchBot){Expect(worker.ExecutableGivers.Contains("Research")&&worker.ExecutableGivers.Contains("GroundPenetratingScan"),p.def.defName+" research jobs executable");Expect(recommended.Contains("WS_Researcher")&&covered.Contains("Research")&&covered.Contains("GroundPenetratingScan"),p.def.defName+" Researcher recommended");}
   // Every robot with the vanilla beat-fire verb fights fires, with or without WorkTab support.
   Expect(worker.ExecutableGivers.Contains("FightFires"),p.def.defName+" FightFires executable");
   Expect(covered.Contains("FightFires"),p.def.defName+" firefighting covered by a recommended role ("+string.Join(",",recommended.Where(t=>t=="WS_Firefighter"||t=="WS_Core"))+")");
   RoleCommands.PasteRoleSet(p,(researchBot?new[]{researcher.id,firefighter.id}:new[]{firefighter.id}).Select(id=>new RoleAssignment{roleId=id}).ToList());
  }
  // Assigned chips: red lightning = Critical severity on the role chip.
  var button=DefDatabase<MainButtonDef>.AllDefsListForReading.First(d=>d.TabWindow is MainTabWindow_WorkRoles);
  var view=Field(button.TabWindow,"colonistsTab");
  ((ColonistsViewProfile)Field(view,"profile")).PawnsIn=_=>workers.ToList();
  var shown=workers[0];
  Find.Selector.ClearSelection();Find.Selector.Select(shown);Find.MainTabsRoot.SetCurrentTab(button);
  Call(Field(view,"selection"),"Click",shown);
  Call(view,"RefreshExternalSnapshotIfNeeded");
  foreach(var p in workers) {
   var sequence=Call(view,"ChipSequenceFor",p,store);
   for(int i=0;i<(int)Prop(sequence,"Count");i++) {
    var chip=Call(sequence,"ChipAt",i); var capability=Prop(chip,"Capability");
    var role=store.RoleById((int)Prop(Prop(chip,"RenderData"),"RoleId"));
    var severity=Prop(capability,"WarningSeverity").ToString();
    Record("CHIP "+p.def.defName+" "+role.templateDefName+" severity="+severity+" tooltip="+Prop(capability,"Tooltip"));
    Expect(severity=="None",p.def.defName+" "+role.templateDefName+" chip has no warning");
   }
  }
  Stage("CAPTURE chips");
 }
 static IEnumerable<ThinkNode> Descendants(ThinkNode node){yield return node;if(node.subNodes!=null)foreach(var s in node.subNodes)foreach(var d in Descendants(s))yield return d;}
 static void PrepareRooms()
 {
  foreach(var p in workers)foreach(var c in CellRect.CenteredOn(p.Position,7,7).Cells) {
   foreach(var t in c.GetThingList(map).ToArray())if(t is Plant||t is Filth)t.Destroy();
   map.terrainGrid.SetTerrain(c,TerrainDefOf.Concrete);map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);map.fogGrid.Unfog(c);
   if(CellRect.CenteredOn(p.Position,7,7).EdgeCells.Contains(c)){var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.BlocksGranite);wall.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(wall,c,map);}
  }
  map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
  foreach(var p in workers){var room=p.GetRoom();room.Vacuum=0;room.Temperature=21;}
 }
 static void BeginOutcome()
 {
  outcomeRunning=true;elapsed=0;
  project=DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(r=>!r.IsFinished&&r.CanStartNow&&r.requiredResearchBuilding==null&&r.requiredResearchFacilities.NullOrEmpty()&&r.knowledgeCategory==null&&r.baseCost>0);
  if(project==null)throw new Exception("No researchable project without facilities");
  Find.ResearchManager.SetCurrentProject(project);progressBefore=project.ProgressReal;
  Record("PROJECT "+project.defName+" progress="+progressBefore);
  for(int i=0;i<workers.Count;i++) {
   var p=workers[i];p.jobs.EndCurrentJob(JobCondition.InterruptForced);p.Position=origins[i];p.Notify_Teleported();
   if(p.needs.rest!=null)p.needs.rest.CurLevel=1; if(p.needs.energy!=null)p.needs.energy.CurLevel=1;
   foreach(var c in CellRect.CenteredOn(p.Position,7,7).Cells)map.areaManager.Home[c]=true;
   jobs[p]=new HashSet<string>();
   if(ResearchBots.Contains(p.def.defName)) {
    var bench=(Building_ResearchBench)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SimpleResearchBench"),ThingDefOf.WoodLog);bench.SetFactionDirect(Faction.OfPlayer);
    GenSpawn.Spawn(bench,p.Position+IntVec3.North*2,map,Rot4.North);targets[p]=bench;
    Record("BENCH "+p.def.defName+" researchable="+project.CanBeResearchedAt(bench,false)+" reachable="+p.CanReach(bench.InteractionCell,PathEndMode.OnCell,Danger.Deadly));
   } else {
    var cell=p.Position+IntVec3.East*2; var wood=ThingMaker.MakeThing(ThingDefOf.WoodLog);wood.stackCount=75;GenSpawn.Spawn(wood,cell,map);
    if(!FireUtility.TryStartFireIn(cell,map,.15f,null))throw new Exception("Could not create fire");targets[p]=cell.GetFirstThing<Fire>(map);
   }
  }
  Stage("RUNNING outcome");
 }
 static void StepOutcome()
 {
  for(int tick=0;tick<50;tick++){Find.TickManager.DoSingleTick();elapsed++;foreach(var p in workers)if(p.CurJob!=null)jobs[p].Add(p.CurJob.def.defName);}
  if(elapsed<1200)return;
  foreach(var p in workers) {
   bool researchBot=ResearchBots.Contains(p.def.defName);
   bool success=researchBot?jobs[p].Contains("Research"):targets[p].Destroyed&&jobs[p].Contains("BeatFire");
   Record("OUTCOME "+p.def.defName+" success="+success+" jobs="+string.Join(",",jobs[p])+(researchBot?" progress="+(project.ProgressReal-progressBefore):" fireDestroyed="+targets[p].Destroyed)+" downed="+p.Downed);
   if(!success&&researchBot)Diagnose(p);
  }
  foreach(var p in workers) {
   bool researchBot=ResearchBots.Contains(p.def.defName);
   Expect(researchBot?jobs[p].Contains("Research"):targets[p].Destroyed&&jobs[p].Contains("BeatFire"),p.def.defName+(researchBot?" researched":" fought fire")+" through its WorkRoles role");
  }
  done=true;Stage("SUCCESS");
 }
 static void Diagnose(Pawn p)
 {
  var bench=targets[p];
  Record("DIAG "+p.def.defName+" researchSpeed="+p.GetStatValue(StatDefOf.ResearchSpeed)+" timetable="+p.timetable?.CurrentAssignment?.defName+" intellectual="+p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level+"/disabled="+p.skills?.GetSkill(SkillDefOf.Intellectual)?.TotallyDisabled+" reserve="+p.CanReserve(bench)+" sittable="+p.CanReserveSittableOrSpot(bench.InteractionCell)+" reserver="+map.reservationManager.FirstRespectedReserver(bench,p)+" job="+map.reservationManager.FirstRespectedReserver(bench,p)?.CurJob?.def.defName+" drafted="+map.reservationManager.FirstRespectedReserver(bench,p)?.Drafted);
  foreach(WorkGiver g in (List<WorkGiver>)Call(p,"GetWorkGivers",false))
   Record("DIAG giver "+g.def.defName+" skip="+g.ShouldSkip(p)+" missing="+g.MissingRequiredCapacity(p)?.defName+(g is WorkGiver_Scanner s&&g.def.scanThings?" hasJobOnBench="+s.HasJobOnThing(p,bench):""));
 }
}
