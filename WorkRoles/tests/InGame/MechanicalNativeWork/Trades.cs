using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace RobotReportAudit;
public static partial class Probe
{
 static bool tradeRunning;
 static int tradeTicks,tradePass;
 static readonly Dictionary<Pawn,string> duties=new();
 static readonly Dictionary<Pawn,IntVec3> tradeCells=new();
 static readonly Dictionary<Pawn,Hediff> injuries=new();
 static readonly Dictionary<Pawn,Bill_Production> bills=new();
 static readonly HashSet<Pawn> completed=new();
 static int[] NativeTarget(Pawn pawn)
 {
  var colony=RecsAdapter.BuildColonyView(RoleStore.Current,new List<Pawn>());
  var worker=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",pawn,RoleStore.Current);
  return MechanicalDutyPlan.BuildFixTarget(colony,worker).RoleIds.ToArray();
 }
 static void ExpectRoles(Pawn pawn,params string[] templates)
 {
  var actual=NativeTarget(pawn).Select(id=>RoleStore.Current.RoleById(id).templateDefName).ToArray();
  if(!actual.SequenceEqual(templates))throw new Exception(pawn.def.defName+" expected "+string.Join(",",templates)+" got "+string.Join(",",actual));
  Record("TRANSITION "+pawn.def.defName+" "+string.Join(",",actual));
 }
 static void VerifyTransitions()
 {
  var store=RoleStore.Current;
  var cleaner=workers.Single(p=>p.def.defName=="AIRobot_Cleaner");
  var laborer=store.RoleByTemplate("WS_Laborer");var cleanRole=store.RoleByTemplate("WS_Cleaner");var grunt=store.RoleByTemplate("WS_Grunt");
  ExpectRoles(cleaner,"WS_Laborer","WS_Cleaner");
  RoleCommands.SetRoleRecommendationExcluded(cleanRole.id,true);
  ExpectRoles(cleaner,"WS_Laborer","WS_Grunt");
  RoleCommands.SetRoleRecommendationExcluded(laborer.id,true);
  ExpectRoles(cleaner,"WS_Basics","WS_Grunt");
  RoleCommands.SetRoleRecommendationExcluded(cleanRole.id,false);
  RoleCommands.SetRoleRecommendationExcluded(laborer.id,false);
  RoleCommands.PasteRoleSet(cleaner,new List<RoleAssignment>{new(){roleId=store.RoleByTemplate("WS_Basics").id},new(){roleId=grunt.id,pinned=true}});
  ExpectRoles(cleaner,"WS_Laborer","WS_Grunt");
  RoleCommands.PasteRoleSet(cleaner,new List<RoleAssignment>());
  ExpectRoles(workers.Single(p=>p.def.defName=="Mech_Tunneler"),"WS_Miner");
  ExpectRoles(workers.Single(p=>p.def.defName=="Mech_Agrihand"),"WS_Farmer");
  ExpectRoles(workers.Single(p=>p.def.defName=="RPP_Bot_Builder_I"),"WS_Builder","WS_Miner");
  ExpectRoles(workers.Single(p=>p.def.defName=="RPP_Bot_Kitchen_I"),"WS_Cook","WS_Farmer");
  ExpectRoles(workers.Single(p=>p.def.defName=="RPP_Bot_ER_I"),"WS_Core","WS_Doctor");
  var omni=workers.Single(p=>p.def.defName=="RPP_Bot_Omni_II");int hunter=store.RoleByTemplate("WS_Hunter").id;
  if(NativeTarget(omni).Contains(hunter))throw new Exception("Unarmed Omni must not be recommended Hunter");
  var rifle=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
  omni.equipment.AddEquipment(rifle);
  if(!NativeTarget(omni).Contains(hunter))throw new Exception("Armed native hunter should become eligible immediately while paused");
  RoleCommands.PasteRoleSet(omni,new List<RoleAssignment>{new(){roleId=hunter}});
  omni.equipment.DestroyEquipment(rifle);
  if(NativeTarget(omni).Contains(hunter))throw new Exception("Lost ranged weapon must remove unpinned Hunter immediately while paused");
  RoleCommands.PasteRoleSet(omni,new List<RoleAssignment>());
  Record("TRANSITION hunting unarmed -> armed -> lost weapon PASS paused="+(Find.TickManager.CurTimeSpeed==TimeSpeed.Paused));
 }
 static void CaptureHumanRows(string label,ColonyView colony,List<Pawn> pawns)
 {
  var plan=RecommendationPlan.Build(colony,RoleStore.Current.recommendationTuning);
  var fix=ColonyFixPlanner.Build(colony,plan);
  for(int p=0;p<plan.PawnCount;p++)Record("HUMAN "+label+" "+pawns[p].LabelShort+" ROLES="+string.Join(",",Enumerable.Range(0,plan.RoleCountAt(p)).Select(i=>plan.RoleAt(p,i)))+" TARGET="+string.Join(",",fix[p].RoleIds)+" PATHS="+string.Join(",",Enumerable.Range(0,plan.PathCountAt(p)).Select(i=>plan.PathAt(p,i)+":"+plan.PathActivatedAt(p,i))));
 }
 static void CaptureMechanicalImpact(ColonyView catalog,ColonyView humans,List<Pawn> pawns)
 {
  var copy=(ColonyView)HarmonyLib.AccessTools.Method(typeof(object),"MemberwiseClone").Invoke(humans,null);
  copy.Roles=catalog.Roles;
  copy.MechanicalWorkers=new List<MechanicalWorkerView>();
  foreach(var p in workers) {
   var w=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",p,RoleStore.Current);
   var ids=MechanicalDutyPlan.Recommend(catalog,w);
   w.ActiveRoleIds=ids.ToList();w.CommittedGivers=new HashSet<string>(ids.SelectMany(id=>catalog.Roles.Single(r=>r.Id==id).Coverage).Where(w.ExecutableGivers.Contains));
   w.ProvidesService=true;copy.MechanicalWorkers.Add(w);
  }
  copy.IntegrateMechanicalWorkers=false;CaptureHumanRows("DEFAULTS_OFF",copy,pawns);
  copy.IntegrateMechanicalWorkers=true;CaptureHumanRows("DEFAULTS_FULL_SERVICE",copy,pawns);
 }
 static void BeginTrades()
 {
  var specs=new Dictionary<string,string>{["AIRobot_Cleaner"]="clean",["AIRobot_Hauler"]="haul",["AIRobot_Hauler_II"]="flick",
   ["RPP_Bot_Builder_I"]="repair",["RPP_Bot_ER_I"]="tend",["RPP_Bot_Kitchen_I"]="sow",["RPP_Bot_Omni_I"]="butcher",
   ["Mech_Fabricor"]="butcher",["Mech_Tunneler"]="mine",["Mech_Agrihand"]="sow"};
  var kept=new List<Pawn>();var cells=new List<IntVec3>();
  for(int i=0;i<workers.Count;i++)if(specs.ContainsKey(workers[i].def.defName)){kept.Add(workers[i]);cells.Add(origins[i]);}else workers[i].DeSpawn();
  workers=kept;origins=cells;
  bool native=phase=="trades-native";
  RoleCommands.SetMechanicalWorkersEnabled(!native);
  var colony=RecsAdapter.BuildColonyView(RoleStore.Current,new List<Pawn>());
  if(!map.areaManager.TryMakeNewAllowed(out testArea))throw new Exception("Cannot create trades area");
  for(int i=0;i<workers.Count;i++) {
   var pawn=workers[i];var origin=origins[i];var cell=origin+IntVec3.East;
   duties[pawn]=specs[pawn.def.defName];tradeCells[pawn]=cell;jobs[pawn]=new HashSet<string>();
   foreach(var c in CellRect.CenteredOn(origin,5,5).Cells){map.areaManager.Home[c]=true;testArea[c]=true;}
   pawn.playerSettings.AreaRestrictionInPawnCurrentMap=testArea;
   RoleCommands.PasteRoleSet(pawn,new List<RoleAssignment>());
   if(!native) {
    var w=(MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",pawn,RoleStore.Current);
    var ids=MechanicalDutyPlan.BuildFixTarget(colony,w).RoleIds;
    RoleCommands.PasteRoleSet(pawn,ids.Select(id=>new RoleAssignment{roleId=id}).ToList());
    Record("TRADE_APPLY "+pawn.def.defName+" "+string.Join(",",ids.Select(id=>RoleStore.Current.RoleById(id).label)));
   }
   if(pawn.needs.rest!=null)pawn.needs.rest.CurLevel=1;if(pawn.needs.energy!=null)pawn.needs.energy.CurLevel=1;
   switch(duties[pawn]) {
    case "clean": FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Dirt);targets[pawn]=cell.GetFirstThing<Filth>(map);break;
    case "haul":
     var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(origin+IntVec3.West);
     zone.settings.filter.SetDisallowAll();zone.settings.filter.SetAllow(ThingDefOf.Steel,true);zone.settings.Priority=StoragePriority.Critical;
     var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=10;GenSpawn.Spawn(steel,cell,map);targets[pawn]=steel;tradeCells[pawn]=origin+IntVec3.West;break;
    case "flick": case "repair":
     var lamp=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("StandingLamp"));lamp.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(lamp,cell,map);targets[pawn]=lamp;
     if(duties[pawn]=="flick")lamp.TryGetComp<CompFlickable>().CompGetGizmosExtra().OfType<Command_Toggle>().First().toggleAction();
     else lamp.TakeDamage(new DamageInfo(DamageDefOf.Crush,20));break;
    case "mine":
     var rock=ThingMaker.MakeThing(ThingDefOf.MineableSteel);GenSpawn.Spawn(rock,cell,map);targets[pawn]=rock;
     map.designationManager.AddDesignation(new Designation(rock,DesignationDefOf.Mine));break;
    case "sow":
     before[pawn]=pawn.records.GetValue(RecordDefOf.PlantsSown);
     map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);var grow=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(grow);grow.AddCell(cell);grow.SetPlantDefToGrow(DefDatabase<ThingDef>.GetNamed("Plant_Rice"));break;
    case "tend":
     var patient=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(patient,cell,map);
     patient.playerSettings.medCare=MedicalCareCategory.NoMeds;patient.health.AddHediff(HediffDefOf.Anesthetic);
     var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,patient,patient.health.hediffSet.GetNotMissingParts().First(p=>p.def==BodyPartDefOf.Leg));cut.Severity=3;patient.health.AddHediff(cut);injuries[pawn]=cut;targets[pawn]=patient;
     var bed=(Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog);bed.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(bed,cell,map);bed.Medical=true;RestUtility.TuckIntoBed(bed,patient,patient,false);break;
    case "butcher":
     var table=(Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("TableButcher"),ThingDefOf.WoodLog);table.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(table,origin+IntVec3.North,map);
     var animal=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Hare"));GenSpawn.Spawn(animal,cell,map);animal.Kill(null);animal.Corpse.SetForbidden(false,false);targets[pawn]=animal.Corpse;
     var bill=(Bill_Production)BillUtility.MakeNewBill(DefDatabase<RecipeDef>.GetNamed("ButcherCorpseFlesh"));bill.repeatMode=BillRepeatModeDefOf.RepeatCount;bill.repeatCount=1;bill.SetPawnRestriction(pawn);bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);table.BillStack.AddBill(bill);bills[pawn]=bill;break;
   }
   pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
   Record("TRADE_START "+pawn.def.defName+" "+duties[pawn]+" compiled="+string.Join(",",((IEnumerable<WorkGiver>)Static("WorkRoles.CompiledJobOrders","NormalFor",pawn)).Select(g=>g.def.defName)));
  }
  tradeRunning=true;tradeTicks=0;Stage("RUNNING trades");
 }
 static bool TradeDone(Pawn pawn)
 {
  switch(duties[pawn]) {
   case "clean":return targets[pawn].Destroyed;
   case "haul":return targets[pawn].Spawned&&targets[pawn].Position==tradeCells[pawn];
   case "flick":return !targets[pawn].TryGetComp<CompFlickable>().SwitchIsOn;
   case "repair":return targets[pawn].HitPoints==targets[pawn].MaxHitPoints;
   case "mine":return targets[pawn].Destroyed&&RoomThings(pawn).Any(t=>t.def==ThingDefOf.Steel);
   case "sow":return pawn.records.GetValue(RecordDefOf.PlantsSown)>before[pawn]&&tradeCells[pawn].GetPlant(map)?.def.defName=="Plant_Rice";
   case "harvest":return RoomThings(pawn).Any(t=>t.def.defName=="RawRice");
   case "tend":return injuries[pawn].IsTended();
   case "butcher":return bills[pawn].repeatCount==0&&RoomThings(pawn).Any(t=>t.def.IsMeat);
  }
  return false;
 }
 static IEnumerable<Thing> RoomThings(Pawn pawn)=>CellRect.CenteredOn(origins[workers.IndexOf(pawn)],3,3).Cells.SelectMany(c=>c.GetThingList(map));
 static void StepTrades()
 {
  for(int tick=0;tick<200;tick++) {
   Find.TickManager.DoSingleTick();tradeTicks++;
   foreach(var pawn in workers) {
    if(pawn.CurJob!=null)jobs[pawn].Add(pawn.CurJob.def.defName);
    if(!completed.Contains(pawn)&&TradeDone(pawn)) {
     completed.Add(pawn);Record("TRADE_DONE "+pawn.def.defName+" "+duties[pawn]+" ticks="+tradeTicks+" jobs="+string.Join(",",jobs[pawn]));
    }
   }
  }
  if(completed.Count<workers.Count&&tradeTicks<12000)return;
  if(completed.Count<workers.Count) {
   foreach(var pawn in workers.Where(p=>!completed.Contains(p)))Record("TRADE_FAILED "+pawn.def.defName+" "+duties[pawn]+" jobs="+string.Join(",",jobs[pawn])+" position="+pawn.Position+" temp="+pawn.GetRoom().Temperature);
   throw new Exception("Incomplete native work: "+completed.Count+"/"+workers.Count);
  }
  if(tradePass==0) {
   var farmers=workers.Where(p=>duties[p]=="sow").ToArray();completed.Clear();tradePass++;
   foreach(var pawn in workers)if(!farmers.Contains(pawn))completed.Add(pawn);
   foreach(var pawn in farmers){tradeCells[pawn].GetPlant(map).Growth=1;duties[pawn]="harvest";jobs[pawn].Clear();pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);}
   tradeTicks=0;return;
  }
  tradeRunning=false;done=true;Stage("SUCCESS");
 }
}
