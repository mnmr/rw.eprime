using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using QualityJobs;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobsCwcBugReview
{
    // Two same-material almost-finished fixtures are consumed by real pawn jobs:
    // the source bill itself, then the QJ-generated finisher bill. CWC's normal
    // toil/product patches choose the observed styles; none are replaced.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static int stage;
        private static DateTime started;
        private static Map map;
        private static QualityJobsStore store;
        private static RecipeDef recipe;
        private static Building_WorkTable bench;
        private static Pawn pawn;
        private static Bill_ProductionWithUft source;
        private static WorkGiver_DoBill giver;
        private static ThingStyleDef expected;
        private static ThingStyleDef baseline;
        private static int scenario;
        private static object addition;
        private static object explicitOption, basicOption, randomOption;
        private static StyleSnapshot preparedStyle;
        private static readonly HashSet<string> randomResults = new HashSet<string>();
        private static readonly HashSet<int> oldProducts = new HashSet<int>();
        private static readonly Dictionary<Pawn, int> oldPriorities = new Dictionary<Pawn, int>();
        private static WorkTypeDef workType;
        private static string Root => GenFilePaths.SaveDataFolderPath;

        static Probe()
        {
            var harmony = new Harmony("eprime.qualityjobs.cwcbugreview");
            harmony.Patch(
                AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            // Keep the real CWC random-style picker, with the same deterministic
            // random stream for each source/finisher comparison. No style or
            // product callback is replaced by the fixture.
            Type state = AccessTools.TypeByName("CraftWithColor.State");
            if (state != null)
                harmony.Patch(AccessTools.Method(state, "StyleForLast"),
                    prefix: new HarmonyMethod(typeof(Probe), nameof(SeedStyle)),
                    finalizer: new HarmonyMethod(typeof(Probe), nameof(EndStyleSeed)));
        }

        private static void SeedStyle(ThingDef def, out bool __state)
        {
            __state = stage != 99 && recipe != null && def == recipe.ProducedThingDef;
            if (__state) Rand.PushState(4100 + scenario);
        }

        private static void EndStyleSeed(bool __state) { if (__state) Rand.PopState(); }

        private static void Mark(string text)
        {
            Log.Message("[QualityJobsCwcBugReview] " + text);
            File.AppendAllText(Path.Combine(Root, "cwc-review-results.txt"), text + Environment.NewLine);
        }

        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null || (stage == 0 && LongEventHandler.AnyEventNowOrWaiting)) return;
            try
            {
                if (stage == 0)
                {
                    started = DateTime.UtcNow;
                    Setup();
                    stage = 1;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                }
                if ((DateTime.UtcNow - started).TotalSeconds > 35)
                    throw new Exception("Native craft timeout; stage=" + stage + "; job=" + pawn?.CurJob
                        + "; pawn=" + pawn?.Position + "; downed=" + pawn?.Downed
                        + "; target=" + pawn?.CurJob?.targetB.Thing?.Position
                        + "; work=" + (pawn?.CurJob?.targetB.Thing as UnfinishedThing)?.workLeft);
                Thing product = NewProduct();
                if (product == null) return;
                oldProducts.Add(product.thingIDNumber);
                if (stage == 1)
                {
                    baseline = product.GetInnerIfMinified().StyleDef;
                    Mark("source actual=" + baseline?.defName + "; scenario=" + scenario
                        + "; vanillaSourceStyle=" + source.style?.defName);
                    if (scenario == 0 && baseline != expected || scenario == 1 && baseline != null)
                        throw new Exception("CWC source control did not apply requested style.");
                    if (scenario >= 2) randomResults.Add(baseline?.defName ?? "Basic");
                    source.repeatCount = 1;
                    Configure(true);
                    UnfinishedThing uft = Uft();
                    // The shared item was prepared before either completion:
                    // random style must be selected at completion, not frozen
                    // to the source's last completed product.
                    store.RegisterPaused(uft, pawn, source, preparedStyle);
                    UftAuthor.Clear(uft);
                    WorkItemEntry entry = store.FindByUft(uft);
                    Dispatcher.TryDispatch(store, entry);
                    if (entry.finishBill == null) throw new Exception("QJ did not dispatch CWC fixture.");
                    Type state = AccessTools.TypeByName("CraftWithColor.State");
                    object addition = AccessTools.Method(state, "TryGetAddition").Invoke(null,
                        new object[] { entry.finishBill });
                    Mark("temporary vanilla style=" + entry.finishBill.style?.defName
                        + "; custom addition exists=" + (addition != null));
                    stage = 2;
                    started = DateTime.UtcNow;
                    TakeJob();
                }
                else if (stage == 2)
                {
                    ThingStyleDef actual = product.GetInnerIfMinified().StyleDef;
                    string id = scenario == 0 ? "QJ013" : scenario == 1 ? "CR2-BASIC" : "CR3-RANDOM-" + scenario;
                    Mark(id + " passed=" + (actual == baseline)
                        + "; game=" + VersionControl.CurrentVersionString
                        + "; CWC=" + AccessTools.TypeByName("CraftWithColor.State").Assembly.FullName
                        + "; source=" + baseline?.defName + "; finishing=" + actual?.defName
                        + "; ideology=" + pawn.Ideo.GetStyleFor(recipe.ProducedThingDef)?.defName
                        + "; both native jobs consumed full ingredient fixtures, debugCompleted=false.");
                    if (++scenario == 6)
                    {
                        Mark("CR3-VARIATION passed=" + (randomResults.Count > 1)
                            + "; controlled native random styles=" + string.Join(",", randomResults));
                        Done();
                    }
                    else StartSource();
                }
            }
            catch (Exception ex) { Mark("ERROR " + ex); Done(); }
        }

        private static void Setup()
        {
            map = Find.CurrentMap;
            store = QualityJobsStore.Active;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            for (int i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
                if (Find.WindowStack.Windows[i] is not MainTabWindow)
                    Find.WindowStack.TryRemove(Find.WindowStack.Windows[i], false);
            Type state = AccessTools.TypeByName("CraftWithColor.State");
            if (state == null) throw new Exception("Craft with Color not loaded.");
            recipe = DefDatabase<RecipeDef>.GetNamed("Make_SculptureSmall");
            ThingDef tableDef = DefDatabase<ThingDef>.GetNamed("TableSculpting");
            foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                if (def.Worker is WorkGiver_DoBill candidate && def.fixedBillGiverDefs != null
                    && def.fixedBillGiverDefs.Contains(tableDef))
                { giver = candidate; workType = def.workType; break; }
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                if (p.workSettings != null && !p.WorkTypeIsDisabled(workType))
                { oldPriorities[p] = p.workSettings.GetPriority(workType); p.workSettings.SetPriority(workType, 0); }
            IntVec3 position = FindSpace(tableDef.size);
            bench = (Building_WorkTable)ThingMaker.MakeThing(tableDef, ThingDefOf.WoodLog);
            bench.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bench, position, map, Rot4.North);
            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                fixedBiologicalAge: 30f, forceNoIdeo: true, forceNoBackstory: true, forbidAnyTitle: true));
            GenSpawn.Spawn(pawn, bench.InteractionCell, map);
            pawn.skills.GetSkill(recipe.workSkill).Level = 12;
            pawn.workSettings.SetPriority(workType, 1);
            if (pawn.needs.food != null) pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
            if (pawn.needs.rest != null) pawn.needs.rest.CurLevel = pawn.needs.rest.MaxLevel;
            foreach (string name in new[] { "Apparel_Vacsuit", "Apparel_VacsuitHelmet" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(def));
            }
            source = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(source);
            source.SetPawnRestriction(pawn);
            source.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            source.globalStyle = false;
            source.style = null;
            addition = AccessTools.Method(state, "GetAddition").Invoke(null, new object[] { source });
            Type addType = addition.GetType();
            foreach (object option in (IEnumerable)AccessTools.Property(addType, "Styles").GetValue(addition))
            {
                if (option.GetType().Name == "BasicStyle") basicOption = option;
                if (option.GetType().Name == "RandomStyle") randomOption = option;
                if (option.GetType().Name != "ExplicitStyle") continue;
                if (explicitOption != null) continue;
                expected = (ThingStyleDef)AccessTools.Property(option.GetType(), "Style").GetValue(option);
                if (expected == null) continue;
                explicitOption = option;
            }
            if (expected == null || basicOption == null || randomOption == null)
                throw new Exception("CWC fixture lacks a style selection mode.");
            var ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(FactionDefOf.PlayerColony,
                styles: new List<StyleCategoryDef> { expected.Category }));
            ideo.thingStyleCategories.Clear();
            ideo.thingStyleCategories.Add(new ThingStyleCategoryWithPriority(expected.Category, 100f));
            ideo.style.ResetStyleForThing(recipe.ProducedThingDef);
            pawn.ideo.SetIdeo(ideo);
            if (pawn.Ideo.GetStyleFor(recipe.ProducedThingDef) == null)
                throw new Exception("Basic-style fixture must have a styled ideology.");
            AccessTools.Field(addType, "styleActive").SetValue(addition, true);
            AccessTools.Method(addType, "UpdateBill").Invoke(addition, null);
            foreach (Thing product in map.listerThings.AllThings)
                if (product.GetInnerIfMinified().def == recipe.ProducedThingDef)
                    oldProducts.Add(product.thingIDNumber);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            StartSource();
            File.WriteAllText(Path.Combine(Root, "cwc-review-stage.txt"), "CRAFTING");
        }

        private static void StartSource()
        {
            Rand.PushState(123);
            try
            {
                AccessTools.Property(addition.GetType(), "StyleSelection").SetValue(addition,
                    scenario == 0 ? explicitOption : scenario == 1 ? basicOption : randomOption);
            }
            finally { Rand.PopState(); }
            preparedStyle = StyleSnapshot.From(source);
            // Roundtrip the detached paused-item snapshot before handing it to
            // the native finisher; the resulting product proves persisted mode.
            string snapshotPath = Path.Combine(Root, "cwc-style-snapshot.xml");
            try
            {
                Scribe.saver.InitSaving(snapshotPath, "StyleProbe");
                Scribe_Deep.Look(ref preparedStyle, "snapshot");
                Scribe.saver.FinalizeSaving();
                preparedStyle = null;
                Scribe.loader.InitLoading(snapshotPath);
                Scribe_Deep.Look(ref preparedStyle, "snapshot");
                Scribe.loader.FinalizeLoading();
            }
            finally { Scribe.ForceStop(); }
            if (preparedStyle == null) throw new Exception("Style snapshot did not survive Scribe.");
            source.repeatCount = 1;
            Configure(false);
            Uft();
            stage = 1;
            started = DateTime.UtcNow;
            TakeJob();
        }

        private static void Configure(bool managed)
        {
            string id = BillIds.IdOf(source);
            Commands.SetBillManaged(id, managed);
            Commands.SetBillAutoBest(id, false);
            Commands.SetBillMinSkill(id, 0);
            Commands.SetBillRequireInspired(id, false);
            Commands.SetBillRequireSpecialist(id, false);
            Commands.SetBillTargetQuality(id, 0);
        }

        private static UnfinishedThing Uft()
        {
            var uft = (UnfinishedThing)ThingMaker.MakeThing(recipe.unfinishedThingDef, ThingDefOf.WoodLog);
            uft.Creator = pawn;
            uft.BoundBill = source;
            Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            wood.stackCount = recipe.ingredients[0].CountRequiredOfFor(ThingDefOf.WoodLog, recipe, source);
            uft.ingredients.Add(wood);
            uft.workLeft = 0.1f;
            GenSpawn.Spawn(uft, bench.Position + new IntVec3(3, 0, 0), map);
            return uft;
        }

        private static void TakeJob()
        {
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            source.nextTickToSearchForIngredients = -1;
            Job job = giver.JobOnThing(pawn, bench, true);
            if (job == null) throw new Exception("Native CWC fixture job unavailable: " + JobFailReason.Reason);
            pawn.jobs.TryTakeOrderedJob(job);
        }

        private static Thing NewProduct()
        {
            foreach (Thing product in map.listerThings.AllThings)
                if (!oldProducts.Contains(product.thingIDNumber)
                    && product.GetInnerIfMinified().def == recipe.ProducedThingDef) return product;
            return null;
        }

        private static IntVec3 FindSpace(IntVec2 size)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, 100f, true))
            {
                bool valid = true;
                foreach (IntVec3 test in GenAdj.OccupiedRect(cell, Rot4.North, size).ExpandedBy(3))
                    if (!test.InBounds(map) || !test.Standable(map) || test.Fogged(map)
                        || test.GetEdifice(map) != null) { valid = false; break; }
                if (valid) return cell;
            }
            throw new Exception("No clear fixture space.");
        }

        private static void Done()
        {
            stage = 99;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            foreach (KeyValuePair<Pawn, int> pair in oldPriorities)
                pair.Key.workSettings.SetPriority(workType, pair.Value);
            File.WriteAllText(Path.Combine(Root, "cwc-review-stage.txt"), "DONE");
        }
    }
}
