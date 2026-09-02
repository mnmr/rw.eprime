using Verse;

namespace Implanner.UI
{
    /// Translated UI labels resolved once per language revision so render
    /// passes never translate. Read-only during drawing.
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: none (single snapshot of all keys).
    // Value: immutable translated strings.
    // Dependencies: UiVersion.LanguageCurrent.
    // Refresh policy: immediate rebuild on next Ensure() after the language
    //   revision moves.
    // Equality policy: unchanged language returns the same strings.
    // Teardown: Reset() clears the stamp; strings rebuild on next use.
    internal static class PlannerLabels
    {
        private static int stamp = -1;

        internal static string TabOverview = "";
        internal static string TabPlans = "";
        internal static string TabAutomation = "";
        internal static string TabHelp = "";
        internal static string ImportPlans = "";
        internal static string ExportPlans = "";
        internal static string ColColonist = "";
        internal static string ColPlan = "";
        internal static string ColProgress = "";
        internal static string ColState = "";
        internal static string ColShooting = "";
        internal static string ColMelee = "";
        internal static string ColShootingTip = "";
        internal static string ColMeleeTip = "";
        internal static string ColPriority = "";
        internal static string ColonistDetails = "";
        internal static string NoSelection = "";
        internal static string AddPlan = "";
        internal static string NoPlans = "";
        internal static string Rename = "";
        internal static string DeletePlan = "";
        internal static string NoPlan = "";
        internal static string OptEnable = "";
        private static readonly string[] priorityLabels = new string[5];

        /// Priority level (0 first … 4 last) to its display label.
        internal static string PriorityLabel(int level) =>
            level >= 0 && level < priorityLabels.Length
                ? priorityLabels[level]
                : priorityLabels[2];

        internal static string PlansHeader = "";
        internal static string PlanNameTitle = "";
        internal static string ExtendsPlan = "";
        internal static string ExtendsNothing = "";
        internal static string Inherited = "";
        internal static string OptSurgery = "";
        internal static string OptIteration = "";
        internal static string OptManualFloor = "";
        internal static string OptSurgeryConcurrency = "";
        internal static string OptCountHospitalized = "";
        internal static string OptAutoFloor = "";
        internal static string OptImplantReserves = "";
        internal static string AddImplantReserve = "";
        internal static string OptProduction = "";
        internal static string OptAutoProduction = "";
        internal static string OptConcurrency = "";
        internal static string OptIdleBenches = "";
        internal static string OptProductionSkill = "";
        internal static string OptIntermediaries = "";
        internal static string OptReserves = "";
        internal static string DragItemsHere = "";
        internal static string Help = "";
        internal static string RankTiersTitle = "";
        internal static string RankTiersHelp = "";

        /// Segment labels for the plan editor's region filter, in
        /// ImplantRegion order; cached array so render passes hand
        /// SegmentedRow a stable reference.
        internal static readonly string[] ImplantRegions = new string[3];

        /// Right-aligned tier captions, indexed by tier (0 = five stars).
        internal static readonly string[] TierPriorities = new string[5];
        internal static string AutomationOffTitle = "";
        /// Formatted once with the level mod's name, which is fixed for the
        /// session (PlannerAutomation).
        internal static string AutomationOffBody = "";

        /// Segment labels in DISPLAY order — tier batching (the default)
        /// first, full sets, then ASAP; parallel to
        /// AutomationSnapshot.IterationByDisplay. Cached array so render
        /// passes hand SegmentedRow a stable reference.
        internal static readonly string[] IterationModes = new string[3];

