using System;
using System.Collections.Generic;
using Multiplayer.API;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// All UI-originated mutations of per-save state (spec §13). Primitive
    /// parameters or explicitly synced primitive-field payloads only. Every
    /// setter compares before writing: no-op edits change nothing (AGENTS.md).
    public static class Commands
    {
        [SyncMethod]
        public static void SetBillManaged(string billId, bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            bool effective = store.billManaged.TryGetValue(billId, out bool current)
                ? current : store.manageNewBillsDefault;
            if (effective == value) return;
            store.billManaged[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetBillMinSkill(string billId, int value)
        {
            value = ConfigurationLimits.Skill(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            int effective = store.billMinSkill.TryGetValue(billId, out int current)
                ? current : store.minSkillDefault;
            if (effective == value) return;
            store.billMinSkill[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetBillRequireInspired(string billId, bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            bool effective = store.billRequireInspired.TryGetValue(billId, out bool current)
                ? current : store.requireInspiredDefault;
            if (effective == value) return;
            store.billRequireInspired[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetBillRequireSpecialist(string billId, bool value)
        {
            value = value && ModsConfig.IdeologyActive;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            bool effective = store.billRequireSpecialist.TryGetValue(billId, out bool current)
                ? current : store.requireSpecialistDefault;
            if (effective == value) return;
            store.billRequireSpecialist[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetBillAutoBest(string billId, bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            bool effective = store.billAutoBest.TryGetValue(billId, out bool current)
                ? current : store.autoBestDefault;
            if (effective == value) return;
            store.billAutoBest[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetBillTargetQuality(string billId, int value)
        {
            value = ConfigurationLimits.Quality(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            int effective = store.billTargetQuality.TryGetValue(billId, out int current)
                ? current : store.targetQualityDefault;
            if (effective == value) return;
            store.billTargetQuality[billId] = value;
            store.NotifyBillConfigurationChanged(billId, affectsEligibility: false);
        }

        [SyncMethod]
        public static void SetProductCap(string productDefName, int cap)
        {
            cap = ConfigurationLimits.StockCap(cap);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || productDefName == null) return;
            if (store.CapFor(productDefName) == cap) return;
            store.productCaps[productDefName] = cap;
            store.NotifyProductCapChanged(productDefName, isDefault: false);
        }

        [SyncMethod]
        public static void SetShareUnfinishedWork(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.shareUnfinishedWork == value) return;
            store.shareUnfinishedWork = value;
            store.NotifyShareChanged();
        }

        [SyncMethod]
        public static void SetHighPriorityFinish(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.highPriorityFinish == value) return;
            store.highPriorityFinish = value;
            store.NotifyHighPriorityFinishChanged();
        }

        /// Resolves the first-install migration for bills that were explicitly
        /// quarantined before play began. Closing the dialog is the safe decline
        /// path: bills remain unmanaged while unfinished-work sharing continues.
        [SyncMethod]
        public static void ResolveExistingBillMigration(bool enableQualityJobs)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.pendingExistingBillMigrationIds.Count == 0)
                return;

            ExistingBillMigrationConfig config =
                ExistingBillMigrationPolicy.ConfigurationFor(enableQualityJobs);
            List<string> pending = store.pendingExistingBillMigrationIds;
            for (int i = 0; i < pending.Count; i++)
            {
                string id = pending[i];
                bool effectiveManaged = store.billManaged.TryGetValue(id,
                    out bool managed) ? managed : store.manageNewBillsDefault;
                bool effectiveAutoBest = store.billAutoBest.TryGetValue(id,
                    out bool autoBest) ? autoBest : store.autoBestDefault;
                bool effectiveInspired = store.billRequireInspired.TryGetValue(id,
                    out bool inspired) ? inspired : store.requireInspiredDefault;
                bool effectiveSpecialist = store.billRequireSpecialist.TryGetValue(id,
                    out bool specialist) ? specialist : store.requireSpecialistDefault;
                int effectiveTarget = store.billTargetQuality.TryGetValue(id,
                    out int target) ? target : store.targetQualityDefault;
                bool eligibilityChanged = effectiveManaged != config.Managed
                    || effectiveAutoBest != config.AutoBest
                    || effectiveInspired != config.RequireInspired
                    || effectiveSpecialist != config.RequireSpecialist;
                bool presentationChanged = eligibilityChanged
                    || effectiveTarget != config.TargetQuality;
                store.billManaged[id] = config.Managed;
                store.billAutoBest[id] = config.AutoBest;
                store.billRequireInspired[id] = config.RequireInspired;
                store.billRequireSpecialist[id] = config.RequireSpecialist;
                store.billTargetQuality[id] = config.TargetQuality;
                if (presentationChanged)
                    store.NotifyBillConfigurationChanged(id, eligibilityChanged);
            }

            pending.Clear();
            store.existingBillMigrationVersion =
                QualityJobsStore.CurrentExistingBillMigrationVersion;
        }

        // ---- Per-save bill default setters (dual-pattern) -----------------------

        [SyncMethod]
        public static void SetManageNewBillsDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.manageNewBillsDefault == value) return;
            store.manageNewBillsDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.Managed, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetMinSkillDefault(int value)
        {
            value = ConfigurationLimits.Skill(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.minSkillDefault == value) return;
            store.minSkillDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.MinSkill, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetRequireInspiredDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.requireInspiredDefault == value) return;
            store.requireInspiredDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.RequireInspired, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetRequireSpecialistDefault(bool value)
        {
            value = value && ModsConfig.IdeologyActive;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.requireSpecialistDefault == value) return;
            store.requireSpecialistDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.RequireSpecialist, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetAutoBestDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.autoBestDefault == value) return;
            store.autoBestDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.AutoBest, affectsEligibility: true);
        }

        [SyncMethod]
        public static void SetTargetQualityDefault(int value)
        {
            value = ConfigurationLimits.Quality(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.targetQualityDefault == value) return;
            store.targetQualityDefault = value;
            store.NotifyBillDefaultsChanged(
                BillDefaultField.TargetQuality, affectsEligibility: false);
        }

        [SyncMethod]
        public static void SetProductCapDefault(int value)
        {
            value = ConfigurationLimits.StockCap(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.productCapDefault == value) return;
            store.productCapDefault = value;
            store.NotifyProductCapChanged(null, isDefault: true);
        }

        // ---- Per-save construction default setters (dual-pattern) ---------------

        [SyncMethod]
        public static void SetManageNewConstructionDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.manageNewConstructionDefault == value) return;
            store.manageNewConstructionDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        [SyncMethod]
        public static void SetConstructionMinSkillDefault(int value)
        {
            value = ConfigurationLimits.Skill(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.constructionMinSkillDefault == value) return;
            store.constructionMinSkillDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        [SyncMethod]
        public static void SetConstructionRequireInspiredDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.constructionRequireInspiredDefault == value) return;
            store.constructionRequireInspiredDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        [SyncMethod]
        public static void SetConstructionRequireSpecialistDefault(bool value)
        {
            value = value && ModsConfig.IdeologyActive;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.constructionRequireSpecialistDefault == value) return;
            store.constructionRequireSpecialistDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        [SyncMethod]
        public static void SetConstructionAutoBestDefault(bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.constructionAutoBestDefault == value) return;
            store.constructionAutoBestDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        [SyncMethod]
        public static void SetConstructionTargetQualityDefault(int value)
        {
            value = ConfigurationLimits.Quality(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.constructionTargetQualityDefault == value) return;
            store.constructionTargetQualityDefault = value;
            store.NotifyConstructionDefaultsChanged();
        }

        /// <summary>
        /// Replay surface for QualityJobsApi.CreateQualityBill. The exact giver,
        /// product, and recipe identities lock the operation to the map and
        /// recipe validated by the caller; replay validates them again. Keep the
        /// values in one SyncWorker payload: expanding them into parameters can
        /// crash RimWorld Mono during Multiplayer registration (see AGENTS.md).
        /// </summary>
        [SyncMethod]
        public static void CreateQualityBillFromApi(CreateQualityBillValues v)
        {
            if (v == null) return;
            int billGiverThingId = v.billGiverThingId;
            int mapUniqueId = v.mapUniqueId;
            string productDefName = v.productDefName;
            string recipeDefName = v.recipeDefName;
            bool explicitOptions = v.explicitOptions;
            int skillGate = v.skillGate;
            bool requireInspired = v.requireInspired;
            bool requireSpecialist = v.requireSpecialist;
            bool autoBest = v.autoBest;
            int targetQuality = v.targetQuality;

            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || productDefName == null || recipeDefName == null)
                return;
            Thing? thing = FindSpawnedThing(billGiverThingId);
            if (thing is not IBillGiver giver || !thing.Spawned
                || thing.MapHeld == null
                || thing.MapHeld.uniqueID != mapUniqueId
                || !ReferenceEquals(giver.Map, thing.MapHeld)
                || giver.BillStack.Count >= BillStack.MaxCount)
                return;

            ThingDef? product = DefDatabase<ThingDef>.GetNamedSilentFail(
                productDefName);
            RecipeDef? recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(
                recipeDefName);
            if (product == null || recipe == null
                || !ManagedRecipes.IsManagedRecipe(recipe)
                || !ReferenceEquals(recipe.ProducedThingDef, product)
                || !thing.def.AllRecipes.Contains(recipe))
                return;

            if (!explicitOptions)
            {
                skillGate = store.minSkillDefault;
                requireInspired = store.requireInspiredDefault;
                requireSpecialist = store.requireSpecialistDefault;
                autoBest = store.autoBestDefault;
                targetQuality = store.targetQualityDefault;
            }
            skillGate = ConfigurationLimits.Skill(skillGate);
            requireSpecialist = requireSpecialist && ModsConfig.IdeologyActive;
            targetQuality = ConfigurationLimits.Quality(targetQuality);

            var bill = new Bill_ProductionWithUft(recipe)
            {
                repeatMode = BillRepeatModeDefOf.RepeatCount,
                repeatCount = 1,
                suspended = false,
            };
            giver.BillStack.AddBill(bill);

            string billId = BillIds.IdOf(bill);
            store.billManaged[billId] = true;
            store.billMinSkill[billId] = skillGate;
            store.billRequireInspired[billId] = requireInspired;
            store.billRequireSpecialist[billId] = requireSpecialist;
            store.billAutoBest[billId] = autoBest;
            store.billTargetQuality[billId] = targetQuality;
            store.NotifyBillConfigurationChanged(
                billId, affectsEligibility: true);
        }

        /// Spec §12: enable adds a fresh component seeded from the ISSUING
        /// client's defaults (passed as primitives for MP determinism — all
        /// clients seed identically from the same parameter set).
        /// Disable is session-scoped uninstall preparation: it restores vanilla
        /// state and removes the component so saves carry zero trace. RimWorld
        /// will re-add the component with default settings the next time the
        /// save is loaded while the mod remains installed.
        [SyncMethod]
        public static void Enable(SeedValues v)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store != null) return;
            var fresh = new QualityJobsStore(Current.Game);
            Current.Game.components.Add(fresh);
            fresh.SeedExplicit(v);
        }

        [SyncMethod]
        public static void Disable()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            Dispatcher.RestoreAllToVanilla(store);
            store.ReleasePresentation();
            Current.Game.components.Remove(store);
            // Clear the static fast-path flag so the draw patch sees zero plans
            // immediately after the component is removed.
            QualityJobsStore.AnyOverlays = false;
        }

        /// UI helper: captures the local defaults into the synced enable payload.
        public static void RequestEnable()
        {
            Enable(SeedValues.FromSettings(QualityJobsMod.Settings));
        }

        /// Fix 4: arms the synced pending-copy session state. Issued from the
        /// initiator's copy-gizmo action; replicates to all clients so the
        /// blueprint spawn hook reads identical settings everywhere.
        [SyncMethod]
        public static void SetPendingCopy(int minSkill, bool inspired, bool specialist,
            int quality, bool autoBest)
        {
            minSkill = ConfigurationLimits.Skill(minSkill);
            specialist = specialist && ModsConfig.IdeologyActive;
            quality = ConfigurationLimits.Quality(quality);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            store.pendingCopyMinSkill   = minSkill;
            store.pendingCopyInspired   = inspired;
            store.pendingCopySpecialist = specialist;
            store.pendingCopyQuality    = quality;
            store.pendingCopyAutoBest   = autoBest;
            store.pendingCopyActive     = true;
        }

        /// Fix 4: disarms the synced pending-copy session state. Best-effort UI
        /// clearing; a stale pending value is desync-safe (all clients read the
        /// same synced value), so imperfect clearing is only a minor UX issue.
        [SyncMethod]
        public static void ClearPendingCopy()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !store.pendingCopyActive) return;
            store.pendingCopyActive = false;
        }

        /// Removes the plan for the given thingId and any Deconstruct designation
        /// we placed. This is the explicit Clear command issued from the dialog.
        [SyncMethod]
        public static void RemovePlan(int thingId)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            ConstructionPlan? plan = store?.FindPlanById(thingId);
            if (store == null || plan == null) return;
            Dispatcher.RemoveOurDeconstructDesignation(plan);
            store.RemovePlan(plan);
        }

        /// Sets the minimum construction skill for the plan identified by thingId.
        /// If no plan exists and value is non-neutral, implicitly creates one.
        /// After applying, removes the plan if it becomes fully neutral.
        [SyncMethod]
        public static void SetPlanMinSkill(int thingId, int value)
        {
            value = ConfigurationLimits.Skill(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            if (plan == null)
            {
                if (value == 0) return; // neutral: no plan needed
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }
            if (plan.minSkill == value) return;
            plan.minSkill = value;
            if (!RemoveIfNeutral(store, plan))
                store.NotifyPlanConfigurationChanged();
        }

        /// Sets the require-inspired flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanRequireInspired(int thingId, bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            if (plan == null)
            {
                if (!value) return; // neutral: no plan needed
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }
            if (plan.requireInspired == value) return;
            plan.requireInspired = value;
            if (!RemoveIfNeutral(store, plan))
                store.NotifyPlanConfigurationChanged();
        }

        /// Sets the require-specialist flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanRequireSpecialist(int thingId, bool value)
        {
            value = value && ModsConfig.IdeologyActive;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            if (plan == null)
            {
                if (!value) return; // neutral: no plan needed
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }
            if (plan.requireSpecialist == value) return;
            plan.requireSpecialist = value;
            if (!RemoveIfNeutral(store, plan))
                store.NotifyPlanConfigurationChanged();
        }

        /// Sets the auto-best flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanAutoBest(int thingId, bool value)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            if (plan == null)
            {
                if (!value) return; // neutral: no plan needed
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }
            if (plan.autoBest == value) return;
            plan.autoBest = value;
            if (!RemoveIfNeutral(store, plan))
                store.NotifyPlanConfigurationChanged();
        }

        /// Sets the minimum acceptable quality for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanMinQuality(int thingId, int value)
        {
            // Clamp incoming value to [0, 6]: a minQuality > 6 would retry
            // forever because no quality level (even Legendary = 6) can meet it.
            value = ConfigurationLimits.Quality(value);
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            if (plan == null)
            {
                if (value == 0) return; // neutral: no plan needed
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }
            if (plan.minQuality == value) return;
            plan.minQuality = value;
            if (!RemoveIfNeutral(store, plan))
                store.NotifyPlanConfigurationChanged();
        }

        /// Returns true when the plan has all-neutral values (no active options).
        private static bool IsNeutral(ConstructionPlan plan)
            => plan.minSkill == 0 && !plan.requireInspired && !plan.requireSpecialist
               && plan.minQuality == 0 && !plan.autoBest;

        /// Removes the plan and its Deconstruct designation if it is fully neutral.
        private static bool RemoveIfNeutral(QualityJobsStore store, ConstructionPlan plan)
        {
            if (!IsNeutral(plan)) return false;
            Dispatcher.RemoveOurDeconstructDesignation(plan);
            store.RemovePlan(plan);
            return true;
        }

        /// Resolves or creates a neutral plan for the given thingId.
        /// Returns null if the thing cannot be found or is not a Blueprint_Build/Frame.
        private static ConstructionPlan? CreateNeutralPlan(QualityJobsStore store, int thingId)
        {
            Thing? target = FindSpawnedThing(thingId);
            if (target == null) return null;
            // SyncMethods are a public replay surface: reject anything that is not
            // a Blueprint_Build or Frame. Other thing types cannot be gate-managed.
            if (!(target is Blueprint_Build) && !(target is Frame)) return null;
            var plan = new ConstructionPlan
            {
                target = target,
                state = ConstructionPlanState.Active,
            };
            store.AddPlan(plan);
            return plan;
        }

        /// Creates or overwrites the plan for the given thingId with the supplied values.
        /// Values are clamped and Ideology-coerced exactly as the individual setters do.
        /// After applying, removes the plan if it is fully neutral (all defaults).
        /// Used by Fix 4 (copy plan settings) to propagate plan settings to placed copies.
        [SyncMethod]
        public static void ApplyPlanSettings(int thingId, int minSkill, bool requireInspired,
            bool requireSpecialist, int minQuality, bool autoBest)
        {
            // Fix 2: synced entry point resolves the store and delegates the
            // create-or-overwrite-or-remove-if-neutral logic to the non-synced
            // PlanOps.Apply core (clamping + Ideology coercion live there).
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            PlanOps.Apply(store, thingId, minSkill, requireInspired, requireSpecialist,
                minQuality, autoBest);
        }

        private static Thing? FindSpawnedThing(int thingId)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> things = maps[m].listerThings.AllThings;
                for (int i = 0; i < things.Count; i++)
                    if (things[i].thingIDNumber == thingId) return things[i];
            }
            return null;
        }
    }
}
