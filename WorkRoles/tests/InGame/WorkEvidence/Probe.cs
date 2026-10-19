using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRolesWorkEvidenceVerification
{
    // Test-only main-thread observer; also runs against the baseline assembly.
    [StaticConstructorOnStartup]
    public static partial class Probe
    {
        private static int stage, settle;
#if !BASELINE
        private static int startedTick;
        private static Pawn worker;
        private static Mineable target;
        private static Bill_Production proofBill;
        private static bool includeHar;
#endif
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static Probe() => new Harmony("eprime.workroles.workevidenceprobe").Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        private static void Mark(string text) => File.WriteAllText(Path.Combine(Root, "work-evidence-stage.txt"), text);
        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Log.Message("[WorkEvidenceProbe] PASS " + message);
        }
        private static object Call(Type type, string name, params object[] args) => type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(owner, args);
#if !BASELINE
        private static PawnWorkEvidence Evidence(object stats, Pawn pawn)
        {
            object snapshot = Invoke(stats, "ExternalSnapshot", pawn);
            return ((PawnView)snapshot.GetType().GetProperty("RecommendationFacts", All).GetValue(snapshot, null)).WorkEvidence;
        }
        private static int Revision(string domain, Pawn pawn)
        {
            object revisions = typeof(RecsAdapter).Assembly.GetType("WorkRoles.ExternalPawnFacts").GetField(domain, All).GetValue(null);
            return (int)Invoke(revisions, "RevisionOf", pawn);
        }
        private static void Refresh(object stats, List<Pawn> pawns) => Invoke(stats, "RefreshExternalSnapshot", pawns, false);
#endif
        private static bool Holds(RecommendationPlan plan, int pawn, int role) => Enumerable.Range(0, plan.RoleCountAt(pawn)).Any(i => plan.RoleAt(pawn, i) == role);
        private static XElement Rows(RecommendationPlan plan, List<Pawn> pawns, string name) => new XElement(name,
            pawns.Select((pawn, p) => new XElement("pawn", new XAttribute("id", pawn.ThingID), new XAttribute("name", pawn.LabelShort),
                new XAttribute("roles", string.Join(",", Enumerable.Range(0, plan.RoleCountAt(p)).Select(i => plan.RoleAt(p, i)))),
                new XAttribute("paths", string.Join(",", Enumerable.Range(0, plan.PathCountAt(p)).Select(i => plan.PathAt(p, i)))))));

        private static XElement Dump(string name, object value)
        {
            var result = new XElement(name);
            if (value == null) { result.SetAttributeValue("null", true); return result; }
            Type type = value.GetType();
            if (value is string || type.IsPrimitive || type.IsEnum || value is decimal) { result.Value = Convert.ToString(value, CultureInfo.InvariantCulture); return result; }
            if (value is IDictionary dictionary)
            { foreach (DictionaryEntry item in dictionary) result.Add(new XElement("entry", Dump("key", item.Key), Dump("value", item.Value))); return result; }
            if (value is IEnumerable sequence) { foreach (object item in sequence) result.Add(Dump("item", item)); return result; }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name)) result.Add(Dump(field.Name, field.GetValue(value)));
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
                if (property.CanRead && property.GetIndexParameters().Length == 0) result.Add(Dump(property.Name, property.GetValue(value, null)));
            foreach (string prefix in new[] { "Capability", "Aversion" })
            {
                MethodInfo at = type.GetMethod(prefix + "At"); PropertyInfo count = type.GetProperty(prefix + "Count");
                if (at != null && count != null)
                    for (int i = 0; i < (int)count.GetValue(value, null); i++) result.Add(Dump(prefix, at.Invoke(value, new object[] { i })));
            }
            return result;
        }

        private static void CaptureColony()
        {
            List<Pawn> pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList();
            RoleStore store = RoleStore.Current;
            ColonyView colony = RecsAdapter.BuildColonyView(store, pawns);
            var document = new XElement("capture", new XAttribute("game", VersionControl.CurrentVersionString), Dump("colony", colony), Dump("tuning", store.recommendationTuning), Rows(RecommendationPlan.Build(colony, store.recommendationTuning), pawns, "actual"));
            FieldInfo evidence = typeof(PawnView).GetField("WorkEvidence");
            if (evidence != null)
            {
                object empty = evidence.FieldType.GetField("Empty").GetValue(null);
                foreach (PawnView pawn in colony.Pawns) evidence.SetValue(pawn, empty);
            }
            document.Add(Rows(RecommendationPlan.Build(colony, store.recommendationTuning), pawns, "emptyEvidence"));
            document.Save(Path.Combine(Root, "work-evidence-colony.xml"));
            Assert(pawns.Count == 27, "captured all 27 Fisso-NAM pawns, saved role/tuning inputs and ordered results");
        }

