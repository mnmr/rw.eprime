using System;
using System.Collections.Generic;
using RimShared.Common;

namespace Implanner.Core
{
    /// Domains a mutation can touch. Commands bump only the domains a
    /// mutator actually reports; no-op mutations report None.
    [Flags]
    public enum PlannerChange
    {
        None = 0,
        /// Plan structure or content: names, base links, implant goals.
        Plans = 1,
        /// Pawn-to-plan assignments.
        Assignments = 2,
        /// Per-pawn implant priorities.
        Priorities = 4,
        /// Item reservations binding stored implant items to designated pawns.
        Reservations = 16,
        /// Shared automation options (pause, iteration strategy, doctor-floor
        /// mode).
        Options = 32,
        /// Global implant star rankings.
        Rankings = 64,
        /// Surgery bookkeeping: doctor-floor high-water marks and owned
        /// operation bills.
        Surgery = 128,
        /// Production automation: options, resource reserves, and owned
        /// production-bill records.
        Production = 256,
        All = Plans | Assignments | Priorities | Reservations
            | Options | Rankings | Surgery | Production,
    }

    /// Narrow domain revisions published by the store. Consumers depend on
    /// the narrowest revision that expresses their inputs.
    public sealed class PlannerRevisions
    {
        public int Version { get; private set; }
        public int Plans { get; private set; }
        public int Assignments { get; private set; }
        public int Priorities { get; private set; }
        public int Reservations { get; private set; }
        public int Options { get; private set; }
        public int Rankings { get; private set; }
        public int Surgery { get; private set; }
        public int Production { get; private set; }

        public void Bump(PlannerChange change)
        {
            if (change == PlannerChange.None) return;
            Version = unchecked(Version + 1);
            if ((change & PlannerChange.Plans) != 0) Plans = unchecked(Plans + 1);
            if ((change & PlannerChange.Assignments) != 0) Assignments = unchecked(Assignments + 1);
            if ((change & PlannerChange.Priorities) != 0) Priorities = unchecked(Priorities + 1);
            if ((change & PlannerChange.Reservations) != 0) Reservations = unchecked(Reservations + 1);
            if ((change & PlannerChange.Options) != 0) Options = unchecked(Options + 1);
            if ((change & PlannerChange.Rankings) != 0) Rankings = unchecked(Rankings + 1);
            if ((change & PlannerChange.Surgery) != 0) Surgery = unchecked(Surgery + 1);
            if ((change & PlannerChange.Production) != 0) Production = unchecked(Production + 1);
        }
    }

    /// The authoritative Implanner model: named Plans and pawn assignments.
    /// Game-free and deterministic; mutated only through commands and
    /// deterministic store lifecycle code. Every mutator returns the exact
    /// change domains it touched, and returns None for no-ops so revisions
    /// and snapshot identity are preserved.
    public sealed class PlannerModel
    {
        /// Priority levels, ordered: 0 first … 4 last. Normal is the default
        /// and is never stored.
        public const int PriorityFirst = 0;
        public const int PriorityNormal = 2;
        public const int PriorityLast = 4;

        /// Doctor-floor skill bounds.
        public const int DoctorFloorMin = 0;
        public const int DoctorFloorMax = 20;

        /// Every implant sits at three stars until the player moves it;
        /// rankings are manual choices, never derived.
        public const int DefaultStars = 3;

        /// Production concurrency bounds and default (benches that may hold
        /// bills).
        public const int ConcurrencyMin = 1;
        public const int ConcurrencyMax = 10;
        public const int ConcurrencyDefault = 3;

        /// Default minimum crafting skill for production bills.
        public const int ProductionSkillDefault = 8;

        readonly List<Plan> plans = new List<Plan>();
        readonly Dictionary<int, int> assignments = new Dictionary<int, int>();
        readonly Dictionary<int, int> priorities = new Dictionary<int, int>();
        readonly Dictionary<int, ItemReservation> reservations = new Dictionary<int, ItemReservation>();
        readonly Dictionary<string, int> implantStars =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> implantOrder =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> doctorFloors =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> implantReserves =
            new Dictionary<string, int>(StringComparer.Ordinal);
        // Every value is a Dictionary<string, string> created by this class;
        // the read-only element type lets the map publish without copying
        // (Dictionary is invariant), and the two mutators cast back.
        readonly Dictionary<int, IReadOnlyDictionary<string, string>> ownedBills =
            new Dictionary<int, IReadOnlyDictionary<string, string>>();
        readonly Dictionary<string, int> resourceReserves =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, string> ownedProductionBills =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// Master automation switch (reservations, surgery scheduling, and
        /// production). Presentation keeps updating while paused.
        public bool AutomationPaused { get; private set; }

        /// Automation iteration strategy: one star tier at a time (the
        /// default, matching the plan editor's tier ordering), one
        /// colonist at a time (whole plan per batch), or ASAP (no batch:
        /// stock flows to the best candidate and is scheduled at once).
        public IterationStrategy Iteration { get; private set; } =
            IterationStrategy.ImplantTier;

        /// A strategy outside the enum (a save or client from a build with
        /// other strategies) falls back to the default.
        static IterationStrategy NormalizeIteration(IterationStrategy iteration) =>
            iteration == IterationStrategy.Colonist || iteration == IterationStrategy.Asap
                ? iteration
                : IterationStrategy.ImplantTier;

