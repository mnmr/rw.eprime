using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using QualityJobs.Core;
using QualityJobs.UI;
using RimWorld;
using Verse;

namespace QualityJobs
{
    internal enum BillDefaultField
    {
        Managed,
        MinSkill,
        RequireInspired,
        RequireSpecialist,
        AutoBest,
        TargetQuality,
    }

    /// Per-save authoritative store (spec §4). Presence in Game.components IS
    /// the enabled flag (spec §12): absent component = mod inert.
    ///
    /// Cache/store contract — Owner: Game (per save). Key: entries by UFT
    /// reference; construction plans by target Thing reference; configs by
    /// bill loadID string; caps by product defName. Value:
    /// mutable authoritative state, mutated only in lifecycle code or synced
    /// commands. Dependencies: game state consumed by the named audit and
    /// responsiveness boundaries. Refresh: explicit invalidation plus fixed
    /// game-tick boundaries. Equality: command setters compare before
    /// writing (no-op edits change nothing). Teardown: ReleaseMap and
    /// ReleasePresentation clear owned indices/snapshots before the component
    /// dies; Active re-resolves per call so no static leaks worlds.
    public class QualityJobsStore : GameComponent
    {
        private readonly struct MapProductKey : IEquatable<MapProductKey>
        {
            internal readonly Map Map;
            private readonly string product;

            internal MapProductKey(Map map, string product)
            {
                Map = map;
                this.product = product;
            }

            public bool Equals(MapProductKey other) =>
                ReferenceEquals(Map, other.Map)
                && string.Equals(product, other.product,
                    StringComparison.Ordinal);

            public override bool Equals(object? obj) =>
                obj is MapProductKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Map) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(product);
                }
            }
        }

        /// Full world audit: one in-game hour. This is the canonical fallback
        /// for stock counts, stale references, and external state drift.
        public const int PeriodicAuditInterval = 2500;

        /// Narrow responsiveness fallback for idle-UFT pooling, dispatch health,
        /// and external pawn facts that do not expose a reliable event. Critical
        /// pause/configuration changes separately request next-tick reconcile.
        public const int ResponsivenessInterval = 250;

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

        // Cache contract — Owner: this per-save store. Key: map identity plus
        // product defName. Value: spawned unfinished-item count. Dependencies:
        // UFT spawn/despawn and managed-recipe definition reload. Refresh:
        // event-driven, with an exact 2500-tick recovery audit. Equality: an
        // equal audit preserves the published dictionary and BillStatusRevision.
        // Teardown: map removal drops its keys; store lifetime owns both buffers.
        private Dictionary<MapProductKey, int> uftCounts =
            new Dictionary<MapProductKey, int>();
        private Dictionary<MapProductKey, int> rebuiltUftCounts =
            new Dictionary<MapProductKey, int>();

        // Cache contract — Owner: this per-save store. Key: Map identity. Value:
        // mod-owned lists of spawned UFT references. Dependencies: UFT spawn/
        // despawn and managed-recipe definition reload. Refresh: event-driven,
        // with an in-place 2500-tick recovery rebuild. Equality: retained maps
        // keep their list identity across rebuilds. Teardown: map removal and
        // ReleasePresentation remove all references. The 250-tick responsiveness
        // path indexes these lists instead of traversing every map/ThingDef.
        private readonly Dictionary<Map, List<UnfinishedThing>> spawnedUftsByMap =
            new Dictionary<Map, List<UnfinishedThing>>();

        // Cache contract — Owner: this per-save store. Key: UFT identity. Value:
        // its authoritative WorkItemEntry. Dependencies: entry add/remove and
        // load normalization. Refresh: event-driven, with a 2500-tick recovery
        // rebuild. Equality: the dictionary identity is retained and values are
        // the authoritative entry identities. Teardown: map removal, disable,
        // and ReleasePresentation clear all strong UFT references.
        private readonly Dictionary<UnfinishedThing, WorkItemEntry> entriesByUft =
            new Dictionary<UnfinishedThing, WorkItemEntry>();

        private readonly FixedTickBoundaryGate periodicAuditGate =
            new FixedTickBoundaryGate(PeriodicAuditInterval);
        private readonly FixedTickBoundaryGate responsivenessGate =
            new FixedTickBoundaryGate(ResponsivenessInterval);
        private bool reconcileRequested;

        // Presentation revisions are exact consumer domains, not a catch-all.
        // They advance only after a mutation that changes the named result.
        internal int ExternalPawnFactsRevision;
        internal int BillStatusRevision;
        internal int PlanStatusRevision;

        // Transient lifecycle notification — Owner: this per-save store.
        // Subscribers: open construction dialogs only. Value: the replaced and
        // replacement Thing IDs for a plan target. Refresh: immediate after the
        // replacement presentation is published. Teardown: dialogs unsubscribe
        // on close and ReleasePresentation clears any remaining subscribers.
        internal event Action<int, int>? PlanRetargeted;

        // Cache contract — Owner: this per-save store. Key: singleton defaults
        // snapshot. Value: immutable StoreSettingsSnapshot. Dependencies: the
        // fourteen per-save default fields. Refresh: immediate after a successful
        // command and during load/seed. Equality: no-op commands do not republish.
        // Teardown: ReleasePresentation clears the published store-owned state.
        private StoreSettingsSnapshot settingsPresentation = null!;
        internal StoreSettingsSnapshot SettingsPresentation => settingsPresentation;

        // Cache contract — Owner: this per-save store. Key: singleton API feed.
        // Value: immutable ManagedQualityJobsSnapshot; its QJ-owned models and
        // collections never mutate after publication, while RimWorld objects are
        // exposed only as live identity/convenience handles. Dependencies: live
        // production-bill membership, management/configuration, suspension/pause,
        // repeat counters and target-count products; tracked UFT membership; plan
        // membership, target/map/build def/stuff/forbidden state and configuration;
        // definition reloads; external pawn facts consumed by auto-best skill
        // gates. Refresh: event invalidation publishes on the next
        // GameComponentUpdate (including while paused), with the named 250-tick
        // responsiveness and 2500-tick audit fallbacks. Equality: an equal rebuild
        // preserves snapshot identity. Teardown: ReleaseMap clears all published
        // handles before map disposal; ReleasePresentation publishes Empty.
        private ManagedQualityJobsSnapshot managedJobsPresentation =
            ManagedQualityJobsSnapshot.Empty;
        private bool managedJobsDirty = true;
        // Cache contract — Owner: this per-save store. Key: map/product,
        // target-count bill, ingredient filter, and included slot-group identity.
        // Value: private mutable event-routing index; it never escapes. Dependencies:
        // the target-count bills and CountProducts inputs consumed by the API feed.
        // Refresh: replaced atomically after each successful snapshot build.
        // Equality: not observable; the published snapshot has the identity policy.
        // Teardown: ReleaseMap and ReleasePresentation replace the complete index.
        private ManagedTargetCountDependencies managedTargetCountDependencies =
            new ManagedTargetCountDependencies();
        internal ManagedQualityJobsSnapshot ManagedJobsPresentation =>
            managedJobsPresentation;

        // Cache contract — Owner: this per-save store. Key: target thing ID.
        // Value: immutable PlanPresentationSnapshot. Dependencies: plan target,
        // configuration, state, and target map. Refresh: immediate at every plan
        // lifecycle/configuration mutation. Equality: unchanged entries preserve
        // snapshot identity. Teardown: ReleasePresentation clears the dictionary.
        private Dictionary<int, PlanPresentationSnapshot> planPresentations =
            new Dictionary<int, PlanPresentationSnapshot>();

        private sealed class BillPresentationEntry
        {
            internal BillPresentationSnapshot Snapshot;
            internal int BillRevision;
            internal int SpecificCapRevision;
            internal int DefinitionsRevision;

            internal BillPresentationEntry(BillPresentationSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }

        // Cache contract — Owner: this per-save store. Key: stable bill load ID.
        // Value: immutable bill configuration/status inputs; snapshots never hold
        // the Bill or bill giver. Dependencies: bill/default config, product cap,
        // recipe, giver map, and product def. Refresh: lazy after that bill's
        // configuration revision, that product's cap revision, an applicable
        // default-field invalidation, or the definition revision moves. Equality:
        // equal rebuilds preserve the snapshot reference. Teardown: save pruning,
        // map release, or store release.
        private readonly Dictionary<string, BillPresentationEntry> billPresentations =
            new Dictionary<string, BillPresentationEntry>();
        // Per-bill presentation revision: configuration commands and bill-giver
        // location lifecycle events advance only the affected bill IDs.
        private readonly Dictionary<string, int> billConfigRevisions =
            new Dictionary<string, int>();
        private readonly Dictionary<string, int> productCapRevisions =
            new Dictionary<string, int>();
        private int definitionsRevision;

        // Cache contract — Owner: this per-save store. Key: Map identity. Value:
        // immutable MapSnapshot containing resolved matrices/materials only.
        // Dependencies: plan membership, target identity, target map/position/
        // rotation/footprint, and sparkle texture lifetime. Refresh: immediate on
        // plan structure/retarget events; periodic audit is a fallback. Equality:
        // equal rebuilt models/maps preserve reference identity. Teardown:
        // ReleasePresentation drops all maps so removed worlds cannot be rooted.
        private Dictionary<Map, SparkleOverlay.MapSnapshot> overlayPresentations =
            new Dictionary<Map, SparkleOverlay.MapSnapshot>();

        // Cache contract — Owner: this per-save store. Key: primary target thing
        // ID. Value: reusable Command_QualityJob with no Thing reference plus
        // last-seen audit generation.
        // Dependencies: plan presence (icon) and language revision (labels).
        // Refresh: fields updated on access; object allocation only on first key.
        // Equality: the command identity is retained. Teardown: unused entries
        // retire after an audit generation and ReleasePresentation clears all.
        private sealed class ConstructionCommandEntry
        {
            internal readonly Command_QualityJob Command;
            internal int Generation;

            internal ConstructionCommandEntry(Command_QualityJob command, int generation)
            {
                Command = command;
                Generation = generation;
            }
        }

        private readonly Dictionary<int, ConstructionCommandEntry> constructionCommands =
            new Dictionary<int, ConstructionCommandEntry>();
        private readonly List<int> staleConstructionCommandIds = new List<int>();
        private int constructionCommandGeneration;

        // Completion-retry signal — Owner: Game/store. Key: executing bill load
        // ID + current game tick. Value: one transient below-target marker.
        // Dependencies: the final product quality decision for the current bill
        // iteration. Refresh: immediate mark in PostProcessProduct and synchronous
        // consume in Notify_IterationCompleted. Equality: marking the same pair is
        // a no-op. Teardown: the signal dies with this GameComponent; it is not
        // scribed because producer and consumer run in the same call chain.
        private readonly CompletionRetrySignal completionRetry = new CompletionRetrySignal();
        private bool seeded;

        // Existing-bill migration state — authoritative per save. Bills that
        // predate the mod are quarantined with an explicit managed=false
        // override before play begins, then listed here until the host accepts
        // or declines the migration dialog. Sharing does not read this state.
        public const int CurrentExistingBillMigrationVersion = 1;
        public int existingBillMigrationVersion;
        public List<string> pendingExistingBillMigrationIds = new List<string>();
        private bool initializeExistingBillsOnLoad;

        // ---- overlay flag (NOT scribed) ----------------------------------------
        //
        // AnyOverlays is a process-static fast-path pre-check: one bool read per
        // draw call in the common case. It reflects published overlay snapshots,
        // not the authoritative plans list, and ReleasePresentation clears it.

        /// True when at least one published map overlay contains draw models.
        public static bool AnyOverlays;

        // ---- plan mutation helpers ---------------------------------------------

        /// Adds a plan and updates AnyOverlays. All callers must use this instead
        /// of plans.Add directly so the flag stays in sync.
        public void AddPlan(ConstructionPlan plan)
        {
            plans.Add(plan);
            NotifyPlanStructureChanged();
        }

        /// Removes a plan by reference and updates AnyOverlays.
        public void RemovePlan(ConstructionPlan plan)
        {
            if (!plans.Remove(plan)) return;
            NotifyPlanStructureChanged();
        }

        /// Removes a plan by index (for sweep loops iterating backwards).
        public void RemovePlanAt(int index)
        {
            plans.RemoveAt(index);
            NotifyPlanStructureChanged();
        }

        public QualityJobsStore(Game game) { }

        public static QualityJobsStore? Active => Current.Game?.GetComponent<QualityJobsStore>();

        internal bool TryGetPlanPresentation(int thingId,
            out PlanPresentationSnapshot? snapshot)
            => planPresentations.TryGetValue(thingId, out snapshot);

        internal bool TryGetOverlayPresentation(Map map,
            out SparkleOverlay.MapSnapshot? snapshot)
            => overlayPresentations.TryGetValue(map, out snapshot);

        internal BillPresentationSnapshot BillPresentationFor(Bill bill)
        {
            string billId = BillIds.IdOf(bill);
            billPresentations.TryGetValue(billId, out BillPresentationEntry? entry);
            int billRevision = RevisionFor(billConfigRevisions, billId);
            string? cachedProduct = entry != null
                && entry.DefinitionsRevision == definitionsRevision
                    ? entry.Snapshot.ProductDefName : null;
            int specificCap = cachedProduct != null
                ? RevisionFor(productCapRevisions, cachedProduct) : 0;
            if (entry != null
                && entry.BillRevision == billRevision
                && entry.SpecificCapRevision == specificCap
                && entry.DefinitionsRevision == definitionsRevision)
                return entry.Snapshot;

            RecipeDef recipe = bill.recipe;
            string? product = ManagedRecipes.ProductDefName(recipe);
            var candidate = new BillPresentationSnapshot(
                billId, ConfigFor(bill), TargetQualityFor(bill), CapFor(product),
                product, recipe,
                (bill.billStack?.billGiver as Thing)?.MapHeld);
            if (entry == null)
            {
                entry = new BillPresentationEntry(candidate);
                billPresentations.Add(billId, entry);
            }
            else
            {
                if (!entry.Snapshot.HasSameContent(candidate))
                    entry.Snapshot = candidate;
            }
            entry.BillRevision = billRevision;
            entry.SpecificCapRevision = product != null
                ? RevisionFor(productCapRevisions, product) : 0;
            entry.DefinitionsRevision = definitionsRevision;
            return entry.Snapshot;
        }

        private static int RevisionFor(Dictionary<string, int> revisions, string key)
            => revisions.TryGetValue(key, out int revision) ? revision : 0;

        private static void BumpRevisionFor(Dictionary<string, int> revisions,
            string key)
        {
            revisions.TryGetValue(key, out int revision);
            revisions[key] = unchecked(revision + 1);
        }

        internal Command_QualityJob ConstructionCommandFor(int thingId, bool enabled)
        {
            if (!constructionCommands.TryGetValue(thingId,
                    out ConstructionCommandEntry? entry))
            {
                entry = new ConstructionCommandEntry(
                    new Command_QualityJob(thingId), constructionCommandGeneration);
                constructionCommands.Add(thingId, entry);
            }
            entry.Generation = constructionCommandGeneration;
            Command_QualityJob command = entry.Command;
            command.RefreshPresentation(enabled);
            return command;
        }

        internal void NotifyBillConfigurationChanged(string billId,
            bool affectsEligibility)
        {
            BumpRevisionFor(billConfigRevisions, billId);
            InvalidateManagedJobs();
            if (affectsEligibility)
            {
                Bump(ref BillStatusRevision);
                RequestReconcile();
            }
        }

        internal void NotifyBillDefaultsChanged(BillDefaultField field,
            bool affectsEligibility)
        {
            PublishSettingsPresentation();
            InvalidateBillPresentationsUsingDefault(field);
            InvalidateManagedJobs();
            if (affectsEligibility)
            {
                Bump(ref BillStatusRevision);
                RequestReconcile();
            }
        }

        internal void NotifyConstructionDefaultsChanged()
        {
            PublishSettingsPresentation();
        }

        internal void NotifyProductCapChanged(string? productDefName, bool isDefault)
        {
            if (isDefault)
            {
                PublishSettingsPresentation();
                foreach (BillPresentationEntry entry in billPresentations.Values)
                {
                    string? product = entry.Snapshot.ProductDefName;
                    if (product == null || !productCaps.ContainsKey(product))
                        entry.SpecificCapRevision = int.MinValue;
                }
            }
            else if (productDefName != null)
                BumpRevisionFor(productCapRevisions, productDefName);
        }

        internal void NotifyDefinitionsChanged()
        {
            Bump(ref definitionsRevision);
            InvalidateManagedJobs();
            // A definition reload is an explicit, rare event. Rebuild its derived
            // artifacts immediately so a paused game cannot display stale recipe,
            // stock, overlay, eligibility, or expected-attempt data.
            RecountAndPool(publishStatusRevision: false);
            PublishOverlayPresentations();
            NotifyExternalPawnFactsChanged();
        }

        private void InvalidateBillPresentationsUsingDefault(BillDefaultField field)
        {
            foreach (KeyValuePair<string, BillPresentationEntry> pair in billPresentations)
            {
                bool usesDefault;
                switch (field)
                {
                    case BillDefaultField.Managed:
                        usesDefault = !billManaged.ContainsKey(pair.Key);
                        break;
                    case BillDefaultField.MinSkill:
                        usesDefault = !billMinSkill.ContainsKey(pair.Key);
                        break;
                    case BillDefaultField.RequireInspired:
                        usesDefault = !billRequireInspired.ContainsKey(pair.Key);
                        break;
                    case BillDefaultField.RequireSpecialist:
                        usesDefault = !billRequireSpecialist.ContainsKey(pair.Key);
                        break;
                    case BillDefaultField.AutoBest:
                        usesDefault = !billAutoBest.ContainsKey(pair.Key);
                        break;
                    default:
                        usesDefault = !billTargetQuality.ContainsKey(pair.Key);
                        break;
                }
                if (usesDefault) pair.Value.BillRevision = int.MinValue;
            }
        }

        internal void NotifyShareChanged()
        {
            PublishSettingsPresentation();
            if (shareUnfinishedWork)
                PoolIdleUnfinishedWork();
            else
                RemoveSharedEntries();
            Bump(ref BillStatusRevision);
            RequestReconcile();
        }

        internal void NotifyPlanConfigurationChanged()
        {
            PublishPlanPresentations(rebuildOverlays: false);
            InvalidateManagedJobs();
            Bump(ref PlanStatusRevision);
            RequestReconcile();
        }

        internal void NotifyPlanStateChanged()
        {
            PublishPlanPresentations(rebuildOverlays: false);
            Bump(ref PlanStatusRevision);
        }

        internal void NotifyPlanStructureChanged()
        {
            PublishPlanPresentations(rebuildOverlays: true);
            InvalidateManagedJobs();
            Bump(ref PlanStatusRevision);
        }

        internal void NotifyEntriesChanged()
        {
            Bump(ref BillStatusRevision);
        }

        internal void NotifyExternalPawnFactsChanged(bool requestReconcile = true)
        {
            Bump(ref ExternalPawnFactsRevision);
            Bump(ref BillStatusRevision);
            Bump(ref PlanStatusRevision);
            InvalidateManagedJobs();
            if (requestReconcile) RequestReconcile();
        }

        internal void RequestReconcile() => reconcileRequested = true;

        internal void NotifyUftSpawned(UnfinishedThing uft, Map map)
        {
            if (!spawnedUftsByMap.TryGetValue(map,
                    out List<UnfinishedThing>? spawned))
            {
                spawned = new List<UnfinishedThing>();
                spawnedUftsByMap.Add(map, spawned);
            }
            if (!spawned.Contains(uft)) spawned.Add(uft);

            string? product = ManagedRecipes.ProductDefName(uft.Recipe);
            if (product == null) return;
            var key = new MapProductKey(map, product);
            uftCounts.TryGetValue(key, out int count);
            uftCounts[key] = count + 1;
            Bump(ref BillStatusRevision);
        }

        internal void NotifyUftDespawned(UnfinishedThing uft, Map map)
        {
            if (spawnedUftsByMap.TryGetValue(map,
                    out List<UnfinishedThing>? spawned))
                spawned.Remove(uft);

            string? product = ManagedRecipes.ProductDefName(uft.Recipe);
            if (product == null) return;
            var key = new MapProductKey(map, product);
            if (!uftCounts.TryGetValue(key, out int count)) return;
            if (count <= 1) uftCounts.Remove(key);
            else uftCounts[key] = count - 1;
            Bump(ref BillStatusRevision);
        }

        internal void NotifyBillGiverLocationChanged(IBillGiver giver)
        {
            List<Bill> bills = giver.BillStack.Bills;
            bool changed = false;
            for (int i = 0; i < bills.Count; i++)
            {
                if (bills[i] is not Bill_ProductionWithUft bill) continue;
                BumpRevisionFor(billConfigRevisions, BillIds.IdOf(bill));
                changed = true;
            }
            if (!changed) return;
            InvalidateManagedJobs();
            Bump(ref BillStatusRevision);
            RequestReconcile();
        }

        internal void NotifyPlanTargetLocationChanged(Thing thing)
        {
            if (!planPresentations.ContainsKey(thing.thingIDNumber)) return;
            NotifyPlanStructureChanged();
        }

        internal void RetargetPlan(ConstructionPlan plan, Thing target,
            ConstructionPlanState state)
        {
            if (ReferenceEquals(plan.target, target) && plan.state == state) return;
            int previousThingId = plan.target?.thingIDNumber ?? -1;
            plan.target = target;
            plan.state = state;
            plan.finisher = null;
            NotifyPlanStructureChanged();
            if (previousThingId >= 0 && previousThingId != target.thingIDNumber)
                PlanRetargeted?.Invoke(previousThingId, target.thingIDNumber);
        }

        internal void ApplyPlanSettings(int thingId, int minSkill,
            bool requireInspired, bool requireSpecialist, int minQuality,
            bool autoBest)
        {
            minSkill = ConfigurationLimits.Skill(minSkill);
            minQuality = ConfigurationLimits.Quality(minQuality);
            requireSpecialist = requireSpecialist && ModsConfig.IdeologyActive;

            ConstructionPlan? plan = FindPlanById(thingId);
            bool neutral = minSkill == 0 && !requireInspired
                && !requireSpecialist && minQuality == 0 && !autoBest;
            if (neutral)
            {
                if (plan != null)
                {
                    Dispatcher.RemoveOurDeconstructDesignation(plan);
                    RemovePlan(plan);
                }
                return;
            }

            if (plan == null)
            {
                Thing? target = FindSpawnedThing(thingId);
                if (!(target is Blueprint_Build) && !(target is Frame)) return;
                plan = new ConstructionPlan
                {
                    target = target,
                    state = ConstructionPlanState.Active,
                };
                AddPlan(plan);
            }

            if (plan.minSkill == minSkill
                && plan.requireInspired == requireInspired
                && plan.requireSpecialist == requireSpecialist
                && plan.minQuality == minQuality
                && plan.autoBest == autoBest)
                return;

            plan.minSkill = minSkill;
            plan.requireInspired = requireInspired;
            plan.requireSpecialist = requireSpecialist;
            plan.minQuality = minQuality;
            plan.autoBest = autoBest;
            NotifyPlanConfigurationChanged();
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

        internal void PausePlan(ConstructionPlan plan)
        {
            if (plan.state == ConstructionPlanState.Paused && plan.finisher == null)
                return;
            plan.state = ConstructionPlanState.Paused;
            plan.finisher = null;
            NotifyPlanStateChanged();
            RequestReconcile();
        }

        internal void DispatchPlan(ConstructionPlan plan, Pawn finisher)
        {
            plan.finisher = finisher;
            plan.state = ConstructionPlanState.Dispatched;
            NotifyPlanStateChanged();
        }

        internal void DispatchEntry(WorkItemEntry entry, Pawn finisher,
            Bill_ProductionWithUft finishBill)
        {
            entry.state = WorkItemState.Dispatched;
            entry.finisher = finisher;
            entry.finishBill = finishBill;
            NotifyEntriesChanged();
        }

        internal void PauseEntry(WorkItemEntry entry)
        {
            entry.state = WorkItemState.Paused;
            entry.finisher = null;
            entry.finishBill = null;
            NotifyEntriesChanged();
        }

        internal void RegisterFinishBillConfig(string billId, in BillConfig config)
        {
            billManaged[billId] = true;
            billMinSkill[billId] = config.Condition.MinSkill;
            billRequireInspired[billId] = config.Condition.RequireInspired;
            billRequireSpecialist[billId] = config.Condition.RequireSpecialist;
            billAutoBest[billId] = config.AutoBest;
            BumpRevisionFor(billConfigRevisions, billId);
        }

        internal void RemoveFinishBillConfig(string billId)
        {
            billManaged.Remove(billId);
            billMinSkill.Remove(billId);
            billRequireInspired.Remove(billId);
            billRequireSpecialist.Remove(billId);
            billAutoBest.Remove(billId);
            billPresentations.Remove(billId);
            billConfigRevisions.Remove(billId);
        }

        internal void ClearAuthoritativeCollectionsForDisable()
        {
            entries.Clear();
            entriesByUft.Clear();
            plans.Clear();
            InvalidateManagedJobs();
            NotifyEntriesChanged();
            NotifyPlanStructureChanged();
        }

        internal void PublishAllPresentation(bool publishManagedJobs = true)
        {
            PublishSettingsPresentation();
            PublishPlanPresentations(rebuildOverlays: true);
            if (publishManagedJobs) PublishManagedJobsPresentation();
        }

        internal void ReleasePresentation()
        {
            PlanRetargeted = null;
            settingsPresentation = null!;
            planPresentations.Clear();
            billPresentations.Clear();
            overlayPresentations.Clear();
            constructionCommands.Clear();
            staleConstructionCommandIds.Clear();
            constructionCommandGeneration = 0;
            billConfigRevisions.Clear();
            productCapRevisions.Clear();
            definitionsRevision = 0;
            uftCounts.Clear();
            rebuiltUftCounts.Clear();
            spawnedUftsByMap.Clear();
            entriesByUft.Clear();
            managedJobsPresentation = ManagedQualityJobsSnapshot.Empty;
            managedTargetCountDependencies =
                new ManagedTargetCountDependencies();
            managedJobsDirty = false;
            AnyOverlays = false;
        }

        private void PruneConstructionCommands()
        {
            constructionCommandGeneration = unchecked(constructionCommandGeneration + 1);
            int oldestRetainedGeneration = constructionCommandGeneration - 1;
            staleConstructionCommandIds.Clear();
            foreach (KeyValuePair<int, ConstructionCommandEntry> pair in constructionCommands)
                if (pair.Value.Generation < oldestRetainedGeneration)
                    staleConstructionCommandIds.Add(pair.Key);
            for (int i = 0; i < staleConstructionCommandIds.Count; i++)
                constructionCommands.Remove(staleConstructionCommandIds[i]);
            staleConstructionCommandIds.Clear();
        }

        internal void ReleaseMap(Map map)
        {
            // Do not retain any handle from the map while Game removes it. The
            // next update republishes the remaining maps from authoritative data.
            managedJobsPresentation = ManagedQualityJobsSnapshot.Empty;
            managedTargetCountDependencies =
                new ManagedTargetCountDependencies();
            managedJobsDirty = true;
            bool entriesChanged = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry entry = entries[i];
                if (entry.uft?.MapHeld != map) continue;
                Dispatcher.DeleteFinishBill(this, entry);
                if (entry.uft != null) entriesByUft.Remove(entry.uft);
                entries.RemoveAt(i);
                entriesChanged = true;
            }
            if (entriesChanged)
            {
                InvalidateManagedJobs();
                NotifyEntriesChanged();
            }

            bool plansChanged = false;
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                if (plans[i].target?.MapHeld != map) continue;
                plans.RemoveAt(i);
                plansChanged = true;
            }
            if (plansChanged) NotifyPlanStructureChanged();
            else
            {
                overlayPresentations.Remove(map);
                AnyOverlays = overlayPresentations.Count != 0;
            }

            if (billPresentations.Count != 0)
            {
                var deadBills = new List<string>();
                foreach (KeyValuePair<string, BillPresentationEntry> pair in billPresentations)
                    if (ReferenceEquals(pair.Value.Snapshot.Map, map))
                        deadBills.Add(pair.Key);
                for (int i = 0; i < deadBills.Count; i++)
                {
                    billPresentations.Remove(deadBills[i]);
                    billConfigRevisions.Remove(deadBills[i]);
                }
            }

            var deadCountKeys = new List<MapProductKey>();
            foreach (KeyValuePair<MapProductKey, int> pair in uftCounts)
                if (ReferenceEquals(pair.Key.Map, map)) deadCountKeys.Add(pair.Key);
            for (int i = 0; i < deadCountKeys.Count; i++)
                uftCounts.Remove(deadCountKeys[i]);
            spawnedUftsByMap.Remove(map);
        }

        internal void InvalidateManagedJobs() => managedJobsDirty = true;

        internal void NotifyTargetCountThingChanged(Thing thing, Map? map)
        {
            if (map == null) return;
            ThingDef product = thing.def;
            if (thing is MinifiedThing minified && minified.InnerThing != null)
                product = minified.InnerThing.def;
            if (managedTargetCountDependencies.Watches(map, product))
                InvalidateManagedJobs();
        }

        internal void NotifyTargetCountProductChanged(ThingDef product, Map? map)
        {
            if (map != null
                && managedTargetCountDependencies.Watches(map, product))
                InvalidateManagedJobs();
        }

        internal void NotifyTargetCountPawnRegistryChanged(Map? map)
        {
            if (map != null
                && managedTargetCountDependencies.WatchesEquipped(map))
                InvalidateManagedJobs();
        }

        internal void NotifyTargetCountBillInputChanged(Bill_Production bill)
        {
            if (managedTargetCountDependencies.Watches(bill))
                InvalidateManagedJobs();
        }

        internal void NotifyTargetCountFilterChanged(ThingFilter filter)
        {
            if (managedTargetCountDependencies.Watches(filter))
                InvalidateManagedJobs();
        }

        internal void NotifyTargetCountSlotGroupChanged(ISlotGroup group)
        {
            if (managedTargetCountDependencies.Watches(group))
                InvalidateManagedJobs();
        }

        private static void Bump(ref int revision) => revision = unchecked(revision + 1);

        private void PublishSettingsPresentation()
        {
            if (settingsPresentation != null && settingsPresentation.Matches(this))
                return;
            settingsPresentation = new StoreSettingsSnapshot(this);
        }

        private sealed class ConstructionApiGroup
        {
            internal readonly Map Map;
            internal readonly ThingDef BuildableDef;
            internal readonly ThingDef? Stuff;
            internal readonly QualityJobSettings Settings;
            internal readonly double Probability;
            internal readonly List<Thing> Targets = new List<Thing>();

            internal ConstructionApiGroup(Map map, ThingDef buildableDef,
                ThingDef? stuff, in QualityJobSettings settings,
                double probability)
            {
                Map = map;
                BuildableDef = buildableDef;
                Stuff = stuff;
                Settings = settings;
                Probability = probability;
            }

            internal bool Matches(Map map, ThingDef buildableDef,
                ThingDef? stuff, in QualityJobSettings settings,
                double probability)
                => ReferenceEquals(Map, map)
                   && ReferenceEquals(BuildableDef, buildableDef)
                   && ReferenceEquals(Stuff, stuff)
                   && Settings.HasSameContent(settings)
                   && Probability.Equals(probability);
        }

        /// <summary>
        /// Event-routing index for the live inputs consumed by target-count
        /// RecipeWorkerCounter implementations. Owned by this store, rebuilt
        /// with the API snapshot, and never exposed. Exact vanilla counters are
        /// keyed by map/product; custom counters use a conservative map key.
        /// </summary>
        private sealed class ManagedTargetCountDependencies
        {
            private readonly Dictionary<Map, HashSet<ThingDef>> productsByMap =
                new Dictionary<Map, HashSet<ThingDef>>();
            private readonly HashSet<Map> opaqueCounterMaps = new HashSet<Map>();
            private readonly HashSet<Map> equippedMaps = new HashSet<Map>();
            private readonly HashSet<Bill_Production> bills =
                new HashSet<Bill_Production>();
            private readonly HashSet<ThingFilter> filters =
                new HashSet<ThingFilter>();
            private readonly HashSet<ISlotGroup> slotGroups =
                new HashSet<ISlotGroup>();

            internal void Add(Map map, Bill_Production bill,
                RecipeDef recipe, ThingDef product)
            {
                bills.Add(bill);
                if (!productsByMap.TryGetValue(map,
                        out HashSet<ThingDef>? products))
                {
                    products = new HashSet<ThingDef>();
                    productsByMap.Add(map, products);
                }
                products.Add(product);
                RecipeWorkerCounter? counter = recipe.WorkerCounter;
                if (counter == null
                    || counter.GetType() != typeof(RecipeWorkerCounter))
                    opaqueCounterMaps.Add(map);
                if (bill.includeEquipped) equippedMaps.Add(map);
                if (bill.limitToAllowedStuff) filters.Add(bill.ingredientFilter);
                ISlotGroup? group = bill.GetIncludeSlotGroup();
                if (group != null)
                {
                    slotGroups.Add(group);
                    StorageGroup? storageGroup = group.StorageGroup;
                    if (storageGroup != null) slotGroups.Add(storageGroup);
                }
            }

            internal bool Watches(Map map, ThingDef product)
                => opaqueCounterMaps.Contains(map)
                   || productsByMap.TryGetValue(map,
                       out HashSet<ThingDef>? products)
                   && products.Contains(product);

            internal bool WatchesEquipped(Map map)
                => equippedMaps.Contains(map) || opaqueCounterMaps.Contains(map);

            internal bool Watches(Bill_Production bill) => bills.Contains(bill);

            internal bool Watches(ThingFilter filter) => filters.Contains(filter);

            internal bool Watches(ISlotGroup group)
            {
                if (slotGroups.Contains(group)) return true;
                StorageGroup? storageGroup = group.StorageGroup;
                return storageGroup != null && slotGroups.Contains(storageGroup);
            }
        }

        private void PublishManagedJobsPresentation()
        {
            var jobs = new List<ManagedQualityJob>();
            var targetCountDependencies =
                new ManagedTargetCountDependencies();
            PublishManagedBillJobs(jobs, targetCountDependencies);
            PublishManagedConstructionJobs(jobs);

            ManagedQualityJobsSnapshot candidate = jobs.Count == 0
                ? ManagedQualityJobsSnapshot.Empty
                : new ManagedQualityJobsSnapshot(jobs.ToArray());
            managedJobsPresentation = SnapshotPublication.Publish(
                managedJobsPresentation, candidate);
            managedTargetCountDependencies = targetCountDependencies;
            managedJobsDirty = false;
        }

        private void PublishManagedBillJobs(List<ManagedQualityJob> jobs,
            ManagedTargetCountDependencies targetCountDependencies)
        {
            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                List<Thing> givers = map.listerThings.ThingsInGroup(
                    ThingRequestGroup.PotentialBillGiver);
                for (int giverIndex = 0; giverIndex < givers.Count; giverIndex++)
                {
                    Thing giverThing = givers[giverIndex];
                    if (giverThing is not IBillGiver giver
                        || !giverThing.Spawned
                        || !ReferenceEquals(giver.Map, map))
                        continue;
                    List<Bill> bills = giver.BillStack.Bills;
                    for (int billIndex = 0; billIndex < bills.Count; billIndex++)
                    {
                        if (bills[billIndex] is not Bill_ProductionWithUft bill
                            || !ManagedRecipes.IsManagedRecipe(bill.recipe))
                            continue;
                        BillPresentationSnapshot presentation =
                            BillPresentationFor(bill);
                        if (!ManagedJobPolicy.IncludeBill(
                                presentation.Config.Managed,
                                bill.suspended, bill.paused,
                                bill.DeletedOrDereferenced,
                                IsFinishBill(bill)))
                            continue;

                        ThingDef? product = presentation.Recipe.ProducedThingDef;
                        if (product == null) continue;
                        if (!TryCounterFor(bill, presentation.Recipe,
                                out ManagedBillCounter counter))
                            continue;
                        if (counter.Mode == ManagedBillRepeat.TargetCount)
                            targetCountDependencies.Add(map, bill,
                                presentation.Recipe, product);

                        QualityJobSettings settings = SettingsForApi(
                            presentation.Config.Condition,
                            presentation.Config.AutoBest,
                            presentation.TargetQuality,
                            presentation.Recipe,
                            out double probability);
                        UnfinishedThing[] unfinished = UnfinishedItemsFor(bill);
                        jobs.Add(new ManagedBillJob(
                            map, bill, presentation.Recipe, product, counter,
                            unfinished, settings, probability));
                    }
                }
            }
        }

        private void PublishManagedConstructionJobs(List<ManagedQualityJob> jobs)
        {
            var groups = new List<ConstructionApiGroup>();
            Faction? player = Faction.OfPlayer;
            for (int i = 0; i < plans.Count; i++)
            {
                ConstructionPlan plan = plans[i];
                Thing? target = plan.target;
                if (target == null || !target.Spawned || target.MapHeld == null)
                    continue;
                bool forbidden = player != null && target.IsForbidden(player);
                if (!ManagedJobPolicy.IncludeConstruction(
                        forbidden, target.Destroyed))
                    continue;

                bool isBlueprintOrFrame = target is Blueprint_Build
                    || target is Frame;
                ThingDef? buildableDef = isBlueprintOrFrame
                    ? target.def.entityDefToBuild as ThingDef
                    : target is Building ? target.def : null;
                if (buildableDef == null) continue;
                ThingDef? stuff = isBlueprintOrFrame
                    ? ((IConstructible)target).EntityToBuildStuff()
                    : target.Stuff;
                Map map = target.MapHeld;
                QualityJobSettings settings = SettingsForApi(
                    plan.Condition, plan.autoBest, plan.minQuality,
                    recipe: null, out double probability);

                ConstructionApiGroup? group = null;
                for (int g = 0; g < groups.Count; g++)
                    if (groups[g].Matches(map, buildableDef, stuff,
                            settings, probability))
                    {
                        group = groups[g];
                        break;
                    }
                if (group == null)
                {
                    group = new ConstructionApiGroup(map, buildableDef,
                        stuff, settings, probability);
                    groups.Add(group);
                }
                group.Targets.Add(target);
            }

            for (int i = 0; i < groups.Count; i++)
            {
                ConstructionApiGroup group = groups[i];
                jobs.Add(new ManagedConstructionJob(
                    group.Map, group.BuildableDef, group.Stuff,
                    group.Targets.ToArray(), group.Settings,
                    group.Probability));
            }
        }

        private QualityJobSettings SettingsForApi(in ResumeCondition condition,
            bool autoBest, int targetQuality, RecipeDef? recipe,
            out double probability)
        {
            int skillGate = condition.MinSkill;
            if (autoBest
                && Dispatcher.ResolveAutoBestFacts(recipe,
                    condition.RequireInspired, condition.RequireSpecialist,
                    out int resolvedSkill, out _, out _) != null)
                skillGate = resolvedSkill;
            var gate = new ResumeCondition(
                skillGate, condition.RequireInspired,
                condition.RequireSpecialist);
            targetQuality = ConfigurationLimits.Quality(targetQuality);
            probability = GateOdds.SuccessChanceFor(
                gate, targetQuality);
            return new QualityJobSettings(
                skillGate, gate.RequireInspired,
                gate.RequireSpecialist, autoBest,
                (QualityCategory)targetQuality);
        }

        private UnfinishedThing[] UnfinishedItemsFor(Bill_ProductionWithUft bill)
        {
            List<UnfinishedThing>? found = null;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry entry = entries[i];
                UnfinishedThing? uft = entry.uft;
                if (!ReferenceEquals(entry.sourceBill, bill)
                    || uft == null || uft.Destroyed)
                    continue;
                if (found == null) found = new List<UnfinishedThing>();
                found.Add(uft);
            }
            return found?.ToArray() ?? Array.Empty<UnfinishedThing>();
        }

        private static bool TryCounterFor(Bill_Production bill,
            RecipeDef recipe, out ManagedBillCounter counterValue)
        {
            ManagedBillRepeat mode;
            int currentCount = 0;
            int yield = 1;
            if (bill.repeatMode == BillRepeatModeDefOf.Forever)
                mode = ManagedBillRepeat.Forever;
            else if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount)
                mode = ManagedBillRepeat.RepeatCount;
            else if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                mode = ManagedBillRepeat.TargetCount;
                RecipeWorkerCounter counter = recipe.WorkerCounter;
                if (counter == null || !counter.CanCountProducts(bill)
                    || bill.billStack?.billGiver is not Thing giver
                    || !giver.Spawned)
                {
                    counterValue = default;
                    return false;
                }
                currentCount = counter.CountProducts(bill);
                yield = YieldPerIteration(recipe);
            }
            else
            {
                counterValue = default;
                return false;
            }

            int iterations = ManagedBillWorkload.Iterations(
                mode, bill.repeatCount, bill.targetCount,
                currentCount, yield);
            counterValue = new ManagedBillCounter(mode, iterations);
            return true;
        }

        private static int YieldPerIteration(RecipeDef recipe)
        {
            ThingDef? product = recipe.ProducedThingDef;
            if (product == null || recipe.products == null) return 1;
            for (int i = 0; i < recipe.products.Count; i++)
                if (ReferenceEquals(recipe.products[i].thingDef, product))
                    return recipe.products[i].count > 0
                        ? recipe.products[i].count : 1;
            return 1;
        }

        private void PublishPlanPresentations(bool rebuildOverlays)
        {
            var next = new Dictionary<int, PlanPresentationSnapshot>(plans.Count);
            for (int i = 0; i < plans.Count; i++)
            {
                ConstructionPlan plan = plans[i];
                Thing? target = plan.target;
                if (target == null) continue;
                int thingId = target.thingIDNumber;
                var candidate = new PlanPresentationSnapshot(plan);
                if (planPresentations.TryGetValue(thingId,
                        out PlanPresentationSnapshot? current)
                    && current.MinSkill == candidate.MinSkill
                    && current.RequireInspired == candidate.RequireInspired
                    && current.RequireSpecialist == candidate.RequireSpecialist
                    && current.MinQuality == candidate.MinQuality
                    && current.AutoBest == candidate.AutoBest
                    && current.State == candidate.State
                    && ReferenceEquals(current.Map, candidate.Map))
                    candidate = current;
                next[thingId] = candidate;
            }
            planPresentations = next;
            if (rebuildOverlays) PublishOverlayPresentations();
        }

        private void PublishOverlayPresentations()
        {
            var grouped = new Dictionary<Map, List<SparkleOverlay.Model>>();
            for (int i = 0; i < plans.Count; i++)
            {
                Thing? target = plans[i].target;
                if (target == null || target.Destroyed || !target.Spawned
                    || (!(target is Blueprint_Build) && !(target is Frame)))
                    continue;
                Map map = target.Map;
                overlayPresentations.TryGetValue(map,
                    out SparkleOverlay.MapSnapshot? oldMap);
                SparkleOverlay.Model? oldModel = oldMap?.Find(target.thingIDNumber);
                SparkleOverlay.Model model = SparkleOverlay.Model.Build(target, oldModel);
                if (!grouped.TryGetValue(map, out List<SparkleOverlay.Model>? list))
                {
                    list = new List<SparkleOverlay.Model>();
                    grouped.Add(map, list);
                }
                list.Add(model);
            }

            var next = new Dictionary<Map, SparkleOverlay.MapSnapshot>(grouped.Count);
            foreach (KeyValuePair<Map, List<SparkleOverlay.Model>> pair in grouped)
            {
                if (overlayPresentations.TryGetValue(pair.Key,
                        out SparkleOverlay.MapSnapshot? current)
                    && current.HasSameModels(pair.Value))
                    next.Add(pair.Key, current);
                else
                    next.Add(pair.Key,
                        new SparkleOverlay.MapSnapshot(pair.Value.ToArray()));
            }
            overlayPresentations = next;
            AnyOverlays = next.Count != 0;
        }

        public override void FinalizeInit()
        {
            bool firstInitialization = !seeded;
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
            // LoadedGame is load-only; remembering the pre-seed state here
            // distinguishes adding the mod to an existing save from starting a
            // new game and from loading a save Quality Jobs already initialized.
            initializeExistingBillsOnLoad = firstInitialization;
            // LoadedGame must quarantine pre-existing bills before the first API
            // feed is published. Other presentation caches are safe to seed now.
            PublishAllPresentation(publishManagedJobs: false);
        }

        public override void LoadedGame()
        {
            int tick = Find.TickManager.TicksGame;
            periodicAuditGate.Observe(tick);
            responsivenessGate.Observe(tick);
            // Sharing is independent from bill management. Adopt existing idle
            // unfinished items immediately so paused-on-load games do not wait.
            RecountAndPool();
            InitializeExistingBillMigration();
            PublishAllPresentation();
        }

        private void InitializeExistingBillMigration()
        {
            // A save made while the prompt was pending must show it again even
            // though the store is no longer undergoing first initialization.
            if (ExistingBillMigrationPolicy.ShouldShowDialog(
                    pendingExistingBillMigrationIds.Count))
            {
                Dialog_ExistingBillMigration.QueueIfNeeded(this);
                return;
            }

            if (existingBillMigrationVersion >= CurrentExistingBillMigrationVersion)
                return;

            // Existing Quality Jobs saves already made their bill-management
            // choice through the established defaults/configuration. This
            // migration is only for a store first added to an existing save.
            if (!initializeExistingBillsOnLoad)
            {
                existingBillMigrationVersion = CurrentExistingBillMigrationVersion;
                return;
            }

            QuarantineExistingBills();
            initializeExistingBillsOnLoad = false;
            if (!ExistingBillMigrationPolicy.ShouldShowDialog(
                    pendingExistingBillMigrationIds.Count))
            {
                existingBillMigrationVersion = CurrentExistingBillMigrationVersion;
                return;
            }

            Dialog_ExistingBillMigration.QueueIfNeeded(this);
        }

        private void QuarantineExistingBills()
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> potentialGivers = maps[m].listerThings
                    .ThingsInGroup(ThingRequestGroup.PotentialBillGiver);
                for (int t = 0; t < potentialGivers.Count; t++)
                {
                    if (potentialGivers[t] is not IBillGiver giver) continue;
                    List<Bill> bills = giver.BillStack.Bills;
                    for (int b = 0; b < bills.Count; b++)
                    {
                        if (bills[b] is not Bill_ProductionWithUft bill) continue;
                        string id = BillIds.IdOf(bill);
                        bool hasExplicitOverride = billManaged.ContainsKey(id);
                        if (!ExistingBillMigrationPolicy.ShouldQuarantine(
                                firstInitialization: true,
                                supportsQualityJobs: ManagedRecipes.IsManagedRecipe(bill.recipe),
                                hasExplicitOverride,
                                manageNewBillsDefault,
                                targetQualityDefault))
                            continue;

                        // Install the safe override before any post-load tick can
                        // reach the completion gate. The dialog later changes
                        // only these recorded bills if the user opts in.
                        ExistingBillMigrationConfig config =
                            ExistingBillMigrationPolicy.ConfigurationFor(
                                enableQualityJobs: false);
                        billManaged[id] = config.Managed;
                        billAutoBest[id] = config.AutoBest;
                        billRequireInspired[id] = config.RequireInspired;
                        billRequireSpecialist[id] = config.RequireSpecialist;
                        billTargetQuality[id] = config.TargetQuality;
                        BumpRevisionFor(billConfigRevisions, id);
                        billPresentations.Remove(id);
                        InvalidateManagedJobs();
                        pendingExistingBillMigrationIds.Add(id);
                    }
                }
            }
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
            PublishAllPresentation();
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
            entriesByUft.TryGetValue(uft, out WorkItemEntry? entry);
            return entry;
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
               && uftCounts.TryGetValue(
                   new MapProductKey(map, productDefName), out int n) ? n : 0;

        /// <summary>Pipeline counts for a product on a map (status display,
        /// spec §11): Paused entries wait for a finisher, Dispatched entries
        /// are being finished, Shared entries sit in the sharing pool.
        /// Bounded indexed loop over live entries — call from revision-gated
        /// dialog cache builders, never on a steady render pass.</summary>
        public void CountEntriesFor(Map map, string? productDefName,
            out int waiting, out int finishing, out int shared)
        {
            waiting = 0;
            finishing = 0;
            shared = 0;
            if (productDefName == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                UnfinishedThing? uft = e.uft;
                if (uft == null || !uft.Spawned || uft.Map != map) continue;
                if (ManagedRecipes.ProductDefName(uft.Recipe) != productDefName) continue;
                switch (e.state)
                {
                    case WorkItemState.Paused: waiting++; break;
                    case WorkItemState.Dispatched: finishing++; break;
                    case WorkItemState.Shared: shared++; break;
                }
            }
        }

        public void MarkBillRetry(Bill bill)
            => completionRetry.Mark(BillIds.IdOf(bill), Find.TickManager.TicksGame);

        public bool ConsumeBillRetry(Bill bill)
            => completionRetry.Consume(BillIds.IdOf(bill), Find.TickManager.TicksGame);

        public void RegisterPaused(UnfinishedThing uft, Pawn? originalCreator,
            Bill_ProductionWithUft? sourceBill, StyleSnapshot? snapshot)
        {
            WorkItemEntry? entry = FindByUft(uft);
            Bill_ProductionWithUft? previousSource = entry?.sourceBill;
            bool added = entry == null;
            if (entry == null)
            {
                entry = new WorkItemEntry { uft = uft };
                entries.Add(entry);
                entriesByUft.Add(uft, entry);
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
            if (added || !ReferenceEquals(previousSource, entry.sourceBill))
                InvalidateManagedJobs();
            NotifyEntriesChanged();
            RequestReconcile();
        }

        public void RemoveEntry(WorkItemEntry entry)
        {
            if (!entries.Remove(entry)) return;
            if (entry.uft != null) entriesByUft.Remove(entry.uft);
            InvalidateManagedJobs();
            NotifyEntriesChanged();
        }

        // ---- scan (spec §6, §8, §9) -------------------------------------------

        public override void GameComponentUpdate()
        {
            if (managedJobsDirty) PublishManagedJobsPresentation();
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            bool audit = periodicAuditGate.Observe(tick);
            bool responsiveness = responsivenessGate.Observe(tick);
            if (!audit && !responsiveness && !reconcileRequested) return;

            reconcileRequested = false;
            bool statusChanged = false;
            if (audit)
                statusChanged = RecountAndPool(publishStatusRevision: false);
            else if (responsiveness)
                statusChanged = PoolIdleUnfinishedWork();

            // The responsiveness invalidation already advances BillStatus;
            // publish a separate status revision only for a standalone audit.
            if (responsiveness)
                NotifyExternalPawnFactsChanged(requestReconcile: false);
            else if (statusChanged)
                Bump(ref BillStatusRevision);

            SweepEntries();
            DispatchPaused();
            SweepAndDispatchPlans();
            if (audit)
            {
                InvalidateManagedJobs();
                PublishOverlayPresentations();
                PruneConstructionCommands();
            }
        }

        private bool RecountAndPool(bool publishStatusRevision = true)
        {
            RebuildEntryIndex();
            rebuiltUftCounts.Clear();
            bool entriesChanged = !shareUnfinishedWork && RemoveSharedEntries();
            foreach (List<UnfinishedThing> spawned in spawnedUftsByMap.Values)
                spawned.Clear();
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
                        if (!spawnedUftsByMap.TryGetValue(map,
                                out List<UnfinishedThing>? spawned))
                        {
                            spawned = new List<UnfinishedThing>();
                            spawnedUftsByMap.Add(map, spawned);
                        }
                        spawned.Add(uft);
                        string? product = ManagedRecipes.ProductDefName(uft.Recipe);
                        if (product != null)
                        {
                            var key = new MapProductKey(map, product);
                            rebuiltUftCounts.TryGetValue(key, out int n);
                            rebuiltUftCounts[key] = n + 1;
                        }
                        if (TryPool(map, uft)) entriesChanged = true;
                    }
                }
            }
            bool countsChanged = !SameCounts(uftCounts, rebuiltUftCounts);
            if (countsChanged)
            {
                Dictionary<MapProductKey, int> previous = uftCounts;
                uftCounts = rebuiltUftCounts;
                rebuiltUftCounts = previous;
            }
            rebuiltUftCounts.Clear();
            bool changed = countsChanged || entriesChanged;
            if (changed && publishStatusRevision) Bump(ref BillStatusRevision);
            return changed;
        }

        private static bool SameCounts(
            Dictionary<MapProductKey, int> left,
            Dictionary<MapProductKey, int> right)
        {
            if (left.Count != right.Count) return false;
            foreach (KeyValuePair<MapProductKey, int> pair in left)
                if (!right.TryGetValue(pair.Key, out int count)
                    || count != pair.Value)
                    return false;
            return true;
        }

        private bool PoolIdleUnfinishedWork()
        {
            bool changed = false;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!spawnedUftsByMap.TryGetValue(map,
                        out List<UnfinishedThing>? spawned))
                    continue;
                for (int i = 0; i < spawned.Count; i++)
                {
                    UnfinishedThing uft = spawned[i];
                    if (uft.Spawned && ReferenceEquals(uft.Map, map))
                        if (TryPool(map, uft)) changed = true;
                }
            }
            return changed;
        }

        /// Sharing pool (spec §8): idle in-progress UFTs get unbound so bills
        /// unlock; creator untouched. Adopts pre-existing UFTs mid-save.
        private bool TryPool(Map map, UnfinishedThing uft)
        {
            if (!shareUnfinishedWork) return false;
            if (uft.workLeft <= 0f || !uft.Initialized) return false;
            if (FindByUft(uft) != null) return false;
            if (map.reservationManager.IsReservedByAnyoneOf(uft, Faction.OfPlayer))
                return false;

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
            entriesByUft.Add(uft, entry);
            uft.BoundBill = null;
            InvalidateManagedJobs();
            return true;
        }

        private bool RemoveSharedEntries()
        {
            bool changed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry entry = entries[i];
                if (entry.state != WorkItemState.Shared) continue;
                if (entry.uft != null) entriesByUft.Remove(entry.uft);
                entries.RemoveAt(i);
                changed = true;
            }
            if (changed) InvalidateManagedJobs();
            return changed;
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
                    if (e.uft != null) entriesByUft.Remove(e.uft);
                    entries.RemoveAt(i);
                    InvalidateManagedJobs();
                    NotifyEntriesChanged();
                    continue;
                }
                if (e.state == WorkItemState.Dispatched && Dispatcher.DispatchInvalid(this, e))
                    Dispatcher.Revert(this, e);
                if (e.state == WorkItemState.Shared && !shareUnfinishedWork)
                {
                    // M4: sharing toggled off — drop Shared entries unconditionally
                    // so the pool clears immediately (creator intact, not rebound).
                    if (e.uft != null) entriesByUft.Remove(e.uft);
                    entries.RemoveAt(i);
                    InvalidateManagedJobs();
                    NotifyEntriesChanged();
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
                    PausePlan(p);
                }
                if (p.state == ConstructionPlanState.Paused)
                {
                    // Self-heal work overshoot on already-paused frames (saves
                    // made before the gate clamped it): vanilla's frame renderer
                    // does not clamp PercentComplete and draws phantom tiles
                    // outside the footprint past 100% (Frame.cs:487).
                    if (t is Frame pausedFrame && pausedFrame.workDone > pausedFrame.WorkToBuild)
                        pausedFrame.workDone = pausedFrame.WorkToBuild;
                    Dispatcher.TryDispatchConstruction(this, p);
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
            int removedPlans = plans.RemoveAll(p => p.target == null || p.target.Destroyed);
            if (removedPlans != 0) NotifyPlanStructureChanged();

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

            deadKeys.Clear();
            foreach (string key in billPresentations.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billPresentations.Remove(deadKeys[i]);

            deadKeys.Clear();
            foreach (string key in billConfigRevisions.Keys)
                if (!liveBillIds.Contains(key)) deadKeys.Add(key);
            for (int i = 0; i < deadKeys.Count; i++) billConfigRevisions.Remove(deadKeys[i]);
        }

        private void NormalizeLoadedState()
        {
            minSkillDefault = ConfigurationLimits.Skill(minSkillDefault);
            targetQualityDefault = ConfigurationLimits.Quality(targetQualityDefault);
            productCapDefault = ConfigurationLimits.StockCap(productCapDefault);
            constructionMinSkillDefault =
                ConfigurationLimits.Skill(constructionMinSkillDefault);
            constructionTargetQualityDefault =
                ConfigurationLimits.Quality(constructionTargetQualityDefault);
            if (!ModsConfig.IdeologyActive)
            {
                requireSpecialistDefault = false;
                constructionRequireSpecialistDefault = false;
            }

            var billIds = new List<string>(billMinSkill.Keys);
            for (int i = 0; i < billIds.Count; i++)
            {
                string id = billIds[i];
                billMinSkill[id] = ConfigurationLimits.Skill(billMinSkill[id]);
            }
            billIds.Clear();
            billIds.AddRange(billTargetQuality.Keys);
            for (int i = 0; i < billIds.Count; i++)
            {
                string id = billIds[i];
                billTargetQuality[id] =
                    ConfigurationLimits.Quality(billTargetQuality[id]);
            }
            if (!ModsConfig.IdeologyActive)
            {
                billIds.Clear();
                billIds.AddRange(billRequireSpecialist.Keys);
                for (int i = 0; i < billIds.Count; i++)
                    billRequireSpecialist[billIds[i]] = false;
            }

            var products = new List<string>(productCaps.Keys);
            for (int i = 0; i < products.Count; i++)
            {
                string product = products[i];
                productCaps[product] = ConfigurationLimits.StockCap(productCaps[product]);
            }

            for (int i = plans.Count - 1; i >= 0; i--)
            {
                ConstructionPlan plan = plans[i];
                plan.minSkill = ConfigurationLimits.Skill(plan.minSkill);
                plan.minQuality = ConfigurationLimits.Quality(plan.minQuality);
                if (!ModsConfig.IdeologyActive) plan.requireSpecialist = false;
                if (plan.minSkill == 0 && !plan.requireInspired
                    && !plan.requireSpecialist && plan.minQuality == 0
                    && !plan.autoBest)
                {
                    Dispatcher.RemoveOurDeconstructDesignation(plan);
                    plans.RemoveAt(i);
                }
            }
            pendingCopyActive = false;
            RebuildEntryIndex();
        }

        private void RebuildEntryIndex()
        {
            entriesByUft.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry entry = entries[i];
                UnfinishedThing? uft = entry.uft;
                if (uft != null && !entriesByUft.ContainsKey(uft))
                    entriesByUft.Add(uft, entry);
            }
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
            Scribe_Values.Look(ref existingBillMigrationVersion,
                "existingBillMigrationVersion", 0);
            Scribe_Collections.Look(ref pendingExistingBillMigrationIds,
                "pendingExistingBillMigrationIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Null-harden collections FIRST: absent XML nodes leave them
                // null, and the finish-bill cleanup below touches the config
                // dictionaries via DeleteFinishBill.
                entries ??= new List<WorkItemEntry>();
                plans ??= new List<ConstructionPlan>();
                plans.RemoveAll(p => p?.target == null);
                billManaged ??= new Dictionary<string, bool>();
                billMinSkill ??= new Dictionary<string, int>();
                billRequireInspired ??= new Dictionary<string, bool>();
                billRequireSpecialist ??= new Dictionary<string, bool>();
                billAutoBest ??= new Dictionary<string, bool>();
                billTargetQuality ??= new Dictionary<string, int>();
                productCaps ??= new Dictionary<string, int>();
                pendingExistingBillMigrationIds ??= new List<string>();
                // Fix C1/I4: clean up any finish bills for entries whose UFTs
                // failed to resolve (null uft after load). DeleteFinishBill
                // guards against a null finishBill internally.
                foreach (WorkItemEntry entry in entries)
                    if (entry?.uft == null)
                        Dispatcher.DeleteFinishBill(this, entry!);
                entries.RemoveAll(e => e?.uft == null);
                NormalizeLoadedState();
                // LoadedGame completes legacy-bill quarantine before the API
                // feed is allowed to publish live bill handles.
                PublishAllPresentation(publishManagedJobs: false);
            }
        }
    }
}
