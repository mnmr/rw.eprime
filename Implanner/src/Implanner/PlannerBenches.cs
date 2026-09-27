using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Keeps designated benches Implanner's inside the reconcile pass: a
    /// bill that reached one without the bill UI (another mod, a paste
    /// through a replaced tab) is suspended, and designations of benches
    /// that no longer exist are dropped. Runs every pass, paused or not —
    /// a designation is the player's standing choice, not automation.
    /// Deterministic: map and bill order are synchronized game state.
    internal static class PlannerBenches
    {
        internal static PlannerChange Reconcile(ImplannerStore store, ReconcilePass pass)
        {
            PlannerModel model = store.Model;
            if (model.DesignatedBenches.Count == 0) return PlannerChange.None;

            var live = new HashSet<int>();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings =
                    maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is Building_WorkTable bench)
                        || !model.IsBenchDesignated(bench.thingIDNumber))
                        continue;
                    live.Add(bench.thingIDNumber);
                    BillStack bills = bench.BillStack;
                    for (int i = 0; i < bills.Count; i++)
                        if (!bills[i].suspended
                            && !model.OwnedProductionBills.ContainsKey(pass.BillId(bills[i])))
                            bills[i].suspended = true;
                }
            }

            // A gravship in flight holds its benches unspawned; nothing is
            // forgotten until it lands and they are visible again.
            if (Find.CurrentGravship != null) return PlannerChange.None;
            return model.PruneDesignatedBenches(live);
        }
    }
}
