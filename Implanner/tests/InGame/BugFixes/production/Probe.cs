using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using Implanner.UI;
using RimWorld;
using Verse;
using Verse.AI;

namespace ProductionReview {
 [StaticConstructorOnStartup]
 public static class Probe {
  static Zone_Stockpile fixtureStockpile; static bool sawFrame; static Verse.AI.Job issuedJob; static int noJobReports; static bool done; static int craftStage, craftStart; static Pawn worker; static Building_WorkTable vat,grinder; static ThingDef finalProduct;
  static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  static readonly Assembly Mod=typeof(ImplannerStore).Assembly;
  static ImplannerStore store; static Map map; static Building_WorkTable first,second; static RecipeDef recipe;
  static Probe(){ new Harmony("implanner.review.production").Patch(AccessTools.Method(typeof(Root_Play),"Update"),postfix:new HarmonyMethod(typeof(Probe),nameof(Update))); }
  static object Call(Type type,object target,string method,params object[] args)=>type.GetMethods(All).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(target,args);
  static object Read(object o,string n)=>AccessTools.Field(o.GetType(),n)?.GetValue(o)??AccessTools.Property(o.GetType(),n).GetValue(o);
  static void Report(string s){Log.Message("[ProductionReview] "+s); File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"production-review.txt"),s+Environment.NewLine);}
  static void Assert(bool ok,string s){if(!ok)throw new Exception(s);Report("CONFIRMED "+s);}
  static void Dispatch(){ var idx=Call(Mod.GetType("Implanner.ColonyIndex"),null,"Build",store.Model); var pass=Activator.CreateInstance(Mod.GetType("Implanner.ReconcilePass"),All,null,new object[]{store.Model,idx,true},null);Call(Mod.GetType("Implanner.PlannerProduction"),null,"Reconcile",store,pass); }
  static Bill_Production[] Owned()=>map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().SelectMany(b=>b.BillStack.Bills).OfType<Bill_Production>().Where(b=>store.Model.OwnedProductionBills.ContainsKey(b.GetUniqueLoadID())).ToArray();
  static void Clear(){foreach(var b in Owned()){b.billStack.Delete(b);store.Model.RemoveOwnedProductionBill(b.GetUniqueLoadID());}}
  static UnfinishedThing RestoreUnfinished(Bill_Production bill,Pawn creator){
   // Restore the exact state produced by Toils_Recipe.MakeUnfinishedThingIfNeeded.
   // This verifies dispatch after interrupted work, not a claim that a job ran.
   var ingredients=new System.Collections.Generic.List<Thing>();
   foreach(var ingredient in bill.recipe.ingredients){var t=ThingMaker.MakeThing(ingredient.FixedIngredient);t.stackCount=ingredient.CountRequiredOfFor(t.def,bill.recipe,bill);ingredients.Add(t);}
   var dominant=ingredients[0];var uft=(UnfinishedThing)ThingMaker.MakeThing(bill.recipe.unfinishedThingDef,bill.recipe.unfinishedThingDef.MadeFromStuff?dominant.def:null);
   uft.Creator=creator;uft.BoundBill=(Bill_ProductionWithUft)bill;uft.ingredients=ingredients;uft.workLeft=bill.GetWorkAmount(uft);uft.TryGetComp<CompColorable>()?.SetColor(dominant.DrawColor);GenSpawn.Spawn(uft,((Building_WorkTable)bill.billStack.billGiver).Position,map);return uft;
  }
  static IntVec3 FreeCell(){return map.AllCells.First(c=>c.x>8&&c.z>8&&c.x<map.Size.x-8&&c.z<map.Size.z-8&&c.Standable(map)&&!c.Fogged(map)&&map.mapPawns.FreeColonistsSpawned[0].CanReach(c,PathEndMode.OnCell,Danger.Some)&&!GenAdj.OccupiedRect(c,Rot4.North,new IntVec2(5,5)).Cells.Any(x=>!x.Standable(map)||x.GetEdifice(map)!=null||x.GetThingList(map).Any()));}
  static Building_WorkTable Bench(){var b=(Building_WorkTable)ThingMaker.MakeThing(ThingDef.Named("FabricationBench"));b.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(b,FreeCell(),map);return b;}
  static void Update(){
   if((done&&craftStage==0)||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.AnyEventNowOrWaiting)return;
   if(craftStage>0){Progress();return;}
   done=true;
   try{
    Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; map=Find.CurrentMap;store=ImplannerStore.Current; store.Model=new PlannerModel();
    if(File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath,"production-quality-mode.txt"))){MixedQuality();return;}
    foreach(var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().ToArray())b.DeSpawn();
    foreach(var t in map.listerThings.ThingsOfDef(ThingDef.Named("BionicArm")).ToArray())t.Destroy();
    first=Bench();second=Bench();first.GetComp<CompPowerTrader>().PowerOn=true;second.GetComp<CompPowerTrader>().PowerOn=true;
    recipe=DefDatabase<RecipeDef>.GetNamed("Make_BionicArm");foreach(var r in recipe.researchPrerequisites??new System.Collections.Generic.List<ResearchProjectDef>())Find.ResearchManager.FinishProject(r,false);
    if(recipe.researchPrerequisite!=null)Find.ResearchManager.FinishProject(recipe.researchPrerequisite,false);
    var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);fixtureStockpile=zone;zone.GetStoreSettings().filter.SetAllowAll(null);
    foreach(var ing in recipe.ingredients){if(!ing.IsFixedIngredient)continue;var d=ing.FixedIngredient;PlannerCommands.SetResourceReserve(d.defName,0);int n=ing.CountRequiredOfFor(d,recipe)*10;while(n>0){var c=FreeCell();zone.AddCell(c);var t=ThingMaker.MakeThing(d);t.stackCount=Math.Min(n,d.stackLimit);GenSpawn.Spawn(t,c,map);n-=t.stackCount;}}
    map.resourceCounter.UpdateResourceCounts();
    var pawn=map.mapPawns.FreeColonistsSpawned.First(p=>p.RaceProps.Humanlike&&p.health.hediffSet.GetNotMissingParts().Any(x=>x.def==BodyPartDefOf.Arm&&!p.health.hediffSet.HasDirectlyAddedPartFor(x)));
    PlannerCommands.CreatePlan("Production review",0);var plan=store.Model.Plans.Single();PlannerCommands.SetImplantSlot(plan.Id,"BionicArm",0,true);PlannerCommands.AssignPlan(pawn.thingIDNumber,plan.Id);PlannerCommands.SetProductionConcurrency(1);PlannerCommands.SetOnlyIdleBenches(true);PlannerCommands.SetProductionSkill(4);Dispatch();
    Assert(Owned().Length>0,"real missing-arm plan queues production bill using actual game recipe and stock");
    var bill=Owned().First();Assert(bill.allowedSkillRange.min==4,"new queued bill begins at requested skill4");PlannerCommands.SetProductionSkill(15);Dispatch();Assert(store.Model.ProductionSkill==15&&Owned().Contains(bill)&&bill.allowedSkillRange.min==15,"IMP004: raising production skill updates queued bill to15");
    PlannerCommands.SetProductionSkill(0);Dispatch();Assert(bill.allowedSkillRange.min==0,"IMP004: lowering production skill updates queued bill to0");
    PlannerCommands.SetOnlyDesignatedBenches(true);Dispatch();Assert(!Owned().Contains(bill)&&Owned().Length==0,"IMP005: only-designated option retracts ordinary unstarted bills");
    PlannerCommands.SetOnlyDesignatedBenches(false);PlannerCommands.SetOnlyIdleBenches(false);Dispatch();bill=Owned().First();var manual=(Bill_Production)recipe.MakeNewBill();manual.repeatMode=BillRepeatModeDefOf.RepeatCount;manual.repeatCount=20;bill.billStack.AddBill(manual);Assert(manual.ShouldDoNow(),"manual bill currently wants work");PlannerCommands.SetOnlyIdleBenches(true);Dispatch();Assert(Owned().Contains(bill),"OnlyIdleBenches retains not-started bill after ordinary bench becomes busy");bill.billStack.Delete(manual);
    Clear();PlannerCommands.SetOnlyIdleBenches(false);first.GetComp<CompPowerTrader>().PowerOn=false;second.GetComp<CompPowerTrader>().PowerOn=true;Assert(!first.UsableForBillsAfterFueling()&&second.UsableForBillsAfterFueling(),"older fabrication bench is unusable without power, newer bench usable");Dispatch();var stuck=Owned().First();Assert(ReferenceEquals(stuck.billStack.billGiver,second),"IMP006: dispatcher selects powered bench over older unpowered one");for(int i=0;i<4;i++)Dispatch();Assert(Owned().All(b=>ReferenceEquals(b.billStack.billGiver,second))&&first.BillStack.Count==0,"IMP006: repeated boundaries preserve usable powered route");
    Clear();first.GetComp<CompPowerTrader>().PowerOn=true;PlannerCommands.SetProductionSkill(15);Dispatch();var high=Owned().First();Assert(high.allowedSkillRange.min==15,"fresh bill captures high skill15");PlannerCommands.SetProductionSkill(8);Dispatch();Assert(high.allowedSkillRange.min==8,"IMP004: lowering required crafting updates queued bill to8");
    pawn.skills.GetSkill(SkillDefOf.Crafting).Level=12;Assert(high.PawnAllowedToStartAnew(pawn),"IMP004: native eligibility accepts crafting12 pawn after minimum lowered to8");
    Clear();PlannerCommands.SetImplantSlot(plan.Id,"BionicArm",1,true);PlannerCommands.SetProductionConcurrency(2);Dispatch();Assert(Owned().Select(b=>b.billStack.billGiver).Distinct().Count()==2,"two real arm goals queue at two benches with cap2");PlannerCommands.SetProductionConcurrency(1);Dispatch();Assert(Owned().Select(b=>b.billStack.billGiver).Distinct().Count()==1,"IMP009: lowering cap to1 retains queued work on only one bench");
    Clear();var extraPatient=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(extraPatient,FreeCell(),map);PlannerCommands.AssignPlan(extraPatient.thingIDNumber,plan.Id);PlannerCommands.SetProductionConcurrency(2);Dispatch();
    Assert(Owned().Length==4&&Owned().GroupBy(b=>b.billStack.billGiver).All(g=>g.Count()==2),"two benches each hold a craft and queued successor");
    var started=Owned().GroupBy(b=>b.billStack.billGiver).Select(g=>g.First()).ToArray();var unfinished=started.Select(b=>RestoreUnfinished(b,pawn)).ToArray();PlannerCommands.SetProductionSkill(15);PlannerCommands.SetProductionConcurrency(1);Dispatch();
    Assert(started.All(b=>Owned().Contains(b)&&b.allowedSkillRange.min==8&&b.PawnAllowedToStartAnew(pawn)),"IMP004 review: interrupted work retains its crafting12 creator after minimum raised to15");
    for(int n=0;n<3;n++){Assert(Owned().Length==2&&Owned().All(b=>started.Contains(b)),"IMP009 review: over-cap started work retains no queued successors");Dispatch();}
    unfinished[0].Destroy();started[0].billStack.Delete(started[0]);Dispatch();Assert(Owned().Select(b=>b.billStack.billGiver).Distinct().Count()==1,"IMP009 review: cancelling one unfinished craft lets production resume on only one bench");
    unfinished[1].Destroy();Clear();PlannerCommands.AssignPlan(extraPatient.thingIDNumber,0);
    var hybrid=DefDatabase<RecipeDef>.GetNamedSilentFail("GR_CraftBearClaws");
    if(hybrid!=null){
     Clear();store.Model=new PlannerModel();foreach(var ing in recipe.ingredients)if(ing.IsFixedIngredient)PlannerCommands.SetResourceReserve(ing.FixedIngredient.defName,0);PlannerCommands.CreatePlan("Genoframe review",0);var hp=store.Model.Plans.Single();PlannerCommands.SetImplantSlot(hp.Id,"GR_BearClaws",0,true);PlannerCommands.AssignPlan(pawn.thingIDNumber,hp.Id);PlannerCommands.SetProductionConcurrency(1);PlannerCommands.SetProductionSkill(0);
     vat=(Building_WorkTable)ThingMaker.MakeThing(ThingDef.Named("GR_TissueGrowingVat"));vat.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(vat,FreeCell(),map);vat.GetComp<CompPowerTrader>().PowerOn=true;
     if(hybrid.researchPrerequisite!=null)Find.ResearchManager.FinishProject(hybrid.researchPrerequisite,false);
     foreach(var ing in hybrid.ingredients){if(!ing.IsFixedIngredient){Report("SHIPPED MULTI-DEF INGREDIENT "+ing.Summary+" allowed="+ing.filter.AllowedDefCount);continue;}var d=ing.FixedIngredient;PlannerCommands.SetResourceReserve(d.defName,0);var c=FreeCell();zone.AddCell(c);var t=ThingMaker.MakeThing(d);t.stackCount=ing.CountRequiredOfFor(d,hybrid)*5;GenSpawn.Spawn(t,c,map);}
     map.resourceCounter.UpdateResourceCounts();Assert(!hybrid.ingredients.Where(i=>!i.IsFixedIngredient).SelectMany(i=>i.filter.AllowedThingDefs).Any(d=>map.listerThings.ThingsOfDef(d).Count>0),"actual VGE implant has zero genoframes of every quality");
     Dispatch();Assert(!Owned().Any(b=>b.recipe==hybrid),"IMP010: no uncraftable hybrid bill is queued without a required genoframe");
     grinder=(Building_WorkTable)ThingMaker.MakeThing(ThingDef.Named("GR_TissueGrinder"));grinder.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(grinder,FreeCell(),map);grinder.GetComp<CompPowerTrader>().PowerOn=true;vat.GetComp<CompPowerTrader>().PowerOn=true;
     var poor=DefDatabase<RecipeDef>.GetNamed("GR_GenoframePoorRecipe");Assert(poor.AvailableNow&&poor.AllRecipeUsers.Contains(grinder.def)&&grinder.UsableForBillsAfterFueling(),"real VGE genoframe production recipe is available and supported by usable tissue grinder");Assert((bool)Call(Mod.GetType("Implanner.PlannerProduction"),null,"IsManufactured",poor.ProducedThingDef),"real VGE genoframes are manufactured intermediaries");
     PlannerCommands.CreatePlan("Later bionic",0);var lp=store.Model.Plans.Last();var later=map.mapPawns.FreeColonistsSpawned.First(p=>p!=pawn&&p.RaceProps.Humanlike&&p.health.hediffSet.GetNotMissingParts().Any(x=>x.def==BodyPartDefOf.Arm&&!p.health.hediffSet.HasDirectlyAddedPartFor(x)));PlannerCommands.SetImplantSlot(lp.Id,"BionicArm",0,true);PlannerCommands.AssignPlan(later.thingIDNumber,lp.Id);PlannerCommands.SetPawnPriority(pawn.thingIDNumber,0);PlannerCommands.SetPawnPriority(later.thingIDNumber,4);PlannerCommands.SetIteration((int)IterationStrategy.Colonist);PlannerCommands.MoveImplantRank("GR_BearClaws",5,"");PlannerCommands.MoveImplantRank("BionicArm",3,"");for(int r=0;r<4;r++)Dispatch();Assert(!Owned().Any(b=>b.recipe==hybrid)&&Owned().Any(b=>b.recipe==recipe),"IMP010: missing-frame demand leaves cap1 available for affordable bionic arm");
     Clear();PlannerCommands.AssignPlan(later.thingIDNumber,0);
     // Real VGE bootstrap: a fresh animal corpse is uncounted by ResourceCounter.
     // Human corpses remain disallowed by the recipe's default ingredient filter.
     var animal=PawnGenerator.GeneratePawn(PawnKindDef.Named("Muffalo"));GenSpawn.Spawn(animal,FreeCell(),map);animal.Kill(null);animal.Corpse.SetForbidden(false,false);
     for(int n=0;n<3;n++){var extra=PawnGenerator.GeneratePawn(PawnKindDef.Named("Muffalo"));GenSpawn.Spawn(extra,FreeCell(),map);extra.Kill(null);extra.Corpse.SetForbidden(false,false);}
     PlannerCommands.SetPlanMinQuality(hp.Id,(int)QualityCategory.Normal);Report("Native control supplies four fresh corpses: the actual VGE Normal frame requires four Awful frames via two Poor frames");
     var human=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(human,FreeCell(),map);human.Kill(null);human.Corpse.SetForbidden(false,false);
     var awful=DefDatabase<RecipeDef>.GetNamed("GR_GenoframeAwfulRecipe");foreach(var frameRecipe in DefDatabase<RecipeDef>.AllDefsListForReading.Where(r=>r.defName.StartsWith("GR_Genoframe")))frameRecipe.workAmount=60;hybrid.workAmount=60;
     Dispatch();var frame=Owned().First(b=>b.recipe==awful);
     Assert(frame.IsFixedOrAllowedIngredient(animal.Corpse)&&!frame.IsFixedOrAllowedIngredient(human.Corpse),"IMP010: queued real genoframe accepts fresh animal corpse and preserves human-corpse exclusion");
     do{worker=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);}while(worker.skills.GetSkill(SkillDefOf.Intellectual).TotallyDisabled||worker.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled);
     GenSpawn.Spawn(worker,grinder.InteractionCell,map);worker.playerSettings.AreaRestrictionInPawnCurrentMap=null;worker.jobs.StopAll();worker.drafter.Drafted=false;worker.skills.GetSkill(SkillDefOf.Intellectual).Level=20;worker.skills.GetSkill(SkillDefOf.Crafting).Level=20;
     finalProduct=hybrid.ProducedThingDef;craftStage=1;craftStart=Find.TickManager.TicksGame;Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;Report("CRAFTING_NATIVE");return;

    }
    Report("SUCCESS");
   }catch(Exception e){Report("FAILED "+e);}
  }
  static void Progress(){
   try{
    AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);foreach(var popup in Find.WindowStack.Windows.ToArray())if(!(popup is MainTabWindow))popup.Close(false);
    vat.GetComp<CompPowerTrader>().PowerOn=true;grinder.GetComp<CompPowerTrader>().PowerOn=true;
    if(map.listerThings.ThingsOfDef(finalProduct).Count>0 || worker.carryTracker.CarriedThing?.def==finalProduct){Assert(true,"IMP010: native game jobs consumed frame prerequisite and completed actual VGE implant product");Find.TickManager.Pause();craftStage=0;Report("SUCCESS");return;}
    if(Find.TickManager.TicksGame-craftStart>60000)throw new Exception("Native VGE chain timed out: job="+worker.CurJob+" bills="+string.Join(",",Owned().Select(b=>b.recipe.defName))+" frames="+string.Join(",",map.listerThings.AllThings.Where(t=>t.def.defName.StartsWith("GR_Genoframe")).Select(t=>t.Label+"/counted="+map.resourceCounter.GetCount(t.def))));
    if(worker.CurJob?.bill!=null || (issuedJob!=null&&worker.CurJob==issuedJob))return;
    foreach(var item in map.listerThings.AllThings.Where(t=>t.def.defName.StartsWith("GR_Genoframe")&&t.def.category==ThingCategory.Item&&!t.def.defName.Contains("Unfinished")).ToArray()){
     if(!sawFrame){sawFrame=true;Report("Native job produced "+item.Label+" #"+item.thingIDNumber);}
     if(item.Position.GetZone(map)==null&&item.Position.Standable(map))fixtureStockpile.AddCell(item.Position);
    }
    map.resourceCounter.UpdateResourceCounts();Dispatch();var bill=Owned().FirstOrDefault();if(bill==null)return;
    var bench=(Building_WorkTable)bill.billStack.billGiver;worker.jobs.StopAll();worker.Position=bench.InteractionCell;worker.Notify_Teleported(true,false);
    // Put fixture materials in the bench's own reachable room; the saved
    // orbital colony contains isolated floor pockets outside its hull.
    foreach(var thing in map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item&&bill.IsFixedOrAllowedIngredient(t)).ToArray())
     if(thing.Position.DistanceToSquared(bench.InteractionCell)>64||!worker.CanReach(thing,PathEndMode.ClosestTouch,Danger.Some)) {thing.DeSpawn();GenPlace.TryPlaceThing(thing,bench.InteractionCell,map,ThingPlaceMode.Near);thing.SetForbidden(false,false);}
    foreach(var need in worker.needs.AllNeeds)need.CurLevelPercentage=1;
    foreach(var def in DefDatabase<WorkGiverDef>.AllDefsListForReading){if(!(def.Worker is WorkGiver_DoBill giver)||def.billGiversAllHumanlikes)continue;bill.nextTickToSearchForIngredients=0;var job=giver.JobOnThing(worker,bench,true);if(job==null)continue;issuedJob=job;worker.jobs.TryTakeOrderedJob(job);Report("Native worker starts "+bill.recipe.defName+" job="+job);return;}
    // Use the same native ingredient matcher and job factory when the
    // saved orbital map's region walk cannot discover these fixture stocks.
    // The real DoBill driver still hauls, works, consumes and produces.
    var reachable=map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item&&!t.IsForbidden(worker)&&bill.IsFixedOrAllowedIngredient(t)&&worker.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.Deadly)).ToList();
    var picked=new System.Collections.Generic.List<ThingCount>();var absent=new System.Collections.Generic.List<IngredientCount>();
    if((bool)Call(typeof(WorkGiver_DoBill),null,"TryFindBestBillIngredientsInSet",reachable,bill,picked,bench.Position,false,absent)){
     var native=WorkGiver_DoBill.TryStartNewDoBillJob(worker,bill,bench,picked,out var haul);if(native!=null){issuedJob=native;worker.jobs.TryTakeOrderedJob(native);Report("Native ingredient matcher + game job factory starts "+bill.recipe.defName+" job="+native);return;}}
    if(noJobReports++<2){var candidates=map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item&&bill.IsFixedOrAllowedIngredient(t)).ToList();var chosen=new System.Collections.Generic.List<ThingCount>();var missing=new System.Collections.Generic.List<IngredientCount>();bool fits=(bool)Call(typeof(WorkGiver_DoBill),null,"TryFindBestBillIngredientsInSet",candidates,bill,chosen,bench.Position,false,missing);Report("Native job unavailable: allowed="+bill.PawnAllowedToStartAnew(worker)+" usable="+bench.UsableForBillsAfterFueling()+" reserve="+worker.CanReserve(bench)+" fits="+fits+" missing="+string.Join(",",missing.Select(i=>i.Summary))+" cells="+worker.Position+"/"+bench.Position+" chosen="+string.Join(",",chosen.Select(c=>c.Thing.Label+"@"+c.Thing.Position)));}
   }catch(Exception e){Find.TickManager.Pause();craftStage=0;Report("FAILED "+e);}
  }
  static void MixedQuality(){
   AccessTools.Field(typeof(LudeonTK.EditWindow_Log),"canAutoOpen").SetValue(null,false);foreach(var popup in Find.WindowStack.Windows.ToArray())if(!(popup is MainTabWindow))popup.Close(false);
   var people=map.mapPawns.FreeColonistsSpawned.Where(p=>p.RaceProps.Humanlike&&p.health.hediffSet.GetNotMissingParts().Any(x=>x.def==BodyPartDefOf.Hand&&!p.health.hediffSet.HasDirectlyAddedPartFor(x))).Take(2).ToArray();Assert(people.Length==2,"two real colonists with natural hands");
   PlannerCommands.CreatePlan("Normal bear claws",0);var normal=store.Model.Plans.Last();PlannerCommands.SetImplantSlot(normal.Id,"GR_BearClaws",0,true);PlannerCommands.SetPlanMinQuality(normal.Id,(int)QualityCategory.Normal);PlannerCommands.AssignPlan(people[0].thingIDNumber,normal.Id);
   PlannerCommands.CreatePlan("Excellent bear claws",0);var excellent=store.Model.Plans.Last();PlannerCommands.SetImplantSlot(excellent.Id,"GR_BearClaws",0,true);PlannerCommands.SetPlanMinQuality(excellent.Id,(int)QualityCategory.Excellent);PlannerCommands.AssignPlan(people[1].thingIDNumber,excellent.Id);
   var item=ThingDef.Named("GR_BearClaws");foreach(var old in map.listerThings.ThingsOfDef(item).ToArray())old.Destroy();for(int i=0;i<2;i++){var t=ThingMaker.MakeThing(item);t.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);GenSpawn.Spawn(t,FreeCell(),map);}
   Dispatch();var idx=Call(Mod.GetType("Implanner.ColonyIndex"),null,"Build",store.Model);var pass=Activator.CreateInstance(Mod.GetType("Implanner.ReconcilePass"),All,null,new object[]{store.Model,idx,true},null);var evaluation=Call(pass.GetType(),pass,"Evaluate",people[1].thingIDNumber);var missing=(IList)Read(evaluation,"Missing");Assert(missing.Count==1,"Excellent plan still has its actual claw slot missing");Assert(map.listerThings.ThingsOfDef(item).Count==2&&map.listerThings.ThingsOfDef(item).All(t=>t.TryGetComp<CompQuality>().Quality==QualityCategory.Normal),"only two Normal claw items exist and zero Excellent items");
   var window=new Dialog_Implanner();Find.WindowStack.Add(window);var ov=Read(window,"overview");AccessTools.Field(ov.GetType(),"SelectedPawnId").SetValue(ov,people[1].thingIDNumber);var snapshot=Call(ov.GetType(),ov,"Current",store);var data=Read(snapshot,"Data");Report("QUALITY UI "+Read(data,"ProductionTitleText")+" | "+Read(data,"StockQueuedText")+" | "+Read(data,"ProductionSubText"));Assert((int)Read(data,"ProductionPercent")==50,"IMP012: overview reports50percent for Normal and Excellent goals with only Normal stock");Report("QUALITY_READY");
  }
 }
}
