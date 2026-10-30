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
namespace RecoveryReview;
[StaticConstructorOnStartup]
public static class Probe
{
 static int stage,updates; static Pawn patient,doctor; static Map map; static Building_Bed bed;
 static Thing arm,heart,inferiorArm; static BodyPartRecord shoulder,heartPart; static Bill_Medical bill; static DateTime began;
 static string Root=>GenFilePaths.SaveDataFolderPath;
 static Probe()=>new Harmony("eprime.implanner.reviewrecovery").Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));
 static void Write(string s){Log.Message("[RecoveryReview] "+s);File.AppendAllText(Path.Combine(Root,"recovery-review.txt"),s+Environment.NewLine);}
 static void Assert(bool x,string s){if(!x)throw new Exception(s);Write("PASS "+s);}
 static void Reconcile()=>AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"),"Reconcile").Invoke(null,new object[]{ImplannerStore.Current,true});
 static void Update()
 {
  if(stage==99||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.AnyEventNowOrWaiting)return;
  try{
   if(updates++<8)return;
   AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);
   foreach(Window w in Find.WindowStack.Windows.ToArray())if(!(w is MainTabWindow))w.Close(false);
   if(stage==0){Setup();return;}
   Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
   if(stage==1){
    if(patient.health.hediffSet.HasHediff(HediffDef.Named("BionicArm"),shoulder)){FinishArm("native doctor job");return;}
    Drive();
    if((DateTime.UtcNow-began).TotalSeconds>8){Write("Native arm navigation timed out; actual doctor job="+doctor.CurJob+" patient job="+patient.CurJob+"; invoking selected real worker boundary");doctor.jobs.StopAll();bill.recipe.Worker.ApplyOnPawn(patient,shoulder,doctor,new List<Thing>{arm},bill);bill.recipe.Worker.ConsumeIngredient(arm,bill.recipe,map);FinishArm("selected real recipe Worker.ApplyOnPawn + Worker.ConsumeIngredient (no full-job claim)");}
   }else if(stage==2){
    if(patient.Dead){FinishHeart("native doctor job");return;}
    Drive();
    if((DateTime.UtcNow-began).TotalSeconds>8){Write("Native removal navigation timed out; invoking actual selected "+bill.recipe.workerClass.FullName+".ApplyOnPawn(patient,Heart,doctor,medicine,bill)");doctor.jobs.StopAll();Thing med=ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);bill.recipe.Worker.ApplyOnPawn(patient,heartPart,doctor,new List<Thing>{med},bill);FinishHeart("selected real Recipe_RemoveBodyPart.ApplyOnPawn boundary (no full-job claim)");}
   }
  }catch(Exception ex){Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;Write("FAILED "+ex);File.WriteAllText(Path.Combine(Root,"recovery-stage.txt"),"FAILED "+ex);stage=99;}
 }
 static PlannerModel Model()
 {
  var s=ImplannerStore.Current;var m=new PlannerModel();s.Model=m;m.SetAutomationPaused(false);m.SetAutoProduction(false);m.SetSurgeryConcurrency(20);m.SetCountHospitalized(false);m.SetAutoDoctorFloor(false);m.SetManualDoctorFloor(0);return m;
 }
 static void Setup()
 {
  Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;map=Find.CurrentMap;
  var near=map.mapPawns.FreeColonistsSpawned.First().Position;
  do{doctor=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);}while(doctor.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled);
  patient=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);
  GenSpawn.Spawn(doctor,CellFinder.RandomClosewalkCellNear(near,map,5),map);GenSpawn.Spawn(patient,CellFinder.RandomClosewalkCellNear(near,map,5),map);
  Assert(patient.genes?.HasActiveGene(GeneDefOf.Deathless)!=true&&!patient.health.hediffSet.HasPreventsDeath,"generated healthy baseliner patient has no death prevention");
  doctor.skills.GetSkill(SkillDefOf.Medicine).Level=20;doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor,1);
  bed=SpawnBed();doctor.Position=CellFinder.RandomClosewalkCellNear(bed.Position,map,2);doctor.Notify_Teleported(true,false);
  foreach(Thing t in map.listerThings.ThingsOfDef(ThingDef.Named("BionicArm")).ToArray())t.Destroy();
  arm=Spawn("BionicArm",QualityCategory.Awful);Assert(arm.TryGetComp<CompQuality>()!=null,"real QBR provides Awful implant quality");
  var parts=patient.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Shoulder"));shoulder=parts[0];
  var injury=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,patient,shoulder);injury.Severity=shoulder.def.GetMaxHealth(patient)*0.75f;patient.health.AddHediff(injury,shoulder);
  float injured=PawnCapacityUtility.CalculatePartEfficiency(patient.health.hediffSet,shoulder);Write("injured shoulder efficiency="+injured);
  var m=Model();var s=ImplannerStore.Current;var plan=m.CreatePlan("Both arms recovery review",s.TakePlanId);m.SetImplantSlot(plan.Id,"BionicArm",0,true);m.SetImplantSlot(plan.Id,"BionicArm",1,true);m.AssignPlan(patient.thingIDNumber,plan.Id);s.Bump(PlannerChange.All);Reconcile();
  Assert(m.Reservations.ContainsKey(arm.thingIDNumber),"Awful arm reserved while real injury makes it acceptable");Assert(patient.BillStack.Count==0,"missing other arm and current injury hold the initial batch");
  injury.Heal(injury.Severity+1);float healed=PawnCapacityUtility.CalculatePartEfficiency(patient.health.hediffSet,shoulder);Assert(healed>0.99f,"native Hediff_Injury.Heal restores shoulder efficiency to1");
  Reconcile();Assert(!m.Reservations.ContainsKey(arm.thingIDNumber),"IMP014: healed shoulder releases now-inferior Awful reservation");
  Thing normal=Spawn("BionicArm",QualityCategory.Normal);s.Bump(PlannerChange.All);Reconcile();
  Assert(patient.BillStack.Count==0,"IMP014: one Normal plus one Awful arm cannot release healthy two-arm batch");
  Spawn("BionicArm",QualityCategory.Normal);s.Bump(PlannerChange.All);Reconcile();
  bill=patient.BillStack.Bills.OfType<Bill_Medical>().Single(b=>b.Part==shoulder);
  Assert(!bill.IsFixedOrAllowedIngredient(arm),"IMP014: scheduled healthy shoulder refuses Awful item");
  Assert(bill.IsFixedOrAllowedIngredient(normal),"IMP014: scheduled healthy shoulder accepts reserved Normal item");
  inferiorArm=arm;arm=normal;
  bill.recipe.workAmount=60;bill.recipe.surgerySuccessChanceFactor=100;patient.playerSettings.medCare=MedicalCareCategory.Best;
  Spawn("MedicineIndustrial",QualityCategory.Normal).stackCount=20;stage=1;began=DateTime.UtcNow;File.WriteAllText(Path.Combine(Root,"recovery-stage.txt"),"arm-driving");Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;Drive();
 }
 static void FinishArm(string boundary)
 {
  float value=PawnCapacityUtility.CalculatePartEfficiency(patient.health.hediffSet,shoulder);Assert(value>=0.99f,"IMP014: healed shoulder stays at least natural efficiency="+value+"; evidence="+boundary);Assert(arm.Destroyed&&!inferiorArm.Destroyed,"IMP014: Normal item consumed and inferior Awful stock preserved");
  doctor.jobs.StopAll();patient.jobs.StopAll();foreach(Bill b in patient.BillStack.Bills.ToArray())patient.BillStack.Delete(b);
  var m=Model();var s=ImplannerStore.Current;heartPart=patient.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Heart"))[0];
  var worn=(HediffWithComps)patient.health.AddHediff(HediffDef.Named("BionicHeart"),heartPart);var comp=worn.comps.Single(c=>c.GetType().Name=="HediffCompQualityBionics");AccessTools.Field(comp.GetType(),"quality").SetValue(comp,QualityCategory.Normal);
  foreach(Thing t in map.listerThings.ThingsOfDef(ThingDef.Named("BionicHeart")).ToArray())t.Destroy();heart=Spawn("BionicHeart",QualityCategory.Excellent);
  var plan=m.CreatePlan("Heart upgrade review",s.TakePlanId);m.SetImplantSlot(plan.Id,"BionicHeart",0,true);m.AssignPlan(patient.thingIDNumber,plan.Id);m.SetPawnPriority(patient.thingIDNumber,1);s.Bump(PlannerChange.All);Reconcile();Assert(patient.BillStack.Count==0,"quality-upgrade option OFF keeps satisfied Normal heart untouched");
  m.SetUpgradeByPriority(true);s.Bump(PlannerChange.Options);Reconcile();
  Assert(patient.BillStack.Count==0,"IMP008: enabling quality upgrades never schedules vital heart removal");
  Reconcile();Assert(!patient.Dead&&patient.health.hediffSet.HasHediff(HediffDef.Named("BionicHeart"),heartPart),"IMP008: healthy patient retains functioning heart after repeated dispatch");
  Assert(!heart.Destroyed,"IMP008: Excellent replacement remains available without unsafe removal");
  Find.TickManager.Pause();File.WriteAllText(Path.Combine(Root,"recovery-stage.txt"),"SUCCESS");stage=99;
 }
 static void FinishHeart(string boundary){Assert(patient.Dead,"healthy nondeathless colonist killed by quality-upgrade heart removal; evidence="+boundary);Assert(!heart.Destroyed,"Excellent replacement heart remains unused");Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;File.WriteAllText(Path.Combine(Root,"recovery-stage.txt"),"SUCCESS");stage=99;}
 static Thing Spawn(string name,QualityCategory q){Thing t=ThingMaker.MakeThing(ThingDef.Named(name));t.TryGetComp<CompQuality>()?.SetQuality(q,ArtGenerationContext.Colony);GenPlace.TryPlaceThing(t,bed.Position,map,ThingPlaceMode.Near);t.SetForbidden(false,false);return t;}
 static Building_Bed SpawnBed(){foreach(IntVec3 cell in GenRadial.RadialCellsAround(patient.Position,25,true)){bool free=true;foreach(IntVec3 c in GenAdj.OccupiedRect(cell,Rot4.North,ThingDefOf.Bed.size))if(!c.InBounds(map)||!c.Standable(map)||c.GetEdifice(map)!=null||c.GetFirstItem(map)!=null||c.GetFirstPawn(map)!=null||!doctor.CanReach(c,PathEndMode.OnCell,Danger.Deadly)){free=false;break;}if(!free)continue;var b=(Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog);GenSpawn.Spawn(b,cell,map);b.SetFaction(Faction.OfPlayer);b.Medical=true;return b;}throw new Exception("no bed");}
 static void Drive(){if(patient.CurrentBed()!=bed&&patient.Spawned&&!patient.Downed){patient.jobs.StopAll();patient.Position=bed.GetSleepingSlotPos(0);patient.Notify_Teleported(true,false);foreach(Need n in patient.needs.AllNeeds)n.CurLevelPercentage=1;var rest=JobMaker.MakeJob(JobDefOf.LayDown,bed);rest.playerForced=true;rest.expiryInterval=60000;patient.jobs.StartJob(rest,JobCondition.InterruptForced);}if(patient.CurrentBed()==null||doctor.CurJob?.bill!=null)return;doctor.jobs.StopAll();foreach(WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading){if(!def.billGiversAllHumanlikes||!(def.Worker is WorkGiver_DoBill giver))continue;bill.nextTickToSearchForIngredients=0;Job job=giver.JobOnThing(doctor,patient,true);if(job?.bill!=bill)continue;Write("native doctor starts "+bill.recipe.defName+" target="+bill.Part.def.defName);doctor.jobs.TryTakeOrderedJob(job);return;}}
}