#if !BASELINE
        private static void Features()
        {
            var pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining)).Take(2).ToList();
            Pawn opposed = pawns[0]; worker = pawns[1];
            object stats = Activator.CreateInstance(typeof(RecsAdapter).Assembly.GetType("WorkRoles.UI.ColonistStatsState"), true);
            Invoke(stats, "Reset", pawns);
            WorkGiverDef miner = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(d => d.giverClass == typeof(WorkGiver_Miner));
            PawnWorkEvidence original = Evidence(stats, opposed);
            int workRevision = Revision("WorkEvidenceRevisions", opposed);
            opposed.health.capacities.Notify_CapacityLevelsDirty(); Refresh(stats, pawns);
            Assert(ReferenceEquals(original, Evidence(stats, opposed)) && workRevision == Revision("WorkEvidenceRevisions", opposed),
                "unchanged capacity notification preserves cached evidence and revision");
            WorkGiverDef capacityWork = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(d => d.requiredCapacities.Contains(PawnCapacityDefOf.Manipulation));
            var impairmentDef = new HediffDef { defName = "WorkEvidenceImpairment", label = "verification impairment", hediffClass = typeof(HediffWithComps),
                stages = new List<HediffStage> { new HediffStage { capMods = new List<PawnCapacityModifier> { new PawnCapacityModifier { capacity = PawnCapacityDefOf.Manipulation, setMax = 0f } } } } };
            Hediff impairment = HediffMaker.MakeHediff(impairmentDef, opposed);
            opposed.health.AddHediff(impairment);
            Assert(Revision("WorkEvidenceRevisions", opposed) != workRevision, "capacity hook publishes changed work revision while paused");
            Refresh(stats, pawns);
            Assert(Evidence(stats, opposed).Blocks(capacityWork.defName), "normal cached consumer sees missing required capacity while paused");
            opposed.health.RemoveHediff(impairment);
            Refresh(stats, pawns);
            Assert(!Evidence(stats, opposed).Blocks(capacityWork.defName), "normal cached consumer sees restored capacity");
            Ideo testIdeo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(FactionDefOf.PlayerColony, forceNoExpansionIdeo: true));
            foreach (Precept precept in testIdeo.PreceptsListForReading.ToArray()) testIdeo.RemovePrecept(precept);
            Find.IdeoManager.Add(testIdeo); opposed.ideo.SetIdeo(testIdeo);
            Refresh(stats, pawns);
            PawnWorkEvidence before = Evidence(stats, opposed);
            int skillRevision = Revision("FactRevisions", opposed);
            workRevision = Revision("WorkEvidenceRevisions", opposed);
            Precept refusal = PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("Mining_Prohibited"));
            testIdeo.AddPrecept(refusal);
            Assert(Revision("WorkEvidenceRevisions", opposed) != workRevision && Revision("FactRevisions", opposed) == skillRevision,
                "precept hook changes only work evidence, leaving skill revisions unchanged");
            Refresh(stats, pawns);
            PawnWorkEvidence after = Evidence(stats, opposed);
            Assert(!ReferenceEquals(before, after), "paused precept edit publishes changed evidence");
            Assert(after.Blocks(miner.defName), "mining refusal blocks the exact mining giver");
            workRevision = Revision("WorkEvidenceRevisions", opposed);
            testIdeo.RecachePrecepts();
            Refresh(stats, pawns);
            Assert(ReferenceEquals(after, Evidence(stats, opposed)) && workRevision == Revision("WorkEvidenceRevisions", opposed),
                "equivalent precept recache retains cached identity and revision");
            WorkGiverDef drill = DefDatabase<WorkGiverDef>.GetNamed("Drill");
            Assert(!after.Blocks(drill.defName), "mining refusal does not disable unrelated Mining givers");
            RoleCommands.CreateRole("Work evidence mining proof");
            Role role = RoleStore.Current.roles.Single(r => r.label == "Work evidence mining proof");
            RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, miner.defName)); RoleCommands.SetRoleColonyMinimum(role.id, 1);
            ColonyView colony = RecsAdapter.BuildColonyView(RoleStore.Current, pawns);
            colony.Roles = colony.Roles.Where(r => r.Id == role.id).ToList(); colony.Paths.Clear(); colony.OrderTemplate = new List<int> { role.id };
            RecommendationPlan plan = RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning);
            Assert(!Holds(plan, 0, role.id) && Holds(plan, 1, role.id), "required mining selects the willing worker from actual game facts");
            colony.Pawns.RemoveAt(1);
            Assert(!Holds(RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning), 0, role.id),
                "hard refusal cannot be used as essential fallback");
            testIdeo.RemovePrecept(refusal);
            testIdeo.AddPrecept(PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("Mining_Disapproved")));
            Refresh(stats, pawns);
            Assert(Evidence(stats, opposed).Opposes("Mining") && !Evidence(stats, opposed).Blocks(miner.defName),
                "soft mining precept remains executable and publishes aversion");
            colony = RecsAdapter.BuildColonyView(RoleStore.Current, pawns);
            colony.Roles = colony.Roles.Where(r => r.Id == role.id).ToList(); colony.Paths.Clear(); colony.OrderTemplate = new List<int> { role.id };
            // Controlled aptitude inputs isolate the score policy; evidence and
            // the completed job below come from the running game.
            foreach (PawnView pawn in colony.Pawns) { pawn.SkillLevels["Mining"] = 10; pawn.SignalBuckets["Mining"] = SignalBucket.Strong; }
            plan = RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning);
            Assert(!Holds(plan, 0, role.id) && Holds(plan, 1, role.id), "soft aversion score selects equally skilled willing worker");
            colony.Pawns.RemoveAt(1);
            Assert(Holds(RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning), 0, role.id),
                "soft aversion retains essential fallback when the willing worker is absent");
            Invoke(stats, "ReleaseSnapshots");
            RoleCommands.AssignRole(worker, role.id, 0);
            Map map = worker.Map;
            IntVec3 rockCell = GenRadial.RadialCellsAround(worker.Position, 12, true).First(c =>
                c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map)
                && GenAdj.CellsAdjacent8Way(c, Rot4.North, IntVec2.One).Any(n => n.InBounds(map) && n.Standable(map)));
            target = (Mineable)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MineableSteel")), rockCell, map);
            IntVec3 position = GenAdj.CellsAdjacent8Way(target).First(c => c.InBounds(map) && c.Standable(map));
            worker.jobs.StopAll(); worker.pather.StopDead(); worker.Position = position; target.HitPoints = 1;
            if (map.designationManager.DesignationOn(target, DesignationDefOf.Mine) == null) map.designationManager.AddDesignation(new Designation(target, DesignationDefOf.Mine));
            Job job = ((WorkGiver_Miner)miner.Worker).JobOnThing(worker, target, true);
            Assert(job != null, "recommended pawn receives a real job from the vanilla mining giver");
            worker.jobs.TryTakeOrderedJob(job); startedTick = Find.TickManager.TicksGame; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast; stage = 2;
        }

        private static void Har()
        {
            Map map = Find.CurrentMap;
            Pawn human = map.mapPawns.FreeColonistsSpawned.First(p => !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Crafting));
            Pawn Make(string kind) => PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed(kind), Faction.OfPlayer,
                forceGenerateNewPawn: true, forceNoIdeo: true, forceNoBackstory: true, fixedBiologicalAge: 30));
            Pawn a = Make("WE_KindA"), b = Make("WE_KindB");
            GenSpawn.Spawn(a, human.Position, map); GenSpawn.Spawn(b, human.Position, map);
            var pawns = new List<Pawn> { human, a, b };
            QueryCounterGiver.Queries = 0;
            ColonyView colony = RecsAdapter.BuildColonyView(RoleStore.Current, pawns);
            PawnWorkEvidence h = colony.Pawns[0].WorkEvidence, ea = colony.Pawns[1].WorkEvidence, eb = colony.Pawns[2].WorkEvidence;
            Assert(h.Blocks("WE_MineA") && h.Blocks("WE_MineB"), "HAR denies human both race-specific givers");
            Assert(!ea.Blocks("WE_MineA") && !ea.Blocks("WE_MineB") && !eb.Blocks("WE_MineA") && !eb.Blocks("WE_MineB"),
                "HAR own-race class permission wins across two races sharing a giver class");
            Assert(h.Blocks("WE_BillA") && eb.Blocks("WE_BillA") && !ea.Blocks("WE_BillA"),
                "HAR complete fixed recipe denial blocks only ineligible races");
            Assert(h.Blocks("WE_BillMixed", "WE_RecipeA") && !h.Blocks("WE_BillMixed") && !h.Blocks("WE_BillMixed", "WE_RecipeOpen"),
                "HAR recipe denial preserves an ordinary alternative on the same giver");
            Assert(QueryCounterGiver.Queries == 0, "capability capture invokes no live job search");
            RoleCommands.CreateRole("HAR restricted crafting proof");
            Role role = RoleStore.Current.roles.Single(r => r.label == "HAR restricted crafting proof");
            RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, "WE_BillA")); RoleCommands.SetRoleColonyMinimum(role.id, 1);
            colony = RecsAdapter.BuildColonyView(RoleStore.Current, pawns);
            colony.Roles = colony.Roles.Where(r => r.Id == role.id).ToList(); colony.Paths.Clear(); colony.OrderTemplate = new List<int> { role.id };
            PawnWorkEvidence[] realEvidence = colony.Pawns.Select(p => p.WorkEvidence).ToArray();
            for (int i = 0; i < colony.Pawns.Count; i++)
            {
                colony.Pawns[i].SkillLevels["Crafting"] = i == 0 ? 18 : 8;
                colony.Pawns[i].SignalBuckets["Crafting"] = SignalBucket.Strong;
                colony.Pawns[i].WorkEvidence = PawnWorkEvidence.Empty;
            }
            Assert(Holds(RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning), 0, role.id),
                "without capability information the ineligible skilled human receives restricted crafting");
            for (int i = 0; i < colony.Pawns.Count; i++) colony.Pawns[i].WorkEvidence = realEvidence[i];
            RecommendationPlan plan = RecommendationPlan.Build(colony, RoleStore.Current.recommendationTuning);
            Assert(!Holds(plan, 0, role.id) && Holds(plan, 1, role.id) && !Holds(plan, 2, role.id),
                "recommendations assign restricted crafting only to the capable race");
            RoleCommands.AssignRole(a, role.id, 0);
            IntVec3 cell = GenRadial.RadialCellsAround(human.Position, 12, true).First(c =>
                c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map)
                && (c + IntVec3.South).InBounds(map) && (c + IntVec3.South).Standable(map));
            var bench = (Building_WorkTable)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WE_BenchA")), cell, map);
            bench.SetFaction(Faction.OfPlayer);
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("WE_RecipeA");
            proofBill = (Bill_Production)recipe.MakeNewBill(); proofBill.repeatMode = BillRepeatModeDefOf.RepeatCount; proofBill.repeatCount = 1;
            bench.BillStack.AddBill(proofBill);
            Assert(!proofBill.PawnAllowedToStartAnew(human) && proofBill.PawnAllowedToStartAnew(a) && !proofBill.PawnAllowedToStartAnew(b),
                "actual HAR bill rule agrees with recommendation capability");
            a.jobs.StopAll(); a.pather.StopDead(); a.Position = bench.InteractionCell;
            Job job = ((WorkGiver_DoBill)DefDatabase<WorkGiverDef>.GetNamed("WE_BillA").Worker).JobOnThing(a, bench, true);
            Assert(job != null, "recommended race receives the actual restricted crafting job");
            a.jobs.TryTakeOrderedJob(job); startedTick = Find.TickManager.TicksGame; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast; stage = 3;
        }

