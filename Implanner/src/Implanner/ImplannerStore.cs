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

        // Scribe staging buffers; live only between ExposeData passes.
        private List<PlanRecord>? planRecords;
        private List<int>? assignmentPawnIds;
        private List<int>? assignmentPlanIds;
        private List<int>? priorityPawnIds;
        private List<int>? priorityLevels;
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
        // 0 = never seeded: FinishInit derives max(1, colonists / 10) once.
        private int surgeryConcurrency;
        private bool countHospitalized = true;
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
            // First init for this save (new game, or a save from before the
            // option existed): seed the concurrent-surgeries cap from the
            // colony size. Deterministic — every client counts the same
            // authoritative colonist roster from the same save data.
            if (surgeryConcurrency <= 0)
            {
                int colonists = ColonyScope.AllPlanableColonists(
                    ColonyScope.AuthoritativeFaction).Count;
                surgeryConcurrency = System.Math.Max(1, colonists / 10);
                Model.SetSurgeryConcurrency(surgeryConcurrency);
            }
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
                surgeryConcurrency = Model.SurgeryConcurrency;
                countHospitalized = Model.CountHospitalized;
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
            Scribe_Values.Look(ref surgeryConcurrency, "surgeryConcurrency", 0);
            Scribe_Values.Look(ref countHospitalized, "countHospitalized", true);
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
                // Legacy goal-id map: saves written before natural goal
                // identities persisted per-goal ids, and their reservation
                // and bill keys use the retired "i{id}:{ordinal}" format.
                // Collect id -> (plan index, kind) while loading so those
                // keys can be rewritten once plan ids are final.
                Dictionary<int, LegacyGoalRef>? legacyGoals = null;
                if (planRecords != null)
                    foreach (PlanRecord record in planRecords)
                    {
                        Plan? plan = record.ToPlan(
                            Model.Plans.Count, ref legacyGoals);
                        if (plan != null) Model.AddLoadedPlan(plan);
                    }
                // Saves from builds that did not persist the plan-id counter
                // (or that carry a duplicated plan id) must never reissue a
                // live id: goal keys, assignments, and base links embed plan
                // ids as identity.
                Model.NormalizeLoadedIds(ref nextPlanId);
                if (assignmentPawnIds != null && assignmentPlanIds != null
                    && assignmentPawnIds.Count == assignmentPlanIds.Count)
                    for (int i = 0; i < assignmentPawnIds.Count; i++)
                        Model.AddLoadedAssignment(assignmentPawnIds[i], assignmentPlanIds[i]);
                if (priorityPawnIds != null && priorityLevels != null
                    && priorityPawnIds.Count == priorityLevels.Count)
                    for (int i = 0; i < priorityPawnIds.Count; i++)
                        Model.AddLoadedPriority(priorityPawnIds[i], priorityLevels[i]);
                if (reservationItemIds != null && reservationPawnIds != null
                    && reservationGoalKeys != null
                    && reservationItemIds.Count == reservationPawnIds.Count
                    && reservationItemIds.Count == reservationGoalKeys.Count)
                    for (int i = 0; i < reservationItemIds.Count; i++)
                        if (!reservationGoalKeys[i].NullOrEmpty())
                        {
                            string? key = MigrateGoalKey(
                                reservationGoalKeys[i], legacyGoals);
                            if (key != null)
                                Model.AddLoadedReservation(reservationItemIds[i],
                                    reservationPawnIds[i], key);
                        }
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
                        {
                            string? key = MigrateGoalKey(
                                ownedBillGoalKeys[i], legacyGoals);
                            if (key != null)
                                Model.AddLoadedOwnedBill(ownedBillPawnIds[i],
                                    key, ownedBillIds[i]);
                        }
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
                    surgeryConcurrency, countHospitalized,
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

        /// Rewrites a legacy "i{goalId}:{ordinal}" key to the natural
        /// "p{planId}:{defName}:{ordinal}" format using the owning plan
        /// recorded in the save; an unmappable legacy key is dropped (its
        /// goal no longer exists, so the reconciler would release it
        /// anyway). Natural keys pass through unchanged. Runs after
        /// NormalizeLoadedIds so the plan index resolves to the final id.
        private string? MigrateGoalKey(
            string key, Dictionary<int, LegacyGoalRef>? legacyGoals)
        {
            if (!GoalKeys.TryParseLegacyImplantSlot(
                    key, out int goalId, out int ordinal))
                return key;
            if (legacyGoals == null
                || !legacyGoals.TryGetValue(goalId, out LegacyGoalRef owner))
                return null;
            return GoalKeys.ImplantSlot(
                Model.Plans[owner.PlanIndex].Id, owner.DefName, ordinal);
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

    /// A legacy goal id's owner: the loaded plan's list position (stable
    /// through NormalizeLoadedIds, which replaces in place) and the implant
    /// kind. Used only to migrate pre-natural-key reservation and bill keys.
    public readonly struct LegacyGoalRef
    {
        public LegacyGoalRef(int planIndex, string defName)
        {
            PlanIndex = planIndex;
            DefName = defName;
        }

        public int PlanIndex { get; }
        public string DefName { get; }
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

        /// planIndex is the position the caller will load this plan into;
        /// legacy goal ids found in the record register there so old keys
        /// can be migrated once plan ids are final.
        public Plan? ToPlan(int planIndex,
            ref Dictionary<int, LegacyGoalRef>? legacyGoals)
        {
            if (id <= 0 || string.IsNullOrEmpty(name)) return null;
            var plan = new Plan(id, name) { BasePlanId = basePlanId };
            if (implants != null)
                foreach (ImplantRecord record in implants)
                {
                    ImplantGoal? goal = record.ToGoal(plan.Id);
                    if (goal == null) continue;
                    plan.Implants.Add(goal);
                    if (record.LegacyId > 0)
                        (legacyGoals ??= new Dictionary<int, LegacyGoalRef>())
                            [record.LegacyId] = new LegacyGoalRef(
                                planIndex, goal.ImplantDefName);
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
        // Read for legacy-key migration only; new saves omit it (goals
        // carry natural identities and no longer persist an id).
        private int id;
        private string implantDef = "";
        private List<int>? slots;

        public ImplantRecord() { }

        public ImplantRecord(ImplantGoal goal)
        {
            implantDef = goal.ImplantDefName;
            slots = new List<int>(goal.SlotOrdinals);
        }

        internal int LegacyId => id;

        public ImplantGoal? ToGoal(int planId)
        {
            if (string.IsNullOrEmpty(implantDef)) return null;
            List<int>? ordinals = slots;
            if (ordinals == null || ordinals.Count == 0) return null;
            ordinals.Sort();
            return new ImplantGoal(planId, implantDef, ordinals);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref implantDef, "implantDef", "");
            Scribe_Collections.Look(ref slots, "slots", LookMode.Value);
        }
    }
}
