using System;
using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// <summary>
    /// Cached, read-only integration surface for other mods plus the one
    /// supported command for creating a managed production bill. Callers must
    /// use this surface from RimWorld's main thread because its identity handles
    /// are live game objects.
    /// </summary>
    public static class QualityJobsApi
    {
        public const int ApiVersion = 1;

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
