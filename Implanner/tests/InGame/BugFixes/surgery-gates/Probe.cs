using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;

namespace ImplannerSurgeryGateReview
{
 [StaticConstructorOnStartup]
 public static class Probe
 {
  static readonly Harmony patches=new Harmony("eprime.implanner.surgerygateprobe");
  static bool ran;
  static string Root=>GenFilePaths.SaveDataFolderPath;
  static Probe(){patches.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));}
  static void Check(bool value,string message){if(!value)throw new Exception(message);Log.Message("[SurgeryGateProbe] PASS "+message);File.AppendAllText(Path.Combine(Root,"surgery-gates-evidence.txt"),message+Environment.NewLine);}
  static void Pass(ImplannerStore store){var t=typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler");AccessTools.Method(t,"Reconcile").Invoke(null,new object[]{store,false});}
  static PlannerModel Model(ImplannerStore store){var model=new PlannerModel();store.Model=model;model.SetAutomationPaused(false);model.SetAutoProduction(false);model.SetAutoDoctorFloor(false);model.SetManualDoctorFloor(5);model.SetSurgeryConcurrency(1);model.SetCountHospitalized(false);store.Bump(PlannerChange.All);return model;}
  static void Clear(Pawn pawn){while(pawn.BillStack.Count>0)pawn.BillStack.Delete(pawn.BillStack[0]);}
  static Pawn NewPawn(Map map,int offset){var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);foreach(var h in new List<Hediff>(pawn.health.hediffSet.hediffs))pawn.health.RemoveHediff(h);GenSpawn.Spawn(pawn,map.Center+new IntVec3(offset,0,0),map);return pawn;}
  static Thing Stock(Map map,string def){Thing t=ThingMaker.MakeThing(ThingDef.Named(def));GenPlace.TryPlaceThing(t,map.Center,map,ThingPlaceMode.Near);t.SetForbidden(false,false);return t;}
  static void Update()
  {
   if(ran||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.AnyEventNowOrWaiting)return;ran=true;
   try{
    Find.TickManager.Pause();var map=Find.CurrentMap;var store=ImplannerStore.Current;
    var early=NewPawn(map,0);var later=NewPawn(map,2);Check(early.thingIDNumber<later.thingIDNumber,"fixture lower id "+early.LabelShort+" precedes "+later.LabelShort);
    var arm=early.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Shoulder)[0];early.health.AddHediff(HediffDef.Named("BionicArm"),arm);
    var model=Model(store);var plan=model.CreatePlan("Elbow blade patients",store.TakePlanId);model.SetImplantSlot(plan.Id,"ElbowBlade",0,true);model.AssignPlan(early.thingIDNumber,plan.Id);model.AssignPlan(later.thingIDNumber,plan.Id);Stock(map,"ElbowBlade");Stock(map,"ElbowBlade");store.Bump(PlannerChange.All);Pass(store);
    Check(model.Reservations.Count>=2,"both patients receive physical elbow-blade reservations");
    Check(early.BillStack.Count==0,"artificial arm patient correctly gets no impossible elbow blade operation");
    Check(later.BillStack.Count==1,"IMP002: valid natural arm patient schedules at cap 1 despite earlier impossible surgery");
    Pass(store);Check(later.BillStack.Count==1,"IMP002: repeated reconciliation preserves valid scheduled operation");
    model.SetSurgeryConcurrency(2);Pass(store);Check(later.BillStack.Count==1,"raising cap to 2 immediately schedules natural arm patient (same stock, plan and health)");
    Clear(early);Clear(later);
    model=Model(store);plan=model.CreatePlan("Doctor floor change",store.TakePlanId);model.SetImplantSlot(plan.Id,"BionicArm",0,true);model.AssignPlan(later.thingIDNumber,plan.Id);Stock(map,"BionicArm");store.Bump(PlannerChange.All);Pass(store);
    Check(later.BillStack.Count==1,"initial arm surgery scheduled");var bill=(Bill_Medical)later.BillStack[0];Check(bill.allowedSkillRange.min==5,"initial surgery enforces Medical 5");
    foreach(var t in new List<Thing>(map.listerThings.ThingsOfDef(ThingDef.Named("BionicLeg"))))t.Destroy();
    model.SetImplantSlot(plan.Id,"BionicLeg",0,true);model.SetManualDoctorFloor(18);store.Bump(PlannerChange.All);Pass(store);
    Check(model.ManualDoctorFloor==18,"new requested doctor floor is Medical 18");Check(bill.allowedSkillRange.min==18,"IMP003: retained surgery immediately enforces Medical 18 while new leg is unavailable");
    var doctor=NewPawn(map,4);doctor.skills.GetSkill(SkillDefOf.Medicine).Level=10;
    Check(!bill.PawnAllowedToStartAnew(doctor),"IMP003: native bill rejects Medical 10 doctor at configured minimum 18");
    Stock(map,"BionicLeg");Pass(store);Check(bill.allowedSkillRange.min==18,"supplying unrelated new leg finally updates existing arm bill to Medical 18");Check(!bill.PawnAllowedToStartAnew(doctor),"same doctor's eligibility is correctly denied once unrelated stock arrives");
    File.WriteAllText(Path.Combine(Root,"surgery-gates-stage.txt"),"SUCCESS");
   }catch(Exception ex){Log.Error("[SurgeryGateProbe] "+ex);File.WriteAllText(Path.Combine(Root,"surgery-gates-stage.txt"),"FAILED "+ex);}
   patches.UnpatchAll(patches.Id);
  }
 }
}
