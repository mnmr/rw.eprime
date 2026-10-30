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
            public Frame? frame;
            public Map? map;
            public IntVec3 position;
            public ThingDef? buildDef;
        }

        // Observe before ordinary behavior prefixes (including Priority.First).
        // Explicit Harmony ordering dependencies still take precedence.
        [HarmonyPrefix, HarmonyPriority(int.MaxValue)]
        public static void CaptureBeforeConstruction(Frame __instance, out FrameState __state)
        {
            __state = default;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null) return;

            // Capture for the retry postfix before the frame is destroyed.
            __state.plan = plan;
            __state.frame = __instance;
            __state.map = __instance.Map;
            __state.position = __instance.Position;
            __state.buildDef = __instance.BuildDef;
        }

        public static bool Prefix(Frame __instance, Pawn worker, ref FrameState __state)
        {
            // This gate may be skipped by a replacement prefix. Its separate
            // observer must still supply the original target to the postfix.
            ConstructionPlan? plan = __state.plan;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (plan == null || store == null) return true;
            if (plan.restoreOnly) return true;

            GateOutcome outcome = plan.autoBest
                ? Dispatcher.DecideAutoConstructionGate(worker, plan.Condition, __instance.def)
                : GateDecision.Decide(billManaged: true, debugCompleted: false,
                    Dispatcher.ConstructionFactsFor(worker, __instance.def), plan.Condition);
            if (outcome == GateOutcome.Complete) return true;

            // Pause: suppress completion. No job bookkeeping needed — the
            // driver's tickIntervalAction calls ReadyForNextToil right after
            // this returns (JobDriver_ConstructFinishFrame.cs:77-79), ending
            // the job; the lock patch prevents anyone but a dispatched
            // finisher from starting a new FinishFrame job.
            //
            // Cap the work overshoot: the driver adds the tick's work BEFORE
            // the completion call we suppress, and vanilla's frame renderer
            // does not clamp PercentComplete (Frame.cs:487) — anything past
            // 100% draws phantom material tiles OUTSIDE the frame's footprint.
            // Forced work (e.g. Achtung) bypasses the lock and can bounce here
            // repeatedly, growing the overshoot without this clamp.
            if (__instance.workDone > __instance.WorkToBuild)
                __instance.workDone = __instance.WorkToBuild;
            store.PausePlan(plan);
            __state.plan = null; // postfix must not run retry logic on a pause
            return false;
        }

        public static void Postfix(FrameState __state)
        {
            ConstructionPlan? plan = __state.plan;
            // Postfixes also run when another prefix suppresses completion.
            // Observe the transition, not __runOriginal: a replacement prefix
            // may successfully consume the frame without running vanilla.
            if (plan == null || __state.frame == null || !__state.frame.Destroyed) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !ReferenceEquals(store.FindPlan(__state.frame), plan)) return;
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

            store.CompleteBuiltPlan(plan, built);
        }
    }
}
