using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;

namespace DeathRolesProbe;
/// Owner report (2026-11-05): mechs lose their roles when they die, though
/// they can be resurrected or repaired. Pawn.Kill ends in Destroy(KillFinalize)
/// and WorkRoles dropped the role set there. Proves: a resurrected colonist
/// (same session), a mech resurrected after a save/reload, and a Misc. Robots
/// robot repaired at its station after a save/reload get their exact roles back.
[StaticConstructorOnStartup]
public static class Probe
{
 static bool started, done, reloading, reloaded; static int mechId, colonistId, stationId, robotId;
 static List<string> mechRoles, colonistRoles, robotRoles;
 static string Root => GenFilePaths.SaveDataFolderPath;
 static void Record(string s) => File.AppendAllText(Path.Combine(Root,"deathroles-results.txt"),s+Environment.NewLine);
 static void Stage(string s) => File.WriteAllText(Path.Combine(Root,"deathroles-stage.txt"),s);
 static void Expect(bool ok,string what){ Record((ok?"PASS ":"FAIL ")+what); if(!ok)throw new Exception("EXPECT "+what); }
 static object Call(object o,string n,params object[] a) => AccessTools.Method(o.GetType(),n,a.Select(x=>x.GetType()).ToArray()).Invoke(o,a);
 static Probe() { new Harmony("eprime.workroles.deathroles").Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));Log.Message("[DeathRolesProbe] initialized"); }
 static List<string> Roles(Pawn p) => RoleStore.Current.pawnSets.TryGetValue(p,out var s)?s.assignments.Select(a=>a.roleId+":"+a.state+":"+a.pinned).ToList():new List<string>();
 static string Show(List<string> l) => "["+string.Join(",",l)+"]";
 static void Update()
 {
  if(done || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting)return;
  try {
   if(!started){started=true;Stage("SETUP");BeforeSave();return;}
   if(reloading){reloading=false;reloaded=true;return;} // first frame of the reloaded game
   if(reloaded){done=true;AfterReload();Stage("SUCCESS");}
  } catch(Exception ex){done=true;Record("ERROR "+ex);Stage("FAILED "+ex.Message);}
 }
 static void Assign(Pawn p,RoleStore store,params (string template,AssignmentState state,bool pinned)[] roles)
 {
  RoleCommands.PasteRoleSet(p,roles.Select(r=>new RoleAssignment{roleId=store.RoleByTemplate(r.template).id,state=r.state,pinned=r.pinned}).ToList());
 }
 static void BeforeSave()
 {
  var map=Find.CurrentMap; Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
  foreach(Window w in Find.WindowStack.Windows.ToArray())w.Close(false);
  var store=RoleStore.Current;
  Record("GAME "+VersionControl.CurrentVersionString);
  RoleCommands.SetAutoOptimize(false);RoleCommands.SetMechanicalWorkersEnabled(true);
  var cell=map.Center; IntVec3 Free(){var c=CellFinder.RandomClosewalkCellNear(cell,map,12,x=>x.Standable(map)&&x.GetFirstPawn(map)==null&&x.GetEdifice(map)==null);return c;}
  var mech=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDef.Named("Mech_Lifter"),Faction.OfPlayer));
  GenSpawn.Spawn(mech,Free(),map);
  var colonist=map.mapPawns.FreeColonistsSpawned.First(p=>!p.Downed&&!p.DevelopmentalStage.Baby());
  var station=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("AIRobot_RechargeStation_Hauler"));station.SetFaction(Faction.OfPlayer);
  var stationCell=CellFinder.RandomClosewalkCellNear(cell,map,15,x=>GenAdj.OccupiedRect(x,Rot4.North,station.def.size).Cells.All(c=>c.Standable(map)&&c.GetEdifice(map)==null&&c.GetFirstPawn(map)==null));
  GenSpawn.Spawn(station,stationCell,map);
  AccessTools.Method(station.GetType(),"Button_SpawnBot").Invoke(station,null);
  var robot=(Pawn)AccessTools.Field(station.GetType(),"robot").GetValue(station);
  if(robot==null)throw new Exception("Station spawned no robot");
  Assign(mech,store,("WS_Firefighter",AssignmentState.Enabled,true),("WS_Core",AssignmentState.Disabled,false));
  Assign(robot,store,("WS_Core",AssignmentState.Enabled,false),("WS_Firefighter",AssignmentState.ForceOn,true));
  mechId=mech.thingIDNumber;colonistId=colonist.thingIDNumber;stationId=station.thingIDNumber;robotId=robot.thingIDNumber;
  mechRoles=Roles(mech);colonistRoles=Roles(colonist);robotRoles=Roles(robot);
  Record("BEFORE mech="+Show(mechRoles)+" colonist="+colonist.LabelShort+Show(colonistRoles)+" robot="+robot.def.defName+Show(robotRoles));
  Expect(mechRoles.Count==2&&robotRoles.Count==2&&colonistRoles.Count>0,"fixture pawns hold roles");
  mech.Kill(null);colonist.Kill(null);robot.Kill(null);
  Record("KILLED mechDead="+mech.Dead+" mechDestroyed="+mech.Destroyed+" corpse="+(mech.Corpse!=null)+" robotDestroyed="+robot.Destroyed+" robotCorpse="+(robot.Corpse!=null));
  Expect(!store.IsManaged(mech)&&!store.IsManaged(colonist)&&!store.IsManaged(robot),"dead pawns leave the live role store");
  Expect(store.dormantPawnSets.ContainsKey(mech)&&store.dormantPawnSets.ContainsKey(colonist),"mech and colonist roles kept dormant");
  Expect(store.stationRoleSets.ContainsKey(station),"robot roles kept on its station");
  Expect(ResurrectionUtility.TryResurrect(colonist),"colonist resurrected");
  Record("COLONIST after resurrection "+Show(Roles(colonist)));
  Expect(Roles(colonist).SequenceEqual(colonistRoles),"resurrected colonist has its exact roles back (same session)");
  Expect(!store.dormantPawnSets.ContainsKey(colonist),"colonist dormant slot released");
  GameDataSaveLoader.SaveGame("DeathRolesProbe");
  Record("SAVED");
  reloading=true;
  GameDataSaveLoader.LoadGame("DeathRolesProbe");
 }
 static void AfterReload()
 {
  var map=Find.CurrentMap; Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; var store=RoleStore.Current;
  Record("RELOADED dormant="+store.dormantPawnSets.Count+" stations="+store.stationRoleSets.Count);
  var corpse=map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Cast<Corpse>().First(c=>c.InnerPawn?.thingIDNumber==mechId);
  var mech=corpse.InnerPawn;
  var station=map.listerThings.AllThings.First(t=>t.thingIDNumber==stationId);
  Expect(store.dormantPawnSets.ContainsKey(mech),"dead mech's roles survive save/reload");
  Expect(store.stationRoleSets.ContainsKey(station),"dead robot's roles survive save/reload on its station");
  Expect(ResurrectionUtility.TryResurrect(mech),"mech resurrected");
  Record("MECH after resurrection "+Show(Roles(mech)));
  Expect(Roles(mech).SequenceEqual(mechRoles),"resurrected mech has its exact roles back");
  // What X2_JobDriver_RepairDamagedRobot calls when a pawn finishes the repair.
  AccessTools.Method(station.GetType(),"Notify_RobotRepaired").Invoke(station,null);
  var robot=(Pawn)AccessTools.Field(station.GetType(),"robot").GetValue(station);
  Record("ROBOT after repair id="+robot?.thingIDNumber+" (was "+robotId+") "+(robot==null?"":Show(Roles(robot))));
  Expect(robot!=null&&robot.thingIDNumber!=robotId,"repair spawned a new robot");
  Expect(Roles(robot).SequenceEqual(robotRoles),"repaired robot has the dead robot's exact roles");
  Expect(!store.stationRoleSets.ContainsKey(station),"station slot released");
 }
}