        /// Player-set minimum Medical skill for Implanner operations (0–20).
        /// Applies only while the automatic floor is off.
        public int ManualDoctorFloor { get; private set; }

        /// Whether the automatic doctor-skill floor (per-colony high-water
        /// mark of the best eligible Medical skill) is active. On by default.
        public bool AutoDoctorFloor { get; private set; } = true;

        /// How many colonists per colony may have Implanner surgeries
        /// planned at once (1–20). Seeded at first init from colony size:
        /// max(1, colonists / 10).
        public const int SurgeryConcurrencyMin = 1;
        public const int SurgeryConcurrencyMax = 20;
        public int SurgeryConcurrency { get; private set; } = 1;

        public PlannerChange SetSurgeryConcurrency(int colonists)
        {
            colonists = ClampSurgeryConcurrency(colonists);
            if (SurgeryConcurrency == colonists) return PlannerChange.None;
            SurgeryConcurrency = colonists;
            return PlannerChange.Options;
        }

        static int ClampSurgeryConcurrency(int colonists) =>
            colonists < SurgeryConcurrencyMin ? SurgeryConcurrencyMin
            : colonists > SurgeryConcurrencyMax ? SurgeryConcurrencyMax
            : colonists;

        /// Whether pawns lying in medical beds or downed awaiting treatment
        /// occupy concurrent-surgery slots too, so new Implanner surgeries
        /// wait while the hospital is busy. On by default.
        public bool CountHospitalized { get; private set; } = true;

        public PlannerChange SetCountHospitalized(bool enabled)
        {
            if (CountHospitalized == enabled) return PlannerChange.None;
            CountHospitalized = enabled;
            return PlannerChange.Options;
        }

        /// Whether missing implant items get crafting bills automatically.
        /// On by default: automation works out of the box.
        public bool AutoProduction { get; private set; } = true;

        /// How many benches per colony may hold Implanner production bills.
        public int ProductionConcurrency { get; private set; } = ConcurrencyDefault;

        /// Whether production bills may only be created at benches that
        /// currently hold no bills at all. On by default.
        public bool OnlyIdleBenches { get; private set; } = true;

        /// Minimum crafting skill Implanner production bills demand (0–20).
        public int ProductionSkill { get; private set; } = ProductionSkillDefault;

        /// Whether a production bill blocked by an ingredient shortfall may
        /// spawn a bill for the missing craftable ingredient itself. On by
        /// default.
        public bool AllowIntermediaries { get; private set; } = true;

        public IReadOnlyList<Plan> Plans => plans;

        /// Pawn id (stable per-save thing id number) to plan id.
        public IReadOnlyDictionary<int, int> Assignments => assignments;

        /// Pawn id to non-default priority level.
        public IReadOnlyDictionary<int, int> Priorities => priorities;

        public int PriorityOf(int pawnId) =>
            priorities.TryGetValue(pawnId, out int level) ? level : PriorityNormal;

        /// Sets a pawn's implant priority (0 first … 4 last). Normal clears
        /// the stored entry so defaults never accumulate.
        public PlannerChange SetPawnPriority(int pawnId, int level)
        {
            if (level < PriorityFirst) level = PriorityFirst;
            if (level > PriorityLast) level = PriorityLast;
            if (PriorityOf(pawnId) == level) return PlannerChange.None;
            if (level == PriorityNormal)
                priorities.Remove(pawnId);
            else
                priorities[pawnId] = level;
            return PlannerChange.Priorities;
        }

        /// Deterministic load path: restores one priority entry.
        public void AddLoadedPriority(int pawnId, int level) =>
            priorities[pawnId] = level;

        /// Deterministic load normalization. Plan ids are identity (goal
        /// keys, assignments, and base links embed them), so they must stay
        /// unique per save. Saves written before the id counter was
        /// persisted load with the counter default below existing ids;
        /// without this, the next created plan reuses a live id. Clamps the
        /// counter above every loaded id, then re-ids any duplicated plan
        /// (first occurrence keeps its id, so stale references resolve to
        /// one deterministic owner); a re-idded plan's goals are restamped
        /// with the new owning id.
        public void NormalizeLoadedIds(ref int nextPlanId)
        {
            for (int p = 0; p < plans.Count; p++)
                if (plans[p].Id >= nextPlanId) nextPlanId = plans[p].Id + 1;
            var seenPlanIds = new HashSet<int>();
            for (int p = 0; p < plans.Count; p++)
            {
                Plan plan = plans[p];
                if (seenPlanIds.Add(plan.Id)) continue;
                int newId = nextPlanId++;
                var goals = new List<ImplantGoal>(plan.Implants.Count);
                for (int i = 0; i < plan.Implants.Count; i++)
                {
                    ImplantGoal goal = plan.Implants[i];
                    goals.Add(new ImplantGoal(
                        newId, goal.ImplantDefName, goal.SlotOrdinals));
                }
                plans[p] = new Plan(newId, plan.Name, plan.BasePlanId, goals);
            }
        }

        // -------------------------------------------------- Reservations

        /// Item id (stable per-save thing id number) to its logical
        /// reservation. Never vanilla reservations or item-side flags.
        public IReadOnlyDictionary<int, ItemReservation> Reservations => reservations;

        public bool TryGetReservation(int itemId, out ItemReservation reservation) =>
            reservations.TryGetValue(itemId, out reservation);

