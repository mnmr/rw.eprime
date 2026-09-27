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
    // Value: the reserved item's thing id; the map is replaced wholesale on
    //   publication and never mutated afterwards.
    // Dependencies: the model's owned operation bills (Surgery domain) and
    //   item reservations (Reservations domain).
    // Refresh policy: immediate: the store publishes on every Surgery or
    //   Reservations change (synced command or reconcile pass), on
    //   construction, and after load normalization.
    // Equality policy: none (a publication follows a real change).
    // Teardown: Reset on world teardown.
    internal static class SurgeryBindings
    {
        private static Dictionary<int, int> itemByBill = new Dictionary<int, int>();

        internal static bool Any => itemByBill.Count > 0;

        internal static void Publish(PlannerModel model)
        {
            var map = new Dictionary<int, int>();
            foreach (KeyValuePair<int, ItemReservation> pair in model.Reservations)
            {
                string? billId = model.OwnedBill(pair.Value.PawnId, pair.Value.GoalKey);
                if (billId != null && TryParseLoadId(billId, out int loadId))
                    map[loadId] = pair.Key;
            }
            itemByBill = map;
        }

        internal static void Reset() => itemByBill = new Dictionary<int, int>();

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
