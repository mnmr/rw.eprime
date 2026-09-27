using System.Collections.Generic;
using Implanner.Core;
using RimWorld;

namespace Implanner
{
    /// The designated-bench ids the bill-UI patches read while drawing, so
    /// a render pass never touches the live model.
    // Cache contract:
    // Owner: the current world's ImplannerStore (process-static, one world
    //   at a time).
    // Key: none (one set for the current store).
    // Value: an immutable set of designated bench thing ids, replaced
    //   wholesale on publication and never mutated afterwards.
    // Dependencies: the model's designations (the Benches domain).
    // Refresh policy: immediate — the store publishes on every Benches
    //   change (synced command or reconcile pass), on construction, and
    //   after load normalization.
    // Equality policy: none (a publication is always a designation change).
    // Teardown: Reset on world teardown.
    internal static class BenchDesignations
    {
        private static HashSet<int> ids = new HashSet<int>();

        internal static void Publish(PlannerModel model) =>
            ids = new HashSet<int>(model.DesignatedBenches.Keys);

        internal static void Reset() => ids = new HashSet<int>();

        internal static bool Contains(IBillGiver? giver) =>
            ids.Count > 0
            && giver is Building_WorkTable bench
            && ids.Contains(bench.thingIDNumber);
    }
}
