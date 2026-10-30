using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Immutable values consumed by the mod-settings render path. The owning
    /// store republishes only after a successful command or lifecycle load.
    internal sealed class StoreSettingsSnapshot
    {
        internal readonly bool ManageNewBills;
        internal readonly int MinSkill;
        internal readonly bool RequireInspired;
        internal readonly bool RequireSpecialist;
        internal readonly bool AutoBest;
        internal readonly int TargetQuality;
        internal readonly int ProductCap;
        internal readonly bool ShareUnfinishedWork;
        internal readonly bool HighPriorityFinish;
        internal readonly bool AllowRobotPreparation;
        internal readonly bool ManageNewConstruction;
        internal readonly int ConstructionMinSkill;
        internal readonly bool ConstructionRequireInspired;
        internal readonly bool ConstructionRequireSpecialist;
        internal readonly int ConstructionTargetQuality;
        internal readonly bool ConstructionAutoBest;

        internal StoreSettingsSnapshot(QualityJobsStore store)
        {
            ManageNewBills = store.manageNewBillsDefault;
            MinSkill = store.minSkillDefault;
            RequireInspired = store.requireInspiredDefault;
            RequireSpecialist = store.requireSpecialistDefault;
            AutoBest = store.autoBestDefault;
            TargetQuality = store.targetQualityDefault;
            ProductCap = store.productCapDefault;
            ShareUnfinishedWork = store.shareUnfinishedWork;
            HighPriorityFinish = store.highPriorityFinish;
            AllowRobotPreparation = store.allowRobotPreparation;
            ManageNewConstruction = store.manageNewConstructionDefault;
            ConstructionMinSkill = store.constructionMinSkillDefault;
            ConstructionRequireInspired = store.constructionRequireInspiredDefault;
            ConstructionRequireSpecialist = store.constructionRequireSpecialistDefault;
            ConstructionTargetQuality = store.constructionTargetQualityDefault;
            ConstructionAutoBest = store.constructionAutoBestDefault;
        }

        internal bool Matches(QualityJobsStore store)
            => ManageNewBills == store.manageNewBillsDefault
               && MinSkill == store.minSkillDefault
               && RequireInspired == store.requireInspiredDefault
               && RequireSpecialist == store.requireSpecialistDefault
               && AutoBest == store.autoBestDefault
               && TargetQuality == store.targetQualityDefault
               && ProductCap == store.productCapDefault
               && ShareUnfinishedWork == store.shareUnfinishedWork
               && HighPriorityFinish == store.highPriorityFinish
               && AllowRobotPreparation == store.allowRobotPreparation
               && ManageNewConstruction == store.manageNewConstructionDefault
               && ConstructionMinSkill == store.constructionMinSkillDefault
               && ConstructionRequireInspired
                    == store.constructionRequireInspiredDefault
               && ConstructionRequireSpecialist
                    == store.constructionRequireSpecialistDefault
               && ConstructionTargetQuality
                    == store.constructionTargetQualityDefault
               && ConstructionAutoBest == store.constructionAutoBestDefault;
    }

    /// Immutable construction-plan values consumed by dialogs and gizmos.
    /// Map, target and construction definition are externally owned identities;
    /// only gated builders inspect the target's current access state. Definition
    /// reload and plan retargeting republish these dependencies. No mutable game
    /// collection is exposed through this snapshot.
    internal sealed class PlanPresentationSnapshot
    {
        internal readonly int ThingId;
        internal readonly int MinSkill;
        internal readonly bool RequireInspired;
        internal readonly bool RequireSpecialist;
        internal readonly int MinQuality;
        internal readonly bool AutoBest;
        internal readonly ConstructionPlanState State;
        internal readonly Map? Map;
        internal readonly Thing? Target;
        internal readonly ThingDef? ConstructionDef;

        internal ResumeCondition Condition =>
            new ResumeCondition(MinSkill, RequireInspired, RequireSpecialist);

        internal PlanPresentationSnapshot(ConstructionPlan plan)
        {
            Thing? target = plan.target;
            ThingId = target?.thingIDNumber ?? -1;
            MinSkill = plan.minSkill;
            RequireInspired = plan.requireInspired;
            RequireSpecialist = plan.requireSpecialist;
            MinQuality = plan.minQuality;
            AutoBest = plan.autoBest;
            State = plan.state;
            Map = target?.MapHeld;
            Target = target;
            ConstructionDef = target?.def;
        }
    }

    internal sealed class BillPresentationSnapshot
    {
        internal readonly string BillId;
        internal readonly BillConfig Config;
        internal readonly int TargetQuality;
        internal readonly int ProductCap;
        internal readonly string? ProductDefName;
        internal readonly RecipeDef Recipe;
        internal readonly Map? Map;

        internal BillPresentationSnapshot(string billId, BillConfig config,
            int targetQuality, int productCap, string? productDefName,
            RecipeDef recipe, Map? map)
        {
            BillId = billId;
            Config = config;
            TargetQuality = targetQuality;
            ProductCap = productCap;
            ProductDefName = productDefName;
            Recipe = recipe;
            Map = map;
        }

        internal bool HasSameContent(BillPresentationSnapshot other)
            => Config.Equals(other.Config)
               && TargetQuality == other.TargetQuality
               && ProductCap == other.ProductCap
               && string.Equals(ProductDefName, other.ProductDefName,
                   System.StringComparison.Ordinal)
               && ReferenceEquals(Recipe, other.Recipe)
               && ReferenceEquals(Map, other.Map);
    }
}
