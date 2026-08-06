using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Dispatch (spec §6) and the one-shot finish bill (spec §7 — reviewed
    /// field-by-field; change only with spec update).
    public static class Dispatcher
    {
        private static readonly List<CandidateFacts> candidateBuffer = new List<CandidateFacts>(32);
        private static readonly List<Pawn> pawnBuffer = new List<Pawn>(32);

        // Auto-best colony pool buffers (auto spec §2.2). Same static-buffer
        // pattern as candidateBuffer/pawnBuffer: cleared before every return so
        // the statics never root a previous world's pawns.
        private static readonly List<CandidateFacts> poolBuffer = new List<CandidateFacts>(64);
        private static readonly List<Pawn> poolPawnBuffer = new List<Pawn>(64);

        /// <summary>Fills poolBuffer/poolPawnBuffer with the colony-wide auto-best
        /// pool: free colonists on all maps, caravans, and travelling transporters.
        /// Downed/mental/asleep/off-map pawns are INCLUDED (temporary
        /// unavailability must not lower the bar); dead are excluded (defensively
        /// — the property is already alive-filtered); mechs are never pool
        /// members (spec §2.2 — FreeColonists excludes them).
        /// Capability (work type, recipe skill requirements) travels as facts
        /// flags and is filtered in Core. recipe == null builds construction
        /// facts.</summary>
        private static void BuildAutoPool(RecipeDef? recipe, WorkTypeDef? workType)
        {
            poolBuffer.Clear();
            poolPawnBuffer.Clear();
            List<Pawn> all = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p.Dead) continue;
                poolBuffer.Add(recipe != null ? FactsFor(p, recipe, workType) : ConstructionFactsFor(p));
                poolPawnBuffer.Add(p);
            }
        }

        private static void ClearAutoPool()
        {
            poolBuffer.Clear();
            poolPawnBuffer.Clear();
        }

        public static void TryDispatch(QualityJobsStore store, WorkItemEntry entry)
        {
            UnfinishedThing? uft = entry.uft;
            // Store only calls TryDispatch with non-null spawned UFTs; guard defensively.
            if (uft == null || !uft.Spawned) return;

            RecipeDef? recipe = uft.Recipe;
            if (recipe == null) return;

            ResumeCondition condition = ConditionFor(store, entry);
            bool autoBest = AutoBestFor(store, entry);
            Pawn? finisher = autoBest
                ? SelectAutoFinisher(uft.Map, recipe, condition)
                : SelectFinisher(uft.Map, recipe, condition, relaxed: false);
            if (finisher == null) return;

            Thing? bench = FindBench(entry, recipe);
            if (bench == null) return;

            Bill_ProductionWithUft bill = BuildFinishBill(entry, recipe, finisher);
            var giver = (IBillGiver)bench;
            giver.BillStack.AddBill(bill);
            // Top of the stack: considered first in StartOrResumeBillJob's loop.
            giver.BillStack.Bills.Remove(bill);
            giver.BillStack.Bills.Insert(0, bill);

            UftAuthor.Assign(uft, finisher);
            uft.BoundBill = bill;

            entry.state = WorkItemState.Dispatched;
            entry.finisher = finisher;
            entry.finishBill = bill;

            // Fix I3: register inherited config so any lookup by finish bill id
            // returns the same condition as the source bill.
            string finishId = BillIds.IdOf(bill);
            BillConfig inherited = entry.sourceBill != null && !entry.sourceBill.DeletedOrDereferenced
                ? store.ConfigFor(entry.sourceBill)
                : new BillConfig(true, autoBest, ConditionFor(store, entry));
            store.billManaged[finishId] = true; // finish bills are always gate-managed
            store.billMinSkill[finishId] = inherited.Condition.MinSkill;
            store.billRequireInspired[finishId] = inherited.Condition.RequireInspired;
            store.billRequireSpecialist[finishId] = inherited.Condition.RequireSpecialist;
            store.billAutoBest[finishId] = inherited.AutoBest;
        }

        /// Spec §7 construction table.
        private static Bill_ProductionWithUft BuildFinishBill(WorkItemEntry entry,
            RecipeDef recipe, Pawn finisher)
        {
            StyleSnapshot? snap = entry.snapshot;
            var bill = new Bill_ProductionWithUft(recipe, snap?.precept);
            if (snap != null && snap.known)
            {
                bill.style = snap.style;
                bill.globalStyle = snap.globalStyle;
                bill.graphicIndexOverride = snap.graphicIndexOverride;
                if (snap.ingredientFilter != null)
                    bill.ingredientFilter.CopyAllowancesFrom(snap.ingredientFilter);
                ApplyStoreMode(bill, snap);
            }
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = 1;
            bill.suspended = false;
            bill.SetPawnRestriction(finisher);
            bill.RenamableLabel = "QJ_FinishBillLabel".Translate(
                recipe.ProducedThingDef.label, finisher.LabelShort);
            return bill;
        }

        /// Spec §7: SpecificStockpile restored when the snapshotted group still
        /// resolves; otherwise BestStockpile.
        private static void ApplyStoreMode(Bill_ProductionWithUft bill, StyleSnapshot snap)
        {
            if (snap.storeMode == BillStoreModeDefOf.SpecificStockpile && snap.storeGroup != null)
            {
                bill.SetStoreMode(BillStoreModeDefOf.SpecificStockpile, snap.storeGroup);
            }
            else if (snap.storeMode != null && snap.storeMode != BillStoreModeDefOf.SpecificStockpile)
            {
                bill.SetStoreMode(snap.storeMode);
            }
            // else: default (BestStockpile) — fallback per spec §7.
        }

        private static Thing? FindBench(WorkItemEntry entry, RecipeDef recipe)
        {
            // Source bench first (spec §7).
            if (entry.sourceBill != null && !entry.sourceBill.DeletedOrDereferenced
                && entry.sourceBill.billStack?.billGiver is Thing sourceBench
                && sourceBench.Spawned && entry.uft != null && sourceBench.Map == entry.uft.Map)
                return sourceBench;

            if (entry.uft == null) return null; // paranoia: should be guarded by caller

            // Nearest usable bench for the recipe; deterministic tie-break.
            Thing? best = null;
            float bestDist = float.MaxValue;
            Map map = entry.uft.Map;
            foreach (ThingDef benchDef in recipe.AllRecipeUsers)
            {
                List<Thing> benches = map.listerThings.ThingsOfDef(benchDef);
                for (int i = 0; i < benches.Count; i++)
                {
                    Thing bench = benches[i];
                    if (!(bench is IBillGiver) || !bench.Spawned) continue;
                    // LengthHorizontalSquared returns int; implicit int→float widening.
                    float d = (bench.Position - entry.uft.Position).LengthHorizontalSquared;
                    if (d < bestDist || (d == bestDist && best != null
                        && bench.thingIDNumber < best.thingIDNumber))
                    {
                        best = bench;
                        bestDist = d;
                    }
                }
            }
            return best;
        }

        // ---- candidates --------------------------------------------------------

        public static Pawn? SelectFinisher(Map map, RecipeDef recipe,
            ResumeCondition condition, bool relaxed)
        {
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            WorkTypeDef? workType = WorkTypeForRecipe(recipe);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p.Dead || p.Downed || p.InMentalState) continue;
                pawnBuffer.Add(p);
                candidateBuffer.Add(FactsFor(p, recipe, workType));
            }
            int bestId = relaxed
                ? FinisherSelector.SelectBestCapable(candidateBuffer)
                : FinisherSelector.SelectBest(candidateBuffer, condition);
            // M3: resolve the pawn to a local before clearing the buffers so the
            // statics do not root a previous world's pawns across calls.
            Pawn? result = null;
            if (bestId != FinisherSelector.None)
                for (int i = 0; i < pawnBuffer.Count; i++)
                    if (pawnBuffer[i].thingIDNumber == bestId) { result = pawnBuffer[i]; break; }
            // Statics must not root a previous world's pawns.
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            return result;
        }

        /// Mech-aware Construction skill level for a pawn. Used by
        /// ConstructionFactsFor and the construction dialog's display cache.
        public static int ConstructionSkillOf(Pawn p)
            => p.RaceProps.IsMechanoid
                ? p.RaceProps.mechFixedSkillLevel
                : (p.skills != null ? p.skills.GetSkill(SkillDefOf.Construction).Level : 0);

        /// Construction candidate facts: Construction skill; frames have no
        /// recipe skill requirements (vanilla CanConstruct gates separately).
        public static CandidateFacts ConstructionFactsFor(Pawn p)
        {
            int skill = ConstructionSkillOf(p);
            bool inspired = p.InspirationDef == InspirationDefOf.Inspired_Creativity;
            bool workEnabled = p.workSettings != null
                && p.workSettings.WorkIsActive(WorkTypeDefOf.Construction);
            return new CandidateFacts(p.thingIDNumber, skill, inspired, RoleOffsetOf(p),
                workEnabled, meetsRecipeSkillRequirements: true,
                XpMilliOf(p, SkillDefOf.Construction));
        }

        /// Construction finisher (spec §10): same deterministic ranking as
        /// bills, Construction skill, no bench needed. Reuses the static
        /// buffers; clears them before returning (no world rooting).
        public static Pawn? SelectConstructionFinisher(Map map, ResumeCondition condition)
        {
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p.Dead || p.Downed || p.InMentalState) continue;
                pawnBuffer.Add(p);
                candidateBuffer.Add(ConstructionFactsFor(p));
            }
            int bestId = FinisherSelector.SelectBest(candidateBuffer, condition);
            Pawn? result = null;
            if (bestId != FinisherSelector.None)
                for (int i = 0; i < pawnBuffer.Count; i++)
                    if (pawnBuffer[i].thingIDNumber == bestId) { result = pawnBuffer[i]; break; }
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            return result;
        }

        /// Paused frame -> Dispatched (spec §10: no one-shot bill; the lock
        /// admits only the recorded finisher).
        public static void TryDispatchConstruction(ConstructionPlan plan)
        {
            if (!(plan.target is Frame frame) || !frame.Spawned) return;
            Pawn? finisher = plan.autoBest
                ? SelectAutoFinisher(frame.Map, null, plan.Condition)
                : SelectConstructionFinisher(frame.Map, plan.Condition);
            if (finisher == null) return;
            plan.finisher = finisher;
            plan.state = ConstructionPlanState.Dispatched;
        }

        /// Spec §10 revert triggers for construction dispatches.
        public static bool ConstructionDispatchInvalid(ConstructionPlan plan)
        {
            if (!(plan.target is Frame frame) || !frame.Spawned) return true;
            Pawn? f = plan.finisher;
            if (f == null || f.Dead || f.Destroyed || !f.Spawned || f.Downed) return true;
            CandidateFacts facts = ConstructionFactsFor(f);
            if (!facts.WorkTypeEnabled) return true;
            if (plan.autoBest)
            {
                // Auto spec §2.4: revert when the dispatched finisher is no
                // longer colony-wide top (someone surpassed them mid-walk).
                BuildAutoPool(null, null);
                bool stillBest = FinisherSelector.WorkerPassesAutoGate(facts, poolBuffer, plan.Condition);
                ClearAutoPool();
                if (!stillBest) return true;
            }
            else if (!plan.Condition.IsSatisfiedBy(facts)) return true;
            return false;
        }

        public static CandidateFacts FactsFor(Pawn p, RecipeDef recipe, WorkTypeDef? workType)
        {
            int skill = SkillOf(p, recipe);
            bool inspired = p.InspirationDef == InspirationDefOf.Inspired_Creativity;
            int roleOffset = RoleOffsetOf(p);
            bool workEnabled = workType == null
                || (p.workSettings != null && p.workSettings.WorkIsActive(workType));
            bool meetsSkill = recipe.PawnSatisfiesSkillRequirements(p);
            return new CandidateFacts(p.thingIDNumber, skill, inspired, roleOffset,
                workEnabled, meetsSkill, XpMilliOf(p, recipe.workSkill));
        }

        public static int SkillOf(Pawn p, RecipeDef recipe)
        {
            if (p.RaceProps.IsMechanoid) return p.RaceProps.mechFixedSkillLevel;
            if (recipe.workSkill == null || p.skills == null) return 0;
            return p.skills.GetSkill(recipe.workSkill).Level;
        }

        public static int RoleOffsetOf(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p.Ideo == null) return 0;
            Precept_Role? role = p.Ideo.GetRole(p);
            if (role?.def.roleEffects == null) return 0;
            for (int i = 0; i < role.def.roleEffects.Count; i++)
                if (role.def.roleEffects[i] is RoleEffect_ProductionQualityOffset eff)
                    return eff.offset;
            return 0;
        }

        /// <summary>XP progress toward the next level as fixed-point milli
        /// (auto spec §2.1). Mechs and skill-less pawns return 0. Deterministic:
        /// reads scribed floats identical on every MP client.</summary>
        public static int XpMilliOf(Pawn p, SkillDef? skillDef)
        {
            if (p.RaceProps.IsMechanoid || skillDef == null || p.skills == null) return 0;
            SkillRecord rec = p.skills.GetSkill(skillDef);
            float required = rec.XpRequiredForLevelUp;
            if (required <= 0f) return 0;
            float frac = rec.xpSinceLastLevel / required;
            if (frac < 0f) frac = 0f;
            if (frac > 0.999f) frac = 0.999f;
            return (int)(frac * 1000f);
        }

        /// Same resolution as vanilla BoundWorker (Bill_ProductionWithUft.cs:36-53):
        /// the work type of the first work giver whose fixedBillGiverDefs cover
        /// benches for this recipe.
        ///
        /// Memo cache — Owner: process (def-derived only). Key: RecipeDef identity.
        /// Value: WorkTypeDef? (nullable; null = no bench giver found). Dependencies:
        /// def database contents (stable after startup; ManagedRecipes.Invalidate()
        /// calls InvalidateWorkTypeCache() to clear this when a definition reload
        /// occurs). Refresh: lazy on first call per recipe; cleared by
        /// InvalidateWorkTypeCache(). Equality: n/a (single value per key).
        /// Teardown: none needed (no world data). Cache hits are plain Dictionary
        /// lookups — no allocation.
        private static readonly Dictionary<RecipeDef, WorkTypeDef?> s_workTypeCache =
            new Dictionary<RecipeDef, WorkTypeDef?>();

        /// Clears the recipe→workType memo cache. Called by ManagedRecipes.Invalidate()
        /// after a definition reload so both caches stay coherent.
        public static void InvalidateWorkTypeCache() => s_workTypeCache.Clear();

        public static WorkTypeDef? WorkTypeForRecipe(RecipeDef recipe)
        {
            if (s_workTypeCache.TryGetValue(recipe, out WorkTypeDef? cached))
                return cached;
            List<WorkGiverDef> givers = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            WorkTypeDef? result = null;
            foreach (ThingDef benchDef in recipe.AllRecipeUsers)
                for (int i = 0; i < givers.Count; i++)
                    if (givers[i].fixedBillGiverDefs != null
                        && givers[i].fixedBillGiverDefs.Contains(benchDef))
                    {
                        result = givers[i].workType;
                        goto done;
                    }
            done:
            s_workTypeCache[recipe] = result;
            return result;
        }

        // ---- config, revert, completion ---------------------------------------

        /// <summary>The bill whose config governs this entry: source bill while it
        /// lives, else the finish bill. Shared by ConditionFor and AutoBestFor so
        /// the two resolutions can never diverge.</summary>
        private static Bill? ConfigSourceOf(WorkItemEntry entry)
            => entry.sourceBill != null && !entry.sourceBill.DeletedOrDereferenced
                ? entry.sourceBill : entry.finishBill;

        public static ResumeCondition ConditionFor(QualityJobsStore store, WorkItemEntry entry)
        {
            Bill? configSource = ConfigSourceOf(entry);
            if (configSource != null)
                return store.ConfigFor(configSource).Condition; // ConfigFor already coerces specialist
            // Fallback: no bill reference available; apply the same Ideology gate as ConfigFor.
            bool specialist = store.requireSpecialistDefault && ModsConfig.IdeologyActive;
            return new ResumeCondition(store.minSkillDefault, store.requireInspiredDefault,
                specialist);
        }

        /// <summary>Resolves the auto-best flag exactly as ConditionFor resolves
        /// the condition: source bill first, then finish bill, then the per-save
        /// default.</summary>
        public static bool AutoBestFor(QualityJobsStore store, WorkItemEntry entry)
        {
            Bill? configSource = ConfigSourceOf(entry);
            if (configSource != null) return store.ConfigFor(configSource).AutoBest;
            return store.autoBestDefault;
        }

        /// <summary>Auto-best gate evaluation for the bill completion patch. The
        /// pool is built fresh at the gate moment — never cached from the sweep
        /// (auto spec §2.3).</summary>
        public static GateOutcome DecideAutoGate(bool managed, bool debugCompleted,
            Pawn actor, RecipeDef recipe, ResumeCondition condition)
        {
            WorkTypeDef? workType = WorkTypeForRecipe(recipe);
            CandidateFacts worker = FactsFor(actor, recipe, workType);
            BuildAutoPool(recipe, workType);
            GateOutcome outcome = GateDecision.DecideAuto(managed, debugCompleted,
                worker, poolBuffer, condition);
            ClearAutoPool();
            return outcome;
        }

        /// <summary>Auto-best gate evaluation for the construction gate patch.</summary>
        public static GateOutcome DecideAutoConstructionGate(Pawn worker, ResumeCondition condition)
        {
            CandidateFacts facts = ConstructionFactsFor(worker);
            BuildAutoPool(null, null);
            GateOutcome outcome = GateDecision.DecideAuto(billManaged: true,
                debugCompleted: false, facts, poolBuffer, condition);
            ClearAutoPool();
            return outcome;
        }

        /// <summary>Auto-best dispatch (auto spec §2.4): the colony-wide best must
        /// itself be dispatchable on the item's map; otherwise returns null and
        /// the item waits. recipe == null selects a construction finisher.</summary>
        public static Pawn? SelectAutoFinisher(Map map, RecipeDef? recipe, ResumeCondition condition)
        {
            WorkTypeDef? workType = recipe != null ? WorkTypeForRecipe(recipe) : null;
            BuildAutoPool(recipe, workType);
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p.Dead || p.Downed || p.InMentalState) continue;
                pawnBuffer.Add(p);
                candidateBuffer.Add(recipe != null
                    ? FactsFor(p, recipe, workType) : ConstructionFactsFor(p));
            }
            int bestId = FinisherSelector.SelectAutoBest(candidateBuffer, poolBuffer, condition);
            Pawn? result = null;
            if (bestId != FinisherSelector.None)
                for (int i = 0; i < pawnBuffer.Count; i++)
                    if (pawnBuffer[i].thingIDNumber == bestId) { result = pawnBuffer[i]; break; }
            candidateBuffer.Clear();
            pawnBuffer.Clear();
            ClearAutoPool();
            return result;
        }

        /// <summary>Current auto-best pawn for dialog display (auto spec §5).
        /// Ranks the full colony pool, availability ignored — shows who the gate
        /// demands. recipe == null ranks by Construction skill. Call only from
        /// tick-throttled dialog caches, never per frame.</summary>
        public static Pawn? AutoBestForDisplay(RecipeDef? recipe, ResumeCondition condition)
        {
            WorkTypeDef? workType = recipe != null ? WorkTypeForRecipe(recipe) : null;
            BuildAutoPool(recipe, workType);
            int bestId = FinisherSelector.SelectBestOfPool(poolBuffer, condition);
            Pawn? result = null;
            if (bestId != FinisherSelector.None)
                for (int i = 0; i < poolPawnBuffer.Count; i++)
                    if (poolPawnBuffer[i].thingIDNumber == bestId) { result = poolPawnBuffer[i]; break; }
            ClearAutoPool();
            return result;
        }

        public static bool DispatchInvalid(QualityJobsStore store, WorkItemEntry e)
        {
            if (e.uft == null || !e.uft.Spawned) return true;
            if (e.finisher == null || e.finisher.Dead || e.finisher.Destroyed
                || !e.finisher.Spawned || e.finisher.Downed) return true;
            if (e.finishBill == null || e.finishBill.DeletedOrDereferenced) return true;
            // Spec §4: required inspiration lost or work type disabled → revert early
            // instead of letting the finisher walk over and bounce off the gate.
            RecipeDef? recipe = e.uft?.Recipe;
            if (recipe != null && e.finisher != null)
            {
                ResumeCondition condition = ConditionFor(store, e);
                WorkTypeDef? workType = WorkTypeForRecipe(recipe);
                CandidateFacts facts = FactsFor(e.finisher, recipe, workType);
                if (!facts.WorkTypeEnabled) return true;
                if (AutoBestFor(store, e))
                {
                    // Auto spec §2.4: revert when the dispatched finisher is no
                    // longer colony-wide top (someone surpassed them mid-walk).
                    BuildAutoPool(recipe, workType);
                    bool stillBest = FinisherSelector.WorkerPassesAutoGate(facts, poolBuffer, condition);
                    ClearAutoPool();
                    if (!stillBest) return true;
                }
                else if (!condition.IsSatisfiedBy(facts)) return true;
            }
            return false;
        }

        /// Dispatched -> Paused (spec §4 revert).
        public static void Revert(QualityJobsStore store, WorkItemEntry entry)
        {
            DeleteFinishBill(store, entry);
            if (entry.uft != null) entry.uft.BoundBill = null;
            if (entry.uft != null) UftAuthor.Clear(entry.uft);
            entry.state = WorkItemState.Paused;
            entry.finisher = null;
            entry.finishBill = null;
        }

        public static void CompleteDispatch(QualityJobsStore store, WorkItemEntry entry)
        {
            DeleteFinishBill(store, entry);
            store.RemoveEntry(entry);
        }

        /// Removes the one-shot finish bill from its stack and cleans up the
        /// five per-bill config entries registered for it in the store.
        /// Compute the bill's load ID BEFORE deletion (safe: BillIds.IdOf reads
        /// the cached string, never touches billStack).
        internal static void DeleteFinishBill(QualityJobsStore store, WorkItemEntry entry)
        {
            Bill_ProductionWithUft? bill = entry.finishBill;
            if (bill == null) return;
            string id = BillIds.IdOf(bill);
            if (!bill.DeletedOrDereferenced)
                bill.billStack?.Delete(bill);
            store.billManaged.Remove(id);
            store.billMinSkill.Remove(id);
            store.billRequireInspired.Remove(id);
            store.billRequireSpecialist.Remove(id);
            store.billAutoBest.Remove(id);
        }

        /// Removes the Deconstruct designation this mod placed on an
        /// AwaitingRebuild building, if the building is still spawned and the
        /// designation exists. Shared by Commands.RemovePlan and
        /// RestoreAllToVanilla so the removal logic is never duplicated.
        internal static void RemoveOurDeconstructDesignation(ConstructionPlan plan)
        {
            if (plan.state == ConstructionPlanState.AwaitingRebuild
                && plan.target is Building b && b.Spawned)
            {
                Designation? d = b.Map.designationManager
                    .DesignationOn(b, DesignationDefOf.Deconstruct);
                if (d != null) b.Map.designationManager.RemoveDesignation(d);
            }
        }

        /// Disable restore routine (spec §12): everything back to vanilla.
        public static void RestoreAllToVanilla(QualityJobsStore store)
        {
            for (int i = store.entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry e = store.entries[i];
                DeleteFinishBill(store, e);
                if (e.uft != null && !e.uft.Destroyed
                    && (e.state == WorkItemState.Paused || e.state == WorkItemState.Dispatched))
                {
                    e.uft.BoundBill = null;
                    Pawn? owner = null;
                    // Skip SelectFinisher when recipe is null to avoid NRE.
                    if (e.uft.Map != null && e.uft.Recipe != null)
                        owner = SelectFinisher(e.uft.Map, e.uft.Recipe, default, relaxed: true);
                    if (owner == null && e.originalCreator != null && !e.originalCreator.Dead)
                        owner = e.originalCreator;
                    if (owner != null) UftAuthor.Assign(e.uft, owner);
                    // M5: no owner could be assigned — clear the reserved label so
                    // post-disable saves don't carry the mod label on authorless items.
                    else UftAuthor.ClearLabelIfReserved(e.uft);
                }
            }
            store.entries.Clear();
            // Construction plan cleanup (spec §10): remove our Deconstruct
            // designations before clearing plans, so frames complete vanilla-style
            // after disable.
            for (int i = store.plans.Count - 1; i >= 0; i--)
                RemoveOurDeconstructDesignation(store.plans[i]);
            store.plans.Clear();
            // AnyOverlays is cleared by Commands.Disable after component removal;
            // set it here too so it is correct if RestoreAllToVanilla is called
            // independently of Disable (defensive: currently only called by Disable).
            QualityJobsStore.AnyOverlays = false;
        }
    }
}
