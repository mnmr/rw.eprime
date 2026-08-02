using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Allocation-free bill and precept load-id lookups for hot paths.
    ///
    /// Cache — Owner: process. Key: Bill/Precept_ThingStyle reference (weak).
    /// Value: the object's immutable GetUniqueLoadID() string. Dependencies: none
    /// (load ids never change after construction). Refresh: n/a (immutable).
    /// Equality: n/a. Teardown: ConditionalWeakTable entries die with their keys;
    /// no world state is retained.
    public static class BillIds
    {
        private static readonly ConditionalWeakTable<Bill, string> ids = new ConditionalWeakTable<Bill, string>();
        private static readonly ConditionalWeakTable<Bill, string>.CreateValueCallback createBill =
            bill => bill.GetUniqueLoadID();

        public static string IdOf(Bill bill) => ids.GetValue(bill, createBill);
    }

    /// Allocation-free precept load-id lookup for hot paths (see BillIds).
    public static class PreceptIds
    {
        private static readonly ConditionalWeakTable<Precept_ThingStyle, string> ids =
            new ConditionalWeakTable<Precept_ThingStyle, string>();
        private static readonly ConditionalWeakTable<Precept_ThingStyle, string>.CreateValueCallback createPrecept =
            precept => precept.GetUniqueLoadID();

        public static string IdOf(Precept_ThingStyle precept) => ids.GetValue(precept, createPrecept);
    }
}
