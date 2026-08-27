using QualityJobs.Core;
using Verse;

namespace QualityJobs
{
    /// One quality-managed construction (spec §10). `target` follows the thing
    /// chain blueprint -> frame -> building -> blueprint across the patched
    /// vanilla transitions; the plan owns no Things and never outlives its
    /// target (swept when the target is destroyed outside a tracked
    /// transition).
    public class ConstructionPlan : IExposable
    {
        public Thing? target;
        public ConstructionPlanState state = ConstructionPlanState.Active;
        public Pawn? finisher;                 // Dispatched only
        public int minSkill;
        public bool requireInspired;
        public bool requireSpecialist;
        /// QualityLevel as int (0..6); rolled quality below this deconstructs
        /// and rebuilds. 0 (Awful) = never retry.
        public int minQuality;
        /// <summary>Auto-best mode (auto spec §2): only the colony-wide best
        /// builder may complete the frame; the skill threshold is dynamic.</summary>
        public bool autoBest;

        public ResumeCondition Condition => new ResumeCondition(minSkill, requireInspired, requireSpecialist);

        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref state, "state", ConstructionPlanState.Active);
            Scribe_References.Look(ref finisher, "finisher");
            Scribe_Values.Look(ref minSkill, "minSkill", 0);
            Scribe_Values.Look(ref requireInspired, "requireInspired", false);
            Scribe_Values.Look(ref requireSpecialist, "requireSpecialist", false);
            Scribe_Values.Look(ref minQuality, "minQuality", 0);
            Scribe_Values.Look(ref autoBest, "autoBest", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A minQuality > 6 would make RetryDecision retry forever
                // (even Legendary rolls compare below it); clamp defensively.
                if (minQuality < 0) minQuality = 0;
                if (minQuality > 6) minQuality = 6;
                // Dispatched requires a finisher; a pawn lost from the save
                // must not leave the lock suppressing everyone.
                if (state == ConstructionPlanState.Dispatched && finisher == null)
                    state = ConstructionPlanState.Paused;
            }
        }
    }
}
