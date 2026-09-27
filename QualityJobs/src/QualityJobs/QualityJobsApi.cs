using System;
using System.Collections.Generic;
using QualityJobs.Core;
using RimShared.Common;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// <summary>
    /// Cached, read-only integration surface for other mods plus the supported
    /// commands for creating and managing production bills. Callers must use
    /// this surface from RimWorld's main thread because its identity handles
    /// are live game objects.
    /// </summary>
    public static class QualityJobsApi
    {
        /// <summary>
        /// 1: GetManagedJobs and CreateQualityBill. 2: adds ManageBill and
        /// UnmanageBill; every version 1 member is unchanged. Callers binding
        /// by reflection read this constant and require at least 2 before
        /// looking up the version 2 methods.
        /// </summary>
        public const int ApiVersion = 2;

        /// <summary>
        /// Returns the currently published immutable snapshot. Cache hits do no
        /// game-state traversal and return the same reference until its consumed
        /// dependencies change.
        /// </summary>
        public static ManagedQualityJobsSnapshot GetManagedJobs()
            => QualityJobsStore.Active?.ManagedJobsPresentation
               ?? ManagedQualityJobsSnapshot.Empty;

        /// <summary>
        /// Creates one repeat-count production bill using the current per-save
        /// Quality Jobs defaults. Success means the deterministic command was
        /// accepted; Multiplayer may replay it after this call returns.
        /// </summary>
        public static CreateQualityBillResult CreateQualityBill(
            Thing billGiver, ThingDef product)
            => CreateQualityBillCore(billGiver, product, null);

        /// <summary>
        /// Creates one repeat-count production bill using explicit Quality Jobs
        /// options. Values are normalized by the authoritative command.
        /// </summary>
        public static CreateQualityBillResult CreateQualityBill(
            Thing billGiver, ThingDef product, QualityBillOptions options)
            => CreateQualityBillCore(billGiver, product, options);

        private static CreateQualityBillResult CreateQualityBillCore(
            Thing? billGiver, ThingDef? product, QualityBillOptions? options)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null)
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.QualityJobsInactive);
            if (billGiver is not IBillGiver giver)
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.InvalidBillGiver);
            if (!billGiver.Spawned || billGiver.MapHeld == null
                || !ReferenceEquals(giver.Map, billGiver.MapHeld))
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.BillGiverUnavailable);
            if (product == null)
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.UnsupportedProduct);
            if (giver.BillStack.Count >= BillStack.MaxCount)
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.BillStackFull);

            RecipeDef? recipe = null;
            List<RecipeDef> recipes = billGiver.def.AllRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef candidate = recipes[i];
                if (!ManagedRecipes.IsManagedRecipe(candidate)
                    || !ReferenceEquals(candidate.ProducedThingDef, product))
                    continue;
                if (recipe != null && !ReferenceEquals(recipe, candidate))
                    return new CreateQualityBillResult(
                        CreateQualityBillStatus.AmbiguousRecipe);
                recipe = candidate;
            }
            if (recipe == null)
                return new CreateQualityBillResult(
                    CreateQualityBillStatus.UnsupportedProduct);

            bool explicitOptions = options.HasValue;
            QualityBillOptions values = options ?? new QualityBillOptions(
                store.minSkillDefault,
                store.requireInspiredDefault,
                store.requireSpecialistDefault,
                store.autoBestDefault,
                (QualityCategory)store.targetQualityDefault);
            Commands.CreateQualityBillFromApi(new CreateQualityBillValues
            {
                billGiverThingId = billGiver.thingIDNumber,
                mapUniqueId = billGiver.MapHeld.uniqueID,
                productDefName = product.defName,
                recipeDefName = recipe.defName,
                explicitOptions = explicitOptions,
                skillGate = values.SkillGate,
                requireInspired = values.RequireInspired,
                requireSpecialist = values.RequireSpecialist,
                autoBest = values.AutoBest,
                targetQuality = (int)values.TargetQuality,
            });
            return new CreateQualityBillResult(CreateQualityBillStatus.Success);
        }

        /// <summary>
        /// Places a caller-created bill under Quality Jobs management with an
        /// explicit target quality (API version 2).
        /// <para>Writes only the bill's managed flag and target quality, both
        /// pinned so later per-save default edits cannot undo them. The skill
        /// gate, inspiration, specialist, and auto-best settings keep following
        /// the per-save defaults (or the bill's existing overrides): the
        /// finisher gate is Quality Jobs' own. Quality Jobs never reads or
        /// writes <c>bill.allowedSkillRange</c>, so a minimum skill the caller
        /// set still limits who may start the bill; Quality Jobs never lowers
        /// it. A caller that wants only Quality Jobs to decide who works the
        /// bill leaves that range open.</para>
        /// <para>A managed bill is paused at completion for a crafter who
        /// fails the gate (target quality 0 still gates on skill), counts
        /// toward the per-product unfinished-item cap, and, in RepeatCount
        /// mode, retries below-target results: the below-target product stays
        /// spawned like any finished product, and the bill keeps its repeat
        /// count, so it stays alive and makes the item again.</para>
        /// <para>Success means the synced command was issued; Multiplayer may
        /// apply it after this call returns. Repeating an identical request
        /// changes nothing.</para>
        /// </summary>
        /// <param name="targetQuality">0 (Awful, any quality accepted) to 6
        /// (Legendary), as <c>(int)QualityCategory</c>.</param>
        /// <returns>
        /// <see cref="BillManagementStatus.Success"/>,
        /// <see cref="BillManagementStatus.QualityJobsInactive"/>,
        /// <see cref="BillManagementStatus.InvalidBill"/>,
        /// <see cref="BillManagementStatus.BillUnavailable"/>,
        /// <see cref="BillManagementStatus.UnsupportedRecipe"/>, or
        /// <see cref="BillManagementStatus.InvalidTargetQuality"/>. Nothing
        /// changes unless the status is Success.
        /// </returns>
        public static BillManagementStatus ManageBill(
            Bill_ProductionWithUft bill, int targetQuality)
        {
            BillManagementStatus status = Validate(bill, out string? billId);
            if (status != BillManagementStatus.Success) return status;
            if (!BillManagement.IsValidTargetQuality(targetQuality))
                return BillManagementStatus.InvalidTargetQuality;
            Commands.ManageBillFromApi(billId!, targetQuality);
            return BillManagementStatus.Success;
        }

        /// <summary>
        /// Hands a bill back to vanilla behaviour (API version 2): no
        /// completion gate, no finisher dispatch, no below-target retry, no
        /// unfinished-item cap. The unmanaged state is pinned, so it also
        /// overrides the per-save "manage new bills" default for this bill.
        /// Unfinished items of this bill that the gate had paused or
        /// dispatched are released to a vanilla crafter. Any stored target
        /// quality is kept but unused while the bill is unmanaged.
        /// Success means the synced command was issued; repeating it changes
        /// nothing.
        /// </summary>
        /// <returns>
        /// <see cref="BillManagementStatus.Success"/>,
        /// <see cref="BillManagementStatus.QualityJobsInactive"/>,
        /// <see cref="BillManagementStatus.InvalidBill"/>,
        /// <see cref="BillManagementStatus.BillUnavailable"/>, or
        /// <see cref="BillManagementStatus.UnsupportedRecipe"/> (Quality Jobs
        /// never manages that recipe, so the bill already behaves vanilla).
        /// Nothing changes unless the status is Success.
        /// </returns>
        public static BillManagementStatus UnmanageBill(Bill_ProductionWithUft bill)
        {
            BillManagementStatus status = Validate(bill, out string? billId);
            if (status != BillManagementStatus.Success) return status;
            Commands.UnmanageBillFromApi(billId!);
            return BillManagementStatus.Success;
        }

        private static BillManagementStatus Validate(
            Bill_ProductionWithUft? bill, out string? billId)
        {
            billId = null;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return BillManagementStatus.QualityJobsInactive;
            if (bill == null || store.IsFinishBill(bill))
                return BillManagementStatus.InvalidBill;
            // billStack first: vanilla DeletedOrDereferenced dereferences it,
            // and a bill that was never added to a stack has none.
            if (bill.billStack?.billGiver is not Thing giver
                || bill.DeletedOrDereferenced
                || !giver.Spawned
                || !bill.billStack.Bills.Contains(bill))
                return BillManagementStatus.BillUnavailable;
            if (!ManagedRecipes.IsManagedRecipe(bill.recipe))
                return BillManagementStatus.UnsupportedRecipe;
            billId = BillIds.IdOf(bill);
            return BillManagementStatus.Success;
        }
    }

    /// <summary>Result of QualityJobsApi.ManageBill and UnmanageBill (API
    /// version 2). Nothing changes unless the status is Success.</summary>
    public enum BillManagementStatus
    {
        /// <summary>The synced command was issued. Multiplayer may apply it
        /// after the call returns.</summary>
        Success = 0,
        /// <summary>No Quality Jobs store in the current game: no game is
        /// loaded, or Quality Jobs is disabled for this save.</summary>
        QualityJobsInactive = 1,
        /// <summary>The bill is null, or is one of Quality Jobs' own one-shot
        /// finish bills.</summary>
        InvalidBill = 2,
        /// <summary>The bill is deleted, or is not on the bill stack of a
        /// spawned bill giver.</summary>
        BillUnavailable = 3,
        /// <summary>Quality Jobs does not manage the recipe: it makes no
        /// unfinished item, its product has no quality, or an ingredient
        /// decides that quality rather than the crafter (Vanilla Genetics
        /// Expanded genoframes). Keep the bill's own skill limits.</summary>
        UnsupportedRecipe = 4,
        /// <summary>ManageBill only: the target quality is outside 0
        /// (Awful) to 6 (Legendary).</summary>
        InvalidTargetQuality = 5,
    }

    /// <summary>Immutable published collection of all active managed work.</summary>
    public sealed class ManagedQualityJobsSnapshot
        : IContentSnapshot<ManagedQualityJobsSnapshot>
    {
        private static readonly IReadOnlyList<ManagedQualityJob> EmptyJobs =
            Array.AsReadOnly(Array.Empty<ManagedQualityJob>());

        internal static readonly ManagedQualityJobsSnapshot Empty =
            new ManagedQualityJobsSnapshot(Array.Empty<ManagedQualityJob>());

        private readonly IReadOnlyList<ManagedQualityJob> jobs;

        internal ManagedQualityJobsSnapshot(ManagedQualityJob[] jobs)
        {
            this.jobs = jobs.Length == 0 ? EmptyJobs : Array.AsReadOnly(jobs);
        }

        public IReadOnlyList<ManagedQualityJob> Jobs => jobs;

        bool IContentSnapshot<ManagedQualityJobsSnapshot>.HasSameContent(
            ManagedQualityJobsSnapshot other) => HasSameContent(other);

        internal bool HasSameContent(ManagedQualityJobsSnapshot other)
        {
            if (jobs.Count != other.jobs.Count) return false;
            for (int i = 0; i < jobs.Count; i++)
                if (!jobs[i].HasSameContent(other.jobs[i])) return false;
            return true;
        }
    }

    /// <summary>Captured Quality Jobs gate and target values.</summary>
    public readonly struct QualityJobSettings
    {
        internal QualityJobSettings(int skillGate, bool requireInspired,
            bool requireSpecialist, bool autoBest, QualityCategory targetQuality)
        {
            SkillGate = skillGate;
            RequireInspired = requireInspired;
            RequireSpecialist = requireSpecialist;
            AutoBest = autoBest;
            TargetQuality = targetQuality;
        }

        public int SkillGate { get; }
        public bool RequireInspired { get; }
        public bool RequireSpecialist { get; }
        public bool AutoBest { get; }
        public QualityCategory TargetQuality { get; }

        internal bool HasSameContent(in QualityJobSettings other)
            => SkillGate == other.SkillGate
               && RequireInspired == other.RequireInspired
               && RequireSpecialist == other.RequireSpecialist
               && AutoBest == other.AutoBest
               && TargetQuality == other.TargetQuality;
    }

    public abstract class ManagedQualityJob
    {
        internal ManagedQualityJob(Map map, in QualityJobSettings settings,
            double probabilityAtOrAboveTarget)
        {
            Map = map;
            Settings = settings;
            ProbabilityAtOrAboveTarget = probabilityAtOrAboveTarget;
        }

        public Map Map { get; }
        public QualityJobSettings Settings { get; }
        public double ProbabilityAtOrAboveTarget { get; }

        internal bool HasSameCommonContent(ManagedQualityJob other)
            => ReferenceEquals(Map, other.Map)
               && Settings.HasSameContent(other.Settings)
               && ProbabilityAtOrAboveTarget.Equals(
                   other.ProbabilityAtOrAboveTarget);

        internal abstract bool HasSameContent(ManagedQualityJob other);
    }

    public sealed class ManagedBillJob : ManagedQualityJob
    {
        private static readonly IReadOnlyList<UnfinishedThing> EmptyItems =
            Array.AsReadOnly(Array.Empty<UnfinishedThing>());
        private readonly IReadOnlyList<UnfinishedThing> unfinishedItems;
        private readonly ManagedBillCounter counter;

        internal ManagedBillJob(Map map, Bill_ProductionWithUft bill,
            RecipeDef recipe, ThingDef product, in ManagedBillCounter counter,
            UnfinishedThing[] unfinishedItems, in QualityJobSettings settings,
            double probabilityAtOrAboveTarget)
            : base(map, settings, probabilityAtOrAboveTarget)
        {
            Bill = bill;
            Recipe = recipe;
            Product = product;
            this.counter = counter;
            this.unfinishedItems = unfinishedItems.Length == 0
                ? EmptyItems : Array.AsReadOnly(unfinishedItems);
        }

        public Bill_ProductionWithUft Bill { get; }
        public RecipeDef Recipe { get; }
        public ThingDef Product { get; }
        /// <summary>Normalized Forever, RepeatCount, or TargetCount mode.</summary>
        public ManagedBillRepeat RepeatMode => counter.Mode;
        public int RemainingAcceptedIterations =>
            counter.RemainingAcceptedIterations;
        public IReadOnlyList<UnfinishedThing> UnfinishedItems => unfinishedItems;

        internal override bool HasSameContent(ManagedQualityJob other)
        {
            if (other is not ManagedBillJob bill
                || !HasSameCommonContent(bill)
                || !ReferenceEquals(Bill, bill.Bill)
                || !ReferenceEquals(Recipe, bill.Recipe)
                || !ReferenceEquals(Product, bill.Product)
                || !counter.HasSameContent(bill.counter)
                || unfinishedItems.Count != bill.unfinishedItems.Count)
                return false;
            for (int i = 0; i < unfinishedItems.Count; i++)
                if (!ReferenceEquals(unfinishedItems[i], bill.unfinishedItems[i]))
                    return false;
            return true;
        }
    }

    public sealed class ManagedConstructionJob : ManagedQualityJob
    {
        private readonly IReadOnlyList<Thing> targets;

        internal ManagedConstructionJob(Map map, ThingDef buildableDef,
            ThingDef? stuff, Thing[] targets, in QualityJobSettings settings,
            double probabilityAtOrAboveTarget)
            : base(map, settings, probabilityAtOrAboveTarget)
        {
            BuildableDef = buildableDef;
            Stuff = stuff;
            this.targets = Array.AsReadOnly(targets);
        }

        public ThingDef BuildableDef { get; }
        /// <summary>
        /// Selected construction material. For blueprints and frames this is
        /// IConstructible.EntityToBuildStuff(); for completed buildings it is
        /// Thing.Stuff.
        /// </summary>
        public ThingDef? Stuff { get; }
        public IReadOnlyList<Thing> Targets => targets;
        public int Count => targets.Count;

        internal override bool HasSameContent(ManagedQualityJob other)
        {
            if (other is not ManagedConstructionJob construction
                || !HasSameCommonContent(construction)
                || !ReferenceEquals(BuildableDef, construction.BuildableDef)
                || !ReferenceEquals(Stuff, construction.Stuff)
                || targets.Count != construction.targets.Count)
                return false;
            for (int i = 0; i < targets.Count; i++)
                if (!ReferenceEquals(targets[i], construction.targets[i]))
                    return false;
            return true;
        }
    }

    /// <summary>Explicit QJ options for a newly created production bill.</summary>
    public readonly struct QualityBillOptions
    {
        public QualityBillOptions(int skillGate, bool requireInspired,
            bool requireSpecialist, bool autoBest, QualityCategory targetQuality)
        {
            SkillGate = skillGate;
            RequireInspired = requireInspired;
            RequireSpecialist = requireSpecialist;
            AutoBest = autoBest;
            TargetQuality = targetQuality;
        }

        public int SkillGate { get; }
        public bool RequireInspired { get; }
        public bool RequireSpecialist { get; }
        public bool AutoBest { get; }
        public QualityCategory TargetQuality { get; }
    }

    public enum CreateQualityBillStatus
    {
        Success = 0,
        QualityJobsInactive = 1,
        InvalidBillGiver = 2,
        BillGiverUnavailable = 3,
        UnsupportedProduct = 4,
        AmbiguousRecipe = 5,
        BillStackFull = 6,
    }

    public readonly struct CreateQualityBillResult
    {
        internal CreateQualityBillResult(CreateQualityBillStatus status)
        {
            Status = status;
        }

        public CreateQualityBillStatus Status { get; }
        public bool Succeeded => Status == CreateQualityBillStatus.Success;
    }
}
