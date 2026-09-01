namespace Implanner.UI
{
    /// The Automation tab's tooltip sources, resolved from the shared
    /// WrTips registry once per UI revision instead of thirteen dictionary
    /// lookups per pass.
    // Cache contract:
    // Owner: the Implanner dialog window.
    // Key: UiVersion.Current.
    // Value: WrTip references (the registry's own immutable-per-revision
    //   entries; their text gathers lazily on hover).
    // Dependencies: UiVersion.Current only — the WrTips registry clears
    //   its entries on that revision, so the holder must re-resolve then.
    // Refresh policy: immediate on the first Ensure after the stamp moves.
    // Equality policy: an unchanged stamp reuses every reference.
    // Teardown: Release() drops the references; the registry keeps its
    //   own lifecycle (WrTips.Reset on world teardown).
    internal sealed class AutomationTips
    {
        private int stamp = -1;

        internal WrTip Enable = null!;
        internal WrTip Iteration = null!;
        internal WrTip SurgeryConcurrency = null!;
        internal WrTip CountHospitalized = null!;
        internal WrTip AutoFloor = null!;
        internal WrTip ManualFloor = null!;
        internal WrTip ImplantReserves = null!;
        internal WrTip AddImplantReserve = null!;
        internal WrTip AutoProduction = null!;
        internal WrTip Concurrency = null!;
        internal WrTip IdleBenches = null!;
        internal WrTip ProductionSkill = null!;
        internal WrTip Intermediaries = null!;
        internal WrTip Reserves = null!;

        /// Called after the window observed the current UI metrics.
        internal void Ensure()
        {
            int current = UiVersion.Current;
            if (stamp == current) return;
            stamp = current;
            Enable = WrTips.Key("IMP_OptEnableTip");
            Iteration = WrTips.Key("IMP_OptIterationTip");
            SurgeryConcurrency = WrTips.Key("IMP_OptSurgeryConcurrencyTip");
            CountHospitalized = WrTips.Key("IMP_OptCountHospitalizedTip");
            AutoFloor = WrTips.Key("IMP_OptAutoFloorTip");
            ManualFloor = WrTips.Key("IMP_OptManualFloorTip");
            ImplantReserves = WrTips.Key("IMP_OptImplantReservesTip");
            AddImplantReserve = WrTips.Key("IMP_AddImplantReserveTip");
            AutoProduction = WrTips.Key("IMP_OptAutoProductionTip");
            Concurrency = WrTips.Key("IMP_OptConcurrencyTip");
            IdleBenches = WrTips.Key("IMP_OptIdleBenchesTip");
            ProductionSkill = WrTips.Key("IMP_OptProductionSkillTip");
            Intermediaries = WrTips.Key("IMP_OptIntermediariesTip");
            Reserves = WrTips.Key("IMP_OptReservesTip");
        }

        internal void Release()
        {
            stamp = -1;
            Enable = Iteration = SurgeryConcurrency = CountHospitalized = null!;
            AutoFloor = ManualFloor = ImplantReserves = AddImplantReserve = null!;
            AutoProduction = Concurrency = IdleBenches = null!;
            ProductionSkill = Intermediaries = Reserves = null!;
        }
    }
}
