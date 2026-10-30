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

namespace RobotReportAudit;
[StaticConstructorOnStartup]
public static partial class Probe
{
 static bool started, done;
 static Map map;
 static List<Pawn> workers = new();
 static List<IntVec3> origins=new();
 static readonly Dictionary<Pawn,Thing> targets=new();
 static readonly Dictionary<Pawn,float> before=new();
 static readonly Dictionary<Pawn,HashSet<string>> jobs=new();
 static readonly Dictionary<Pawn,string> extinguishedBy=new();
 static string phase;
 static int outcomeStage=-1, elapsed;
 static Area_Allowed testArea;
 static string Root => GenFilePaths.SaveDataFolderPath;
 static void Record(string s) => File.AppendAllText(Path.Combine(Root,"robot-audit-results.txt"),s+Environment.NewLine);
 static void Stage(string s) => File.WriteAllText(Path.Combine(Root,"robot-audit-stage.txt"),s);
 static object Field(object o,string n) => AccessTools.Field(o.GetType(),n).GetValue(o);
 static object Prop(object o,string n) => AccessTools.Property(o.GetType(),n).GetValue(o);
 static object Call(object o,string n,params object[] a) => AccessTools.GetDeclaredMethods(o.GetType()).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(o,a);
 static object Static(string type,string n,params object[] a) => AccessTools.GetDeclaredMethods(AccessTools.TypeByName(type)).Single(m=>m.Name==n && m.GetParameters().Length==a.Length).Invoke(null,a);
 static Probe() { var h=new Harmony("eprime.workroles.robotreportaudit");h.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));h.Patch(AccessTools.Method(typeof(Thing),"TakeDamage"),postfix:new HarmonyMethod(typeof(Probe),nameof(DamageObserved)));Log.Message("[RobotReportAudit] initialized"); }
 static void DamageObserved(Thing __instance,DamageInfo dinfo) {
  if(!(__instance is Fire)||dinfo.Def!=DamageDefOf.Extinguish||!(dinfo.Instigator is Pawn p)||!targets.TryGetValue(p,out var target)||target!=__instance)return;
  if(__instance.Destroyed){extinguishedBy[p]=p.CurJob?.def.defName??"none";Record("EXTINGUISHED "+phase+" "+p.def.defName+" nativeJob="+extinguishedBy[p]);}
 }
 static void Update()
 {
  if(done || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting)return;
  try {
   if(!started) { started=true; Stage("SETUP");Setup(); return; }
   if(tradeRunning){StepTrades();return;}
   if(outcomeStage>=0){StepOutcome();return;}
   if(File.Exists(Path.Combine(Root,"captured-cleaner"))) {
    if(phase=="info"){done=true;Stage("SUCCESS");}
    else {foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);if(phase.StartsWith("trades-"))BeginTrades();else BeginOutcome(0);}
   }
  } catch(Exception ex) { done=true; Record("ERROR "+ex);Stage("FAILED "+ex); }
 }
 static void Setup()
 {
  map=Find.CurrentMap; Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
  phase=File.ReadAllText(Path.Combine(Root,"robot-audit-phase.txt")).Trim();
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"wantsToOpen").SetValue(null,false);
  foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);
  RoleCommands.SetAutoOptimize(false);RoleCommands.SetMechanicalWorkersEnabled(false);
  var store=RoleStore.Current;
  // The copied save was seeded with the 1.7.0 default exclusions; loading
  // under the current defaults must have cleared exactly those, once.
  var retired=new[]{"WS_Rescuer","WS_Firefighter","WS_Patient","WS_Bedrest","WS_Laborer"};
  Record("EXCLUSIONS version="+store.recommendationExclusionsVersion+" stillExcluded="+string.Join(",",retired.Where(t=>store.RoleByTemplate(t)?.excludedFromRecommendations==true))+" otherExcluded="+string.Join(",",store.roles.Where(r=>r.excludedFromRecommendations).Select(r=>r.label)));
  if(store.recommendationExclusionsVersion<2||retired.Any(t=>store.RoleByTemplate(t)?.excludedFromRecommendations==true))throw new Exception("Retired default exclusions must be cleared once on load");
  var freshLaborer=(Role)Static("WorkRoles.RoleCommands","CreateRoleFromDef",DefDatabase<RoleDef>.GetNamed("WS_Laborer"));
  if(freshLaborer.excludedFromRecommendations)throw new Exception("Fresh Laborer must be eligible by default");
  RoleCommands.DeleteRole(freshLaborer.id);
  var humanPawns=map.mapPawns.FreeColonistsSpawned.ToList();
  var humanInput=RecsAdapter.BuildColonyView(store,humanPawns);
  foreach(var p in map.mapPawns.AllPawnsSpawned.ToArray()) {
   if(p.CurJob!=null&&p.CurJob.def==null){p.jobs.curJob=null;p.jobs.curDriver=null;p.jobs.ClearQueuedJobs();p.pather.StopDead();}
   if(p.RaceProps.Humanlike&&p.drafter!=null)p.drafter.Drafted=true;
   else if(!p.RaceProps.Humanlike)p.DeSpawn();
  }
  if(phase!="info") {
   var overseers=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed&&!p.DevelopmentalStage.Baby()).Take(10).ToArray();
   var parent=(MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);parent.SetFaction(Faction.OfPlayer);parent.Tile=TileFinder.RandomStartingTile();Find.WorldObjects.Add(parent);
   map=MapGenerator.GenerateMap(new IntVec3(100,1,100),parent,new MapGeneratorDef{defName="RobotReportGround",genSteps=new List<GenStepDef>()},extraInitBeforeContentGen:m=>{foreach(var cell in m.AllCells)m.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);});
   if(map.Biome.inVacuum)throw new Exception("Ground fixture must not be in vacuum");
   for(int i=0;i<overseers.Length;i++){overseers[i].DeSpawn();GenSpawn.Spawn(overseers[i],new IntVec3(4+i,0,4),map);}
   Current.Game.CurrentMap=map;Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
   Record("GROUND_MAP "+map.Biome.defName+" "+map.Tile);
  }
  Record("GAME "+VersionControl.CurrentVersionString+" BIOTECH="+ModsConfig.BiotechActive);
  foreach(var mod in LoadedModManager.RunningModsListForReading)Record("MOD "+mod.PackageId+" root="+mod.RootDir);
  var factory=AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),"CreateRobot",new[]{typeof(string),typeof(IntVec3),typeof(Map)});
  var robotType=AccessTools.TypeByName("AIRobot.X2_AIRobot");
  var positions=origins;
  var defs=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.race!=null && robotType.IsAssignableFrom(d.thingClass)).ToArray();
  foreach(var def in defs) {
   var cell=map.AllCells.First(c=>c.x>10&&c.z>10&&c.x<map.Size.x-10&&c.z<map.Size.z-10&&positions.All(p=>p.DistanceTo(c)>7)&&CellRect.CenteredOn(c,5,5).Cells.All(x=>x.Standable(map)&&x.GetEdifice(map)==null));
   positions.Add(cell);
   var pawn=(Pawn)factory.Invoke(null,new object[]{def.defName,cell,map});workers.Add(pawn);
  }
  if(ModsConfig.BiotechActive)foreach(var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k=>k.race.race.IsMechanoid && !robotType.IsAssignableFrom(k.race.thingClass) && k.race.race.mechEnabledWorkTypes?.Count>0)) {
   var cell=map.AllCells.First(c=>c.x>10&&c.z>10&&c.x<map.Size.x-10&&c.z<map.Size.z-10&&positions.All(p=>p.DistanceTo(c)>7)&&CellRect.CenteredOn(c,5,5).Cells.All(x=>x.Standable(map)&&x.GetEdifice(map)==null));positions.Add(cell);
   var pawn=PawnGenerator.GeneratePawn(kind,Faction.OfPlayer);GenSpawn.Spawn(pawn,cell,map);workers.Add(pawn);
   var overseer=map.mapPawns.FreeColonistsSpawned.First(p=>!p.Downed&&(p.mechanitor==null||p.mechanitor.TotalBandwidth-p.mechanitor.UsedBandwidth>=pawn.GetStatValue(StatDefOf.BandwidthCost)));
   if(!overseer.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))overseer.health.AddHediff(HediffDefOf.MechlinkImplant,overseer.health.hediffSet.GetBrain());
   overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer,pawn);overseer.mechanitor.AssignPawnControlGroup(pawn,MechWorkModeDefOf.Work);
  }
  // WR34: Misc. Robots++ compatibility patches leave mechEnabledWorkTypes out
  // of sync with robotWorkTypes. Reproduce that on the tier-II hauler: vanilla
  // then calls BasicWorker disabled while the robot still flicks natively.
  var desyncHauler=workers.FirstOrDefault(p=>p.def.defName=="AIRobot_Hauler_II");
  if(desyncHauler!=null){desyncHauler.def.race.mechEnabledWorkTypes.Remove(DefDatabase<WorkTypeDef>.GetNamed("BasicWorker"));desyncHauler.Notify_DisabledWorkTypesChanged();Record("DESYNC AIRobot_Hauler_II BasicWorker vanillaDisabled="+desyncHauler.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("BasicWorker")));}
  if(phase!="info")PrepareRooms();
  foreach(var p in workers) {
   string native=robotType.IsInstanceOfType(p)?string.Join(",",((IEnumerable)Call(p,"GetWorkGivers",false)).Cast<WorkGiver>().Select(g=>g.def.defName)):string.Join(",",DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g=>g.canBeDoneByMechs&&p.RaceProps.mechEnabledWorkTypes.Contains(g.workType)).Select(g=>g.defName));
   Record("NATIVE "+p.def.defName+" label="+p.LabelShort+" disabledTypes="+string.Join(",",p.GetDisabledWorkTypes().Select(t=>t.defName))+" disabledTags="+p.CombinedDisabledWorkTags+" givers="+native);
  }
  RoleCommands.SetMechanicalWorkersEnabled(true);
  if(store.RoleByTemplate("WS_DrugMaker")==null){var dm=(Role)Static("WorkRoles.RoleCommands","CreateRoleFromDef",DefDatabase<RoleDef>.GetNamed("WS_DrugMaker"));Record("CREATED DrugMaker "+dm.id);}
  ColonyView colony=RecsAdapter.BuildColonyView(store,new List<Pawn>());
  var giverFacts=(IReadOnlyDictionary<string,JobProfileGiverFacts>)Static("WorkRoles.JobSkillProfiles","GiverFacts");
  foreach(var giverName in new[]{"DoBillsProduceDrugs","DoBillsSerumCentrifuge","DoBillsStonecut"})
   Record("GIVERFACTS "+giverName+" present="+giverFacts.ContainsKey(giverName)+(giverFacts.TryGetValue(giverName,out var gf)?" usesRecipes="+gf.UsesRecipes+" recipes="+gf.RecipeIdentities.Count+" used="+string.Join(",",gf.UsedSkillDefNames)+" relevant="+string.Join(",",gf.RelevantSkillDefNames)+" requirements="+string.Join(",",gf.Requirements.Select(q=>q.SkillDefName)):""));
  foreach(var r in colony.Roles.Where(r=>r.TemplateDefName=="WS_DrugMaker"))Record("ROLESKILLS "+r.Id+" "+string.Join(",",r.Skills.Select(s=>s.SkillDefName+":primary="+s.Primary+":used="+s.UsedGivers)));
  foreach(var r in colony.Roles)Record("ROLE "+r.Id+" "+store.RoleById(r.Id).label+" template="+r.TemplateDefName+" excluded="+r.ExcludedFromRecommendations+" members="+(r.MemberRoleIds==null?"none":string.Join(",",r.MemberRoleIds))+" auto="+r.AutoAssign+" skill="+r.UsesSkills+" coverage="+string.Join(",",r.Coverage));
  Record("ORDER "+string.Join(",",colony.OrderTemplate));
  foreach(var p in workers) {
   var worker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",p,store);
   if(robotType.IsInstanceOfType(p)&&!(bool)Static("WorkRoles.MechanicalWorkers","CanDo",p,WorkGiverDefOf.FightFires)) {
    var fireFacts=Enumerable.Range(0,worker.Facts.WorkEvidence.CapabilityCount).Select(i=>worker.Facts.WorkEvidence.CapabilityAt(i)).Where(f=>f.GiverDefName=="FightFires").ToArray();
    if(!fireFacts.Any(f=>f.ReasonKey=="WR_WorkNativeFireNearby"))throw new Exception("WR32 missing nearby-fire explanation for "+p.def.defName);
   }
   Record("WR "+p.def.defName+" executable="+string.Join(",",worker.ExecutableGivers));
   if(p==desyncHauler&&(!worker.ExecutableGivers.Contains("Flick")||!worker.Facts.CapableWorkTypes.Contains("BasicWorker")))throw new Exception("WR34 native flicking must survive a stale mechEnabledWorkTypes list");
   var recommended=MechanicalDutyPlan.Recommend(colony,worker);
   Record("REC "+p.def.defName+" "+string.Join(",",recommended.Select(id=>store.RoleById(id).label)));
   // Every native job must be covered by some recommended role; hunting is
   // the one legitimate exception for an unarmed worker.
   var coveredByRecs=new HashSet<string>(recommended.SelectMany(id=>colony.Roles.Single(r=>r.Id==id).Coverage));
   var lostNative=worker.ExecutableGivers.Where(g=>!coveredByRecs.Contains(g)&&!(g=="HunterHunt"&&!worker.Facts.HasRangedWeapon)).ToList();
   if(lostNative.Count>0)throw new Exception("Recommendations lose native work for "+p.def.defName+": "+string.Join(",",lostNative));
   var factsColony=new ColonyView{Roles=colony.Roles,Paths=colony.Paths};factsColony.Pawns.Add(worker.Facts);var facts=new EngineContext(factsColony);
   var verdicts=new Dictionary<int,SignalBucket>();MechanicalDutyPlan.BuildFixTarget(colony,worker,null,verdicts);
   // Badge invariant for every mechanical worker and plain role: no signals
   // means Neutral, unless the role's primary skill is not one the worker
   // exercises natively (then Awful, like a human lacking the skill).
   int awfulCount=0,neutralCount=0;
   foreach(var role in colony.Roles.Where(r=>!r.Blocker&&r.MemberRoleIds==null&&(r.DeclaredRequiredSkills==null||r.DeclaredRequiredSkills.Count==0))) {
    var primary=role.Skills.FirstOrDefault(s=>s.Primary);
    var expected=primary!=null&&!worker.Facts.SkillLevels.ContainsKey(primary.SkillDefName)&&!role.PartialCapability?SignalBucket.Awful:SignalBucket.Neutral;
    if(verdicts[role.Id]!=expected)throw new Exception("Badge mismatch "+p.def.defName+" "+store.RoleById(role.Id).label+" expected "+expected+" got "+verdicts[role.Id]);
    if(expected==SignalBucket.Awful)awfulCount++;else neutralCount++;
   }
   Record("VERDICTS "+p.def.defName+" neutral="+neutralCount+" awful="+awfulCount+" awfulRoles="+string.Join(",",colony.Roles.Where(r=>verdicts.TryGetValue(r.Id,out var b)&&b==SignalBucket.Awful).Select(r=>store.RoleById(r.Id).label)));
   Record("SKILLS "+p.def.defName+" "+string.Join(",",worker.Facts.SkillLevels.OrderBy(k=>k.Key).Select(k=>k.Key+":"+k.Value))+" drugMakerVerdict="+(colony.Roles.FirstOrDefault(r=>r.TemplateDefName=="WS_DrugMaker") is RoleView dmRole?verdicts[dmRole.Id].ToString():"absent")+" native="+(p.skills==null?"none":string.Join(",",p.skills.skills.Select(s=>s.def.defName+":"+s.Level))));
   foreach(var role in colony.Roles.Where(r=>new[]{"WS_Cleaner","WS_Farmer","WS_Miner","WS_Grunt","WS_Firefighter","WS_Basics","WS_DrugMaker"}.Contains(r.TemplateDefName)))
    Record("ROLECHECK "+p.def.defName+" "+role.TemplateDefName+" ordered="+colony.OrderTemplate.Contains(role.Id)+" capable="+facts.MeetsCapabilityRequirement(0,role)+" band="+Static("WorkRoles.Core.Recs.PathActivation","TargetBandContains",facts,0,role)+" missing="+string.Join(",",role.Coverage.Where(g=>!worker.ExecutableGivers.Contains(g)))+" skills="+string.Join(",",worker.Facts.SkillLevels.Select(k=>k.Key+":"+k.Value)));
   foreach(var g in DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g=>g.workType!=null&&(g.workType.defName=="Firefighter"||g.workType.defName=="BasicWorker"||g.workType.defName=="Cleaning")))
    Record("FACT "+p.def.defName+" "+g.defName+" type="+g.workType.defName+" canDo="+Static("WorkRoles.MechanicalWorkers","CanDo",p,g)+" disabledType="+p.WorkTypeIsDisabled(g.workType)+" disabledTag="+p.WorkTagIsDisabled(g.workTags)+" missingCapacity="+g.Worker.MissingRequiredCapacity(p)+" evidenceBlocks="+worker.Facts.WorkEvidence.Blocks(g.defName));
  }
  var cleaner=workers.First(p=>p.def.defName=="AIRobot_Cleaner");
  CaptureHumanRows("SAVED",humanInput,humanPawns);
  RoleCommands.SetRoleRecommendationExcluded(store.RoleByTemplate("WS_Laborer").id,false);
  colony=RecsAdapter.BuildColonyView(store,new List<Pawn>());
  foreach(var p in workers) {
   var worker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",p,store);
   Record("DEFAULTREC "+p.def.defName+" "+string.Join(",",MechanicalDutyPlan.Recommend(colony,worker).Select(id=>store.RoleById(id).label)));
  }
  CaptureMechanicalImpact(colony,humanInput,humanPawns);
  if(phase=="trades-fixed")VerifyTransitions();
  var cleanWorker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",cleaner,store);
  var grunt=store.RoleByTemplate("WS_Grunt");
  RoleCommands.SetRoleRecommendationExcluded(grunt.id,true);
  Record("CONTROL exclude Grunt => "+string.Join(",",MechanicalDutyPlan.Recommend(RecsAdapter.BuildColonyView(store,new List<Pawn>()),cleanWorker).Select(id=>store.RoleById(id).label)));
  RoleCommands.SetRoleRecommendationExcluded(grunt.id,false);
  var oldOrder=store.recommendationOrder.ToList();
  var cleanerFirst=colony.OrderTemplate.ToList();cleanerFirst.Insert(0,store.RoleByTemplate("WS_Cleaner").id);
  RoleCommands.SetRecommendationOrder(cleanerFirst);
  Record("CONTROL Cleaner first => "+string.Join(",",MechanicalDutyPlan.Recommend(RecsAdapter.BuildColonyView(store,new List<Pawn>()),cleanWorker).Select(id=>store.RoleById(id).label)));
  RoleCommands.SetRecommendationOrder(oldOrder);
  var expanded=RecsAdapter.BuildColonyView(store,new List<Pawn>());
  expanded.OrderTemplate=expanded.OrderTemplate.ToList();expanded.OrderTemplate.Insert(0,store.RoleByTemplate("WS_Cleaner").id);
  Record("ENGINE CONTROL consider Cleaner before Grunt => "+string.Join(",",MechanicalDutyPlan.Recommend(expanded,cleanWorker).Select(id=>store.RoleById(id).label)));
  RoleCommands.PasteRoleSet(cleaner,new List<RoleAssignment>());
  var button=DefDatabase<MainButtonDef>.AllDefsListForReading.First(d=>d.TabWindow is MainTabWindow_WorkRoles);
  var view=Field(button.TabWindow,"colonistsTab");
  ((ColonistsViewProfile)Field(view,"profile")).PawnsIn=_=>workers.Where(p=>p==cleaner||p.def.defName=="RPP_Bot_Omni_V"||p.def.defName=="RPP_Bot_ER_I"||p.def.defName=="Mech_Cleansweeper").ToList();
  Find.Selector.ClearSelection();Find.Selector.Select(cleaner);Find.MainTabsRoot.SetCurrentTab(button);
  Call(Field(view,"selection"),"Click",cleaner);
  Call(view,"RefreshExternalSnapshotIfNeeded");
  Call(view,"BuildFixEntries",cleaner,false);
  var initialPlans=((IEnumerable)Field(Field(view,"recommendationState"),"plans")).Cast<object>();
  var initialPlan=initialPlans.Single(p=>ReferenceEquals(Prop(p,"Pawn"),cleaner));
  Record("UI UNASSIGNED TARGET "+string.Join(",",((IEnumerable)Prop(initialPlan,"Target")).Cast<RoleAssignment>().Select(a=>store.RoleById(a.roleId).label)));
  Call(view,"ApplyFix",cleaner,null,false);
  var appliedWorker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",cleaner,store);
  Record("UI APPLIED "+string.Join(",",appliedWorker.ActiveRoleIds.Select(id=>store.RoleById(id).label))+" nativeGivers="+string.Join(",",((IEnumerable)Call(cleaner,"GetWorkGivers",false)).Cast<WorkGiver>().Select(g=>g.def.defName)));
  Call(view,"RefreshExternalSnapshotIfNeeded");
  var sequence=Call(view,"ChipSequenceFor",cleaner,store);
  var evidence1=Static("WorkRoles.Capabilities.PawnWorkFacts","Capture",cleaner);
  var evidence2=Static("WorkRoles.Capabilities.PawnWorkFacts","Capture",cleaner);
  if(!ReferenceEquals(evidence1,evidence2))throw new Exception("Unchanged native work evidence must preserve snapshot identity");
  Record("ASSERT WR32 unchanged native evidence preserves identity PASS");
  for(int i=0;i<(int)Prop(sequence,"Count");i++) {
   var chip=Call(sequence,"ChipAt",i); var capability=Prop(chip,"Capability");
   Record("UI CAPABILITY "+store.RoleById((int)Prop(Prop(chip,"RenderData"),"RoleId")).label+" severity="+Prop(capability,"WarningSeverity")+" tooltip="+Prop(capability,"Tooltip"));
   if(store.RoleById((int)Prop(Prop(chip,"RenderData"),"RoleId")).templateDefName=="WS_Firefighter") {
    var tip=(string)Prop(capability,"Tooltip");
    if(!tip.Contains("assigned")||!tip.Contains("nearby fires")||tip.Contains("incapable of this work"))throw new Exception("WR32 tooltip does not distinguish assigned work from nearby extinguishing");
    if((bool)Static("WorkRoles.MechanicalWorkers","CanDo",cleaner,WorkGiverDefOf.FightFires))throw new Exception("WR32 must not grant native firefighting jobs");
    Record("ASSERT WR32 corrected native tooltip and unchanged job eligibility PASS");
   }
  }
  Call(view,"BuildFixEntries",cleaner,false);
  var plans=((IEnumerable)Field(Field(view,"recommendationState"),"plans")).Cast<object>();
  var plan=plans.Single(p=>ReferenceEquals(Prop(p,"Pawn"),cleaner));
  Record("UI TARGET "+string.Join(",",((IEnumerable)Prop(plan,"Target")).Cast<RoleAssignment>().Select(a=>store.RoleById(a.roleId).label)));
  Stage("CAPTURE cleaner");
 }
 static void PrepareRooms()
 {
  foreach(var p in workers)foreach(var c in CellRect.CenteredOn(p.Position,5,5).Cells) {
   foreach(var t in c.GetThingList(map).ToArray())if(t is Plant||t is Filth)t.Destroy();
   map.terrainGrid.SetTerrain(c,TerrainDefOf.Concrete);map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);map.fogGrid.Unfog(c);
   if(CellRect.CenteredOn(p.Position,5,5).EdgeCells.Contains(c)){var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.BlocksGranite);wall.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(wall,c,map);}
  }
  map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
  foreach(var p in workers){var room=p.GetRoom();room.Vacuum=0;room.Temperature=21;Record("SCENE "+p.def.defName+" biome="+map.Biome.defName+" outdoors="+room.PsychologicallyOutdoors+" vacuum="+room.Vacuum+" reachable="+p.CanReach(p.Position+IntVec3.East,PathEndMode.Touch,Danger.Deadly));}
 }
 static void BeginOutcome(int next)
 {
  foreach(var t in targets.Values)if(!t.Destroyed)t.Destroy(); targets.Clear();jobs.Clear();before.Clear();extinguishedBy.Clear();
  foreach(var p in workers) {foreach(var c in CellRect.CenteredOn(origins[workers.IndexOf(p)],3,3).Cells)foreach(var f in c.GetThingList(map).OfType<Fire>().ToArray())f.Destroy();p.GetRoom().Temperature=21;p.GetRoom().Vacuum=0;}
  outcomeStage=next;elapsed=0;
  RoleCommands.SetMechanicalWorkersEnabled(phase!="native");
  var colony=RecsAdapter.BuildColonyView(RoleStore.Current,new List<Pawn>());
  for(int i=0;i<workers.Count;i++) {
   var p=workers[i];p.jobs.EndCurrentJob(JobCondition.InterruptForced);p.Position=origins[i];p.Notify_Teleported();
   if(p.needs.rest!=null)p.needs.rest.CurLevel=1; if(p.needs.energy!=null)p.needs.energy.CurLevel=1;
   if(testArea==null&&!map.areaManager.TryMakeNewAllowed(out testArea))testArea=map.areaManager.AllAreas.OfType<Area_Allowed>().First();
   foreach(var c in CellRect.CenteredOn(p.Position,5,5).Cells){testArea[c]=true;map.areaManager.Home[c]=true;map.terrainGrid.SetTerrain(c,TerrainDefOf.Concrete);}
   p.playerSettings.AreaRestrictionInPawnCurrentMap=testArea;
   if(phase!="native") {
    var view=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",p,RoleStore.Current);
    view.ActiveRoleIds.Clear();view.CommittedGivers.Clear();view.Facts.Existing.Clear();
    var ids=MechanicalDutyPlan.Recommend(colony,view);
    RoleCommands.PasteRoleSet(p,ids.Select(id=>new RoleAssignment{roleId=id}).ToList());
    Record("APPLY "+p.def.defName+" "+string.Join(",",ids.Select(id=>RoleStore.Current.RoleById(id).label)));
   }
   var cell=p.Position+IntVec3.East;
   if(next==0) {
    var wood=ThingMaker.MakeThing(ThingDefOf.WoodLog);wood.stackCount=75;GenSpawn.Spawn(wood,cell,map);
    if(!FireUtility.TryStartFireIn(cell,map,.15f,null))throw new Exception("Could not create fire");
    var fire=cell.GetFirstThing<Fire>(map);targets[p]=fire;before[p]=p.records.GetValue(RecordDefOf.FiresExtinguished);
    if(phase=="forced") {
     var giver=(WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("FightFires").Worker;
     Record("FORCED_ADMISSION "+p.def.defName+" "+giver.HasJobOnThing(p,fire,true));
     p.jobs.TryTakeOrderedJob(giver.JobOnThing(p,fire,true),JobTag.Misc);
    }
   } else if(next==1) {
    if(!FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Dirt))throw new Exception("Could not create dirt");targets[p]=cell.GetThingList(map).OfType<Filth>().First();before[p]=0;
   } else {
    var lamp=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("StandingLamp"));lamp.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(lamp,cell,map);
    var flick=lamp.TryGetComp<CompFlickable>();flick.CompGetGizmosExtra().OfType<Command_Toggle>().First().toggleAction();targets[p]=lamp;before[p]=0;
   }
   jobs[p]=new HashSet<string>();
   if(phase!="forced"||next!=0)p.jobs.EndCurrentJob(JobCondition.InterruptForced);
  }
  Record("START_OUTCOME "+phase+" stage="+next+" workers="+workers.Count);Stage("RUNNING "+phase+" "+next);
 }
 static void StepOutcome()
 {
  for(int tick=0;tick<50;tick++) {
   Find.TickManager.DoSingleTick();elapsed++;
   foreach(var p in workers)if(p.CurJob!=null)jobs[p].Add(p.CurJob.def.defName);
  }
  if(elapsed<(outcomeStage==1?1800:900))return;
  foreach(var p in workers) {
   bool success=outcomeStage==0?extinguishedBy.ContainsKey(p):outcomeStage==1?targets[p].Destroyed:!targets[p].TryGetComp<CompFlickable>().SwitchIsOn;
   Record("OUTCOME "+phase+" "+(outcomeStage==0?"fire":outcomeStage==1?"clean":"flick")+" "+p.def.defName+" success="+success+" targetDestroyed="+targets[p].Destroyed+" jobs="+string.Join(",",jobs[p])+" extinguishingJob="+(extinguishedBy.TryGetValue(p,out var by)?by:"none")+" fires="+(outcomeStage==0?p.records.GetValue(RecordDefOf.FiresExtinguished)-before[p]:0)+" vacuum="+p.GetRoom().Vacuum+" downed="+p.Downed);
  }
  if(outcomeStage<2&&phase!="forced")BeginOutcome(outcomeStage+1);
  else {done=true;Stage("SUCCESS");}
 }
}