        /// Reserves an item for a pawn's goal. Deterministic reconcile path.
        public PlannerChange Reserve(int itemId, int pawnId, string goalKey)
        {
            if (reservations.TryGetValue(itemId, out var existing)
                && existing.PawnId == pawnId
                && string.Equals(existing.GoalKey, goalKey, StringComparison.Ordinal))
                return PlannerChange.None;
            reservations[itemId] = new ItemReservation(pawnId, goalKey);
            return PlannerChange.Reservations;
        }

        public PlannerChange ReleaseReservation(int itemId) =>
            reservations.Remove(itemId)
                ? PlannerChange.Reservations
                : PlannerChange.None;

        /// Deterministic load path: restores one reservation.
        public void AddLoadedReservation(int itemId, int pawnId, string goalKey) =>
            reservations[itemId] = new ItemReservation(pawnId, goalKey);

        // ------------------------------------------------------- Options

        public PlannerChange SetAutomationPaused(bool paused)
        {
            if (AutomationPaused == paused) return PlannerChange.None;
            AutomationPaused = paused;
            return PlannerChange.Options;
        }

        /// The synced command carries the strategy as a plain int, so a
        /// value outside the enum normalizes to the default exactly like
        /// LoadOptions before the no-op comparison.
        public PlannerChange SetIteration(IterationStrategy iteration)
        {
            iteration = NormalizeIteration(iteration);
            if (Iteration == iteration) return PlannerChange.None;
            Iteration = iteration;
            return PlannerChange.Options;
        }

        public PlannerChange SetManualDoctorFloor(int level)
        {
            if (level < DoctorFloorMin) level = DoctorFloorMin;
            if (level > DoctorFloorMax) level = DoctorFloorMax;
            if (ManualDoctorFloor == level) return PlannerChange.None;
            ManualDoctorFloor = level;
            return PlannerChange.Options;
        }

        public PlannerChange SetAutoDoctorFloor(bool enabled)
        {
            if (AutoDoctorFloor == enabled) return PlannerChange.None;
            AutoDoctorFloor = enabled;
            return PlannerChange.Options;
        }

        /// Deterministic load path. Persisted values clamp exactly like the
        /// setters; an iteration value outside the enum (a save from a
        /// build with other strategies) falls back to the default.
        public void LoadOptions(bool automationPaused,
            IterationStrategy iteration, int manualDoctorFloor, bool autoDoctorFloor,
            int surgeryConcurrency, bool countHospitalized,
            bool autoProduction, int productionConcurrency,
            bool onlyIdleBenches, int productionSkill, bool allowIntermediaries)
        {
            AutomationPaused = automationPaused;
            Iteration = NormalizeIteration(iteration);
            ManualDoctorFloor = manualDoctorFloor < DoctorFloorMin ? DoctorFloorMin
                : manualDoctorFloor > DoctorFloorMax ? DoctorFloorMax
                : manualDoctorFloor;
            AutoDoctorFloor = autoDoctorFloor;
            SurgeryConcurrency = ClampSurgeryConcurrency(surgeryConcurrency);
            CountHospitalized = countHospitalized;
            AutoProduction = autoProduction;
            ProductionConcurrency = ClampConcurrency(productionConcurrency);
            OnlyIdleBenches = onlyIdleBenches;
            ProductionSkill = ClampSkill(productionSkill);
            AllowIntermediaries = allowIntermediaries;
        }

        // ---------------------------------------------------- Star rankings

        /// Global implant star rankings keyed by HediffDef name; the entry is
        /// absent while the implant sits at the three-star default. The star
        /// tiers are the implant families: one surgery batch per tier. Stars
        /// are a manual preference order the player edits by dragging — the
        /// mod never derives or overwrites them.
        public IReadOnlyDictionary<string, int> ImplantStars => implantStars;

        public int ImplantStarsOf(string defName) =>
            defName != null && implantStars.TryGetValue(defName, out int stars)
                ? stars
                : DefaultStars;

        /// Ranks an implant kind (stars 1–5, clamped). The default tier is
        /// stored sparsely: moving back to three stars removes the entry.
        public PlannerChange SetImplantStars(string defName, int stars)
        {
            if (string.IsNullOrEmpty(defName)) return PlannerChange.None;
            stars = stars < 1 ? 1 : stars > StarRanking.Max ? StarRanking.Max : stars;
            if (ImplantStarsOf(defName) == stars) return PlannerChange.None;
            if (stars == DefaultStars)
                implantStars.Remove(defName);
            else
                implantStars[defName] = stars;
            return PlannerChange.Rankings;
        }

        /// Deterministic load path: restores one implant ranking.
        public void AddLoadedImplantStars(string defName, int stars)
        {
            stars = stars < 1 ? 1 : stars > StarRanking.Max ? StarRanking.Max : stars;
            if (stars != DefaultStars) implantStars[defName] = stars;
        }

        /// Explicit positions within a star tier, keyed by HediffDef name.
        /// Unordered kinds sort after ordered ones by defName; consumers
        /// order by (ImplantOrderOf, defName).
        public IReadOnlyDictionary<string, int> ImplantOrder => implantOrder;

        public int ImplantOrderOf(string defName) =>
            defName != null && implantOrder.TryGetValue(defName, out int order)
                ? order
                : int.MaxValue;

