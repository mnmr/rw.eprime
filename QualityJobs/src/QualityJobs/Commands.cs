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
            // Unmanaging hands already-paused items back to vanilla.
            if (!value) store.ReleaseUnmanagedBillWork();
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
            // Bills following the default are now unmanaged: release their
            // paused items like the per-bill checkbox does.
            if (!value) store.ReleaseUnmanagedBillWork();
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
            Thing? thing = QualityJobsStore.FindSpawnedThing(billGiverThingId);
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

            // The game's factory, so other mods' MakeNewBill hooks apply.
            var bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = 1;
            bill.suspended = false;
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

        /// <summary>
        /// Replay surface for QualityJobsApi.ManageBill. Replay resolves the
        /// bill again by load ID and applies nothing when it is gone, is not a
        /// managed recipe, or the quality is out of range.
        /// </summary>
        [SyncMethod]
        public static void ManageBillFromApi(string billId, int targetQuality)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || billId == null
                || !BillManagement.IsValidTargetQuality(targetQuality)
                || FindApiBill(store, billId) == null)
                return;
            ApplyBillManagement(store, billId, BillManagement.Manage(
                store.billManaged.TryGetValue(billId, out bool managed)
                    ? managed : null,
                store.manageNewBillsDefault,
                store.billTargetQuality.TryGetValue(billId, out int target)
                    ? target : null,
                targetQuality));
        }

        /// <summary>
        /// Replay surface for QualityJobsApi.UnmanageBill. Also hands the
        /// bill's gate-locked unfinished items back to vanilla.
        /// </summary>
        [SyncMethod]
        public static void UnmanageBillFromApi(string billId)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || billId == null) return;
            if (FindApiBill(store, billId) == null) return;
            ApplyBillManagement(store, billId, BillManagement.Unmanage(
                store.billManaged.TryGetValue(billId, out bool managed)
                    ? managed : null,
                store.manageNewBillsDefault));
            store.ReleaseUnmanagedBillWork();
        }

        private static void ApplyBillManagement(QualityJobsStore store,
            string billId, in BillManagementChange change)
        {
            if (change.IsNoOp) return;
            if (change.WritesManaged) store.billManaged[billId] = change.Managed;
            if (change.WritesTarget)
                store.billTargetQuality[billId] = change.TargetQuality;
            store.NotifyBillConfigurationChanged(billId, change.EligibilityChanged);
        }

        /// The live API-manageable bill with this load ID: a
        /// Bill_ProductionWithUft on a spawned bill giver's stack, not one of
        /// our finish bills, with a managed recipe. Scans bill givers; API
        /// commands are rare explicit calls, never a tick or render path.
        internal static Bill_ProductionWithUft? FindApiBill(
            QualityJobsStore store, string billId)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> givers = maps[m].listerThings.ThingsInGroup(
                    ThingRequestGroup.PotentialBillGiver);
                for (int t = 0; t < givers.Count; t++)
                {
                    if (givers[t] is not IBillGiver giver || !givers[t].Spawned)
                        continue;
                    List<Bill> bills = giver.BillStack.Bills;
                    for (int b = 0; b < bills.Count; b++)
                        if (bills[b] is Bill_ProductionWithUft bill
                            && BillIds.IdOf(bill) == billId)
                            return !bill.DeletedOrDereferenced
                                && !store.IsFinishBill(bill)
                                && ManagedRecipes.IsManagedRecipe(bill.recipe)
                                ? bill : null;
                }
            }
            return null;
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
            => ApplyPlanField(thingId, minSkill: value);

        /// Sets the require-inspired flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanRequireInspired(int thingId, bool value)
            => ApplyPlanField(thingId, requireInspired: value);

        /// Sets the require-specialist flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanRequireSpecialist(int thingId, bool value)
            => ApplyPlanField(thingId, requireSpecialist: value);

        /// Sets the auto-best flag for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanAutoBest(int thingId, bool value)
            => ApplyPlanField(thingId, autoBest: value);

        /// Sets the minimum acceptable quality for the plan identified by thingId.
        /// Implicit creation/removal follows the same pattern as SetPlanMinSkill.
        [SyncMethod]
        public static void SetPlanMinQuality(int thingId, int value)
            => ApplyPlanField(thingId, minQuality: value);

        /// One-field plan edit shared by the SetPlan* commands. The other four
        /// options are read here, inside the synced method (neutral when no
        /// plan exists), never captured in the UI, so concurrent edits of
        /// different fields do not overwrite each other. store.ApplyPlanSettings
        /// clamps, coerces specialist off without Ideology, creates the plan
        /// when needed, and removes it once fully neutral.
        private static void ApplyPlanField(int thingId, int? minSkill = null,
            bool? requireInspired = null, bool? requireSpecialist = null,
            int? minQuality = null, bool? autoBest = null)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlanById(thingId);
            store.ApplyPlanSettings(thingId,
                minSkill ?? plan?.minSkill ?? 0,
                requireInspired ?? plan?.requireInspired ?? false,
                requireSpecialist ?? plan?.requireSpecialist ?? false,
                minQuality ?? plan?.minQuality ?? 0,
                autoBest ?? plan?.autoBest ?? false);
        }

        /// Creates or overwrites the plan for the given thingId with the supplied values.
        /// Values are clamped and Ideology-coerced exactly as the individual setters do.
        /// After applying, removes the plan if it is fully neutral (all defaults).
        /// Used by Fix 4 (copy plan settings) to propagate plan settings to placed copies.
        [SyncMethod]
        public static void ApplyPlanSettings(int thingId, int minSkill, bool requireInspired,
            bool requireSpecialist, int minQuality, bool autoBest)
        {
            // Synced entry point: the store holds the non-synced core that the
            // blueprint spawn hook also calls directly.
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            store.ApplyPlanSettings(thingId, minSkill, requireInspired, requireSpecialist,
                minQuality, autoBest);
        }
    }
}
