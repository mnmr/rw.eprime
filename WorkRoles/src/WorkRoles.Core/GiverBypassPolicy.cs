using System;

namespace WorkRoles.Core
{
    /// Givers that legitimately issue a job to one specific pawn outside the
    /// compiled role lists. EPrime's Quality Jobs hands its finish job to the
    /// chosen finisher through a JobGiver_Work prefix when its high-priority
    /// option is on; that job targets the pawn regardless of role order, so
    /// it is not a bypass worth reporting.
    public static class GiverBypassPolicy
    {
        private const string QualityJobsFinishPrefix = "QJ_FinishQualityWork_";

        public static bool IsExemptGiver(string? giverDefName)
            => giverDefName != null
               && giverDefName.StartsWith(QualityJobsFinishPrefix, StringComparison.Ordinal);
    }
}