        /// Publishes one tier's complete order: every listed def takes the
        /// tier's stars and its list index. The game-side move command
        /// materializes the list deterministically from the catalog, so
        /// every multiplayer client applies the identical sequence.
        public PlannerChange ApplyTierOrder(int stars, IReadOnlyList<string> orderedDefs)
        {
            var change = PlannerChange.None;
            for (int i = 0; i < orderedDefs.Count; i++)
            {
                string defName = orderedDefs[i];
                if (string.IsNullOrEmpty(defName)) continue;
                change |= SetImplantStars(defName, stars);
                if (ImplantOrderOf(defName) != i)
                {
                    implantOrder[defName] = i;
                    change |= PlannerChange.Rankings;
                }
            }
            return change;
        }

        /// Deterministic load path: restores one order entry. Any value is
        /// a valid position (consumers only compare positions), so nothing
        /// is normalized; a stale entry for a kind no longer in its tier is
        /// harmless and gets overwritten by the next ApplyTierOrder.
        public void AddLoadedImplantOrder(string defName, int order) =>
            implantOrder[defName] = order;

        // ------------------------------------------------- Doctor floors

        /// The current best eligible Medical skill per colony identity
        /// (serviceable location id: settlement map or gravship), refreshed
        /// by automation; entries exist only while a colony has one.
        public IReadOnlyDictionary<string, int> DoctorFloors => doctorFloors;

        public int DoctorFloorOf(string colonyId) =>
            doctorFloors.TryGetValue(colonyId, out int floor) ? floor : 0;

        /// The floor Implanner operations enforce at a colony: the current
        /// best doctor while the automatic mode is on, the player's manual
        /// minimum otherwise.
        public int EffectiveDoctorFloor(string colonyId) =>
            AutoDoctorFloor ? DoctorFloorOf(colonyId) : ManualDoctorFloor;

        /// Publishes a colony's current best doctor skill — up, down, or
        /// gone (zero removes the entry). Deterministic reconcile path.
        public PlannerChange SetDoctorFloor(string colonyId, int skill)
        {
            if (string.IsNullOrEmpty(colonyId)) return PlannerChange.None;
            if (skill < DoctorFloorMin) skill = DoctorFloorMin;
            if (skill > DoctorFloorMax) skill = DoctorFloorMax;
            if (DoctorFloorOf(colonyId) == skill) return PlannerChange.None;
            if (skill == 0)
                doctorFloors.Remove(colonyId);
            else
                doctorFloors[colonyId] = skill;
            return PlannerChange.Surgery;
        }

        /// Drops floors of colonies not in the live set. Deterministic; takes
        /// the set directly so the tick path allocates no delegate.
        public PlannerChange PruneDoctorFloors(HashSet<string> liveColonies)
        {
            List<string>? dead = null;
            foreach (KeyValuePair<string, int> pair in doctorFloors)
                if (!liveColonies.Contains(pair.Key))
                    (dead ??= new List<string>()).Add(pair.Key);
            if (dead == null) return PlannerChange.None;
            dead.Sort(StringComparer.Ordinal);
            for (int i = 0; i < dead.Count; i++)
                doctorFloors.Remove(dead[i]);
            return PlannerChange.Surgery;
        }

        /// Deterministic load path: restores one floor entry, normalized
        /// like SetDoctorFloor (clamped; zero stores nothing).
        public void AddLoadedDoctorFloor(string colonyId, int floor)
        {
            if (floor > DoctorFloorMax) floor = DoctorFloorMax;
            if (floor > 0) doctorFloors[colonyId] = floor;
        }

        // ------------------------------------------- Implant reservations

        /// Implant items held back for the player, keyed by implant
        /// HediffDef name: surgery automation may only reserve stock while
        /// at least this many of the implant's item stay available for
        /// manual use. Absent = 0 (automation may take everything).
        public IReadOnlyDictionary<string, int> ImplantReserves => implantReserves;

        public int ImplantReserveOf(string defName) =>
            defName != null && implantReserves.TryGetValue(defName, out int count)
                ? count
                : 0;

        /// Sets an implant's held-back count; 0 removes the entry.
        public PlannerChange SetImplantReserve(string defName, int count)
        {
            if (string.IsNullOrEmpty(defName)) return PlannerChange.None;
            if (count < 0) count = 0;
            if (ImplantReserveOf(defName) == count) return PlannerChange.None;
            if (count == 0)
                implantReserves.Remove(defName);
            else
                implantReserves[defName] = count;
            return PlannerChange.Surgery;
        }

        /// Deterministic load path: restores one implant-reserve entry.
        public void AddLoadedImplantReserve(string defName, int count)
        {
            if (count > 0) implantReserves[defName] = count;
        }

        // --------------------------------------------------- Owned bills

        /// Implanner-owned operation bills per pawn: goal key to the bill's
        /// stable unique load id. Only bookkeeping — the bill object itself
        /// is owned by the game.
        public IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> OwnedBills =>
            ownedBills;

        public IReadOnlyDictionary<string, string>? OwnedBillsFor(int pawnId) =>
            ownedBills.TryGetValue(pawnId, out var bills) ? bills : null;

        public string? OwnedBill(int pawnId, string goalKey) =>
            ownedBills.TryGetValue(pawnId, out var bills)
                && bills.TryGetValue(goalKey, out string billId)
                ? billId
                : null;

        Dictionary<string, string> OwnedBillsOrCreate(int pawnId)
        {
            if (ownedBills.TryGetValue(pawnId, out var bills))
                return (Dictionary<string, string>)bills;
            var created = new Dictionary<string, string>(StringComparer.Ordinal);
            ownedBills.Add(pawnId, created);
            return created;
        }

