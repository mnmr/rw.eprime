using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Vanilla's per-giver eligibility gates, private in JobGiver_Work
    /// (JobGiver_Work.cs:268-295 PawnCanUseWorkGiver). Shared by the
    /// high-priority finish path and its care-work walk. High-priority finishers
    /// skip the giver-category gate and check the target's category instead.
    /// Re-verify against the decompile on game updates.
    internal static class WorkGiverGates
    {
        internal static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver giver,
            bool checkShouldSkip, bool checkWorkType = true)
        {
            WorkGiverDef def = giver.def;
            if (!def.nonColonistsCanDo && !pawn.IsColonist && !pawn.IsColonyMech
                && !pawn.IsColonySubhuman) return false;
            if (pawn.WorkTagIsDisabled(def.workTags)) return false;
            if (checkWorkType && def.workType != null && pawn.WorkTypeIsDisabled(def.workType)) return false;
            if (checkShouldSkip && giver.ShouldSkip(pawn)) return false;
            if (giver.MissingRequiredCapacity(pawn) != null) return false;
            if (pawn.RaceProps.IsMechanoid && !def.canBeDoneByMechs) return false;
            return true;
        }
    }
}
