using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// A pass's pending output, shared by final goals and prerequisites. Quantities
    /// are items, not bills: each bill keeps its own yield and quality promise.
    public sealed class ProductionSupply<T> where T : notnull
    {
        private sealed class Output
        {
            internal string Bill = "";
            internal int Count;
            internal int Quality;
        }
        private readonly Dictionary<T, List<Output>> outputs = new Dictionary<T, List<Output>>();
        private readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        public bool Uses(string bill) => used.Contains(bill);
        /// Detached route probes may account for pending output without claiming
        /// it. Only Take allocates it to a final goal or prerequisite.
        public void AddAvailableTo(ProductionBudget<T> budget)
        {
            foreach (var pair in outputs)
                for (int i = 0; i < pair.Value.Count; i++)
                    budget.Supply(pair.Key, pair.Value[i].Count);
        }
        public void Add(T item, string bill, int count, int quality)
        {
            if (!outputs.TryGetValue(item, out var list))
                outputs[item] = list = new List<Output>();
            list.Add(new Output { Bill = bill, Count = count, Quality = quality });
        }
        /// Returns the shortfall after spending pending output exactly once.
        public int Take(T item, int count, int minimum = 0,
            int acceptableQualities = ImplantQuality.AllQualities)
        {
            if (!outputs.TryGetValue(item, out var list)) return count;
            while (count > 0)
            {
                Output? chosen = null;
                for (int i = 0; i < list.Count; i++)
                {
                    Output output = list[i];
                    if (output.Count <= 0 || (output.Quality != ProductionQueue.UnknownQuality
                        && (output.Quality < minimum
                            || !ImplantQuality.Accepts(acceptableQualities, output.Quality)))) continue;
                    if (chosen == null || (output.Quality != ProductionQueue.UnknownQuality
                        && (chosen.Quality == ProductionQueue.UnknownQuality
                            || output.Quality < chosen.Quality))) chosen = output;
                }
                if (chosen == null) break;
                int take = Math.Min(count, chosen.Count);
                chosen.Count -= take;
                count -= take;
                used.Add(chosen.Bill);
            }
            return count;
        }
    }
}
