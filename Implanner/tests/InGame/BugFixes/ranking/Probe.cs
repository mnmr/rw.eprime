using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using Implanner.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImplannerRankingReview
{
 [StaticConstructorOnStartup]
 public static class Probe
 {
  static readonly Harmony patches=new Harmony("eprime.implanner.rankingreviewprobe");
  static int stage;static bool sawDrag;static Dialog_Implanner window;static PlannerModel model;
  static string Root=>GenFilePaths.SaveDataFolderPath;
  static Probe(){patches.Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update)));patches.Patch(AccessTools.Method(typeof(Dialog_Implanner),"DrawRankingRow"),prefix:new HarmonyMethod(typeof(Probe),nameof(Row)));patches.Patch(AccessTools.Method(typeof(PlannerCommands),"MoveImplantRank"),prefix:new HarmonyMethod(typeof(Probe),nameof(Move)));}
  static void Note(string text){File.AppendAllText(Path.Combine(Root,"ranking-evidence.txt"),text+Environment.NewLine);Log.Message("[RankingReview] "+text);}
  static void Mark(string value)=>File.WriteAllText(Path.Combine(Root,"ranking-stage.txt"),value);
  static object Read(object value,string name){return AccessTools.Field(value.GetType(),name).GetValue(value);}
  static void Move(string implantDefName,int stars,string beforeDefName){Note("UI issued MoveImplantRank payload="+implantDefName+" stars="+stars+" before="+beforeDefName);if(implantDefName=="BionicArm"&&beforeDefName==implantDefName)sawDrag=true;}
  static void Row(Rect rowRect,object row)
  {
   if(stage!=1||Event.current.type!=EventType.Repaint||(string)Read(row,"DefName")!="BionicArm")return;
   Vector2 p=GUIUtility.GUIToScreenPoint(new Vector2(rowRect.x+70f,rowRect.y+5f));p+=GUIUtility.GUIToScreenPoint(Vector2.zero)*(Prefs.UIScale-1f);
   File.WriteAllText(Path.Combine(Root,"ranking-point.txt"),Math.Round(p.x).ToString(CultureInfo.InvariantCulture)+","+Math.Round(p.y).ToString(CultureInfo.InvariantCulture));Mark("ready");
  }
  static void Update()
  {
   if(stage==99||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.AnyEventNowOrWaiting)return;
   try{
    if(stage==0){
     Find.TickManager.Pause();AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);foreach(var p in new List<Window>(Find.WindowStack.Windows))if(!(p is MainTabWindow))p.Close(false);
     var store=ImplannerStore.Current;model=new PlannerModel();store.Model=model;model.SetAutomationPaused(true);var plan=model.CreatePlan("Three implant ranking",store.TakePlanId);foreach(string def in new[]{"BionicArm","BionicLeg","BionicSpine"})model.SetImplantSlot(plan.Id,def,0,true);model.ApplyTierOrder(3,new[]{"BionicArm","BionicLeg","BionicSpine"});store.Bump(PlannerChange.All);
     ImplannerMod.Settings.helpPlanTiersFolded=true;window=new Dialog_Implanner();var tab=AccessTools.Field(typeof(Dialog_Implanner),"curTab");tab.SetValue(null,Enum.Parse(tab.FieldType,"Plans"));var state=AccessTools.Field(typeof(Dialog_Implanner),"plans").GetValue(window);AccessTools.Field(state.GetType(),"SelectedPlanId").SetValue(state,plan.Id);Find.WindowStack.Add(window);stage=1;Note("Initial order BionicArm, BionicLeg, BionicSpine; UI scale="+Prefs.UIScale+"; physical window="+Screen.width+"x"+Screen.height);
    }else if(stage==1&&sawDrag){
     int arm=model.ImplantOrderOf("BionicArm"),leg=model.ImplantOrderOf("BionicLeg"),spine=model.ImplantOrderOf("BionicSpine");Note("After actual mouse drag: BionicArm order="+arm+", BionicLeg="+leg+", BionicSpine="+spine);
     if(!(arm<leg&&leg<spine))throw new Exception("Same-row drag changed ranking order");Note("IMP007 PASS: actual horizontal self-anchor drag preserves BionicArm, BionicLeg, BionicSpine");stage=99;Mark("SUCCESS");patches.UnpatchAll(patches.Id);
    }
   }catch(Exception ex){stage=99;Note("FAILED "+ex);Mark("FAILED "+ex);patches.UnpatchAll(patches.Id);}
  }
 }
}
