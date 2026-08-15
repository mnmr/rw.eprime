namespace QualityJobs
{
    /// <summary>
    /// Primitive-field payload for the synced API bill-creation command.
    /// RimWorld Multiplayer must bind this through SyncCreateQualityBillValues:
    /// expanding the values back into SyncMethod parameters can crash Mono's
    /// ILGenerator while Multiplayer builds Harmony's invocation delegate.
    /// </summary>
    public sealed class CreateQualityBillValues
    {
        // WARNING: MultiplayerSupport.SyncCreateQualityBillValues binds these
        // fields positionally. Keep both declarations in lockstep.
        public int billGiverThingId;
        public int mapUniqueId;
        public string productDefName = string.Empty;
        public string recipeDefName = string.Empty;
        public bool explicitOptions;
        public int skillGate;
        public bool requireInspired;
        public bool requireSpecialist;
        public bool autoBest;
        public int targetQuality;

        /// Parameterless ctor required by [SyncWorker(shouldConstruct = true)].
        public CreateQualityBillValues() { }
    }
}
