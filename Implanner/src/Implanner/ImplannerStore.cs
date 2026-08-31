using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// Authoritative per-save Implanner state: the Core model plus
    /// deterministic id allocation. Mutate only via PlannerCommands or
    /// deterministic lifecycle code here — direct writes bypass revision
    /// publication.
    public class ImplannerStore : WorldComponent
    {
        public PlannerModel Model = new PlannerModel();
        private int nextPlanId = 1;
        private int nextGoalId = 1;

        // Scribe staging buffers; live only between ExposeData passes.
        private List<PlanRecord>? planRecords;
        private List<int>? assignmentPawnIds;
        private List<int>? assignmentPlanIds;
        private List<int>? priorityPawnIds;
        private List<int>? priorityLevels;
        private List<int>? latchPawnIds;
        private List<string>? latchKeyBlobs;   // comma-joined sorted goal keys
        private List<int>? reservationItemIds;
        private List<int>? reservationPawnIds;
        private List<string>? reservationGoalKeys;
        private List<string>? implantStarDefs;
        private List<int>? implantStarLevels;
        private List<string>? implantOrderDefs;
        private List<int>? implantOrderValues;
        private List<string>? doctorFloorColonies;
        private List<int>? doctorFloorLevels;
        private List<string>? implantReserveDefs;
        private List<int>? implantReserveCounts;
        private List<int>? ownedBillPawnIds;
        private List<string>? ownedBillGoalKeys;
        private List<string>? ownedBillIds;
        private List<string>? reserveDefs;
        private List<int>? reserveAmounts;
        private List<string>? productionBillIds;
        private List<string>? productionBillDefs;
        private bool automationPaused;
        private int iteration = (int)IterationStrategy.ImplantTier;
        private int manualDoctorFloor;
        private bool autoDoctorFloor = true;
        private bool autoProduction = true;
        private int productionConcurrency = PlannerModel.ConcurrencyDefault;
        private bool onlyIdleBenches = true;
        private int productionSkill = PlannerModel.ProductionSkillDefault;
        private bool allowIntermediaries = true;

        private readonly PlannerRevisions revisions = new PlannerRevisions();

        /// Global mutation stamp for consumers that depend on the entire model.
        public int Version => revisions.Version;
        public int PlansVersion => revisions.Plans;
        public int AssignmentsVersion => revisions.Assignments;
        public int OptionsVersion => revisions.Options;
        public int RankingsVersion => revisions.Rankings;
        public int SurgeryVersion => revisions.Surgery;
        public int ProductionVersion => revisions.Production;

        public ImplannerStore(World world) : base(world)
        {
            Model.SlotConflictResolver = ImplantConflicts.Resolver;
        }

        public static ImplannerStore? Current => Find.World?.GetComponent<ImplannerStore>();

        public int TakePlanId() => nextPlanId++;

        /// Goal ids are globally unique per save so goal keys stay stable
        /// across plan extension.
        public int TakeGoalId() => nextGoalId++;

        public void Bump(PlannerChange change) => revisions.Bump(change);

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            // Loaded saves finish their initialization from the PostLoadInit
            // scribe pass instead: World.FinalizeInit runs mid-load, BEFORE
            // PostLoadInit replaces Model with the loaded state — anything
            // done to the model here would be silently discarded.
            if (!fromLoad) FinishInit();
        }

        /// Deterministic lifecycle initialization on the fully hydrated
        /// model: normalization must finish before revisions publish.
        private void FinishInit()
        {
            Model.CleanupMissing(PawnExists);
            Bump(PlannerChange.All);
        }

        private static bool PawnExists(int pawnId)
        {
            List<Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == pawnId)
                    return true;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextPlanId, "nextPlanId", 1);
            Scribe_Values.Look(ref nextGoalId, "nextGoalId", 1);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                planRecords = new List<PlanRecord>();
                foreach (Plan plan in Model.Plans)
                    planRecords.Add(new PlanRecord(plan));
                assignmentPawnIds = new List<int>();
                assignmentPlanIds = new List<int>();
                foreach (KeyValuePair<int, int> pair in Model.Assignments)
                {
                    assignmentPawnIds.Add(pair.Key);
                    assignmentPlanIds.Add(pair.Value);
                }
                // Deterministic save output: sort by pawn id, keeping the
                // parallel plan list aligned.
                SortAssignments(assignmentPawnIds, assignmentPlanIds);
                priorityPawnIds = new List<int>();
                priorityLevels = new List<int>();
                foreach (KeyValuePair<int, int> pair in Model.Priorities)
                {
                    priorityPawnIds.Add(pair.Key);
                    priorityLevels.Add(pair.Value);
                }
                SortAssignments(priorityPawnIds, priorityLevels);

                latchPawnIds = new List<int>();
                latchKeyBlobs = new List<string>();
                foreach (KeyValuePair<int, HashSet<string>> pair in Model.Latches)
                {
                    var keys = new List<string>(pair.Value);
                    keys.Sort(System.StringComparer.Ordinal);
                    latchPawnIds.Add(pair.Key);
                    latchKeyBlobs.Add(string.Join(",", keys));
                }
                SortParallel(latchPawnIds, latchKeyBlobs);

                reservationItemIds = new List<int>();
                reservationPawnIds = new List<int>();
                reservationGoalKeys = new List<string>();
                foreach (KeyValuePair<int, ItemReservation> pair in Model.Reservations)
                {
                    reservationItemIds.Add(pair.Key);
                    reservationPawnIds.Add(pair.Value.PawnId);
                    reservationGoalKeys.Add(pair.Value.GoalKey);
                }
                SortReservations(reservationItemIds, reservationPawnIds, reservationGoalKeys);

                implantStarDefs = new List<string>(Model.ImplantStars.Keys);
                implantStarDefs.Sort(System.StringComparer.Ordinal);
                implantStarLevels = new List<int>();
                for (int i = 0; i < implantStarDefs.Count; i++)
                    implantStarLevels.Add(Model.ImplantStarsOf(implantStarDefs[i]));
                implantOrderDefs = new List<string>(Model.ImplantOrder.Keys);
                implantOrderDefs.Sort(System.StringComparer.Ordinal);
                implantOrderValues = new List<int>();
                for (int i = 0; i < implantOrderDefs.Count; i++)
                    implantOrderValues.Add(Model.ImplantOrderOf(implantOrderDefs[i]));

                doctorFloorColonies = new List<string>(Model.DoctorFloors.Keys);
                doctorFloorColonies.Sort(System.StringComparer.Ordinal);
                doctorFloorLevels = new List<int>();
                for (int i = 0; i < doctorFloorColonies.Count; i++)
                    doctorFloorLevels.Add(Model.DoctorFloorOf(doctorFloorColonies[i]));

                implantReserveDefs = new List<string>(Model.ImplantReserves.Keys);
                implantReserveDefs.Sort(System.StringComparer.Ordinal);
                implantReserveCounts = new List<int>();
                for (int i = 0; i < implantReserveDefs.Count; i++)
                    implantReserveCounts.Add(
                        Model.ImplantReserveOf(implantReserveDefs[i]));

                ownedBillPawnIds = new List<int>();
                ownedBillGoalKeys = new List<string>();
                ownedBillIds = new List<string>();
                var billPawns = new List<int>(Model.OwnedBills.Keys);
                billPawns.Sort();
                for (int i = 0; i < billPawns.Count; i++)
                {
                    var keys = new List<string>(Model.OwnedBills[billPawns[i]].Keys);
                    keys.Sort(System.StringComparer.Ordinal);
                    for (int k = 0; k < keys.Count; k++)
                    {
                        ownedBillPawnIds.Add(billPawns[i]);
                        ownedBillGoalKeys.Add(keys[k]);
                        ownedBillIds.Add(Model.OwnedBills[billPawns[i]][keys[k]]);
                    }
                }

                reserveDefs = new List<string>(Model.ResourceReserves.Keys);
                reserveDefs.Sort(System.StringComparer.Ordinal);
                reserveAmounts = new List<int>();
                for (int i = 0; i < reserveDefs.Count; i++)
                    reserveAmounts.Add(Model.ResourceReserveOf(reserveDefs[i]));

                productionBillIds = new List<string>(Model.OwnedProductionBills.Keys);
                productionBillIds.Sort(System.StringComparer.Ordinal);
                productionBillDefs = new List<string>();
                for (int i = 0; i < productionBillIds.Count; i++)
                    productionBillDefs.Add(
                        Model.OwnedProductionBills[productionBillIds[i]]);

                automationPaused = Model.AutomationPaused;
                iteration = (int)Model.Iteration;
                manualDoctorFloor = Model.ManualDoctorFloor;
                autoDoctorFloor = Model.AutoDoctorFloor;
                autoProduction = Model.AutoProduction;
                productionConcurrency = Model.ProductionConcurrency;
                onlyIdleBenches = Model.OnlyIdleBenches;
                productionSkill = Model.ProductionSkill;
                allowIntermediaries = Model.AllowIntermediaries;
            }

            Scribe_Collections.Look(ref planRecords, "plans", LookMode.Deep);
            Scribe_Collections.Look(ref assignmentPawnIds, "assignmentPawns", LookMode.Value);
            Scribe_Collections.Look(ref assignmentPlanIds, "assignmentPlans", LookMode.Value);
            Scribe_Collections.Look(ref priorityPawnIds, "priorityPawns", LookMode.Value);
            Scribe_Collections.Look(ref priorityLevels, "priorityLevels", LookMode.Value);
            Scribe_Collections.Look(ref latchPawnIds, "latchPawns", LookMode.Value);
            Scribe_Collections.Look(ref latchKeyBlobs, "latchKeys", LookMode.Value);
            Scribe_Collections.Look(ref reservationItemIds, "reservationItems", LookMode.Value);
            Scribe_Collections.Look(ref reservationPawnIds, "reservationPawns", LookMode.Value);
            Scribe_Collections.Look(ref reservationGoalKeys, "reservationGoals", LookMode.Value);
            Scribe_Collections.Look(ref implantStarDefs, "implantStarDefs", LookMode.Value);
            Scribe_Collections.Look(ref implantStarLevels, "implantStarLevels", LookMode.Value);
            Scribe_Collections.Look(ref implantOrderDefs, "implantOrderDefs", LookMode.Value);
            Scribe_Collections.Look(ref implantOrderValues, "implantOrderValues", LookMode.Value);
            Scribe_Collections.Look(ref doctorFloorColonies, "doctorFloorColonies", LookMode.Value);
            Scribe_Collections.Look(ref doctorFloorLevels, "doctorFloorLevels", LookMode.Value);
            Scribe_Collections.Look(ref implantReserveDefs, "implantReserveDefs", LookMode.Value);
            Scribe_Collections.Look(ref implantReserveCounts, "implantReserveCounts", LookMode.Value);
            Scribe_Collections.Look(ref ownedBillPawnIds, "ownedBillPawns", LookMode.Value);
            Scribe_Collections.Look(ref ownedBillGoalKeys, "ownedBillGoals", LookMode.Value);
            Scribe_Collections.Look(ref ownedBillIds, "ownedBillIds", LookMode.Value);
            Scribe_Collections.Look(ref reserveDefs, "reserveDefs", LookMode.Value);
            Scribe_Collections.Look(ref reserveAmounts, "reserveAmounts", LookMode.Value);
            Scribe_Collections.Look(ref productionBillIds, "productionBillIds", LookMode.Value);
            Scribe_Collections.Look(ref productionBillDefs, "productionBillDefs", LookMode.Value);
            Scribe_Values.Look(ref automationPaused, "automationPaused", false);
            Scribe_Values.Look(ref iteration, "iteration",
                (int)IterationStrategy.ImplantTier);
            Scribe_Values.Look(ref manualDoctorFloor, "manualDoctorFloor", 0);
            Scribe_Values.Look(ref autoDoctorFloor, "autoDoctorFloor", true);
            Scribe_Values.Look(ref autoProduction, "autoProduction", true);
            Scribe_Values.Look(ref productionConcurrency, "productionConcurrency",
                PlannerModel.ConcurrencyDefault);
            Scribe_Values.Look(ref onlyIdleBenches, "onlyIdleBenches", true);
            Scribe_Values.Look(ref productionSkill, "productionSkill",
                PlannerModel.ProductionSkillDefault);
            Scribe_Values.Look(ref allowIntermediaries, "allowIntermediaries", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Model = new PlannerModel
                {
                    SlotConflictResolver = ImplantConflicts.Resolver,
                };
                if (planRecords != null)
                    foreach (PlanRecord record in planRecords)
                    {
                        Plan? plan = record.ToPlan();
                        if (plan != null) Model.AddLoadedPlan(plan);
                    }
                if (assignmentPawnIds != null && assignmentPlanIds != null
                    && assignmentPawnIds.Count == assignmentPlanIds.Count)
                    for (int i = 0; i < assignmentPawnIds.Count; i++)
                        Model.AddLoadedAssignment(assignmentPawnIds[i], assignmentPlanIds[i]);
                if (priorityPawnIds != null && priorityLevels != null
                    && priorityPawnIds.Count == priorityLevels.Count)
                    for (int i = 0; i < priorityPawnIds.Count; i++)
                        Model.AddLoadedPriority(priorityPawnIds[i], priorityLevels[i]);
                if (latchPawnIds != null && latchKeyBlobs != null
                    && latchPawnIds.Count == latchKeyBlobs.Count)
                    for (int i = 0; i < latchPawnIds.Count; i++)
                        Model.AddLoadedLatches(latchPawnIds[i],
                            latchKeyBlobs[i]?.Split(',') ?? System.Array.Empty<string>());
                if (reservationItemIds != null && reservationPawnIds != null
                    && reservationGoalKeys != null
                    && reservationItemIds.Count == reservationPawnIds.Count
                    && reservationItemIds.Count == reservationGoalKeys.Count)
                    for (int i = 0; i < reservationItemIds.Count; i++)
                        if (!reservationGoalKeys[i].NullOrEmpty())
                            Model.AddLoadedReservation(reservationItemIds[i],
                                reservationPawnIds[i], reservationGoalKeys[i]);
                if (implantStarDefs != null && implantStarLevels != null
                    && implantStarDefs.Count == implantStarLevels.Count)
                    for (int i = 0; i < implantStarDefs.Count; i++)
                        if (!implantStarDefs[i].NullOrEmpty())
                            Model.AddLoadedImplantStars(implantStarDefs[i], implantStarLevels[i]);
                if (implantOrderDefs != null && implantOrderValues != null
                    && implantOrderDefs.Count == implantOrderValues.Count)
                    for (int i = 0; i < implantOrderDefs.Count; i++)
                        if (!implantOrderDefs[i].NullOrEmpty())
                            Model.AddLoadedImplantOrder(implantOrderDefs[i], implantOrderValues[i]);
                if (doctorFloorColonies != null && doctorFloorLevels != null
                    && doctorFloorColonies.Count == doctorFloorLevels.Count)
                    for (int i = 0; i < doctorFloorColonies.Count; i++)
                        if (!doctorFloorColonies[i].NullOrEmpty())
                            Model.AddLoadedDoctorFloor(
                                doctorFloorColonies[i], doctorFloorLevels[i]);
                if (implantReserveDefs != null && implantReserveCounts != null
                    && implantReserveDefs.Count == implantReserveCounts.Count)
                    for (int i = 0; i < implantReserveDefs.Count; i++)
                        if (!implantReserveDefs[i].NullOrEmpty())
                            Model.AddLoadedImplantReserve(
                                implantReserveDefs[i], implantReserveCounts[i]);
                if (ownedBillPawnIds != null && ownedBillGoalKeys != null
                    && ownedBillIds != null
                    && ownedBillPawnIds.Count == ownedBillGoalKeys.Count
                    && ownedBillPawnIds.Count == ownedBillIds.Count)
                    for (int i = 0; i < ownedBillPawnIds.Count; i++)
                        if (!ownedBillGoalKeys[i].NullOrEmpty()
                            && !ownedBillIds[i].NullOrEmpty())
                            Model.AddLoadedOwnedBill(ownedBillPawnIds[i],
                                ownedBillGoalKeys[i], ownedBillIds[i]);
                if (reserveDefs != null && reserveAmounts != null
                    && reserveDefs.Count == reserveAmounts.Count)
                    for (int i = 0; i < reserveDefs.Count; i++)
                        if (!reserveDefs[i].NullOrEmpty())
                            Model.AddLoadedResourceReserve(
                                reserveDefs[i], reserveAmounts[i]);
                if (productionBillIds != null && productionBillDefs != null
                    && productionBillIds.Count == productionBillDefs.Count)
                    for (int i = 0; i < productionBillIds.Count; i++)
                        if (!productionBillIds[i].NullOrEmpty()
                            && !productionBillDefs[i].NullOrEmpty())
                            Model.AddLoadedProductionBill(
                                productionBillIds[i], productionBillDefs[i]);
                Model.LoadOptions(automationPaused,
                    (IterationStrategy)iteration, manualDoctorFloor, autoDoctorFloor,
                    autoProduction, productionConcurrency,
                    onlyIdleBenches, productionSkill, allowIntermediaries);
                // Deferred so maps and pawns are fully loaded when existence
                // checks run; executes before play begins on every client.
                LongEventHandler.ExecuteWhenFinished(FinishInit);
            }

            if (Scribe.mode == LoadSaveMode.Saving
                || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                planRecords = null;
                assignmentPawnIds = null;
                assignmentPlanIds = null;
                priorityPawnIds = null;
                priorityLevels = null;
                latchPawnIds = null;
                latchKeyBlobs = null;
                reservationItemIds = null;
                reservationPawnIds = null;
                reservationGoalKeys = null;
                implantStarDefs = null;
                implantStarLevels = null;
                implantOrderDefs = null;
                implantOrderValues = null;
                doctorFloorColonies = null;
                doctorFloorLevels = null;
                implantReserveDefs = null;
                implantReserveCounts = null;
                ownedBillPawnIds = null;
                ownedBillGoalKeys = null;
                ownedBillIds = null;
                reserveDefs = null;
                reserveAmounts = null;
                productionBillIds = null;
                productionBillDefs = null;
            }
        }

        private static void SortAssignments(List<int> pawnIds, List<int> planIds)
        {
            int[] keys = pawnIds.ToArray();
            int[] values = planIds.ToArray();
            System.Array.Sort(keys, values);
            pawnIds.Clear();
            planIds.Clear();
            pawnIds.AddRange(keys);
            planIds.AddRange(values);
        }

        private static void SortParallel(List<int> ids, List<string> values)
        {
            int[] keys = ids.ToArray();
            string[] payload = values.ToArray();
            System.Array.Sort(keys, payload);
            ids.Clear();
            values.Clear();
            ids.AddRange(keys);
            values.AddRange(payload);
        }

        private static void SortReservations(
            List<int> itemIds, List<int> pawnIds, List<string> goalKeys)
        {
            int[] keys = itemIds.ToArray();
            var order = new int[keys.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(keys, order);
            var pawns = new int[order.Length];
            var goals = new string[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                pawns[i] = pawnIds[order[i]];
                goals[i] = goalKeys[order[i]];
            }
            itemIds.Clear(); pawnIds.Clear(); goalKeys.Clear();
            itemIds.AddRange(keys);
            pawnIds.AddRange(pawns);
            goalKeys.AddRange(goals);
        }
    }

    /// IExposable projection of one Core Plan. The Core model stays free of
    /// Scribe; records exist only during save/load.
    public class PlanRecord : IExposable
    {
        private int id;
        private string name = "";
        private int basePlanId;
        private List<ImplantRecord>? implants;

        public PlanRecord() { }

        public PlanRecord(Plan plan)
        {
            id = plan.Id;
            name = plan.Name;
            basePlanId = plan.BasePlanId;
            implants = new List<ImplantRecord>();
            foreach (ImplantGoal goal in plan.Implants)
                implants.Add(new ImplantRecord(goal));
        }

        public Plan? ToPlan()
        {
            if (id <= 0 || string.IsNullOrEmpty(name)) return null;
            var plan = new Plan(id, name) { BasePlanId = basePlanId };
            if (implants != null)
                foreach (ImplantRecord record in implants)
                {
                    ImplantGoal? goal = record.ToGoal();
                    if (goal != null) plan.Implants.Add(goal);
                }
            return plan;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref basePlanId, "basePlanId");
            Scribe_Collections.Look(ref implants, "implants", LookMode.Deep);
        }
    }

    public class ImplantRecord : IExposable
    {
        private int id;
        private string implantDef = "";
        private List<int>? slots;

        public ImplantRecord() { }

        public ImplantRecord(ImplantGoal goal)
        {
            id = goal.Id;
            implantDef = goal.ImplantDefName;
            slots = new List<int>(goal.SlotOrdinals);
        }

        public ImplantGoal? ToGoal()
        {
            if (string.IsNullOrEmpty(implantDef)) return null;
            List<int>? ordinals = slots;
            if (ordinals == null || ordinals.Count == 0) return null;
            ordinals.Sort();
            return new ImplantGoal(id, implantDef, ordinals);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref implantDef, "implantDef", "");
            Scribe_Collections.Look(ref slots, "slots", LookMode.Value);
        }
    }
}
