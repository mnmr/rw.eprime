using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Per-save authoritative store (spec §4). Presence in Game.components IS
    /// the enabled flag (spec §12): absent component = mod inert.
    ///
    /// Cache/store contract — Owner: Game (per save). Key: entries by UFT
    /// reference; construction plans by target Thing reference; configs by
    /// bill loadID string; caps by product defName. Value:
    /// mutable authoritative state, mutated only in ticking or synced commands.
    /// Dependencies: game state consumed during the 250-tick scan. Refresh:
    /// ScanInterval game ticks. Equality: command setters compare before
    /// writing (no-op edits change nothing). Teardown: component dies with the
    /// Game; Active property re-resolves per call so no static leaks worlds.
    public class QualityJobsStore : GameComponent
    {
        public const int ScanInterval = 250;

        public List<WorkItemEntry> entries = new List<WorkItemEntry>();
        public List<ConstructionPlan> plans = new List<ConstructionPlan>();
        // Keys are bill.GetUniqueLoadID() strings (bill.loadID is private).
        public Dictionary<string, bool> billManaged = new Dictionary<string, bool>();
        public Dictionary<string, int> billMinSkill = new Dictionary<string, int>();
        public Dictionary<string, bool> billRequireInspired = new Dictionary<string, bool>();
        public Dictionary<string, bool> billRequireSpecialist = new Dictionary<string, bool>();
        public Dictionary<string, bool> billAutoBest = new Dictionary<string, bool>();
        public Dictionary<string, int> billTargetQuality = new Dictionary<string, int>();
        public Dictionary<string, int> productCaps = new Dictionary<string, int>();

        // Per-save behavior settings (seeded from global defaults; spec §11).
        public bool manageNewBillsDefault;
        public int minSkillDefault;
        public bool requireInspiredDefault;
        public bool requireSpecialistDefault;
        public bool autoBestDefault;
        public int targetQualityDefault;
        public int productCapDefault;
        public bool shareUnfinishedWork;

        // Per-save construction defaults (seeded from global defaults; dual-pattern §11).
        public bool manageNewConstructionDefault;
        public int constructionMinSkillDefault;
        public bool constructionRequireInspiredDefault;
        public bool constructionRequireSpecialistDefault;
        public int constructionTargetQualityDefault;
        public bool constructionAutoBestDefault;

        // ---- pending-copy session state (Fix 4; NOT scribed) -------------------
        //
        // Synced runtime state for the vanilla build-copy command: when the
        // player copies a managed thing, these carry the source plan's settings
        // so the blueprint spawn hook (Fix 3) can apply them to placed copies on
        // ALL clients from the SAME synced value. Deliberately not scribed:
        // copy intent is transient session state and must load as inactive
        // (pendingCopyActive = false) after any save/load.
        public bool pendingCopyActive;
        public int pendingCopyMinSkill;
        public bool pendingCopyInspired;
        public bool pendingCopySpecialist;
        public int pendingCopyQuality;
        public bool pendingCopyAutoBest;

        // Rebuilt every scan; keyed (map.uniqueID, productDefName).
        private readonly Dictionary<(int, string), int> uftCounts = new Dictionary<(int, string), int>();
        private bool seeded;

        // ---- overlay flag (NOT scribed) ----------------------------------------
        //
        // AnyOverlays is a process-static fast-path pre-check: one bool read per
        // draw call in the 99% case (no managed construction on map). It is set to
        // (plans.Count > 0) at every plan mutation site and in FinalizeInit/PostLoadInit.
        // Cleared by Commands.Disable after component removal.
        // Stale-true risk on world change: FinalizeInit runs before the first draw
        // after load and resets AnyOverlays correctly — no stale reads possible.

        /// True when at least one plan exists (i.e. plans.Count > 0). Checked first
        /// in the per-frame draw patch to skip the Active component lookup in the
        /// common case. Updated at every plan mutation site.
        public static bool AnyOverlays;

        // ---- plan mutation helpers ---------------------------------------------

        /// Adds a plan and updates AnyOverlays. All callers must use this instead
        /// of plans.Add directly so the flag stays in sync.
        public void AddPlan(ConstructionPlan plan)
        {
            plans.Add(plan);
            AnyOverlays = true;
        }

        /// Removes a plan by reference and updates AnyOverlays.
        public void RemovePlan(ConstructionPlan plan)
        {
            plans.Remove(plan);
            AnyOverlays = plans.Count > 0;
        }

        /// Removes a plan by index (for sweep loops iterating backwards).
        public void RemovePlanAt(int index)
        {
            plans.RemoveAt(index);
            AnyOverlays = plans.Count > 0;
        }

        public QualityJobsStore(Game game) { }

        public static QualityJobsStore? Active => Current.Game?.GetComponent<QualityJobsStore>();

        public override void FinalizeInit()
        {
            if (!seeded)
            {
                var s = QualityJobsMod.Settings;
                manageNewBillsDefault = s.defaultManageNewBills;
                minSkillDefault = s.defaultMinSkill;
                requireInspiredDefault = s.defaultRequireInspired;
                requireSpecialistDefault = s.defaultRequireSpecialist;
                autoBestDefault = s.defaultAutoBest;
                targetQualityDefault = s.defaultTargetQuality;
                productCapDefault = s.defaultProductCap;
                shareUnfinishedWork = s.defaultShareUnfinishedWork;
                manageNewConstructionDefault = s.defaultManageNewConstruction;
                constructionMinSkillDefault = s.defaultConstructionMinSkill;
                constructionRequireInspiredDefault = s.defaultConstructionRequireInspired;
                constructionRequireSpecialistDefault = s.defaultConstructionRequireSpecialist;
                constructionTargetQualityDefault = s.defaultConstructionTargetQuality;
                constructionAutoBestDefault = s.defaultConstructionAutoBest;
                seeded = true;
            }
            // Ensure AnyOverlays is correct before the first draw call on the new save.
            AnyOverlays = plans.Count > 0;
        }

        /// Deterministic seeding for MP-synced enable (spec §12): values travel
        /// as one synced payload (SeedValues) so every client seeds identically.
        public void SeedExplicit(SeedValues v)
        {
            manageNewBillsDefault = v.manageNewBills;
            minSkillDefault = v.minSkill;
            requireInspiredDefault = v.requireInspired;
            requireSpecialistDefault = v.requireSpecialist;
            autoBestDefault = v.autoBest;
            targetQualityDefault = v.targetQuality;
            productCapDefault = v.productCap;
            shareUnfinishedWork = v.share;
            manageNewConstructionDefault = v.manageNewConstruction;
            constructionMinSkillDefault = v.constructionMinSkill;
            constructionRequireInspiredDefault = v.constructionRequireInspired;
            constructionRequireSpecialistDefault = v.constructionRequireSpecialist;
            constructionTargetQualityDefault = v.constructionTargetQuality;
            constructionAutoBestDefault = v.constructionAutoBest;
            seeded = true;
        }

        // ---- config resolution -------------------------------------------------

        public BillConfig ConfigFor(Bill bill)
        {
            string id = BillIds.IdOf(bill);
            bool managed = billManaged.TryGetValue(id, out bool m) ? m : manageNewBillsDefault;
            int minSkill = billMinSkill.TryGetValue(id, out int ms) ? ms : minSkillDefault;
            bool inspired = billRequireInspired.TryGetValue(id, out bool ri) ? ri : requireInspiredDefault;
            bool specialist = billRequireSpecialist.TryGetValue(id, out bool rs) ? rs : requireSpecialistDefault;
            // Hard gate: without Ideology, production-specialist roles never exist so
            // RoleOffset is always 0 and a requireSpecialist=true condition would make
            // items permanently unresumable. Coerce here so the condition is safe
            // regardless of how the flag was stored (e.g. from a save made with Ideology
            // active, then loaded without it).
            specialist = specialist && ModsConfig.IdeologyActive;
            bool autoBest = billAutoBest.TryGetValue(id, out bool ab) ? ab : autoBestDefault;
            return new BillConfig(managed, autoBest, new ResumeCondition(minSkill, inspired, specialist));
        }

        public int CapFor(string? productDefName)
            => productDefName != null && productCaps.TryGetValue(productDefName, out int cap)
                ? cap : productCapDefault;

        /// Target quality for a bill (0 = any quality accepted): per-bill value
        /// with the per-save default as fallback, like the other bill config.
        public int TargetQualityFor(Bill bill)
            => billTargetQuality.TryGetValue(BillIds.IdOf(bill), out int q)
                ? q : targetQualityDefault;

        // ---- entry lookup ------------------------------------------------------

        public WorkItemEntry? FindByUft(UnfinishedThing uft)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].uft == uft) return entries[i];
            return null;
        }

        public ConstructionPlan? FindPlan(Thing target)
        {
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].target == target) return plans[i];
            return null;
        }

        public ConstructionPlan? FindPlanById(int thingId)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                Thing? t = plans[i].target;
                if (t != null && t.thingIDNumber == thingId) return plans[i];
            }
            return null;
        }

        public bool IsShared(UnfinishedThing uft)
            => FindByUft(uft)?.state == WorkItemState.Shared;

        public bool IsFinishBill(Bill? bill)
        {
            if (bill == null) return false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].finishBill == bill) return true;
            return false;
        }

        public int SpawnedUftCount(Map map, string? productDefName)
            => productDefName != null
               && uftCounts.TryGetValue((map.uniqueID, productDefName), out int n) ? n : 0;

        public void RegisterPaused(UnfinishedThing uft, Pawn? originalCreator,
            Bill_ProductionWithUft? sourceBill, StyleSnapshot? snapshot)
        {
            WorkItemEntry? entry = FindByUft(uft);
            if (entry == null)
            {
                entry = new WorkItemEntry { uft = uft };
                entries.Add(entry);
            }
            // Fix C1/I4: if a finish bill was already dispatched and the gate
            // re-pauses this item, remove the orphaned one-shot bill from the bench.
            if (entry.finishBill != null)
                Dispatcher.DeleteFinishBill(this, entry);
            entry.state = WorkItemState.Paused;
            // C1: re-pause of a dispatched item must not replace the original crafter
            // with the finisher. Preserve an existing originalCreator; only assign
            // when the entry has not yet recorded one (first-time pause).
            if (entry.originalCreator == null) entry.originalCreator = originalCreator;
            entry.finisher = null;
            entry.finishBill = null;
            if (sourceBill != null) entry.sourceBill = sourceBill;
            if (snapshot != null) entry.snapshot = snapshot;
        }

        public void RemoveEntry(WorkItemEntry entry) => entries.Remove(entry);

        // ---- scan (spec §6, §8, §9) -------------------------------------------

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % ScanInterval != 0) return;
            RecountAndPool();
            SweepEntries();
            DispatchPaused();
            SweepAndDispatchPlans();
        }

        private void RecountAndPool()
        {
            uftCounts.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                ThingDef[] uftDefs = ManagedRecipes.AllUftDefs;
                for (int d = 0; d < uftDefs.Length; d++)
                {
                    List<Thing> things = map.listerThings.ThingsOfDef(uftDefs[d]);
                    for (int i = 0; i < things.Count; i++)
                    {
                        var uft = (UnfinishedThing)things[i];
                        string? product = ManagedRecipes.ProductDefName(uft.Recipe);
                        if (product != null)
                        {
                            var key = (map.uniqueID, product);
                            uftCounts.TryGetValue(key, out int n);
                            uftCounts[key] = n + 1;
                        }
                        TryPool(map, uft);
                    }
                }
            }
        }

        /// Sharing pool (spec §8): idle in-progress UFTs get unbound so bills
        /// unlock; creator untouched. Adopts pre-existing UFTs mid-save.
        private void TryPool(Map map, UnfinishedThing uft)
        {
            if (!shareUnfinishedWork) return;
            if (uft.workLeft <= 0f || !uft.Initialized) return;
            if (FindByUft(uft) != null) return;
            if (map.reservationManager.IsReservedByAnyoneOf(uft, Faction.OfPlayer)) return;

            StyleSnapshot? snapshot = uft.BoundBill != null ? StyleSnapshot.From(uft.BoundBill) : null;
            var entry = new WorkItemEntry
            {
                uft = uft,
                state = WorkItemState.Shared,
                originalCreator = UftAuthor.Get(uft),
                sourceBill = uft.BoundBill,
                snapshot = snapshot,
            };
            entries.Add(entry);
            uft.BoundBill = null;
        }

        private void SweepEntries()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry e = entries[i];
                if (e.uft == null || e.uft.Destroyed)
                {
                    // Fix C1/I4: destroy any orphaned finish bill before forgetting the entry.
                    Dispatcher.DeleteFinishBill(this, e);
                    entries.RemoveAt(i);
                    continue;
                }
                if (e.state == WorkItemState.Dispatched && Dispatcher.DispatchInvalid(this, e))
                    Dispatcher.Revert(this, e);
                if (e.state == WorkItemState.Shared && !shareUnfinishedWork)
                {
                    // M4: sharing toggled off — drop Shared entries unconditionally
                    // so the pool clears immediately (creator intact, not rebound).
                    entries.RemoveAt(i);
                }
            }
        }

        private void DispatchPaused()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                if (e.state == WorkItemState.Paused && e.uft != null && e.uft.Spawned)
                    Dispatcher.TryDispatch(this, e);
            }
        }

        /// Construction plans (spec §10): sweep dead/cancelled targets, revert
        /// stale dispatches, dispatch paused frames. Player cancelling our
        /// Deconstruct designation is an opt-out: the plan is dropped.
        private void SweepAndDispatchPlans()
        {
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                ConstructionPlan p = plans[i];
                Thing? t = p.target;
                if (t == null || t.Destroyed)
                {
                    // Transitions handle tracked destruction; anything else
                    // (cancelled blueprint, burned frame) lands here.
                    RemovePlanAt(i);
                    continue;
                }
                if (p.state == ConstructionPlanState.AwaitingRebuild)
                {
                    // Review I2: an unspawned AwaitingRebuild target (minified,
                    // uninstalled) can never deconstruct-and-rebuild — drop it.
                    if (!t.Spawned
                        || t.Map.designationManager
                            .DesignationOn(t, DesignationDefOf.Deconstruct) == null)
                        RemovePlanAt(i); // designation gone = player opt-out
                    continue;
                }
                if (p.state == ConstructionPlanState.Dispatched
                    && Dispatcher.ConstructionDispatchInvalid(p))
                {
                    p.state = ConstructionPlanState.Paused;
                    p.finisher = null;
                }
                if (p.state == ConstructionPlanState.Paused)
                {
                    // Self-heal work overshoot on already-paused frames (saves
                    // made before the gate clamped it): vanilla's frame renderer
                    // does not clamp PercentComplete and draws phantom tiles
                    // outside the footprint past 100% (Frame.cs:487).
                    if (t is Frame pausedFrame && pausedFrame.workDone > pausedFrame.WorkToBuild)
                        pausedFrame.workDone = pausedFrame.WorkToBuild;
                    Dispatcher.TryDispatchConstruction(p);
                }
            }
            // AnyOverlays is maintained incrementally by RemovePlanAt; no rebuild needed.
        }

        // ---- scribing ----------------------------------------------------------

        /// <summary>
        /// Removes entries from the five bill-config dictionaries whose key is not
        /// present in the set of live bill IDs on all current maps.  Called once at
        /// save time, so allocations here are acceptable and the method stays off the
        /// tick path (spec §14; unbounded growth otherwise).
        /// </summary>
        private void PruneDeadBillConfigs()
        {
            if (Current.Game == null) return;
            List<Map> maps = Find.Maps;
            if (maps == null) return;

            // Also drop plans whose target died just before save: a dangling
            // reference would log a resolve warning on every later load.
            plans.RemoveAll(p => p.target == null || p.target.Destroyed);
            AnyOverlays = plans.Count > 0;

            var liveBillIds = new HashSet<string>();
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                List<Thing> potentialGivers =
                    map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver);
                for (int t = 0; t < potentialGivers.Count; t++)
                {
                    if (potentialGivers[t] is not IBillGiver giver) continue;
                    List<Bill> bills = giver.BillStack.Bills;
                    for (int b = 0; b < bills.Count; b++)
                    {
                        if (bills[b] is Bill_ProductionWithUft bill)
                            liveBillIds.Add(BillIds.IdOf(bill));
                    }
                }
            }

            var deadKeys = new List<string>();

            foreach (string key in billManaged.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billManaged.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billMinSkill.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billMinSkill.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billRequireInspired.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billRequireInspired.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billRequireSpecialist.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billRequireSpecialist.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billAutoBest.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billAutoBest.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billTargetQuality.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billTargetQuality.Remove(deadKeys[i]);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                PruneDeadBillConfigs();
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            Scribe_Collections.Look(ref plans, "plans", LookMode.Deep);
            Scribe_Collections.Look(ref billManaged, "billManaged", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref billMinSkill, "billMinSkill", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref billRequireInspired, "billRequireInspired", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref billRequireSpecialist, "billRequireSpecialist", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref billAutoBest, "billAutoBest", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref billTargetQuality, "billTargetQuality", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref productCaps, "productCaps", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref manageNewBillsDefault, "manageNewBillsDefault", true);
            Scribe_Values.Look(ref minSkillDefault, "minSkillDefault", 15);
            Scribe_Values.Look(ref requireInspiredDefault, "requireInspiredDefault", false);
            Scribe_Values.Look(ref requireSpecialistDefault, "requireSpecialistDefault", false);
            Scribe_Values.Look(ref autoBestDefault, "autoBestDefault", false);
            Scribe_Values.Look(ref targetQualityDefault, "targetQualityDefault", 0);
            Scribe_Values.Look(ref productCapDefault, "productCapDefault", 10);
            Scribe_Values.Look(ref shareUnfinishedWork, "shareUnfinishedWork", true);
            Scribe_Values.Look(ref manageNewConstructionDefault, "manageNewConstructionDefault", false);
            Scribe_Values.Look(ref constructionMinSkillDefault, "constructionMinSkillDefault", 15);
            Scribe_Values.Look(ref constructionRequireInspiredDefault, "constructionRequireInspiredDefault", false);
            Scribe_Values.Look(ref constructionRequireSpecialistDefault, "constructionRequireSpecialistDefault", false);
            Scribe_Values.Look(ref constructionTargetQualityDefault, "constructionTargetQualityDefault", 0);
            Scribe_Values.Look(ref constructionAutoBestDefault, "constructionAutoBestDefault", false);
            Scribe_Values.Look(ref seeded, "seeded", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Null-harden collections FIRST: absent XML nodes leave them
                // null, and the finish-bill cleanup below touches the config
                // dictionaries via DeleteFinishBill.
                entries ??= new List<WorkItemEntry>();
                plans ??= new List<ConstructionPlan>();
                plans.RemoveAll(p => p?.target == null);
                AnyOverlays = plans.Count > 0;
                billManaged ??= new Dictionary<string, bool>();
                billMinSkill ??= new Dictionary<string, int>();
                billRequireInspired ??= new Dictionary<string, bool>();
                billRequireSpecialist ??= new Dictionary<string, bool>();
                billAutoBest ??= new Dictionary<string, bool>();
                billTargetQuality ??= new Dictionary<string, int>();
                productCaps ??= new Dictionary<string, int>();
                // Fix C1/I4: clean up any finish bills for entries whose UFTs
                // failed to resolve (null uft after load). DeleteFinishBill
                // guards against a null finishBill internally.
                foreach (WorkItemEntry entry in entries)
                    if (entry?.uft == null)
                        Dispatcher.DeleteFinishBill(this, entry!);
                entries.RemoveAll(e => e?.uft == null);
            }
        }
    }
}
