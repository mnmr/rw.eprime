using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// One tracked item (spec §4). Kind is scribed from day one so phase-2
    /// frames need no save migration.
    public enum WorkItemKind
    {
        BillWork = 0,
        Frame = 1, // phase 2
    }

    public class WorkItemEntry : IExposable
    {
        public WorkItemKind kind = WorkItemKind.BillWork;
        public WorkItemState state = WorkItemState.Paused;
        public UnfinishedThing? uft;
        public Pawn? originalCreator;
        public Pawn? finisher;                    // Dispatched only
        public Bill_ProductionWithUft? finishBill; // Dispatched only (our one-shot bill)
        public Bill_ProductionWithUft? sourceBill; // may be deleted; tolerated
        public StyleSnapshot? snapshot;

        public string? ProductDefName => ManagedRecipes.ProductDefName(uft?.Recipe);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (sourceBill != null && sourceBill.DeletedOrDereferenced) sourceBill = null;
                if (finishBill != null && finishBill.DeletedOrDereferenced) finishBill = null;
            }
            Scribe_Values.Look(ref kind, "kind", WorkItemKind.BillWork);
            Scribe_Values.Look(ref state, "state", WorkItemState.Paused);
            Scribe_References.Look(ref uft, "uft");
            Scribe_References.Look(ref originalCreator, "originalCreator");
            Scribe_References.Look(ref finisher, "finisher");
            Scribe_References.Look(ref finishBill, "finishBill");
            Scribe_References.Look(ref sourceBill, "sourceBill");
            Scribe_Deep.Look(ref snapshot, "snapshot");
        }
    }
}
