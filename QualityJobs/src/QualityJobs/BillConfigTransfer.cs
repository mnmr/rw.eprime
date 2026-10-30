using QualityJobs.Core;
using RimShared.Common;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Detached clipboard data, promoted by the synchronized AddBill lifecycle.
    internal static class BillConfigTransfer
    {
        internal sealed class Config : IExposable
        {
            internal bool Managed, AutoBest, Inspired, Specialist;
            internal int MinSkill, TargetQuality;

            public void ExposeData()
            {
                Scribe_Values.Look(ref Managed, "managed");
                Scribe_Values.Look(ref AutoBest, "autoBest");
                Scribe_Values.Look(ref Inspired, "inspired");
                Scribe_Values.Look(ref Specialist, "specialist");
                Scribe_Values.Look(ref MinSkill, "minSkill");
                Scribe_Values.Look(ref TargetQuality, "targetQuality");
            }
        }

        // Owner: store (weak). Key: detached Bill identity (weak). Value: copied
        // primitive config, immutable after staging. Dependencies: source config
        // at clone time. Refresh: clone/Scribe lifecycle. Equality: staged value
        // reused across clipboard clones. Teardown: consumed on AddBill; game
        // disposal clears the table. No value retains a game object.
        private static readonly OwnerScopedTransferTable<Bill, QualityJobsStore, Config> staged =
            new OwnerScopedTransferTable<Bill, QualityJobsStore, Config>();

        internal static void Clone(Bill source, Bill clone)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || source is not Bill_ProductionWithUft) return;
            if (staged.Propagate(source, clone, store)) return;
            // Native clipboard clones are detached. An unstaged clone (for
            // example after disable/re-enable) has no source configuration;
            // DeletedOrDereferenced itself assumes a non-null BillStack.
            if (source.billStack == null || source.DeletedOrDereferenced
                || !ManagedRecipes.IsManagedRecipe(source.recipe)) return;
            BillConfig config = store.ConfigFor(source);
            staged.Set(clone, store, new Config
            {
                Managed = config.Managed, AutoBest = config.AutoBest,
                MinSkill = config.Condition.MinSkill,
                Inspired = config.Condition.RequireInspired,
                Specialist = config.Condition.RequireSpecialist,
                TargetQuality = store.TargetQualityFor(source)
            });
        }

        internal static void Expose(Bill bill)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            staged.TryGet(bill, store, out Config? config);
            // Multiplayer exposes detached AddBill parameters through Scribe.
            // Attached bills already consumed the marker; normal saves omit it.
            if (Scribe.mode == LoadSaveMode.Saving && config == null) return;
            Scribe_Deep.Look(ref config, "qualityJobs_clonedConfig");
            if (Scribe.mode == LoadSaveMode.LoadingVars && config != null)
                staged.Set(bill, store, config);
        }

        internal static void Attach(Bill bill)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || bill.DeletedOrDereferenced
                || !staged.TryConsume(bill, store, out Config? config) || config == null) return;
            store.RestoreClonedBillConfig(bill, config);
        }

        internal static void Reset() => staged.Clear();
    }
}
