using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// One holder of an implant kind at a colony for the "better implants
    /// to high-priority colonists" option: an installed implant on a plan
    /// slot, or a reservation for a missing slot.
    public readonly struct QualityHolder
    {
        public QualityHolder(int priority, int pawnId, string key, int quality,
            int itemId, int minimum, bool available)
        {
            Priority = priority;
            PawnId = pawnId;
            Key = key;
            Quality = quality;
            ItemId = itemId;
            Minimum = minimum;
            Available = available;
        }

        /// The colonist's priority level: 0 first … 4 last.
        public int Priority { get; }
        public int PawnId { get; }
        public string Key { get; }

        /// The installed implant's quality, or the reserved item's.
        public int Quality { get; }

        /// The reserved item, or -1 for an installed implant.
        public int ItemId { get; }

        /// The lowest quality a reservation's slot accepts.
        public int Minimum { get; }

        /// A reservation may swap or give up its item (no operation is
        /// scheduled for it yet); an installed implant may be replaced now
        /// (its colonist can take the surgery).
        public bool Available { get; }

        public bool Installed => ItemId < 0;
    }

    /// A free item of the kind at the colony.
    public readonly struct SpareItem
    {
        public SpareItem(int itemId, int quality)
        {
            ItemId = itemId;
            Quality = quality;
        }

        public int ItemId { get; }
        public int Quality { get; }
    }

    /// A holder's new item: a reservation swaps to it (-1: its item was
    /// taken and nothing replaces it, so the reservation is released), an
    /// installed implant is replaced by it.
    public readonly struct QualityMove
    {
        public QualityMove(int holder, int itemId)
        {
            Holder = holder;
            ItemId = itemId;
        }

        /// Index into the holders passed to Plan.
        public int Holder { get; }
        public int ItemId { get; }
    }

    /// Better items to higher-priority colonists (owner, 2026-09-27), for
    /// one implant kind at one colony. Holders are visited by priority
    /// (then pawn id, then key); each takes the best item that improves on
    /// what it holds: a free item, or the item reserved for a colonist of
    /// strictly lower priority whose operation is not scheduled yet. A
    /// reservation swaps (its old item goes to the colonist it took from
    /// when that slot accepts it, otherwise back to the free items); an
    /// installed implant is replaced by surgery, at most one per colonist
    /// and upgradeSlots per call, and the colonist it took from waits for
    /// the implant that comes out. Every move strictly raises a holder's
    /// quality, so repeated passes settle.
    public static class QualityRebalance
    {
        const int NoItem = int.MinValue;

        public static List<QualityMove> Plan(IReadOnlyList<QualityHolder> holders,
            IReadOnlyList<SpareItem> spare, int upgradeSlots)
        {
            int count = holders.Count;
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = holders[a].Priority.CompareTo(holders[b].Priority);
                if (c != 0) return c;
                c = holders[a].PawnId.CompareTo(holders[b].PawnId);
                return c != 0 ? c : string.CompareOrdinal(holders[a].Key, holders[b].Key);
            });

            var item = new int[count];
            var quality = new int[count];
            for (int i = 0; i < count; i++)
            {
                item[i] = holders[i].ItemId;
                quality[i] = holders[i].Quality;
            }
            var pool = new List<SpareItem>(spare);
            var upgraded = new HashSet<int>();

            for (int rank = 0; rank < count; rank++)
            {
                int h = order[rank];
                QualityHolder holder = holders[h];
                if (!holder.Available) continue;
                if (holder.Installed
                    && (upgradeSlots <= 0 || upgraded.Contains(holder.PawnId)))
                    continue;
                int floor = holder.Installed ? quality[h] + 1
                    : Math.Max(holder.Minimum, quality[h] + 1);

                // The best free item; equal quality: the oldest.
                int poolPick = -1;
                for (int i = 0; i < pool.Count; i++)
                    if (pool[i].Quality >= floor
                        && (poolPick < 0 || pool[i].Quality > pool[poolPick].Quality
                            || (pool[i].Quality == pool[poolPick].Quality
                                && pool[i].ItemId < pool[poolPick].ItemId)))
                        poolPick = i;
                // A better item reserved for a lower-priority colonist; equal
                // quality: the lowest in line gives it up.
                int victim = -1;
                for (int r = rank + 1; r < count; r++)
                {
                    int v = order[r];
                    if (holders[v].Installed || !holders[v].Available
                        || holders[v].Priority <= holder.Priority
                        || item[v] == NoItem || quality[v] < floor)
                        continue;
                    if (victim < 0 || quality[v] >= quality[victim]) victim = v;
                }
                bool steal = victim >= 0
                    && (poolPick < 0 || quality[victim] > pool[poolPick].Quality);
                if (!steal && poolPick < 0) continue;

                int oldItem = item[h], oldQuality = quality[h];
                if (steal)
                {
                    item[h] = item[victim];
                    quality[h] = quality[victim];
                    if (!holder.Installed && oldItem != NoItem
                        && oldQuality >= holders[victim].Minimum)
                    {
                        item[victim] = oldItem;
                        quality[victim] = oldQuality;
                    }
                    else
                    {
                        if (!holder.Installed && oldItem != NoItem)
                            pool.Add(new SpareItem(oldItem, oldQuality));
                        item[victim] = NoItem;
                        quality[victim] = NoItem;
                    }
                }
                else
                {
                    item[h] = pool[poolPick].ItemId;
                    quality[h] = pool[poolPick].Quality;
                    pool.RemoveAt(poolPick);
                    if (!holder.Installed && oldItem != NoItem)
                        pool.Add(new SpareItem(oldItem, oldQuality));
                }
                if (holder.Installed)
                {
                    upgraded.Add(holder.PawnId);
                    upgradeSlots--;
                }
            }

            var moves = new List<QualityMove>();
            for (int i = 0; i < count; i++)
            {
                int final = item[i] == NoItem ? -1 : item[i];
                if (final != holders[i].ItemId) moves.Add(new QualityMove(i, final));
            }
            return moves;
        }
    }
}
