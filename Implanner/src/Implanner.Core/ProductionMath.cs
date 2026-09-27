using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Craft-unit arithmetic for production dispatch. Demand and stock are
    /// measured in items; bills are measured in crafts (repeatCount), and a
    /// recipe may produce several items per craft — the two units meet in
    /// exactly one place, here.
    public static class ProductionMath
    {
        /// The crafts still worth queueing: the item deficit left after
        /// stock and already-pending crafts' output, rounded up to whole
        /// crafts. Zero when stock plus pending output covers the demand or
        /// the recipe produces nothing.
        public static int CraftsNeeded(
            int demandItems, int stockItems, int pendingCrafts, int itemsPerCraft)
        {
            if (itemsPerCraft <= 0) return 0;
            int deficit = demandItems - stockItems - pendingCrafts * itemsPerCraft;
            if (deficit <= 0) return 0;
            return (deficit + itemsPerCraft - 1) / itemsPerCraft;
        }
    }

    /// One missing implant slot's demand on production: its item and the
    /// lowest quality the slot accepts (0 for items without quality).
    public readonly struct CraftNeed<TKey>
    {
        public CraftNeed(TKey item, int minQuality)
        {
            Item = item;
            MinQuality = minQuality;
        }

        public TKey Item { get; }
        public int MinQuality { get; }
    }

    /// The crafting queue one colony still needs, in surgery rollout order.
    public static class ProductionQueue
    {
        /// A pending craft with no quality promise (no Quality Jobs, no
        /// ingredient that decides quality): trusted to cover any slot until
        /// its product lands, when stock tells the truth.
        public const int UnknownQuality = -1;

        /// Walks rollout-ordered demand (one entry per missing implant slot)
        /// against stock, then pending crafts. Each entry takes the best
        /// stocked item it accepts (the way surgery allocates), else the
        /// pending craft promising the least quality that still suffices,
        /// else it is one craft still to queue at the entry's minimum
        /// quality. A craft's surplus output (several items per craft)
        /// covers the next entries of its kind at the quality it promises.
        /// Returns the new crafts in rollout order and records, per kind,
        /// the promises of the pending crafts the demand used — crafts
        /// beyond those are not needed. stock holds one quality per item
        /// (0 for items without quality); neither input is modified.
        public static List<CraftNeed<TKey>> UncoveredCrafts<TKey>(
            IReadOnlyList<CraftNeed<TKey>> rollout,
            IReadOnlyDictionary<TKey, List<int>> stock,
            IReadOnlyDictionary<TKey, List<int>> pendingCrafts,
            IReadOnlyDictionary<TKey, int> itemsPerCraft,
            Dictionary<TKey, List<int>> usedPendingCrafts) where TKey : notnull
        {
            var crafts = new List<CraftNeed<TKey>>();
            var available = new Dictionary<TKey, List<int>>();
            foreach (KeyValuePair<TKey, List<int>> pair in stock)
                available[pair.Key] = new List<int>(pair.Value);
            var pending = new Dictionary<TKey, List<int>>();
            foreach (KeyValuePair<TKey, List<int>> pair in pendingCrafts)
                pending[pair.Key] = new List<int>(pair.Value);

            for (int i = 0; i < rollout.Count; i++)
            {
                TKey item = rollout[i].Item;
                int minimum = rollout[i].MinQuality;
                if (available.TryGetValue(item, out List<int> have)
                    && Take(have, minimum, preferLowest: false))
                    continue;
                itemsPerCraft.TryGetValue(item, out int output);
                if (output <= 0) continue;
                int promise;
                if (pending.TryGetValue(item, out List<int> queued)
                    && TakePromise(queued, minimum, out promise))
                {
                    if (!usedPendingCrafts.TryGetValue(item, out List<int> used))
                        usedPendingCrafts[item] = used = new List<int>();
                    used.Add(promise);
                }
                else
                {
                    promise = minimum;
                    crafts.Add(new CraftNeed<TKey>(item, minimum));
                }
                if (output > 1)
                {
                    if (have == null) available[item] = have = new List<int>();
                    int covers = promise == UnknownQuality ? ImplantQuality.Highest : promise;
                    for (int n = 1; n < output; n++) have.Add(covers);
                }
            }
            return crafts;
        }

        /// Removes the best entry at or above minimum (the lowest acceptable
        /// one when preferLowest).
        static bool Take(List<int> qualities, int minimum, bool preferLowest)
        {
            int pick = ImplantQuality.Choose(qualities, minimum, preferLowest);
            if (pick < 0) return false;
            qualities.RemoveAt(pick);
            return true;
        }

        /// Removes the pending craft whose promise meets minimum most
        /// narrowly (an unknown promise last), leaving better promises for
        /// stricter slots.
        static bool TakePromise(List<int> promises, int minimum, out int promise)
        {
            int pick = -1;
            for (int i = 0; i < promises.Count; i++)
            {
                int p = promises[i];
                if (p != UnknownQuality && p < minimum) continue;
                if (pick < 0 || Narrower(p, promises[pick])) pick = i;
            }
            promise = pick >= 0 ? promises[pick] : UnknownQuality;
            if (pick < 0) return false;
            promises.RemoveAt(pick);
            return true;
        }

        static bool Narrower(int candidate, int current) =>
            current == UnknownQuality
                ? candidate != UnknownQuality
                : candidate != UnknownQuality && candidate < current;
    }

    /// Materials one colony can still promise to new one-craft bills. A
    /// craft fits only when stock covers its cost plus everything already
    /// promised to queued (not yet started) bills plus the player's
    /// reserve, for every fixed ingredient; a craft that does not fit
    /// promises nothing.
    public sealed class ProductionBudget<TKey> where TKey : notnull
    {
        private readonly Dictionary<TKey, (int Stock, int Reserve, int Promised)> resources =
            new Dictionary<TKey, (int, int, int)>();

        public bool Tracks(TKey resource) => resources.ContainsKey(resource);

        public void Track(TKey resource, int stock, int reserve) =>
            resources[resource] = (stock, reserve, 0);

        /// Promises materials to a bill already queued but not started.
        public void Commit(TKey resource, int count)
        {
            resources.TryGetValue(resource, out var r);
            resources[resource] = (r.Stock, r.Reserve, r.Promised + count);
        }

        /// Items of the resource missing for a craft costing count (zero
        /// when it fits).
        public int Shortfall(TKey resource, int count)
        {
            resources.TryGetValue(resource, out var r);
            return Math.Max(0, count + r.Promised + r.Reserve - r.Stock);
        }

        /// Promises one craft's materials when every ingredient fits.
        public bool TryCommit(IReadOnlyList<(TKey Resource, int Count)> costs)
        {
            for (int i = 0; i < costs.Count; i++)
                if (Shortfall(costs[i].Resource, costs[i].Count) > 0)
                    return false;
            for (int i = 0; i < costs.Count; i++)
                Commit(costs[i].Resource, costs[i].Count);
            return true;
        }
    }
}
