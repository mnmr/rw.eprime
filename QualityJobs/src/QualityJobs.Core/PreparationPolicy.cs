namespace QualityJobs.Core
{
    public static class PreparationPolicy
    {
        /// The restriction governs ordinary work only. A dispatched finisher
        /// has already passed the quality gate and must remain able to finish.
        public static bool Allows(bool allowRobotPreparation, bool mechanical,
            bool managedWork, bool dispatchedFinisher)
            => allowRobotPreparation || !mechanical || !managedWork
               || dispatchedFinisher;
    }
}