        /// Records a scheduled Implanner operation. Deterministic reconcile path.
        public PlannerChange SetOwnedBill(int pawnId, string goalKey, string billId)
        {
            Dictionary<string, string> bills = OwnedBillsOrCreate(pawnId);
            if (bills.TryGetValue(goalKey, out string existing)
                && string.Equals(existing, billId, StringComparison.Ordinal))
                return PlannerChange.None;
            bills[goalKey] = billId;
            return PlannerChange.Surgery;
        }

        public PlannerChange RemoveOwnedBill(int pawnId, string goalKey)
        {
            if (!ownedBills.TryGetValue(pawnId, out var bills)
                || !((Dictionary<string, string>)bills).Remove(goalKey))
                return PlannerChange.None;
            if (bills.Count == 0) ownedBills.Remove(pawnId);
            return PlannerChange.Surgery;
        }

        /// Deterministic load path: restores one owned-bill entry.
        public void AddLoadedOwnedBill(int pawnId, string goalKey, string billId) =>
            OwnedBillsOrCreate(pawnId)[goalKey] = billId;

        // ---------------------------------------------------- Production

        /// Baseline keep-in-stock reserves for the common implant
        /// ingredients, applied until the player overrides them. Keyed by
        /// ThingDef name; anything unlisted defaults to zero. Published so
        /// the options UI can always list these resources even when no
        /// currently craftable implant consumes them.
        public static IReadOnlyDictionary<string, int> DefaultResourceReserves =>
            DefaultReserves;

