using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Implanner;
using Implanner.Core;
using RimWorks.Pickle;
using RimWorld;
using Verse;
using Verse.AI;

namespace ImplannerProductionSteps
{
 [PickleSteps]
 public sealed class ProductionSteps
 {
  public sealed class Scene
  {
   public Map Map;
   public Pawn Crafter;
   public Pawn Patient;
   public Building_WorkTable Bench;
   public Thing FreeArm;
   public readonly StringBuilder Log = new StringBuilder();
   public bool SawCraft;
   public bool DecisionVerified;
   public bool MustBootstrap;
   public bool ProtectedArm;
   public string ExpectedRecipe;
   public readonly HashSet<string> Started = new HashSet<string>();
  }
  [Given("EPOE crafting mode is {int}")]
  public void RequireMode(PickleContext ctx, int expected)
  {
   Type settings=GenTypes.GetTypeInAnyAssembly("XmlExtensions.SettingsManager");
   string actual=(string)settings.GetMethod("GetSetting").Invoke(null,new object[]{"vat.epoeforked","CraftingMethod"});
   ctx.Require(actual==expected.ToString(),"loaded XML Extensions crafting mode="+actual+", expected="+expected);
  }
  [When("the player reserves the base arm after the assembly bill queues",TimeoutSeconds=10f)]
  public async Task RaiseHoldback(PickleContext ctx)
  {
   Scene s=ctx.Get<Scene>();
   Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
   await ctx.WaitUntil(()=>HasAssembly(s),8f);
   Find.TickManager.Pause();
   Bill assembly=null;
   foreach(Bill bill in s.Bench.BillStack.Bills) if(bill.recipe.defName=="CreateAdvancedBionicArm") assembly=bill;
   ctx.Require(assembly!=null,"no queued assembly bill");
   PlannerCommands.SetImplantReserve("BionicArm",1);
   Find.TickManager.DoSingleTick();
   ctx.Assert(ImplannerStore.Current.Model.IsProductionIngredientProtected(s.FreeArm.thingIDNumber),"new holdback did not protect the item on the next simulated tick");
   ctx.Assert(!assembly.IsFixedOrAllowedIngredient(s.FreeArm),"the queued assembly bill still accepts the newly protected arm");
   s.ProtectedArm=true;
   s.Log.AppendLine("raised holdback: next-tick ingredient rejection passed for #"+s.FreeArm.thingIDNumber);
  }
  private static bool HasAssembly(Scene s)
  {
   foreach(Bill bill in s.Bench.BillStack.Bills) if(bill.recipe.defName=="CreateAdvancedBionicArm") return true;
   return false;
  }
  [Given("an EPOE advanced arm production scene with {word} free base arm")]
  public void Setup(PickleContext ctx, string availability)
  {
   Find.TickManager.Pause();
   var s = new Scene { Map = Find.CurrentMap };
   s.MustBootstrap=availability=="no" && !HasTableUser(DefDatabase<RecipeDef>.GetNamed("Make_AdvancedBionicArm"));
   ctx.Set(s);
   var pawns = new List<Pawn>(s.Map.mapPawns.FreeColonistsSpawned);
   pawns.Sort((a,b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
   foreach (Pawn p in pawns)
   {
    if (p.workSettings != null)
     foreach (WorkTypeDef work in DefDatabase<WorkTypeDef>.AllDefsListForReading)
      if (!p.WorkTypeIsDisabled(work)) p.workSettings.SetPriority(work, 0);
    if (p.Downed || !p.DevelopmentalStage.Adult()) continue;
    if (s.Crafter == null && !p.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled) s.Crafter=p;
    else if (s.Patient == null) s.Patient=p;
   }
   ctx.Require(s.Crafter != null && s.Patient != null, "need two adult colonists");
   s.Patient.health.RestorePart(s.Patient.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Shoulder)[0]);
   s.Crafter.skills.GetSkill(SkillDefOf.Crafting).Level=20;
   // Ordered WorkGiver jobs are the only crafting driver in this fixture.
   s.Crafter.jobs.StopAll();
   foreach (Need n in s.Crafter.needs.AllNeeds) n.CurLevelPercentage=1f;
   foreach (ThingDef def in new[] { ThingDef.Named("BionicArm"), ThingDef.Named("AdvancedBionicArm") })
    foreach (Thing thing in new List<Thing>(s.Map.listerThings.ThingsOfDef(def))) thing.Destroy();
   foreach (ResearchProjectDef research in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
    Find.ResearchManager.FinishProject(research, false, null, false);
   IntVec3 cell=FindBenchCell(s);
   s.Bench=(Building_WorkTable)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("TableBionics")),cell,s.Map,Rot4.North);
   s.Bench.SetFaction(Faction.OfPlayer);
   s.Crafter.Position=s.Bench.InteractionCell;
   s.Crafter.Notify_Teleported(true,false);
   foreach (string name in new[] { "Steel", "Plasteel", "Gold", "ComponentIndustrial", "ComponentSpacer" })
   {
    Thing t=ThingMaker.MakeThing(ThingDef.Named(name)); t.stackCount=500;
    GenPlace.TryPlaceThing(t,s.Bench.InteractionCell,s.Map,ThingPlaceMode.Near);
    t.SetForbidden(false,false);
   }
   if (availability == "one" || availability == "reserved")
   {
    s.FreeArm=ThingMaker.MakeThing(ThingDef.Named("BionicArm"));
    GenPlace.TryPlaceThing(s.FreeArm,s.Bench.InteractionCell,s.Map,ThingPlaceMode.Near);
   }
   var store=ImplannerStore.Current;
   var model=new PlannerModel(); store.Model=model;
   model.SetAutomationPaused(false); model.SetAutoProduction(true);
   model.SetProductionConcurrency(1); model.SetProductionSkill(0); model.SetOnlyIdleBenches(false);
   model.SetAllowIntermediaries(true); model.SetOnlyDesignatedBenches(true);
   model.DesignateBench(s.Bench.thingIDNumber,Array.Empty<string>());
   foreach (string name in new[] { "Steel", "Plasteel", "Gold", "ComponentIndustrial", "ComponentSpacer" }) model.SetResourceReserve(name,0);
   if (availability == "reserved") { model.SetImplantReserve("BionicArm",1); s.ProtectedArm=true; }
   var plan=model.CreatePlan("EPOE production proof",store.TakePlanId);
   model.SetImplantSlot(plan.Id,"AdvancedBionicArm",0,true);
   model.AssignPlan(s.Patient.thingIDNumber,plan.Id);
   store.Bump(PlannerChange.All);
   DebugSettings.fastCrafting=true;
   var text=new StringBuilder();
   foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
    if (recipe.ProducedThingDef == ThingDef.Named("AdvancedBionicArm"))
    {
     text.Append(recipe.defName).Append(" available=").Append(recipe.AvailableNow).Append(" users=");
     foreach (ThingDef user in recipe.AllRecipeUsers) text.Append(user.defName).Append(',');
     text.AppendLine();
    }
   Type production=typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerProduction");
   MethodInfo legacy=production.GetMethod("ProductionRecipeFor",BindingFlags.Static|BindingFlags.NonPublic);
   if (legacy != null) text.AppendLine("existence candidate="+((RecipeDef)legacy.Invoke(null,new object[]{ThingDef.Named("AdvancedBionicArm")}))?.defName);
   ctx.Attach("loaded production defs",text.ToString());
   System.IO.File.WriteAllText(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"epoe-evidence.txt"),text.ToString());
   s.Log.AppendLine(text.ToString());
  }
  [When("a crafter works Implanner production for {int} ticks",TimeoutSeconds=35f)]
  public async Task Craft(PickleContext ctx,int ticks)
  {
   Scene s=ctx.Get<Scene>(); int end=Find.TickManager.TicksGame+ticks;
   Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
   while (Find.TickManager.TicksGame<end && !s.SawCraft)
   {
    s.Bench.GetComp<CompPowerTrader>().PowerOn=true;
    if (s.Crafter.CurJob?.bill == null)
    {
     foreach (Bill bill in s.Bench.BillStack.Bills) bill.nextTickToSearchForIngredients=0;
     var giver=(WorkGiver_DoBill)DefDatabase<WorkGiverDef>.GetNamed("DoBillsBionicWorkbench").Worker;
     Job job=giver.JobOnThing(s.Crafter,s.Bench,true);
     if (job != null)
     {
      string recipe=job.bill.recipe.defName;
      if(s.Started.Add(recipe)) {
       s.Log.Append("real DoBill job ").Append(recipe).Append(" at tick ").Append(Find.TickManager.TicksGame).Append(" ingredients=");
       if(job.targetQueueB!=null) foreach(LocalTargetInfo item in job.targetQueueB) if(item.Thing!=null) s.Log.Append(item.Thing.def.defName).Append("#").Append(item.Thing.thingIDNumber).Append(" ");
       s.Log.AppendLine();
      }
      s.Crafter.jobs.TryTakeOrderedJob(job);
     }
    }
    await ctx.WaitTicks(30);
    s.SawCraft=s.Map.listerThings.ThingsOfDef(ThingDef.Named("AdvancedBionicArm")).Count>0
      || s.Crafter.carryTracker?.CarriedThing?.def==ThingDef.Named("AdvancedBionicArm");
   }
   Find.TickManager.Pause();
   s.Log.AppendLine("physical advanced output="+s.SawCraft+", free arm destroyed="+(s.FreeArm?.Destroyed.ToString()??"none"));
   Thing carried=s.Crafter.carryTracker?.CarriedThing;
   s.Log.AppendLine("map advanced="+s.Map.listerThings.ThingsOfDef(ThingDef.Named("AdvancedBionicArm")).Count+", carried="+(carried==null ? "none" : carried.def.defName+"#"+carried.thingIDNumber)+", current job="+s.Crafter.CurJob?.def.defName);
   foreach (Bill bill in s.Bench.BillStack.Bills) s.Log.AppendLine("remaining="+bill.recipe.defName+", class="+bill.GetType().Name+", repeats="+(bill is Bill_Production p ? p.repeatCount.ToString() : "n/a"));
   foreach (var pair in ImplannerStore.Current.Model.OwnedProductionBills) s.Log.AppendLine("owned="+pair.Key+":"+pair.Value);
   ctx.Attach("production consumption",s.Log.ToString());
   System.IO.File.WriteAllText(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"epoe-evidence.txt"),s.Log.ToString());
  }
  [Then("the advanced arm was crafted using an active recipe")]
  public void AssertCraft(PickleContext ctx)
  {
   Scene s=ctx.Get<Scene>();
   ctx.Assert(s.SawCraft,"no advanced bionic arm was produced; "+s.Log);
   ctx.Assert(s.Started.Count>0,"no genuine bill job ran");
   if(s.MustBootstrap) ctx.Assert(s.Started.Contains("Make_BionicArm") && s.Started.Contains("CreateAdvancedBionicArm"),"base arm and advanced upgrade jobs were not both consumed");
   if(s.FreeArm!=null) ctx.Assert(s.FreeArm.Destroyed != s.ProtectedArm, s.ProtectedArm ? "protected base arm was consumed" : "free bionic arm was not consumed by upgrade route");
  }
  [Then("the game used the {word} production route")]
  public void AssertRoute(PickleContext ctx, string expected)
  {
   Scene s=ctx.Get<Scene>();
   ctx.Assert(s.Started.Contains(expected),"expected "+expected+"; actual jobs: "+string.Join(",",s.Started));
   foreach(string name in s.Started) {
    RecipeDef recipe=DefDatabase<RecipeDef>.GetNamed(name); bool owns=false;
    foreach(ThingDef user in recipe.AllRecipeUsers) if(user==s.Bench.def) owns=true;
    ctx.Assert(recipe.AvailableNow && owns,"inactive legacy or unworkable recipe was worked: "+name);
   }
  }
  [When("production decisions refresh across bench and stock changes", TimeoutSeconds=20f)]
  public async Task DecisionCache(PickleContext ctx)
  {
   Scene s=ctx.Get<Scene>(); var store=ImplannerStore.Current; var model=store.Model;
   Type production=typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerProduction");
   var flags=BindingFlags.Static|BindingFlags.NonPublic;
   PropertyInfo identity=production.GetProperty("DecisionIdentity",flags);
   MethodInfo decision=production.GetMethod("DecisionFor",flags);
   var maps=new List<Map>{s.Map}; var item=ThingDef.Named("AdvancedBionicArm");
   model.ReleaseBench(s.Bench.thingIDNumber); store.Bump(PlannerChange.Benches);
   Find.TickManager.DoSingleTick();
   object first=decision.Invoke(null,new object[]{maps,item});
   Type choice=first.GetType(); var fields=BindingFlags.Instance|BindingFlags.NonPublic;
   ctx.Assert((bool)choice.GetField("Published",fields).GetValue(first),"no-bench decision was not published");
   ctx.Assert(choice.GetField("Recipe",fields).GetValue(first)!=null,"no bench hid an available recipe");
   ctx.Assert(choice.GetField("Blocker",fields).GetValue(first)==null,"no bench reported a material blocker");
   object firstIdentity=identity.GetValue(null);
   Type overview=typeof(ImplannerStore).Assembly.GetType("Implanner.UI.OverviewState");
   object state=Activator.CreateInstance(overview,true);
   MethodInfo current=overview.GetMethod("Current",BindingFlags.Instance|BindingFlags.NonPublic);
   try
   {
    object firstView=current.Invoke(state,new object[]{store});
    FieldInfo data=firstView.GetType().GetField("Data",fields); object firstData=data.GetValue(firstView);
    foreach(Thing t in new List<Thing>(s.Map.listerThings.ThingsOfDef(ThingDefOf.Plasteel))) t.Destroy();
    s.Map.resourceCounter.UpdateResourceCounts();
    ctx.Require(s.Map.resourceCounter.GetCount(ThingDefOf.Plasteel)==0,"fixture failed to remove counted Plasteel");
    model.DesignateBench(s.Bench.thingIDNumber,Array.Empty<string>()); store.Bump(PlannerChange.Benches);
    object postEditData=data.GetValue(current.Invoke(state,new object[]{store}));
    int postEditVersion=store.Version;
    Find.TickManager.DoSingleTick();
    ctx.Assert(store.Version==postEditVersion,"the decision publication changed an unrelated model revision");
    object second=decision.Invoke(null,new object[]{maps,item});
    ctx.Assert(choice.GetField("Blocker",fields).GetValue(second)==ThingDefOf.Plasteel,"missing Plasteel was not published as the blocker");
    object secondIdentity=identity.GetValue(null);
    ctx.Assert(!ReferenceEquals(firstIdentity,secondIdentity),"changed blocker reused the old decision snapshot");
    object secondData=data.GetValue(current.Invoke(state,new object[]{store}));
    ctx.Assert(!ReferenceEquals(postEditData,secondData),"overview data did not rebuild after the changed decision");
    int boundary=(Find.TickManager.TicksGame/1020+1)*1020;
    Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
    await ctx.WaitUntil(()=>Find.TickManager.TicksGame>boundary,12f);
    Find.TickManager.Pause();
    ctx.Assert(ReferenceEquals(secondIdentity,identity.GetValue(null)),"equal boundary refresh replaced decision identity");
    production.GetMethod("Reset",flags).Invoke(null,null);
    object reset=decision.Invoke(null,new object[]{maps,item});
    ctx.Assert(!(bool)choice.GetField("Published",fields).GetValue(reset),"Reset retained prior map decisions");
    ctx.Assert(!ReferenceEquals(secondIdentity,identity.GetValue(null)),"Reset retained prior snapshot ownership");
    s.Log.AppendLine("decision cache: no bench retains recipe; missing Plasteel changes blocker/identity; overview Data refreshes; equal boundary preserves identity; Reset removes map decisions");
    s.DecisionVerified=true;
    ctx.Attach("decision cache proof",s.Log.ToString());
    System.IO.File.WriteAllText(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"epoe-evidence.txt"),s.Log.ToString());
   }
   finally {overview.GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,null);}
  }
  [Then("the production decision cache contract holds")]
  public void CacheResult(PickleContext ctx) {ctx.Assert(ctx.Get<Scene>().DecisionVerified,"decision assertions did not finish");}
  private static bool HasTableUser(RecipeDef recipe)
  {
   foreach(ThingDef user in recipe.AllRecipeUsers) if(user.defName=="TableBionics") return true;
   return false;
  }
  private static IntVec3 FindBenchCell(Scene s)
  {
   ThingDef def=ThingDef.Named("TableBionics");
   foreach(IntVec3 cell in GenRadial.RadialCellsAround(s.Crafter.Position,35,true))
   {
    bool free=cell.InBounds(s.Map);
    foreach(IntVec3 c in GenAdj.OccupiedRect(cell,Rot4.North,def.size))
     if(!c.InBounds(s.Map)||!c.Standable(s.Map)||c.GetEdifice(s.Map)!=null||c.GetFirstItem(s.Map)!=null||!c.Roofed(s.Map)) { free=false; break; }
    IntVec3 interaction=cell+new IntVec3(0,0,-1);
    if(free && interaction.InBounds(s.Map) && interaction.Standable(s.Map) && s.Crafter.CanReach(interaction,PathEndMode.OnCell,Danger.Deadly)) return cell;
   }
   throw new Exception("no reachable roofed bench cell");
  }
 }
}
