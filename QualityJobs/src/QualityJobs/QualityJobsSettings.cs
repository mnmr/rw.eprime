using Verse;

namespace QualityJobs
{
    /// Global defaults only (spec §11): seed values for NEW saves plus the
    /// dispatch-letter presentation toggle. In-save behavior lives in the
    /// store and is edited via synced commands.
    public class QualityJobsSettings : ModSettings
    {
        // Bill defaults.
        public bool defaultManageNewBills = true;
        public int defaultMinSkill = 15;
        public bool defaultRequireInspired = false;
        public bool defaultRequireSpecialist = false;
        public int defaultProductCap = 10;
        public bool defaultShareUnfinishedWork = true;
        public bool dispatchLetter = true;

        // Construction defaults. These seed the per-save store on first load
        // (dual-pattern: store values are authoritative when a game is loaded).
        // Semantics: 0 = neutral (no skill gate, no retries).
        public bool defaultManageNewConstruction = false;
        public int defaultConstructionMinSkill = 15;
        public bool defaultConstructionRequireInspired = false;
        public bool defaultConstructionRequireSpecialist = false;
        public int defaultConstructionTargetQuality = 0; // 0 = no retries

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref defaultManageNewBills, "defaultManageNewBills", true);
            Scribe_Values.Look(ref defaultMinSkill, "defaultMinSkill", 15);
            Scribe_Values.Look(ref defaultRequireInspired, "defaultRequireInspired", false);
            Scribe_Values.Look(ref defaultRequireSpecialist, "defaultRequireSpecialist", false);
            Scribe_Values.Look(ref defaultProductCap, "defaultProductCap", 10);
            Scribe_Values.Look(ref defaultShareUnfinishedWork, "defaultShareUnfinishedWork", true);
            Scribe_Values.Look(ref dispatchLetter, "dispatchLetter", true);
            Scribe_Values.Look(ref defaultManageNewConstruction, "defaultManageNewConstruction", false);
            Scribe_Values.Look(ref defaultConstructionMinSkill, "defaultConstructionMinSkill", 15);
            Scribe_Values.Look(ref defaultConstructionRequireInspired, "defaultConstructionRequireInspired", false);
            Scribe_Values.Look(ref defaultConstructionRequireSpecialist, "defaultConstructionRequireSpecialist", false);
            Scribe_Values.Look(ref defaultConstructionTargetQuality, "defaultConstructionTargetQuality", 0);
        }
    }
}
