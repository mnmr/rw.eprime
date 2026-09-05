namespace QualityJobs.Core
{
    /// <summary>Dispatched → Paused revert decision (spec §4). The item counts
    /// as present while it is anywhere on the finisher's map: spawned, or
    /// carried by the finisher on the walk from storage to the bench. An
    /// unspawned item is therefore never a revert trigger by itself; only
    /// leaving the map (caravan, destruction) is.</summary>
    public static class DispatchRevertPolicy
    {
        public static bool ShouldRevert(bool itemOnFinisherMap, bool finisherAvailable,
            bool finishBillAlive, bool finisherQualifies)
            => !itemOnFinisherMap || !finisherAvailable || !finishBillAlive
               || !finisherQualifies;
    }
}
