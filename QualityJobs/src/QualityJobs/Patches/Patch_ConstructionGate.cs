using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Construction enforcement point (spec §10): quality is rolled inside
    /// Frame.CompleteConstruction (Frame.cs:294), so a prefix that suppresses
    /// it guarantees no unqualified pawn completes a managed frame. The
    /// calling driver's ReadyForNextToil ends the job cleanly; workDone stays
    /// at 100% and the lock patch keeps everyone else away.
    ///
    /// The postfix implements retries: if the completed building rolled below
    /// the plan's minimum quality, designate vanilla Deconstruct and await the
    /// rebuild hook.
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_ConstructionGate
    {
        public struct FrameState
        {
            public ConstructionPlan? plan;
            public Map? map;
            public IntVec3 position;
            public ThingDef? buildDef;
        }

        public static bool Prefix(Frame __instance, Pawn worker, out FrameState __state)
        {
            __state = default;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return true;
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null) return true;

            // Capture for the retry postfix before the frame is destroyed.
            __state.plan = plan;
            __state.map = __instance.Map;
            __state.position = __instance.Position;
            __state.buildDef = __instance.BuildDef;

            CandidateFacts facts = Dispatcher.ConstructionFactsFor(worker);
            GateOutcome outcome = GateDecision.Decide(billManaged: true,
                debugCompleted: false, facts, plan.Condition);
            if (outcome == GateOutcome.Complete) return true;

            // Pause: suppress completion. No job bookkeeping needed — the
            // driver's tickIntervalAction calls ReadyForNextToil right after
            // this returns (JobDriver_ConstructFinishFrame.cs:77-79), ending
            // the job; the lock patch prevents anyone but a dispatched
            // finisher from starting a new FinishFrame job.
            plan.state = ConstructionPlanState.Paused;
            plan.finisher = null;
            __state.plan = null; // postfix must not run retry logic on a pause
            return false;
        }

        public static void Postfix(FrameState __state)
        {
            ConstructionPlan? plan = __state.plan;
            if (plan == null) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            if (__state.map == null || __state.buildDef == null)
            {
                // Terrain frame or lost map: nothing manageable was produced.
                store.RemovePlan(plan);
                return;
            }

            // The building spawns at the frame's position (Frame.cs:320).
            Thing? built = __state.map.thingGrid.ThingAt(__state.position, __state.buildDef);
            if (built == null)
            {
                store.RemovePlan(plan); // terrain or vanished — nothing to manage
                return;
            }

            CompQuality? comp = built.TryGetComp<CompQuality>();
            if (comp == null
                || !RetryDecision.ShouldRetry((QualityLevel)(int)comp.Quality,
                        (QualityLevel)plan.minQuality))
            {
                store.RemovePlan(plan); // accepted — plan complete
                return;
            }

            // Retry (spec §10): vanilla Deconstruct designation; the rebuild
            // hook re-places the blueprint when deconstruction finishes.
            plan.target = built;
            plan.state = ConstructionPlanState.AwaitingRebuild;
            plan.finisher = null;
            __state.map.designationManager.AddDesignation(
                new Designation(built, DesignationDefOf.Deconstruct));
            if (store.dispatchLetter)
                Find.LetterStack.ReceiveLetter(
                    "QJ_RetryLetterLabel".Translate(built.LabelShort),
                    "QJ_RetryLetterText".Translate(built.LabelShort,
                        comp.Quality.GetLabel(),
                        ((QualityCategory)plan.minQuality).GetLabel()),
                    LetterDefOf.NeutralEvent, built);
        }
    }
}
