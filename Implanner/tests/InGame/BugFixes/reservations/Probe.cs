using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace ReservationReview;

[StaticConstructorOnStartup]
public static class Probe
{
 static int stage, updates;
 static Pawn patient, doctor;
 static Thing arm;
 static BodyPartRecord part;
 static Building_Bed bed;
 static Bill_Medical bill;
 static int startTick;
 static string Root => GenFilePaths.SaveDataFolderPath;
 static Probe() => new Harmony("eprime.implanner.reviewreservations").Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix:new HarmonyMethod(typeof(Probe), nameof(Update)));
 static void Write(string message) { Log.Message("[ReservationReview] " + message); File.AppendAllText(Path.Combine(Root,"reservation-review.txt"), message + Environment.NewLine); }
 static void Assert(bool value,string message) { if (!value) throw new Exception(message); Write("PASS " + message); }
 static void Reconcile() => AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"), "Reconcile").Invoke(null,new object[]{ImplannerStore.Current,true});
 static void Update()
 {
  if(stage==99 || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting) return;
  try {
   if(updates++<8) return;
   if(stage==0) { Setup(); return; }
   if(stage==1) {
    if(patient.health.hediffSet.HasHediff(HediffDef.Named("BionicArm"),part)) {
     Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
     Assert(arm.Destroyed,"IMP001 control: after releasing holdback native doctor consumes arm and completes installation");
     File.WriteAllText(Path.Combine(Root,"reservation-stage.txt"),"SUCCESS"); stage=99; return;
    }
    if(Find.TickManager.TicksGame-startTick>7000) throw new Exception("operation did not finish; doctor="+doctor.CurJob+" patient="+patient.CurJob+" arm="+arm.Spawned+" bill="+patient.BillStack.Count);
    if(updates%8==0) Drive();
   }
  } catch(Exception ex) { Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; Write("FAILED "+ex); File.WriteAllText(Path.Combine(Root,"reservation-stage.txt"),"FAILED "+ex); stage=99; }
 }
 static void Setup()
 {
  Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
  AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
  foreach(var w in Find.WindowStack.Windows.ToArray()) if(!(w is MainTabWindow)) w.Close(false);
  Map map=Find.CurrentMap;
  var pawns=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed && !p.health.HasHediffsNeedingTend()).OrderBy(p=>p.thingIDNumber).ToList();
  doctor=pawns.First(p=>!p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled);
  foreach(Pawn p in pawns) {
   if(p==doctor) continue;
   BodyPartRecord shoulder=p.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Shoulder)[0];
   if(p.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(shoulder) || p.health.hediffSet.PartIsMissing(shoulder)) continue;
   patient=p; part=shoulder; break;
  }
  Assert(patient!=null,"healthy native-shoulder patient found");
  doctor.skills.GetSkill(SkillDefOf.Medicine).Level=20;
  doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor,1);
  var store=ImplannerStore.Current; var model=new PlannerModel(); store.Model=model;
  model.SetAutomationPaused(false); model.SetAutoProduction(false); model.SetSurgeryConcurrency(20); model.SetCountHospitalized(false); model.SetAutoDoctorFloor(false); model.SetManualDoctorFloor(0);
  foreach(var b in patient.BillStack.Bills.ToArray()) patient.BillStack.Delete(b);
  foreach(Thing existing in map.listerThings.ThingsOfDef(ThingDef.Named("BionicArm")).ToArray()) existing.Destroy();
  bed=SpawnBed(map);
  arm=ThingMaker.MakeThing(ThingDef.Named("BionicArm")); GenPlace.TryPlaceThing(arm,bed.Position,map,ThingPlaceMode.Near); arm.SetForbidden(false,false);
  Thing medicine=ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial); medicine.stackCount=20; GenPlace.TryPlaceThing(medicine,bed.Position,map,ThingPlaceMode.Near); medicine.SetForbidden(false,false);
  var plan=model.CreatePlan("Reservation review",store.TakePlanId); model.SetImplantSlot(plan.Id,"BionicArm",0,true); model.AssignPlan(patient.thingIDNumber,plan.Id); store.Bump(PlannerChange.All); Reconcile();
  Assert(model.Reservations.ContainsKey(arm.thingIDNumber),"Implanner reserves arm before surgery");
  bill=patient.BillStack.Bills.OfType<Bill_Medical>().Single();
  Assert(bill.IsFixedOrAllowedIngredient(arm),"initial bound medical bill accepts its reserved arm");
  Write("initial pawn="+patient.thingIDNumber+" arm="+arm.thingIDNumber+" bill="+bill.GetUniqueLoadID());
  PlannerCommands.SetImplantReserve("BionicArm",1); Reconcile();
  Assert(!model.Reservations.ContainsKey(arm.thingIDNumber),"manual holdback releases implant reservation");
  Assert(!patient.BillStack.Bills.Contains(bill),"IMP001: held-back arm retracts unstarted owned operation");
  foreach(WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
   if(def.billGiversAllHumanlikes && def.Worker is WorkGiver_DoBill giver)
    Assert(giver.JobOnThing(doctor,patient,true)==null,"IMP001: native doctor cannot start retracted surgery");
  Assert(!arm.Destroyed && !patient.health.hediffSet.HasHediff(HediffDef.Named("BionicArm"),part),"IMP001: held-back arm and natural limb are preserved");
  PlannerCommands.SetImplantReserve("BionicArm",0); Reconcile();
  bill=patient.BillStack.Bills.OfType<Bill_Medical>().Single();
  Assert(model.Reservations.ContainsKey(arm.thingIDNumber),"IMP001 control: releasing holdback reserves arm again");
  bill.recipe.workAmount=60; bill.recipe.surgerySuccessChanceFactor=100;
  patient.playerSettings.medCare=MedicalCareCategory.Best;
  startTick=Find.TickManager.TicksGame; stage=1; File.WriteAllText(Path.Combine(Root,"reservation-stage.txt"),"driving");
  Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast; Drive();
 }
 static Building_Bed SpawnBed(Map map)
 {
  foreach(IntVec3 cell in GenRadial.RadialCellsAround(patient.Position,25,true)) {
   bool free=true;
   foreach(IntVec3 c in GenAdj.OccupiedRect(cell,Rot4.North,ThingDefOf.Bed.size)) if(!c.InBounds(map)||!c.Standable(map)||c.GetEdifice(map)!=null||c.GetFirstItem(map)!=null||c.GetFirstPawn(map)!=null||!doctor.CanReach(c,PathEndMode.OnCell,Danger.Deadly)) {free=false;break;}
   if(!free) continue;
   var b=(Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog); GenSpawn.Spawn(b,cell,map); b.SetFaction(Faction.OfPlayer); b.Medical=true; return b;
  }
  throw new Exception("no bed location");
 }
 static void Drive()
 {
  if(patient.CurrentBed()==null && patient.Spawned && !patient.Downed) {
   patient.jobs.StopAll(); patient.Position=bed.GetSleepingSlotPos(0); patient.Notify_Teleported(true,false);
   foreach(Need n in patient.needs.AllNeeds) n.CurLevelPercentage=1;
   var rest=JobMaker.MakeJob(JobDefOf.LayDown,bed); rest.playerForced=true;rest.expiryInterval=60000;patient.jobs.StartJob(rest,JobCondition.InterruptForced);
  }
  if(patient.CurrentBed()==null||doctor.CurJob?.bill!=null) return;
  doctor.jobs.StopAll();
  foreach(WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading) {
   if(!def.billGiversAllHumanlikes || !(def.Worker is WorkGiver_DoBill giver)) continue;
   bill.nextTickToSearchForIngredients=0; var job=giver.JobOnThing(doctor,patient,true);
   if(job?.bill!=bill) continue;
   bool uses=job.targetQueueB!=null && job.targetQueueB.Any(t=>t.Thing==arm);
   Assert(uses,"native doctor selects the now-authorized reserved arm");doctor.jobs.TryTakeOrderedJob(job);Write("doctor starts authorized implant operation"); return;
  }
 }
}