#endif
        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 6) return;
                if (stage == 0) { Find.TickManager.CurTimeSpeed = TimeSpeed.Paused; CaptureColony(); stage = 1; Mark("captured"); }
                else if (stage == 1)
                {
                    string control = Path.Combine(Root, "work-evidence-control.txt"); if (!File.Exists(control)) return;
#if BASELINE
                    stage = 99; Mark("SUCCESS");
#else
                    string action = File.ReadAllText(control).Trim();
                    if (action == "features" || action == "har") { includeHar = action == "har"; Features(); }
                    else if (action == "vivi") Vivi();
                    else { stage = 99; Mark("SUCCESS"); }
#endif
                }
#if !BASELINE
                else if (stage == 2)
                {
                    if (target.Destroyed)
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        Assert(true, "recommended willing pawn completed mining and destroyed the rock");
                        if (includeHar) Har(); else { stage = 99; Mark("SUCCESS"); }
                    }
                    else if (Find.TickManager.TicksGame - startedTick > 5000) throw new InvalidOperationException("mining did not finish");
                }
                else if (stage == 3)
                {
                    if (proofBill.repeatCount == 0) { Find.TickManager.CurTimeSpeed = TimeSpeed.Paused; Assert(true, "recommended race completed the restricted recipe through the game's production job"); stage = 99; Mark("SUCCESS"); }
                    else if (Find.TickManager.TicksGame - startedTick > 5000) throw new InvalidOperationException("restricted crafting did not finish");
                }
                else if (stage == 4) ViviUpdate();
#endif
            }
            catch (Exception ex) { Find.TickManager.CurTimeSpeed = TimeSpeed.Paused; stage = 99; Mark("FAILED " + ex); Log.Error("[WorkEvidenceProbe] " + ex); }
        }
    }

    public sealed class QueryCounterGiver : WorkGiver_Scanner
    {
        public static int Queries;
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false) { Queries++; return false; }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false) { Queries++; return null; }
    }
}
