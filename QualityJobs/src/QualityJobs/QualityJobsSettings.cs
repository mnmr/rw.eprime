using Verse;

namespace QualityJobs
{
    /// Global defaults only (spec §11): seed values for NEW saves plus the
    /// dispatch-letter presentation toggle. In-save behavior lives in the
    /// store and is edited via synced commands.
    public class QualityJobsSettings : ModSettings
    {
        public bool defaultManageNewBills = true;
        public int defaultMinSkill = 15;
        public bool defaultRequireInspired = false;
        public bool defaultRequireSpecialist = false;
        public int defaultProductCap = 3;
        public bool defaultShareUnfinishedWork = true;
        public bool dispatchLetter = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref defaultManageNewBills, "defaultManageNewBills", true);
            Scribe_Values.Look(ref defaultMinSkill, "defaultMinSkill", 15);
            Scribe_Values.Look(ref defaultRequireInspired, "defaultRequireInspired", false);
            Scribe_Values.Look(ref defaultRequireSpecialist, "defaultRequireSpecialist", false);
            Scribe_Values.Look(ref defaultProductCap, "defaultProductCap", 3);
            Scribe_Values.Look(ref defaultShareUnfinishedWork, "defaultShareUnfinishedWork", true);
            Scribe_Values.Look(ref dispatchLetter, "dispatchLetter", true);
        }
    }
}