        internal static void Ensure()
        {
            if (stamp == UiVersion.LanguageCurrent) return;
            stamp = UiVersion.LanguageCurrent;
            TabOverview = "IMP_TabOverview".Translate();
            TabPlans = "IMP_TabPlans".Translate();
            TabAutomation = "IMP_TabAutomation".Translate();
            TabHelp = "IMP_TabHelp".Translate();
            ImportPlans = "IMP_Import".Translate();
            ExportPlans = "IMP_Export".Translate();
            ColColonist = "IMP_ColColonist".Translate();
            ColPlan = "IMP_ColPlan".Translate();
            ColProgress = "IMP_ColProgress".Translate();
            ColState = "IMP_ColState".Translate();
            ColShooting = "IMP_ColShooting".Translate();
            ColMelee = "IMP_ColMelee".Translate();
            ColShootingTip = "IMP_ColShootingTip".Translate();
            ColMeleeTip = "IMP_ColMeleeTip".Translate();
            ColPriority = "IMP_ColPriority".Translate();
            ColonistDetails = "IMP_ColonistDetails".Translate();
            NoSelection = "IMP_NoSelection".Translate();
            AddPlan = "IMP_AddPlan".Translate();
            NoPlans = "IMP_NoPlans".Translate();
            Rename = "IMP_Rename".Translate();
            DeletePlan = "IMP_DeletePlan".Translate();
            NoPlan = "IMP_NoPlan".Translate();
            OptEnable = "IMP_OptEnable".Translate();
            AutomationOffTitle = "IMP_AutomationOffTitle".Translate();
            AutomationOffBody =
                "IMP_AutomationOffBody".Translate(PlannerAutomation.BlockedBy);
            for (int i = 0; i < priorityLabels.Length; i++)
                priorityLabels[i] = ("IMP_Priority" + i).Translate();
            PlansHeader = "IMP_PlansHeader".Translate();
            PlanNameTitle = "IMP_PlanNameTitle".Translate();
            ExtendsPlan = "IMP_ExtendsPlan".Translate();
            ExtendsNothing = "IMP_ExtendsNothing".Translate();
            Inherited = "IMP_Inherited".Translate();
            OptSurgery = "IMP_OptSurgery".Translate();
            OptIteration = "IMP_OptIteration".Translate();
            OptManualFloor = "IMP_OptManualFloor".Translate();
            OptSurgeryConcurrency = "IMP_OptSurgeryConcurrency".Translate();
            OptCountHospitalized = "IMP_OptCountHospitalized".Translate();
            OptAutoFloor = "IMP_OptAutoFloor".Translate();
            OptImplantReserves = "IMP_OptImplantReserves".Translate();
            AddImplantReserve = "IMP_AddImplantReserve".Translate();
            OptProduction = "IMP_OptProduction".Translate();
            OptAutoProduction = "IMP_OptAutoProduction".Translate();
            OptConcurrency = "IMP_OptConcurrency".Translate();
            OptIdleBenches = "IMP_OptIdleBenches".Translate();
            OptProductionSkill = "IMP_OptProductionSkill".Translate();
            OptIntermediaries = "IMP_OptIntermediaries".Translate();
            OptReserves = "IMP_OptReserves".Translate();
            DragItemsHere = "IMP_DragItemsHere".Translate();
            Help = "IMP_Help".Translate();
            RankTiersTitle = "IMP_RankTiersTitle".Translate();
            RankTiersHelp = "IMP_RankTiersHelp".Translate();
            ImplantRegions[0] = "IMP_RegionLimbs".Translate();
            ImplantRegions[1] = "IMP_RegionTorso".Translate();
            ImplantRegions[2] = "IMP_RegionHead".Translate();
            TierPriorities[0] = "IMP_TierHighest".Translate();
            TierPriorities[1] = "IMP_TierHigh".Translate();
            TierPriorities[2] = "IMP_TierMedium".Translate();
            TierPriorities[3] = "IMP_TierNormal".Translate();
            TierPriorities[4] = "IMP_TierLow".Translate();
            IterationModes[0] = "IMP_IterTier".Translate();
            IterationModes[1] = "IMP_IterColonist".Translate();
            IterationModes[2] = "IMP_IterAsap".Translate();
        }

        internal static void Reset() => stamp = -1;
    }
}
