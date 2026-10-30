using System.Collections.Generic;
using System.Globalization;
using Implanner.Core;

namespace Implanner
{
    /// Which implant item each Implanner operation must use: the item
    /// reserved for the operation's goal. The ingredient patch reads only
    /// this map (Patch_BoundSurgeryIngredient), never the live model.
    // Cache contract:
    // Owner: the current world's ImplannerStore (process-static, one world
    //   at a time).
    // Key: the operation bill's loadID (the numeric suffix of its unique load
    //   id "Bill_[recipe]_[loadID]").
    // Value: the reserved item's thing id, or -1 for an owned operation
    // temporarily without a reservation (refuses new implant searches).
    // The map is replaced wholesale on
    //   publication and never mutated afterwards.
    // Dependencies: the model's owned operation bills (Surgery domain) and
    //   item reservations (Reservations domain).
    // Refresh policy: immediate: the store publishes on every Surgery or
    //   Reservations change (synced command or reconcile pass), on
    //   construction, and after load normalization.
    // Equality policy: equal maps/sets preserve identity.
    // Teardown: Reset on world teardown.
    internal static class SurgeryBindings
    {
        private static Dictionary<int, int> itemByBill = new Dictionary<int, int>();
        // Owner/teardown: current store, Reset on world teardown, as above.
        // Keys/value: thing and bill ids, immutable membership sets.
        // Dependencies: Reservations for reservedItems; Production for productionBills.
        // Refresh: only on their domain publication, store construction or load.
        // Equality: equal sets preserve identity. No live game objects retained.
        private static HashSet<int> reservedItems = new HashSet<int>();
        private static HashSet<int> productionBills = new HashSet<int>();

        // Owner: the current ImplannerStore; key: thing id.
        // Value: immutable publication of authoritative production exclusions.
        // Dependencies: ProductionProtection only (the reconciler owns changes).
        // Refresh: immediate publication after deterministic reconciliation,
        // store construction, and load normalization; no stock reads here.
        // Equality: equal sets retain identity.
        // Teardown: Reset releases all ids at world teardown.
        private static HashSet<int> productionProtectedItems = new HashSet<int>();

        internal static bool Any => itemByBill.Count > 0;

        internal static void Publish(PlannerModel model, PlannerChange change = PlannerChange.All)
        {
            if ((change & (PlannerChange.Surgery | PlannerChange.Reservations)) != 0)
            {
                var map = new Dictionary<int, int>();
                foreach (var pawn in model.OwnedBills)
                    foreach (string billId in pawn.Value.Values)
                        if (TryParseLoadId(billId, out int loadId)) map[loadId] = -1;
                foreach (KeyValuePair<int, ItemReservation> pair in model.Reservations)
                {
                    string? billId = model.OwnedBill(pair.Value.PawnId, pair.Value.GoalKey);
                    if (billId != null && TryParseLoadId(billId, out int loadId)) map[loadId] = pair.Key;
                }
                bool equal = map.Count == itemByBill.Count;
                foreach (var pair in map)
                    if (!itemByBill.TryGetValue(pair.Key, out int id) || id != pair.Value)
                    { equal = false; break; }
                if (!equal) itemByBill = map;
            }
            if ((change & PlannerChange.Reservations) != 0
                && !reservedItems.SetEquals(model.Reservations.Keys))
                reservedItems = new HashSet<int>(model.Reservations.Keys);
            if ((change & PlannerChange.Production) != 0)
            {
                var production = new HashSet<int>();
                foreach (string bill in model.OwnedProductionBills.Keys)
                    if (TryParseLoadId(bill, out int id)) production.Add(id);
                if (!productionBills.SetEquals(production)) productionBills = production;
            }
        }

        internal static void Reset()
        {
            itemByBill = new Dictionary<int, int>();
            reservedItems = new HashSet<int>();
            productionBills = new HashSet<int>();
            productionProtectedItems = new HashSet<int>();
        }

        internal static void PublishProductionProtection(PlannerModel model)
        {
            if (!productionProtectedItems.SetEquals(model.ProductionProtectedItems))
                productionProtectedItems = new HashSet<int>(model.ProductionProtectedItems);
        }

        internal static bool IsProductionProtected(int itemId) => productionProtectedItems.Contains(itemId);

        internal static bool ProductionOwns(int billId) => productionBills.Contains(billId);
        internal static bool IsReserved(int itemId) => reservedItems.Contains(itemId);

        internal static bool TryGetItem(int billLoadId, out int itemId) =>
            itemByBill.TryGetValue(billLoadId, out itemId);

        /// Bill.GetUniqueLoadID is "Bill_" + recipe defName + "_" + loadID.
        private static bool TryParseLoadId(string billId, out int loadId)
        {
            loadId = 0;
            int separator = billId.LastIndexOf('_');
            return separator >= 0 && int.TryParse(billId.Substring(separator + 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out loadId);
        }
    }
}
