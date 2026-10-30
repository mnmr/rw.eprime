using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace QualityJobs
{
    /// Save/load and migration discovery only. Live bills may be inside a
    /// minified bench, a carrier, or a caravan; spawned registries are incomplete.
    internal static class LiveBills
    {
        internal static List<Bill_ProductionWithUft> Collect()
        {
            var result = new List<Bill_ProductionWithUft>();
            var seen = new HashSet<Bill_ProductionWithUft>();
            var things = new List<Thing>();
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                ThingOwnerUtility.GetAllThingsRecursively(maps[i],
                    ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver), things,
                    allowUnreal: true);
                Append(things, seen, result);
            }
            if (Find.World != null)
            {
                List<WorldObject> objects = Find.WorldObjects.AllWorldObjects;
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is not IThingHolder holder) continue;
                    ThingOwnerUtility.GetAllThingsRecursively(holder, things, allowUnreal: true);
                    Append(things, seen, result);
                }
            }
            return result;
        }

        private static void Append(List<Thing> things, HashSet<Bill_ProductionWithUft> seen,
            List<Bill_ProductionWithUft> result)
        {
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].Destroyed || things[i] is not IBillGiver giver) continue;
                List<Bill> bills = giver.BillStack.Bills;
                for (int j = 0; j < bills.Count; j++)
                    if (bills[j] is Bill_ProductionWithUft bill && !bill.deleted && seen.Add(bill))
                        result.Add(bill);
            }
        }
    }
}
