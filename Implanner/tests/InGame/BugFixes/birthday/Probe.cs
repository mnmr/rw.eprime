using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using Implanner.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImplannerBirthdayReview
{
 [StaticConstructorOnStartup]
 public static class Probe
 {
  static readonly Harmony patches=new Harmony("eprime.implanner.birthdayreviewprobe");
  static int stage,ageTick,factsBefore,versionBefore;static bool sawBirthday;static Pawn child;static Dialog_Implanner window;static object beforeData;
  static string Root=>GenFilePaths.SaveDataFolderPath;
  static Probe(){patches.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));patches.Patch(AccessTools.Method(typeof(Pawn_AgeTracker),"BirthdayBiological"),prefix:new HarmonyMethod(typeof(Probe),nameof(Birthday)));}
  static void Note(string text){File.AppendAllText(Path.Combine(Root,"birthday-evidence.txt"),text+Environment.NewLine);Log.Message("[BirthdayReview] "+text);}
  static void Check(bool value,string text){if(!value)throw new Exception(text);Note(text);}
  static void Mark(string value)=>File.WriteAllText(Path.Combine(Root,"birthday-stage.txt"),value);
  static object Read(object value,string name){return AccessTools.Field(value.GetType(),name).GetValue(value);}
  static object Property(object value,string name)=>AccessTools.Property(value.GetType(),name).GetValue(value);
  static int Facts=>(int)AccessTools.Property(typeof(ImplannerStore).Assembly.GetType("Implanner.ExternalPawnFacts"),"Revision").GetValue(null);
  static void Birthday(Pawn_AgeTracker __instance,int birthdayAge){if(child!=null&&ReferenceEquals(__instance,child.ageTracker)){sawBirthday=true;Note("Real BirthdayBiological("+birthdayAge+") called by running game at tick "+Find.TickManager.TicksGame);}}
  static object Data(){var state=Read(window,"overview");var snapshot=AccessTools.Method(state.GetType(),"Current").Invoke(state,new object[]{ImplannerStore.Current});return Read(snapshot,"Data");}
  static bool Visible(object data){foreach(var row in (IEnumerable)Read(data,"Colonists"))if((int)Read(row,"PawnId")==child.thingIDNumber)return true;return false;}
  static void Open(){window=new Dialog_Implanner();var tab=AccessTools.Field(typeof(Dialog_Implanner),"curTab");tab.SetValue(null,Enum.Parse(tab.FieldType,"Overview"));var state=Read(window,"overview");AccessTools.Field(state.GetType(),"NameAlphabetical").SetValue(state,true);Find.WindowStack.Add(window);}
  static void Update()
  {
   if(stage==99||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.AnyEventNowOrWaiting)return;
   try{
    string control=Path.Combine(Root,"birthday-control.txt");string requested=File.Exists(control)?File.ReadAllText(control).Trim():"";
    if(stage==0){
     Find.TickManager.Pause();AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);foreach(var p in new List<Window>(Find.WindowStack.Windows))if(!(p is MainTabWindow))p.Close(false);
     var store=ImplannerStore.Current;var model=new PlannerModel();store.Model=model;model.SetAutomationPaused(false);model.SetAutoProduction(false);foreach(Pawn doctor in Find.CurrentMap.mapPawns.FreeColonistsSpawned){if(!doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)){doctor.skills.GetSkill(SkillDefOf.Medicine).Level=20;doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor,1);break;}}store.Bump(PlannerChange.All);
     var request=new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,fixedBiologicalAge:12.99999f,developmentalStages:DevelopmentalStage.Child,forceNoGear:true);child=PawnGenerator.GeneratePawn(request);child.skills.GetSkill(SkillDefOf.Medicine).Level=0;child.Name=new NameTriple("AAA","AAA Birthday","Review");GenSpawn.Spawn(child,Find.CurrentMap.Center,Find.CurrentMap);
     var cap=(Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_Tuque"),ThingDefOf.Cloth);child.apparel.Wear(cap);child.drafter.Drafted=true;
     Check(child.DevelopmentalStage==DevelopmentalStage.Child,"Fixture is a real Biotech child at "+child.ageTracker.AgeBiologicalTicks+" biological ticks, wearing only a tuque (Child, Adult apparel)");
     AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"),"Reconcile").Invoke(null,new object[]{store,true});Open();stage=1;
    }else if(stage==1){
     beforeData=Data();Check(!Visible(beforeData),"Open overview correctly excludes child before birthday");factsBefore=Facts;versionBefore=ImplannerStore.Current.Version;Note("Before birthday facts="+factsBefore+" store="+versionBefore);stage=2;Mark("ready");
    }else if(stage==2&&requested=="grow"){
     ageTick=Find.TickManager.TicksGame;Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;stage=3;
    }else if(stage==3&&child.DevelopmentalStage==DevelopmentalStage.Adult){
     Find.TickManager.Pause();Check(sawBirthday,"Natural simulation tick invoked 13th birthday; no age field or debug birthday was changed");Check(child.apparel.WornApparelCount==1,"Birthday retained adult-compatible tuque; no child-only apparel was dropped");Check(Facts>factsBefore,"IMP011: natural adult transition invalidates pawn facts ("+Facts+")");Check(ImplannerStore.Current.Version==versionBefore,"Birthday did not change Implanner store revision");Note("Gate before read: uiStamp="+Read(Read(window,"overview"),"uiStamp")+", locationStamp="+Read(Read(window,"overview"),"locationStamp"));var current=Data();Check(!ReferenceEquals(current,beforeData),"IMP011: open overview replaces child roster after natural birthday");Check(Visible(current),"IMP011: newly adult AAA Birthday appears in the already open overview");Note("Age now "+child.ageTracker.AgeBiologicalYears+" years; elapsed actual simulation ticks="+(Find.TickManager.TicksGame-ageTick));AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"),"Reconcile").Invoke(null,new object[]{ImplannerStore.Current,true});Check(ImplannerStore.Current.Version==versionBefore,"Normal 1020-boundary reconciliation leaves stable colony revisions unchanged");Check(Visible(Data()),"IMP011: adult remains visible after normal boundary reconciliation");stage=4;Mark("stale");
    }else if(stage==4&&requested=="reopen"){
     PlannerCommands.SetSurgeryConcurrency(2);stage=5;
    }else if(stage==5){Check(Visible(Data()),"Unrelated concurrency setting change makes newly adult AAA Birthday appear immediately in same open window");stage=99;Mark("SUCCESS");patches.UnpatchAll(patches.Id);}
   }catch(Exception ex){Find.TickManager.Pause();stage=99;Note("FAILED "+ex);Mark("FAILED "+ex);patches.UnpatchAll(patches.Id);}
  }
 }
}