        static readonly Dictionary<string, int> DefaultReserves =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "ComponentSpacer", 5 },
                { "ComponentIndustrial", 20 },
                { "Gold", 100 },
                { "Plasteel", 500 },
                { "Steel", 2000 },
            };

        /// Player overrides of the default reserves (stored sparsely: only
        /// values that differ from the baseline).
        public IReadOnlyDictionary<string, int> ResourceReserves => resourceReserves;

        /// Minimum stock the colony keeps of a resource: a production bill is
        /// created only when the ingredient stock minus the bill's full cost
        /// stays at or above the reserve.
        public int ResourceReserveOf(string defName)
        {
            if (defName == null) return 0;
            if (resourceReserves.TryGetValue(defName, out int amount)) return amount;
            return DefaultReserveOf(defName);
        }

        static int DefaultReserveOf(string defName) =>
            DefaultReserves.TryGetValue(defName, out int amount) ? amount : 0;

        public PlannerChange SetAutoProduction(bool enabled)
        {
            if (AutoProduction == enabled) return PlannerChange.None;
            AutoProduction = enabled;
            return PlannerChange.Production;
        }

        public PlannerChange SetProductionConcurrency(int benches)
        {
            benches = ClampConcurrency(benches);
            if (ProductionConcurrency == benches) return PlannerChange.None;
            ProductionConcurrency = benches;
            return PlannerChange.Production;
        }

        public PlannerChange SetOnlyIdleBenches(bool enabled)
        {
            if (OnlyIdleBenches == enabled) return PlannerChange.None;
            OnlyIdleBenches = enabled;
            return PlannerChange.Production;
        }

        public PlannerChange SetProductionSkill(int level)
        {
            level = ClampSkill(level);
            if (ProductionSkill == level) return PlannerChange.None;
            ProductionSkill = level;
            return PlannerChange.Production;
        }

        public PlannerChange SetAllowIntermediaries(bool enabled)
        {
            if (AllowIntermediaries == enabled) return PlannerChange.None;
            AllowIntermediaries = enabled;
            return PlannerChange.Production;
        }

        /// Sets a resource's keep-in-stock reserve; matching the baseline
        /// default removes the override so defaults never accumulate.
        public PlannerChange SetResourceReserve(string defName, int amount)
        {
            if (string.IsNullOrEmpty(defName)) return PlannerChange.None;
            if (amount < 0) amount = 0;
            if (ResourceReserveOf(defName) == amount) return PlannerChange.None;
            if (amount == DefaultReserveOf(defName))
                resourceReserves.Remove(defName);
            else
                resourceReserves[defName] = amount;
            return PlannerChange.Production;
        }

        static int ClampConcurrency(int benches) =>
            benches < ConcurrencyMin ? ConcurrencyMin
                : benches > ConcurrencyMax ? ConcurrencyMax
                : benches;

        static int ClampSkill(int level) =>
            level < DoctorFloorMin ? DoctorFloorMin
                : level > DoctorFloorMax ? DoctorFloorMax
                : level;

        /// Deterministic load path: restores one reserve override (which may
        /// be an explicit zero overriding a baseline default).
        public void AddLoadedResourceReserve(string defName, int amount)
        {
            if (amount < 0) amount = 0;
            if (amount != DefaultReserveOf(defName))
                resourceReserves[defName] = amount;
        }

        /// Implanner-owned production bills: the bill's stable unique load id
        /// to the implant item ThingDef name it produces. Only bookkeeping —
        /// the bill object itself is owned by the game.
        public IReadOnlyDictionary<string, string> OwnedProductionBills =>
            ownedProductionBills;

        public PlannerChange SetOwnedProductionBill(string billId, string itemDefName)
        {
            if (string.IsNullOrEmpty(billId) || string.IsNullOrEmpty(itemDefName))
                return PlannerChange.None;
            if (ownedProductionBills.TryGetValue(billId, out string existing)
                && string.Equals(existing, itemDefName, StringComparison.Ordinal))
                return PlannerChange.None;
            ownedProductionBills[billId] = itemDefName;
            return PlannerChange.Production;
        }

        public PlannerChange RemoveOwnedProductionBill(string billId) =>
            billId != null && ownedProductionBills.Remove(billId)
                ? PlannerChange.Production
                : PlannerChange.None;

        /// Deterministic load path: restores one production-bill record.
        public void AddLoadedProductionBill(string billId, string itemDefName) =>
            ownedProductionBills[billId] = itemDefName;

        // ---------------------------------------------------------- Plans

        public Plan? PlanById(int planId)
        {
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].Id == planId)
                    return plans[i];
            return null;
        }

        public Plan? AssignedPlan(int pawnId) =>
            assignments.TryGetValue(pawnId, out int planId) ? PlanById(planId) : null;

        /// Creates a plan, optionally extending an existing one. A missing
        /// base id is dropped rather than failing the creation.
        public Plan? CreatePlan(string? preferredName, Func<int> takePlanId,
            int basePlanId = 0)
        {
            string? name = CatalogNameRules.Unique(preferredName, plans, PlanNameOf);
            if (name == null) return null;
            var plan = new Plan(takePlanId(), name);
            if (basePlanId != 0 && PlanById(basePlanId) != null)
                plan.BasePlanId = basePlanId;
            plans.Add(plan);
            return plan;
        }

        static readonly Func<Plan, string> PlanNameOf = p => p.Name;

        public PlannerChange RenamePlan(int planId, string? newName)
        {
            var plan = PlanById(planId);
            if (plan == null) return PlannerChange.None;
            string? name = newName?.Trim();
            if (string.IsNullOrEmpty(name)
                || string.Equals(plan.Name, name, StringComparison.Ordinal))
                return PlannerChange.None;
            if (!CatalogNameRules.IsAvailable(name, plans, PlanNameOf, plan))
                return PlannerChange.None;
            plan.Name = name!;
            return PlannerChange.Plans;
        }

        /// Deleting a plan detaches every plan that extended it: the derived
        /// plans keep their own goals and simply stop inheriting.
        public PlannerChange DeletePlan(int planId)
        {
            var plan = PlanById(planId);
            if (plan == null) return PlannerChange.None;
            plans.Remove(plan);
            var change = PlannerChange.Plans;
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].BasePlanId == planId)
                    plans[i].BasePlanId = 0;
            var orphaned = new List<int>();
            foreach (var pair in assignments)
                if (pair.Value == planId)
                    orphaned.Add(pair.Key);
            for (int i = 0; i < orphaned.Count; i++)
                assignments.Remove(orphaned[i]);
            if (orphaned.Count > 0) change |= PlannerChange.Assignments;
            return change;
        }

        /// Answers whether two planned slots can never coexist on a pawn
        /// (same part, replacement subtrees, incompatible hediff tags).
        /// Injected by the game layer from definition data, so it is
        /// identical on every multiplayer client; null disables conflict
        /// suppression (Core tests exercise it explicitly).
        public Func<ImplantGoal, int, ImplantGoal, int, bool>? SlotConflictResolver
        {
            get;
            private set;
        }

        /// Wiring, not a mutation: the resolver is definition-derived and
        /// set once when the store is constructed or hydrated.
        public void SetSlotConflictResolver(
            Func<ImplantGoal, int, ImplantGoal, int, bool>? resolver) =>
            SlotConflictResolver = resolver;

        /// The plan's effective implant goals: its own goals first, then the
        /// base chain's, with an own selection overriding a base goal's
        /// overlapping slots (per implant kind) and suppressing inherited
        /// slots it conflicts with (a derived plan choosing a different
        /// stomach wins over the base's). A base goal whose every slot is
        /// overridden disappears; a partially overridden goal keeps its id
        /// with the remaining slots, so its goal keys stay stable. Base links
        /// are set only at creation, so the chain cannot cycle; the visited
        /// guard is cheap defense against corrupted data. A plan without a
        /// base has nothing to merge or suppress and answers with its own
        /// (read-only) goal list, so the common case allocates nothing.
        public IReadOnlyList<ImplantGoal> EffectiveImplants(Plan plan)
        {
            if (plan.BasePlanId == 0) return plan.Implants;
            var result = new List<ImplantGoal>(plan.Implants);
            var covered = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Implants.Count; i++)
                CoverSlots(covered, plan.Implants[i]);

            var visited = new HashSet<int> { plan.Id };
            int baseId = plan.BasePlanId;
            while (baseId != 0 && visited.Add(baseId))
            {
                var basePlan = PlanById(baseId);
                if (basePlan == null) break;
                for (int i = 0; i < basePlan.Implants.Count; i++)
                {
                    ImplantGoal goal = basePlan.Implants[i];
                    List<int>? remaining = null;
                    bool allRemain = true;
                    for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                    {
                        int ordinal = goal.SlotOrdinals[j];
                        if ((covered.TryGetValue(goal.ImplantDefName, out var taken)
                                && taken.Contains(ordinal))
                            || ConflictsWithAccepted(result, goal, ordinal))
                        {
                            allRemain = false;
                            continue;
                        }
                        (remaining ??= new List<int>()).Add(ordinal);
                    }
                    if (remaining == null) continue;
                    ImplantGoal effective = allRemain
                        ? goal
                        : new ImplantGoal(goal.PlanId, goal.ImplantDefName, remaining);
                    result.Add(effective);
                    CoverSlots(covered, effective);
                }
                baseId = basePlan.BasePlanId;
            }
            return result;
        }

        bool ConflictsWithAccepted(
            List<ImplantGoal> accepted, ImplantGoal goal, int ordinal)
        {
            var resolver = SlotConflictResolver;
            if (resolver == null) return false;
            for (int i = 0; i < accepted.Count; i++)
            {
                ImplantGoal other = accepted[i];
                for (int j = 0; j < other.SlotOrdinals.Count; j++)
                    if (resolver(other, other.SlotOrdinals[j], goal, ordinal))
                        return true;
            }
            return false;
        }

        static void CoverSlots(
            Dictionary<string, HashSet<int>> covered, ImplantGoal goal)
        {
            if (!covered.TryGetValue(goal.ImplantDefName, out var set))
            {
                set = new HashSet<int>();
                covered.Add(goal.ImplantDefName, set);
            }
            for (int i = 0; i < goal.SlotOrdinals.Count; i++)
                set.Add(goal.SlotOrdinals[i]);
        }

        /// Removes an implant goal with all its selected slots.
        public PlannerChange RemoveImplant(int planId, string implantDefName)
        {
            var plan = PlanById(planId);
            if (plan == null || string.IsNullOrEmpty(implantDefName))
                return PlannerChange.None;
            for (int i = 0; i < plan.Implants.Count; i++)
            {
                if (!string.Equals(plan.Implants[i].ImplantDefName,
                        implantDefName, StringComparison.Ordinal))
                    continue;
                plan.MutableImplants.RemoveAt(i);
                return PlannerChange.Plans;
            }
            return PlannerChange.None;
        }

        /// Toggles one anatomy slot of an implant goal. Removing the last
        /// selected slot removes the goal; adding a slot first deselects any
        /// of the plan's own slots it can never coexist with (the click IS
        /// the choice — the previous pick is not kept as a dead goal).
        public PlannerChange SetImplantSlot(int planId, string implantDefName,
            int slotOrdinal, bool wanted)
        {
            var plan = PlanById(planId);
            if (plan == null || string.IsNullOrEmpty(implantDefName) || slotOrdinal < 0)
                return PlannerChange.None;
            for (int i = 0; i < plan.Implants.Count; i++)
            {
                var existing = plan.Implants[i];
                if (!string.Equals(existing.ImplantDefName, implantDefName, StringComparison.Ordinal))
                    continue;
                bool has = ContainsOrdinal(existing.SlotOrdinals, slotOrdinal);
                if (has == wanted) return PlannerChange.None;
                var ordinals = new List<int>(existing.SlotOrdinals);
                if (wanted)
                {
                    ordinals.Add(slotOrdinal);
                    ordinals.Sort();
                }
                else
                {
                    ordinals.Remove(slotOrdinal);
                }
                if (ordinals.Count == 0)
                    plan.MutableImplants.RemoveAt(i);
                else
                    plan.MutableImplants[i] = new ImplantGoal(plan.Id, implantDefName, ordinals);
                if (wanted)
                    RemoveConflictingSlots(plan, implantDefName, slotOrdinal);
                return PlannerChange.Plans;
            }
            if (!wanted) return PlannerChange.None;
            plan.MutableImplants.Add(new ImplantGoal(
                plan.Id, implantDefName, new List<int> { slotOrdinal }));
            RemoveConflictingSlots(plan, implantDefName, slotOrdinal);
            return PlannerChange.Plans;
        }

        /// Deselects every own slot of another implant kind that can never
        /// coexist with the newly selected slot; a goal losing its last slot
        /// disappears. Reverse iteration keeps removal order-safe.
        void RemoveConflictingSlots(Plan plan, string implantDefName, int slotOrdinal)
        {
            var resolver = SlotConflictResolver;
            if (resolver == null) return;
            var added = new ImplantGoal(plan.Id, implantDefName, new[] { slotOrdinal });
            for (int i = plan.Implants.Count - 1; i >= 0; i--)
            {
                ImplantGoal other = plan.Implants[i];
                if (string.Equals(other.ImplantDefName, implantDefName,
                        StringComparison.Ordinal))
                    continue;
                List<int>? surviving = null;
                bool anyRemoved = false;
                for (int j = 0; j < other.SlotOrdinals.Count; j++)
                {
                    int ordinal = other.SlotOrdinals[j];
                    if (resolver(added, slotOrdinal, other, ordinal))
                    {
                        anyRemoved = true;
                        continue;
                    }
                    (surviving ??= new List<int>()).Add(ordinal);
                }
                if (!anyRemoved) continue;
                if (surviving == null)
                    plan.MutableImplants.RemoveAt(i);
                else
                    plan.MutableImplants[i] = new ImplantGoal(
                        other.PlanId, other.ImplantDefName, surviving);
            }
        }

        static bool ContainsOrdinal(IReadOnlyList<int> ordinals, int ordinal)
        {
            for (int i = 0; i < ordinals.Count; i++)
                if (ordinals[i] == ordinal)
                    return true;
            return false;
        }

        /// Assigns or clears (planId 0) a pawn's Plan.
        public PlannerChange AssignPlan(int pawnId, int planId)
        {
            if (planId == 0)
            {
                return assignments.Remove(pawnId)
                    ? PlannerChange.Assignments
                    : PlannerChange.None;
            }
            if (PlanById(planId) == null) return PlannerChange.None;
            if (assignments.TryGetValue(pawnId, out int current) && current == planId)
                return PlannerChange.None;
            assignments[pawnId] = planId;
            return PlannerChange.Assignments;
        }

        /// Load-time cleanup: drops assignments to missing plans and pawns
        /// that no longer exist, detaches plans whose base plan is gone,
        /// releases reservations of unassigned or missing pawns, and drops
        /// owned-bill records only for pawns that no longer exist.
        /// Deterministic for the same inputs.
        public PlannerChange CleanupMissing(Func<int, bool> pawnExists)
        {
            var change = PlannerChange.None;
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].BasePlanId != 0
                    && PlanById(plans[i].BasePlanId) == null)
                {
                    plans[i].BasePlanId = 0;
                    change |= PlannerChange.Plans;
                }

            var dead = new List<int>();
            foreach (var pair in assignments)
                if (PlanById(pair.Value) == null || !pawnExists(pair.Key))
                    dead.Add(pair.Key);
            dead.Sort();
            for (int i = 0; i < dead.Count; i++)
                assignments.Remove(dead[i]);
            if (dead.Count > 0) change |= PlannerChange.Assignments;

            dead.Clear();
            foreach (var pair in priorities)
                if (pair.Value == PriorityNormal || !pawnExists(pair.Key))
                    dead.Add(pair.Key);
            dead.Sort();
            for (int i = 0; i < dead.Count; i++)
                priorities.Remove(dead[i]);
            if (dead.Count > 0) change |= PlannerChange.Priorities;

            // Reservations follow their pawn's existence and assignment;
            // the reconciler owns finer-grained lifecycle.
            dead.Clear();
            foreach (var pair in reservations)
                if (!pawnExists(pair.Value.PawnId)
                    || !assignments.ContainsKey(pair.Value.PawnId))
                    dead.Add(pair.Key);
            dead.Sort();
            for (int i = 0; i < dead.Count; i++)
                reservations.Remove(dead[i]);
            if (dead.Count > 0) change |= PlannerChange.Reservations;

            // Owned-bill records are the only link to the Bill_Medical
            // objects the game keeps on the pawn. Records of a pawn that
            // still exists stay even without an assignment: the reconcile
            // sweep deletes the bill object and the record together, while
            // dropping the record here would strand the bill on the pawn.
            dead.Clear();
            foreach (var pair in ownedBills)
                if (!pawnExists(pair.Key))
                    dead.Add(pair.Key);
            dead.Sort();
            for (int i = 0; i < dead.Count; i++)
                ownedBills.Remove(dead[i]);
            if (dead.Count > 0) change |= PlannerChange.Surgery;
            return change;
        }

        /// Deterministic load/import path: adds a fully hydrated plan.
        public void AddLoadedPlan(Plan plan) => plans.Add(plan);

        /// Additive import of a PlansXml payload: appends the parsed plans
        /// with fresh ids, uniquifying names against existing plans
        /// (CatalogNameRules), and remapping the payload's TEMPORARY base
        /// link ids (see PlansXml's temp-id contract) onto the new ids.
        /// Validates before the first mutation; invalid input returns None
        /// with the model untouched — never a half-applied payload. A plan
        /// whose name cannot be uniquified is skipped (its dependents lose
        /// their base link, matching plan deletion), not a failure.
        /// Deterministic for identical input and allocator state.
        public PlannerChange ImportPlans(List<Plan> parsed, Func<int> takePlanId)
        {
            if (parsed == null || parsed.Count == 0) return PlannerChange.None;

            // Pre-validate defensively (TryImport already guarantees this):
            // every parsed plan must exist and carry a non-blank name.
            for (int i = 0; i < parsed.Count; i++)
                if (parsed[i] == null
                    || string.IsNullOrEmpty(parsed[i].Name?.Trim()))
                    return PlannerChange.None;

            // First pass: create real plans and goals; record temp → real.
            var tempToReal = new Dictionary<int, int>();
            var added = new List<Plan>();
            var addedBaseTempIds = new List<int>();
            for (int i = 0; i < parsed.Count; i++)
            {
                Plan source = parsed[i];
                string? name = CatalogNameRules.Unique(source.Name, plans, PlanNameOf);
                if (name == null) continue; // cannot uniquify → skip, not fail
                int planId = takePlanId();
                var goals = new List<ImplantGoal>(source.Implants.Count);
                for (int g = 0; g < source.Implants.Count; g++)
                {
                    ImplantGoal goal = source.Implants[g];
                    goals.Add(new ImplantGoal(
                        planId, goal.ImplantDefName,
                        new List<int>(goal.SlotOrdinals)));
                }
                var plan = new Plan(planId, name, 0, goals);
                plans.Add(plan);
                added.Add(plan);
                addedBaseTempIds.Add(source.BasePlanId);
                tempToReal[source.Id] = plan.Id;
            }

            // Second pass: remap base links. A temp base id whose plan was
            // skipped resolves to 0 (no base).
            for (int i = 0; i < added.Count; i++)
            {
                int baseTempId = addedBaseTempIds[i];
                if (baseTempId != 0
                    && tempToReal.TryGetValue(baseTempId, out int realBaseId))
                    added[i].BasePlanId = realBaseId;
            }

            return added.Count > 0 ? PlannerChange.Plans : PlannerChange.None;
        }

        /// Deterministic load path: restores one assignment.
        public void AddLoadedAssignment(int pawnId, int planId) =>
            assignments[pawnId] = planId;
    }
}
