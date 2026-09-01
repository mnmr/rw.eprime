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
        // Persisted as 0 until seeded: the first reconcile pass that sees a
        // colonist derives max(1, colonists / 10) (old saves that already
        // have colonists seed at load), and a player edit counts as seeded.
        private int surgeryConcurrency;
        private bool surgeryConcurrencySeeded;

        // Reconcile triggers travel with the save so a late-joining or
        // resynced multiplayer client (which loads the host's state) runs
        // exactly the passes the host runs. Set only by the synced command
        // path (Bump), cleared by the pass; load-time code never sets them.
        // Absent in older saves reads as false.
        private bool pendingReconcile;
        private bool pendingProductionPass;
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
            Model.SetSlotConflictResolver(ImplantConflicts.Resolver);
        }

        public static ImplannerStore? Current => Find.World?.GetComponent<ImplannerStore>();

        public int TakePlanId() => nextPlanId++;

        /// Whether a synced command mutated the model since the last
        /// reconcile pass: the next simulated tick runs one.
        public bool PendingReconcile => pendingReconcile;

        /// Whether a production-domain option changed since the last
        /// production dispatch: the next reconcile pass dispatches early.
        public bool PendingProductionPass => pendingProductionPass;

        /// The synced command path: publishes the change and requests a
        /// reconcile pass on the next simulated tick. The pass request is
        /// unconditional: every client runs the same command, and a request
        /// that depended on whether the command changed anything locally
        /// could set the flag on one client only. Revisions still move only
        /// for real changes.
        public void Bump(PlannerChange change)
        {
            pendingReconcile = true;
            if (change == PlannerChange.None) return;
            revisions.Bump(change);
            if ((change & PlannerChange.Production) != 0)
                pendingProductionPass = true;
        }

        /// The reconcile pass's own bookkeeping: published without
        /// requesting another pass, so the pass's mutations never re-trigger
        /// it on the following tick.
        internal void PublishPass(PlannerChange change) => revisions.Bump(change);

        internal void ClearPendingReconcile() => pendingReconcile = false;

        internal void ClearPendingProductionPass() => pendingProductionPass = false;

        /// The player set the cap explicitly: never seed over it.
        internal void MarkSurgeryConcurrencySeeded() =>
            surgeryConcurrencySeeded = true;

        /// Seeds the concurrent-surgeries cap once from the colony size,
        /// on the first pass that observes at least one authoritative
        /// colonist while the persisted value is still the unseeded
        /// sentinel. Runs inside the synchronized tick pass from the
        /// pass's own colonist count, so every client seeds identically.
        internal PlannerChange SeedSurgeryConcurrency(int colonists)
        {
            if (surgeryConcurrencySeeded || colonists <= 0)
                return PlannerChange.None;
            surgeryConcurrencySeeded = true;
            return Model.SetSurgeryConcurrency(SeededSurgeryConcurrency(colonists));
        }

        private static int SeededSurgeryConcurrency(int colonists) =>
            System.Math.Max(1, colonists / 10);

        // World.FinalizeInit is deliberately not overridden. On a new game
        // WorldGenerator.GenerateWorld calls it BEFORE
        // Scenario.PostWorldGenerate creates the player faction, so
        // Faction.OfPlayer logs "Could not find player faction" there; a
        // fresh store has nothing to clean up and seeds surgery concurrency
        // on the first reconcile pass that observes a colonist. Loaded saves
        // finish their initialization from FinishInit, queued by the
        // PostLoadInit scribe pass through LongEventHandler so it runs after
        // maps finalize: World.FinalizeInit runs mid-load, BEFORE
        // PostLoadInit replaces Model with the loaded state — anything done
        // to the model there would be silently discarded.

        /// Deterministic lifecycle initialization on the fully hydrated
        /// loaded model: normalization must finish before revisions
        /// publish. Never mutates the model beyond the one-time concurrency
        /// seed and never requests a reconcile pass — a joining client
        /// loads the host's state and must not diverge from it (dead-pawn
        /// entries are cleaned by the first synced pass, identically on
        /// every client).
        private void FinishInit()
        {
            // A save from before the option existed that already has
            // colonists seeds at load (the same save data on every client);
            // a new game has no colonists yet and seeds on the first
            // reconcile pass that observes one.
            if (!surgeryConcurrencySeeded)
                SeedSurgeryConcurrency(ColonyScope.AllPlanableColonists(
                    ColonyScope.AuthoritativeFaction).Count);
            revisions.Bump(PlannerChange.All);
        }

        /// Drops model state of pawns that no longer exist anywhere (maps,
        /// world, caravans, transporters, a gravship in flight; alive or
        /// dead). One id set per call; the model probes it without further
        /// allocation. Deterministic for the same game state, so the
        /// reconcile pass runs it too and a joiner's loaded model converges
        /// with the host's.
        internal PlannerChange CleanupMissingPawns()
        {
            var existing = new HashSet<int>();
            List<Pawn> pawns = PawnsFinder.All_AliveOrDead;
            for (int i = 0; i < pawns.Count; i++)
                existing.Add(pawns[i].thingIDNumber);
            return Model.CleanupMissing(existing.Contains);
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
                // The unseeded sentinel survives a save so the seed still
                // happens once colonists exist.
                surgeryConcurrency = surgeryConcurrencySeeded
                    ? Model.SurgeryConcurrency
                    : 0;
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
            Scribe_Values.Look(ref pendingReconcile, "pendingReconcile", false);
            Scribe_Values.Look(ref pendingProductionPass, "pendingProductionPass", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                surgeryConcurrencySeeded = surgeryConcurrency > 0;
                Model = new PlannerModel();
                Model.SetSlotConflictResolver(ImplantConflicts.Resolver);
                // Legacy goal-id map: saves written before natural goal
                // identities persisted per-goal ids, and their reservation
                // and bill keys use the retired "i{id}:{ordinal}" format.
                // Collect id -> (plan index, kind) while loading; the index
                // is stable through NormalizeLoadedIds (which replaces in
                // place), so the final plan ids are read from it afterwards.
                Dictionary<int, (int PlanIndex, string DefName)>? legacyByIndex = null;
                if (planRecords != null)
                    foreach (PlanRecord record in planRecords)
                    {
                        Plan? plan = record.ToPlan(
                            Model.Plans.Count, ref legacyByIndex);
                        if (plan != null) Model.AddLoadedPlan(plan);
                    }
                // Saves from builds that did not persist the plan-id counter
                // (or that carry a duplicated plan id) must never reissue a
                // live id: goal keys, assignments, and base links embed plan
                // ids as identity.
                Model.NormalizeLoadedIds(ref nextPlanId);
                // Only now are plan ids final: a re-idded duplicate's legacy
                // keys must migrate onto its NEW id (GoalKeys.MigrateLegacy).
                Dictionary<int, LegacyGoalRef>? legacyGoals = null;
                if (legacyByIndex != null)
                {
                    legacyGoals = new Dictionary<int, LegacyGoalRef>(legacyByIndex.Count);
                    foreach (KeyValuePair<int, (int PlanIndex, string DefName)> pair
                        in legacyByIndex)
                        legacyGoals[pair.Key] = new LegacyGoalRef(
                            Model.Plans[pair.Value.PlanIndex].Id, pair.Value.DefName);
                }
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
                            string? key = GoalKeys.MigrateLegacy(
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
                            string? key = GoalKeys.MigrateLegacy(
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

        /// planIndex is the position the caller will load this plan into;
        /// legacy goal ids found in the record register there (the index is
        /// stable through NormalizeLoadedIds) so old keys can be migrated
        /// once plan ids are final.
        public Plan? ToPlan(int planIndex,
            ref Dictionary<int, (int PlanIndex, string DefName)>? legacyGoals)
        {
            if (id <= 0 || string.IsNullOrEmpty(name)) return null;
            var goals = new List<ImplantGoal>();
            if (implants != null)
                foreach (ImplantRecord record in implants)
                {
                    ImplantGoal? goal = record.ToGoal(id);
                    if (goal == null) continue;
                    goals.Add(goal);
                    if (record.LegacyId > 0)
                        (legacyGoals ??= new Dictionary<int, (int, string)>())
                            [record.LegacyId] = (planIndex, goal.ImplantDefName);
                }
            return new Plan(id, name, basePlanId, goals);
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
