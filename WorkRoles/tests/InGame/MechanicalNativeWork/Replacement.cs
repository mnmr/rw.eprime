using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.UI;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace RobotReportAudit;
public static partial class Probe
{
 static void RequireReplacement(bool condition,string message)
 {
  if(!condition)throw new Exception(message);
  Record("REPLACEMENT PASS "+message);
 }
 static MechanicalWorkerView ReplacementWorker(Pawn pawn)=>
  (MechanicalWorkerView)Static("WorkRoles.MechanicalWorkers","Capture",pawn,RoleStore.Current);
 static void VerifyReplacementApply()
 {
  var store=RoleStore.Current;
  RoleCommands.SetMechanicalWorkersEnabled(true);
  RoleCommands.SetMechanicalRecommendationsEnabled(true);
  var grunt=store.RoleByTemplate("WS_Grunt");
  var laborer=store.RoleByTemplate("WS_Laborer");
  var hauler=store.RoleByTemplate("WS_Hauler");
  RequireReplacement(grunt!=null&&laborer!=null&&hauler!=null,"fixture has Grunt, Laborer and Hauler");
  foreach(var role in new[]{grunt,laborer,hauler})RoleCommands.SetRoleRecommendationExcluded(role.id,false);
  RoleCommands.SetRoleColonyMinimum(hauler.id,2);
  RoleCommands.SetRoleCoverage(hauler.id,0);
  var stationType=AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
  var stationDef=DefDatabase<ThingDef>.AllDefsListForReading.Single(d=>d.thingClass==stationType&&d.defName.Contains("Hauler")&&d.defName.EndsWith("_II"));
  var cell=map.AllCells.First(c=>GenAdj.OccupiedRect(c,Rot4.North,stationDef.size).Cells.All(x=>x.InBounds(map)&&x.Standable(map)&&x.GetEdifice(map)==null&&x.GetFirstPawn(map)==null));
  var station=ThingMaker.MakeThing(stationDef);station.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(station,cell,map);
  if(station.TryGetComp<CompPowerTrader>() is CompPowerTrader power)power.PowerOn=true;
  AccessTools.Field(stationType,"SpawnRobotAfterRecharge").SetValue(station,true);
  Call(station,"Button_SpawnBot");
  var pawn=(Pawn)Field(station,"robot");
  RequireReplacement(pawn!=null&&pawn.def.defName=="AIRobot_Hauler_II","native station spawns Tier II hauler");
  pawn.playerSettings.AreaRestrictionInPawnCurrentMap=null;
  var humans=map.mapPawns.FreeColonistsSpawned.ToList();
  var button=DefDatabase<MainButtonDef>.AllDefsListForReading.First(d=>d.TabWindow is MainTabWindow_WorkRoles);
  var view=Field(button.TabWindow,"colonistsTab");
  ((ColonistsViewProfile)Field(view,"profile")).PawnsIn=_=>humans.Concat(new[]{pawn}).ToList();
  Find.Selector.ClearSelection();Find.Selector.Select(pawn);Find.MainTabsRoot.SetCurrentTab(button);
  Call(Field(view,"selection"),"Click",pawn);
  foreach(string scenario in new[]{"disabled","cross-pin","pinned-target","force-on"})
  {
   var assignments=new List<RoleAssignment>{new(){roleId=grunt.id}};
   if(scenario=="cross-pin")assignments.Add(new(){roleId=laborer.id,pinned=true});
   assignments.Add(new(){roleId=hauler.id,state=scenario=="disabled"?AssignmentState.Disabled:scenario=="force-on"?AssignmentState.ForceOn:AssignmentState.Enabled,pinned=scenario=="pinned-target"});
   RoleCommands.PasteRoleSet(pawn,assignments);
   var before=ReplacementWorker(pawn);
   var held=before.Facts.Existing.Single(a=>a.RoleId==hauler.id);
   RequireReplacement(held.Enabled==(scenario!="disabled")&&held.ForceOn==(scenario=="force-on"),scenario+" native projection retains assignment state");
   RequireReplacement(before.ProvidesService&&before.ActiveRoleIds.First()==grunt.id&&before.CommittedGivers.Contains("HaulGeneral"),scenario+" starts with active primary hauling service");
   var colony=RecsAdapter.BuildColonyView(store,humans);
   colony.MechanicalWorkers.Clear();colony.MechanicalWorkers.Add(before);
   RequireReplacement(new EngineContext(colony).RequirementOf(hauler.id).RequiredTotal==1,scenario+" before Apply supplies one hauling worker");
   Call(view,"RefreshExternalSnapshotIfNeeded");
   Call(view,"RebuildFixEntries",pawn,false);
   var plans=((IEnumerable)Field(Field(view,"recommendationState"),"plans")).Cast<object>();
   var plan=plans.Single(p=>ReferenceEquals(Prop(p,"Pawn"),pawn));
   var target=((IEnumerable)Prop(plan,"Target")).Cast<RoleAssignment>().ToList();
   Record("REPLACEMENT TARGET "+scenario+" "+string.Join(",",target.Select(a=>store.RoleById(a.roleId).templateDefName+":"+a.state+":pin="+a.pinned)));
   Call(view,"ApplyFix",pawn,null,false);
   var after=ReplacementWorker(pawn);
   RequireReplacement(after.ActiveRoleIds.First()==(scenario=="force-on"?hauler.id:grunt.id),scenario+" Apply preserves primary hauling work");
   RequireReplacement(after.CommittedGivers.Contains("HaulGeneral"),scenario+" Apply keeps hauling compiled");
   colony.MechanicalWorkers[0]=after;
   RequireReplacement(new EngineContext(colony).RequirementOf(hauler.id).RequiredTotal==1,scenario+" after Apply supplies one hauling worker");
   if(scenario=="cross-pin"||scenario=="pinned-target")
   {
    int pinned=scenario=="cross-pin"?laborer.id:hauler.id;
    RequireReplacement(after.Facts.Existing.Single(a=>a.RoleId==pinned).Pinned,scenario+" Apply preserves pin");
    RequireReplacement(after.Facts.Existing.FindIndex(a=>a.RoleId==grunt.id)<after.Facts.Existing.FindIndex(a=>a.RoleId==pinned),scenario+" Apply retains work before pin");
   }
   if(scenario=="force-on")RequireReplacement(after.Facts.Existing.Single(a=>a.RoleId==hauler.id).ForceOn,"Apply preserves ForceOn on safe replacement");
  }
  Find.MainTabsRoot.EscapeCurrentTab(false);
  RequireReplacement(Find.TickManager.CurTimeSpeed==TimeSpeed.Paused,"all Apply transitions verified while paused");
 }
}
