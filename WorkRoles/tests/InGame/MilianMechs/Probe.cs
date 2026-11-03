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

namespace MilianMechsProbe;
/// Milira Race's Milian "androids" are Biotech mechanoids (Mechanoid flesh and
/// think tree, overseer subject, mechEnabledWorkTypes) with an Ancot Library
/// work tab writing Pawn_WorkSettings.SetPriority. Checks that WorkRoles' mech
/// support covers them unchanged:
///  M1 a Milian Pawn I is a listed mechanical worker with native work givers;
///  M2 roles are recommended and a role chip carries no warning;
///  M3 the Milian work tab's write lands with support off and is blocked with
///     the one-time notice for a managed Milian;
///  M4 both Milian tabs open beside WorkRoles;
///  M5 the game consumes the role: the Milian cleans filth through Cleaner.
[StaticConstructorOnStartup]
public static class Probe
{
 static bool started, done, outcomeRunning; static int elapsed, failures;
 static Map map; static Pawn milian; static Thing filth; static readonly HashSet<string> jobs=new();
 static string Root => GenFilePaths.SaveDataFolderPath;
 static void Record(string s) => File.AppendAllText(Path.Combine(Root,"milian-results.txt"),s+Environment.NewLine);
 static void Check(bool ok,string s){Record((ok?"PASS ":"FAIL ")+s);if(!ok)failures++;}
 static void Stage(string s) => File.WriteAllText(Path.Combine(Root,"milian-stage.txt"),s);
 static object Field(object o,string n) => AccessTools.Field(o.GetType(),n).GetValue(o);
 static object Prop(object o,string n) => AccessTools.Property(o.GetType(),n).GetValue(o);
 static object Call(object o,string n,params object[] a) => AccessTools.GetDeclaredMethods(o.GetType()).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(o,a);
 static object Static(string type,string n,params object[] a) => AccessTools.GetDeclaredMethods(AccessTools.TypeByName(type)).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(null,a);
 static Probe() { var h=new Harmony("tests.workroles.milianmechs");h.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));Log.Message("[MilianMechsProbe] initialized"); }
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
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"wantsToOpen").SetValue(null,false);
  foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);
  RoleCommands.SetAutoOptimize(false);RoleCommands.SetMechanicalWorkersEnabled(false);
  var store=RoleStore.Current;
  Record("GAME "+VersionControl.CurrentVersionString+" BIOTECH="+ModsConfig.BiotechActive);
  foreach(var mod in LoadedModManager.RunningModsListForReading)Record("MOD "+mod.PackageId+" root="+mod.RootDir);
  foreach(var p in map.mapPawns.AllPawnsSpawned.ToArray()) {
   if(p.CurJob!=null&&p.CurJob.def==null){p.jobs.curJob=null;p.jobs.curDriver=null;p.jobs.ClearQueuedJobs();p.pather.StopDead();}
   if(p.RaceProps.Humanlike&&p.drafter!=null)p.drafter.Drafted=true; else if(!p.RaceProps.Humanlike)p.DeSpawn();
  }
  var overseers=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed&&!p.DevelopmentalStage.Baby()).Take(4).ToArray();
  var parent=(MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);parent.SetFaction(Faction.OfPlayer);parent.Tile=TileFinder.RandomStartingTile();Find.WorldObjects.Add(parent);
  map=MapGenerator.GenerateMap(new IntVec3(80,1,80),parent,new MapGeneratorDef{defName="MilianProbeGround",genSteps=new List<GenStepDef>()},extraInitBeforeContentGen:m=>{foreach(var cell in m.AllCells)m.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);});
  for(int i=0;i<overseers.Length;i++){overseers[i].DeSpawn();GenSpawn.Spawn(overseers[i],new IntVec3(4+i,0,4),map);}
  Current.Game.CurrentMap=map;Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
  var kind=DefDatabase<PawnKindDef>.GetNamed("Milian_Mechanoid_PawnI");
  Record("KIND "+kind.defName+" race="+kind.race.defName+" mechanoid="+kind.race.race.IsMechanoid+" humanlike="+kind.race.race.Humanlike+" thinkTree="+kind.race.race.thinkTreeMain.defName+" workTypes="+string.Join(",",kind.race.race.mechEnabledWorkTypes.Select(w=>w.defName)));
  var cell=new IntVec3(40,0,40);
  milian=PawnGenerator.GeneratePawn(kind,Faction.OfPlayer);GenSpawn.Spawn(milian,cell,map);
  var overseer=map.mapPawns.FreeColonistsSpawned.First(p=>!p.Downed);
  if(!overseer.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))overseer.health.AddHediff(HediffDefOf.MechlinkImplant,overseer.health.hediffSet.GetBrain());
  overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer,milian);overseer.mechanitor.AssignPawnControlGroup(milian,MechWorkModeDefOf.Work);
  PrepareRoom(cell);
  Record("MILIAN "+milian.def.defName+" label="+milian.LabelShort+" controlled="+milian.IsColonyMechPlayerControlled+" overseer="+(milian.GetOverseer()?.LabelShort??"none")+" workSettings="+(milian.workSettings!=null&&milian.workSettings.EverWork)+" disabledTypes="+string.Join(",",milian.GetDisabledWorkTypes().Select(t=>t.defName)));
  // M1
  Check((bool)Static("WorkRoles.MechanicalWorkers","IsColonyWorker",milian),"M1 Milian is a colony mechanical worker");
  RoleCommands.SetMechanicalWorkersEnabled(true);
  var listed=(List<Pawn>)Static("WorkRoles.MechanicalWorkers","ListedPawns",new ScopeOption{Kind=ScopeKind.All});
  Check(listed.Contains(milian),"M1 Milian is listed in the Colonists roster with support on");
  var worker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",milian,store);
  Record("WR executable="+string.Join(",",worker.ExecutableGivers)+" providesService="+worker.ProvidesService+" skills="+string.Join(",",worker.Facts.SkillLevels.Select(k=>k.Key+":"+k.Value)));
  Check(worker.ExecutableGivers.Contains("CleanFilth")&&worker.ExecutableGivers.Contains("HaulGeneral")&&worker.ExecutableGivers.Contains("ConstructFinishFrames")&&!worker.ExecutableGivers.Contains("Research"),"M1 native work givers follow mechEnabledWorkTypes (clean, haul, construct; no research)");
  Check(worker.ProvidesService,"M1 an overseen Milian in Work mode provides service");
  // M2
  var colony=RecsAdapter.BuildColonyView(store,new List<Pawn>());
  var recommended=MechanicalDutyPlan.Recommend(colony,worker).Select(id=>store.RoleById(id).templateDefName).ToList();
  Record("REC "+string.Join(",",recommended));
  Check(recommended.Count>0,"M2 roles are recommended for the Milian");
  var cleaner=store.RoleByTemplate("WS_Cleaner"); var builder=store.RoleByTemplate("WS_Builder");
  RoleCommands.PasteRoleSet(milian,new List<RoleAssignment>{new RoleAssignment{roleId=cleaner.id},new RoleAssignment{roleId=builder.id}});
  var button=DefDatabase<MainButtonDef>.AllDefsListForReading.First(d=>d.TabWindow is MainTabWindow_WorkRoles);
  var view=Field(button.TabWindow,"colonistsTab");
  ((ColonistsViewProfile)Field(view,"profile")).PawnsIn=_=>new List<Pawn>{milian};
  Find.Selector.ClearSelection();Find.Selector.Select(milian);Find.MainTabsRoot.SetCurrentTab(button);
  Call(Field(view,"selection"),"Click",milian);
  Call(view,"RefreshExternalSnapshotIfNeeded");
  var sequence=Call(view,"ChipSequenceFor",milian,store);
  for(int i=0;i<(int)Prop(sequence,"Count");i++) {
   var chip=Call(sequence,"ChipAt",i); var capability=Prop(chip,"Capability");
   var role=store.RoleById((int)Prop(Prop(chip,"RenderData"),"RoleId")); var severity=Prop(capability,"WarningSeverity").ToString();
   Record("CHIP "+role.templateDefName+" severity="+severity+" tooltip="+Prop(capability,"Tooltip"));
   Check(severity=="None","M2 "+role.templateDefName+" chip has no warning");
  }
  // M3: the Ancot mech work tab writes through Pawn_WorkSettings.SetPriority.
  Type watcher=typeof(RoleCommands).Assembly.GetType("WorkRoles.PrioritySetWatcher",true);
  bool Pending()=>(bool)AccessTools.Property(watcher,"HasPendingWarning").GetValue(null);
  var work=WorkTypeDefOf.Cleaning;
  RoleCommands.PasteRoleSet(milian,new List<RoleAssignment>());RoleCommands.SetMechanicalWorkersEnabled(false);
  int before=milian.workSettings.GetPriority(work); int target=before==1?2:1;
  milian.workSettings.SetPriority(work,target);
  Check(milian.workSettings.GetPriority(work)==target&&!Pending(),"M3 support off: a Milian work tab priority write lands ("+before+" -> "+target+")");
  milian.workSettings.SetPriority(work,before);
  RoleCommands.SetMechanicalWorkersEnabled(true);
  RoleCommands.PasteRoleSet(milian,new List<RoleAssignment>{new RoleAssignment{roleId=cleaner.id},new RoleAssignment{roleId=builder.id}});
  int managed=milian.workSettings.GetPriority(work);
  Check(store.IsManaged(milian)&&managed>0,"M3 fixture: managed Milian with Cleaner reports Cleaning priority "+managed);
  milian.workSettings.SetPriority(work,0);
  Check(milian.workSettings.GetPriority(work)==managed,"M3 support on + role: the tab's write is blocked");
  Check(Pending(),"M3 support on + role: the one-time notice is queued for the writing mod");
  // M4
  foreach(string name in new[]{"Milian_Work","Milian_Config"}) {
   var tab=DefDatabase<MainButtonDef>.GetNamedSilentFail(name);
   if(tab==null){Check(false,"M4 tab "+name+" not loaded");continue;}
   try{Find.MainTabsRoot.SetCurrentTab(tab);Check(Find.WindowStack.Windows.Contains(tab.TabWindow),"M4 "+name+" opens beside WorkRoles");Find.MainTabsRoot.SetCurrentTab(tab);}
   catch(Exception ex){Check(false,"M4 "+name+" threw "+ex.Message);}
  }
  Find.MainTabsRoot.SetCurrentTab(button);
  Stage("CAPTURE chips");
 }
 static void PrepareRoom(IntVec3 origin)
 {
  foreach(var c in CellRect.CenteredOn(origin,9,9).Cells) {
   foreach(var t in c.GetThingList(map).ToArray())if(t is Plant||t is Filth)t.Destroy();
   map.terrainGrid.SetTerrain(c,TerrainDefOf.Concrete);map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);map.fogGrid.Unfog(c);map.areaManager.Home[c]=true;
   if(CellRect.CenteredOn(origin,9,9).EdgeCells.Contains(c)){var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.BlocksGranite);wall.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(wall,c,map);}
  }
  map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
  var room=milian.GetRoom();room.Vacuum=0;room.Temperature=21;
 }
 static void BeginOutcome()
 {
  outcomeRunning=true;elapsed=0;
  milian.jobs.EndCurrentJob(JobCondition.InterruptForced);
  if(milian.needs?.energy!=null)milian.needs.energy.CurLevel=1;
  var cell=milian.Position+IntVec3.East*2;
  if(!FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Dirt))throw new Exception("Could not create dirt");
  filth=cell.GetThingList(map).OfType<Filth>().First();
  Record("OUTCOME start filth="+filth.def.defName+" controlled="+milian.IsColonyMechPlayerControlled);
  Stage("RUNNING outcome");
 }
 static void StepOutcome()
 {
  for(int tick=0;tick<50;tick++){Find.TickManager.DoSingleTick();elapsed++;if(milian.CurJob!=null)jobs.Add(milian.CurJob.def.defName);}
  if(elapsed<1200&&!filth.Destroyed)return;
  bool success=filth.Destroyed&&jobs.Contains("Clean");
  Record("OUTCOME success="+success+" jobs="+string.Join(",",jobs)+" filthDestroyed="+filth.Destroyed+" ticks="+elapsed+" downed="+milian.Downed);
  Check(success,"M5 the Milian cleaned the filth through its Cleaner role");
  done=true;Stage(failures==0?"SUCCESS":"FAILED "+failures+" check(s)");
 }
}
