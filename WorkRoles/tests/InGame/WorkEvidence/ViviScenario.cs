#if !BASELINE
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRolesWorkEvidenceVerification
{
    public static partial class Probe
    {
        private const string Gathering = "VV_DoBillsGathering";
        private static Pawn humanGatherer, viviGatherer;
        private static int gatheringRole, gatheringJob, gatheringQueries;
        private static ThingDef gatheringProduct;
        private static ThingWithComps gatheringTarget;
        private static int productBefore;
        private static Building_WorkTable gatheringBench;
        private static void CountGatheringQuery() => gatheringQueries++;

        private static RecommendationPlan GatheringPlan(List<Pawn> pawns)
        {
            ColonyView colony = RecsAdapter.BuildColonyView(RoleStore.Current, pawns);
            colony.Roles = colony.Roles.Where(r => r.Id == gatheringRole).ToList();
            colony.Paths.Clear(); colony.OrderTemplate = new List<int> { gatheringRole };
            return RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning);
        }

        private static void Vivi()
        {
            Map map = Find.CurrentMap;
            // CaptureColony has already recorded the untouched save. The test
            // mod set omits the old hauling mod, leaving a few mech jobs with
            // null defs. Give only those invalid jobs a cleanup-safe def before
            // stopping them, so they do not flood every simulation tick with errors.
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.CurJob != null && pawn.CurJob.def == null)
                { pawn.CurJob.def = JobDefOf.Wait; pawn.jobs.StopAll(); pawn.pather.StopDead(); }
            // Fisso-NAM is a gravship in vacuum. Keep this work proof inside a
            // pressurized, temperate room; oxygen emergencies are not capability.
            IntVec3 safeCell = map.AllCells.First(c => c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map)
                && c.GetRoom(map) != null && c.GetRoom(map).CellCount >= 30 && c.GetVacuum(map) < 0.05f
                && c.GetTemperature(map) >= 10f && c.GetTemperature(map) <= 30f
                && GenAdj.CellsAdjacent8Way(c, Rot4.North, IntVec2.One).All(n => n.InBounds(map) && n.Standable(map) && n.GetRoom(map) == c.GetRoom(map)));
            Pawn Make(PawnKindDef kind)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer,
                    forceGenerateNewPawn: true, forceNoIdeo: true, forceNoBackstory: true, fixedBiologicalAge: 30));
                foreach (Apparel item in pawn.apparel.WornApparel.ToArray()) pawn.apparel.Remove(item);
                pawn.skills.GetSkill(SkillDefOf.Plants).Level = 20;
                GenSpawn.Spawn(pawn, safeCell, map);
                pawn.drafter.Drafted = true; // Keep the waiting worker inside.
                return pawn;
            }
            humanGatherer = Make(PawnKindDefOf.Colonist);
            viviGatherer = Make(DefDatabase<PawnKindDef>.GetNamed("VV_PlayerVivi"));
            var pawns = new List<Pawn> { humanGatherer, viviGatherer };
            foreach (string research in new[] { "VV_BasicGathering", "VV_AdvancedGathering" })
                Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(research), doCompletionDialog: false);
            RoleCommands.CreateRole("Vivi gathering proof");
            gatheringRole = RoleStore.Current.roles.Single(r => r.label == "Vivi gathering proof").id;
            RoleCommands.AddEntry(gatheringRole, new JobEntry(JobEntryKind.WorkGiver, Gathering));
            RoleCommands.SetRoleColonyMinimum(gatheringRole, 1);
            new Harmony("eprime.workroles.viviverification").Patch(
                AccessTools.Method(AccessTools.TypeByName("VVRace.WorkGiver_GatheringBill"), "JobOnThing"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(CountGatheringQuery)));
            object stats = Activator.CreateInstance(typeof(RecsAdapter).Assembly.GetType("WorkRoles.UI.ColonistStatsState"), true);
            Invoke(stats, "Reset", pawns);
            Assert(Evidence(stats, humanGatherer).Blocks(Gathering), "unequipped human cannot gather any Vivi recipe");
            Assert(!Holds(GatheringPlan(new List<Pawn> { humanGatherer }), 0, gatheringRole),
                "unequipped human receives no gathering recommendation, even for required coverage");
            Assert(!Evidence(stats, viviGatherer).Blocks(Gathering) && Holds(GatheringPlan(new List<Pawn> { viviGatherer }), 0, gatheringRole),
                "Vivi without tools retains gathering recommendation");
            int tick = Find.TickManager.TicksGame;
            int skills = Revision("FactRevisions", humanGatherer);
            int work = Revision("WorkEvidenceRevisions", humanGatherer);
            var straw = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VV_StrawTool"));
            humanGatherer.apparel.Wear(straw, false);
            Refresh(stats, pawns);
            PawnWorkEvidence honey = Evidence(stats, humanGatherer);
            Assert(!honey.Blocks(Gathering) && !honey.Blocks(Gathering, "VV_GatherPollen")
                && honey.Blocks(Gathering, "VV_GatherPropolis"), "paused straw equip enables pollen but preserves plant-recipe restrictions");
            Assert(Holds(GatheringPlan(new List<Pawn> { humanGatherer }), 0, gatheringRole),
                "partially capable human receives gathering recommendation through the existing eligibility logic");
            Assert(work != Revision("WorkEvidenceRevisions", humanGatherer) && skills == Revision("FactRevisions", humanGatherer),
                "paused gear change invalidates work evidence without invalidating skills");
            work = Revision("WorkEvidenceRevisions", humanGatherer);
            humanGatherer.health.capacities.Notify_CapacityLevelsDirty(); Refresh(stats, pawns);
            Assert(ReferenceEquals(honey, Evidence(stats, humanGatherer)) && work == Revision("WorkEvidenceRevisions", humanGatherer),
                "unchanged health notification preserves evidence identity and work revision");
            var hat = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_CowboyHat"), ThingDefOf.Cloth);
            humanGatherer.apparel.Wear(hat, false); Refresh(stats, pawns);
            Assert(ReferenceEquals(honey, Evidence(stats, humanGatherer)) && work == Revision("WorkEvidenceRevisions", humanGatherer),
                "unrelated apparel preserves gathering evidence and revision");
            humanGatherer.apparel.Remove(straw); Refresh(stats, pawns);
            Assert(Evidence(stats, humanGatherer).Blocks(Gathering) && !Holds(GatheringPlan(new List<Pawn> { humanGatherer }), 0, gatheringRole),
                "paused straw removal removes gathering recommendation");
            var brush = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VV_MultiuseBrushTool"));
            humanGatherer.apparel.Wear(brush, false); Refresh(stats, pawns);
            PawnWorkEvidence plants = Evidence(stats, humanGatherer);
            Assert(!plants.Blocks(Gathering) && plants.Blocks(Gathering, "VV_GatherPollen")
                && !plants.Blocks(Gathering, "VV_GatherPropolis") && !plants.Blocks(Gathering, "VV_GatherLivingLeaf"),
                "paused brush swap enables plant recipes but leaves pollen blocked");
            humanGatherer.apparel.Remove(brush); Refresh(stats, pawns);
            Assert(Evidence(stats, humanGatherer).Blocks(Gathering), "paused brush removal restores complete gathering restriction");
            Assert(Find.TickManager.TicksGame == tick && Find.TickManager.Paused, "all cached gear transitions completed without advancing simulation");

            // Compatibility safety cannot be established from an ordinary stock
            // recommendation. Temporarily extend the real bench recipe set, then
            // use the existing lifecycle reset to observe the new definitions.
            WorkGiverDef giver = DefDatabase<WorkGiverDef>.GetNamed(Gathering);
            RecipeDef unknown = new RecipeDef { defName = "WE_UnknownGathering", label = "unknown gathering" };
            List<RecipeDef> recipes = giver.fixedBillGiverDefs[0].AllRecipes;
            recipes.Add(unknown);
            try
            {
                Call(typeof(RecsAdapter).Assembly.GetType("WorkRoles.ExternalPawnFacts"), "InvalidateAll"); Refresh(stats, pawns);
                Assert(!Evidence(stats, humanGatherer).Blocks(Gathering)
                    && Evidence(stats, humanGatherer).Blocks(Gathering, "VV_GatherPollen"),
                    "unknown recipe prevents whole-giver denial while retaining verified recipe facts");
            }
            finally { recipes.Remove(unknown); Call(typeof(RecsAdapter).Assembly.GetType("WorkRoles.ExternalPawnFacts"), "InvalidateAll"); Refresh(stats, pawns); }
            StatDef speed = DefDatabase<StatDef>.GetNamed("VV_HoneyGatherSpeed");
            List<StatPart> originalParts = speed.parts;
            speed.parts = new List<StatPart> { new UnknownGatheringStatPart() };
            try
            {
                Call(typeof(RecsAdapter).Assembly.GetType("WorkRoles.ExternalPawnFacts"), "InvalidateAll"); Refresh(stats, pawns);
                Assert(!Evidence(stats, humanGatherer).Blocks(Gathering, "VV_GatherPollen")
                    && Evidence(stats, humanGatherer).Blocks(Gathering, "VV_GatherPropolis"),
                    "unfamiliar stat rule remains unknown without discarding verified plant restrictions");
            }
            finally { speed.parts = originalParts; Call(typeof(RecsAdapter).Assembly.GetType("WorkRoles.ExternalPawnFacts"), "InvalidateAll"); Refresh(stats, pawns); }
            Assert(gatheringQueries == 0, "all capability and recommendation queries avoided Vivi job search");
            Invoke(stats, "ReleaseSnapshots");
            humanGatherer.apparel.Wear(straw, false);
            // Unrelated emergencies in the source colony must not force this
            // isolated functional run back to 1x and exhaust its 60-second cap.
            DebugViewSettings.neverForceNormalSpeed = true;
            gatheringJob = 0; StartGatheringJob(); stage = 4;
        }

        private static void StartGatheringJob()
        {
            worker = gatheringJob == 2 ? viviGatherer : humanGatherer;
            Map map = worker.Map;
            worker.drafter.Drafted = false;
            if (gatheringJob == 1)
            {
                foreach (Apparel item in worker.apparel.WornApparel.ToArray()) worker.apparel.Remove(item);
                worker.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VV_MultiuseBrushTool")), false);
            }
            string recipeName = new[] { "VV_GatherPollen", "VV_GatherPropolis", "VV_GatherHoney" }[gatheringJob];
            Assert(Holds(GatheringPlan(new List<Pawn> { worker }), 0, gatheringRole), "actual gathering worker is recommended before " + recipeName);
            RoleCommands.AssignRole(worker, gatheringRole, 0);
            if (gatheringBench == null)
            {
                IntVec3 benchCell = worker.Position;
                gatheringBench = (Building_WorkTable)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VV_GatheringSpot")), benchCell, map);
                gatheringBench.SetFaction(Faction.OfPlayer);
            }
            IntVec3 cell = gatheringBench.Position;
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed(recipeName);
            // Prefer an unmodified stock tree with high resin yield. Vivi
            // rounds fractional products stochastically; a brush can yield zero.
            ThingDef plantDef = gatheringJob == 1
                ? DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.plant != null && d.plant.IsTree)
                    .OrderByDescending(d => d.GetStatValueAbstract(DefDatabase<StatDef>.GetNamed("VV_TreeResinGatherYield"))).First()
                : DefDatabase<ThingDef>.GetNamed("Plant_Dandelion");
            IntVec3 plantCell = GenRadial.RadialCellsAround(cell, 5, true).First(c => c != cell && c != gatheringBench.InteractionCell
                && c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetPlant(map) == null && c.GetRoom(map) == cell.GetRoom(map));
            Plant plant = (Plant)GenSpawn.Spawn(ThingMaker.MakeThing(plantDef), plantCell, map); plant.Growth = 1f;
            proofBill = (Bill_Production)recipe.MakeNewBill(); proofBill.repeatMode = BillRepeatModeDefOf.RepeatCount; proofBill.repeatCount = 1;
            proofBill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            proofBill.ingredientFilter.SetDisallowAll(); proofBill.ingredientFilter.SetAllow(plantDef, true);
            gatheringBench.BillStack.AddBill(proofBill);
            gatheringProduct = recipe.products[0].thingDef;
            productBefore = map.listerThings.ThingsOfDef(gatheringProduct).Sum(t => t.stackCount);
            worker.jobs.StopAll(); worker.pather.StopDead(); worker.Position = gatheringBench.InteractionCell;
            Job job = ((WorkGiver_DoBill)DefDatabase<WorkGiverDef>.GetNamed(Gathering).Worker).JobOnThing(worker, gatheringBench, true);
            Assert(job != null, "recommended pawn receives real Vivi job " + recipeName);
            gatheringTarget = (ThingWithComps)job.targetA.Thing;
            Log.Message("[WorkEvidenceProbe] Gathering " + recipeName + " from " + job.targetA.Thing.def.defName);
            worker.jobs.TryTakeOrderedJob(job); startedTick = Find.TickManager.TicksGame;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
        }

        private static void ViviUpdate()
        {
            if (proofBill.repeatCount > 0 && worker.CurJob?.bill != proofBill)
                throw new InvalidOperationException("gathering interrupted: " + proofBill.recipe.defName + "; ticks=" + (Find.TickManager.TicksGame - startedTick)
                    + "; job=" + worker.CurJob + "; health=" + string.Join(",", worker.health.hediffSet.hediffs.Select(h => h.def.defName + ":" + h.Severity)));
            if (proofBill.repeatCount == 0)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                object repeat = gatheringTarget.AllComps.Single(c => c.GetType().FullName == "VVRace.CompRepeatGatherable");
                Assert((bool)Invoke(repeat, "IsCooldown", DefDatabase<StatDef>.GetNamed("VV_PlantGatherCooldown")),
                    "recommended pawn completed " + proofBill.recipe.defName + " and consumed the plant's gathering cycle");
                int produced = worker.Map.listerThings.ThingsOfDef(gatheringProduct).Sum(t => t.stackCount) - productBefore;
                if (gatheringJob != 1) Assert(produced > 0, "completed gathering produced " + gatheringProduct.defName);
                else Log.Message("[WorkEvidenceProbe] Completed brush gathering; stochastic propolis yield=" + produced);
                worker.drafter.Drafted = true;
                if (++gatheringJob < 3) StartGatheringJob();
                else { gatheringBench.Destroy(); stage = 99; Mark("SUCCESS"); }
            }
            else if (Find.TickManager.TicksGame - startedTick > 16000)
                throw new InvalidOperationException("gathering did not finish: " + proofBill.recipe.defName + "; job=" + worker.CurJob);
        }
    }

    public sealed class UnknownGatheringStatPart : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val) { }
        public override string ExplanationPart(StatRequest req) => null;
    }
}
#endif
