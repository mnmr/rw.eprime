using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Detached recipe facts. Candidates arrive in ordinal recipe-id order.
    public sealed class ProductionRecipe<T> where T : notnull
    {
        public ProductionRecipe(string id, T item, int output,
            IReadOnlyList<(T Resource, int Count)> costs, bool available,
            double work = 0)
        { Id = id; Item = item; Output = output; Costs = costs; Available = available; Work = work; }
        public string Id { get; }
        public T Item { get; }
        public int Output { get; }
        public IReadOnlyList<(T Resource, int Count)> Costs { get; }
        public bool Available { get; }
        public double Work { get; }
    }

    public readonly struct ProductionRoute<T> where T : notnull
    {
        private ProductionRoute(ProductionRecipe<T>? recipe, T? blocker, bool blocked,
            IReadOnlyList<ProductionRecipe<T>> crafts)
        { Recipe = recipe; Blocker = blocker; Blocked = blocked; Crafts = crafts; }
        public ProductionRecipe<T>? Recipe { get; }
        public T? Blocker { get; }
        public bool Blocked { get; }
        /// A feasible sequence of crafts, prerequisites first, including the final
        /// craft. Dispatch must use these recipes: reselecting a prerequisite in
        /// isolation can spend materials that this route leaves for its parent.
        public IReadOnlyList<ProductionRecipe<T>> Crafts { get; }

        public static ProductionRoute<T> Select(T item,
            IReadOnlyDictionary<T, IReadOnlyList<ProductionRecipe<T>>> recipes,
            ProductionBudget<T> budget, ISet<T> intermediaries, int maxDepth = 8,
            ProductionBudget<T>? futureBudget = null)
        {
            ProductionRecipe<T>? best = null;
            State? solution = null;
            T? blocker = item;
            int rank = 3;
            double work = double.MaxValue;
            if (recipes.TryGetValue(item, out var candidates))
                for (int i = 0; i < candidates.Count; i++)
                {
                    ProductionRecipe<T> recipe = candidates[i];
                    if (!recipe.Available || recipe.Output <= 0) continue;
                    State? found = null;
                    var failure = new Failure { Item = item };
                    bool complete = Search(recipe, new State(futureBudget ?? budget), recipes,
                        intermediaries, new HashSet<T>(), 0, maxDepth, failure,
                        value => { found = value; return true; });
                    int candidateRank = complete ? (Fits(recipe, budget) ? 0 : 1) : 2;
                    double candidateWork = found?.Work ?? recipe.Work;
                    if (best != null && (candidateRank > rank || (candidateRank == rank
                        && (candidateWork > work || (candidateWork == work
                            && string.CompareOrdinal(recipe.Id, best.Id) >= 0))))) continue;
                    best = recipe;
                    rank = candidateRank;
                    work = candidateWork;
                    blocker = failure.Item;
                    solution = found;
                }
            return new ProductionRoute<T>(best, rank >= 2 ? blocker : default, rank >= 2,
                solution == null ? Array.Empty<ProductionRecipe<T>>() : solution.Crafts.AsReadOnly());
        }

        private sealed class State
        {
            internal State(ProductionBudget<T> budget) { Budget = budget; }
            internal ProductionBudget<T> Budget;
            internal List<ProductionRecipe<T>> Crafts = new List<ProductionRecipe<T>>();
            internal double Work;
            internal State Copy() => new State(Budget.Copy())
                { Crafts = new List<ProductionRecipe<T>>(Crafts), Work = Work };
        }

        private sealed class Failure { internal T? Item; }

        private static bool Fits(ProductionRecipe<T> recipe, ProductionBudget<T> budget) =>
            budget.Copy().TryCommit(recipe.Costs);

        private static bool Search(ProductionRecipe<T> recipe, State start,
            IReadOnlyDictionary<T, IReadOnlyList<ProductionRecipe<T>>> recipes,
            ISet<T> intermediaries, HashSet<T> ancestors, int depth, int maximum,
            Failure failure, Func<State, bool> accept)
        {
            if (depth > maximum || ancestors.Contains(recipe.Item))
            { failure.Item = recipe.Item; return false; }
            var path = new HashSet<T>(ancestors) { recipe.Item };
            State trial = start.Copy();
            var missing = new List<T>();
            // Reserve every parent ingredient before considering prerequisites.
            // Negative promises represent shortages, not permission to spend
            // materials already allocated to an ancestor or sibling.
            for (int i = 0; i < recipe.Costs.Count; i++)
            {
                var cost = recipe.Costs[i];
                trial.Budget.Commit(cost.Resource, cost.Count);
            }
            for (int i = 0; i < recipe.Costs.Count; i++)
            {
                var cost = recipe.Costs[i];
                int shortage = trial.Budget.Shortfall(cost.Resource, 0);
                if (shortage <= 0) continue;
                if (!intermediaries.Contains(cost.Resource) || depth >= maximum)
                { failure.Item = cost.Resource; return false; }
                if (!missing.Contains(cost.Resource)) missing.Add(cost.Resource);
            }
            return Fill(0, trial);

            bool Fill(int index, State state)
            {
                if (index == missing.Count)
                {
                    State done = state.Copy();
                    done.Crafts.Add(recipe);
                    done.Work += recipe.Work;
                    done.Budget.Supply(recipe.Item, recipe.Output);
                    return accept(done);
                }
                T item = missing[index];
                if (state.Budget.Shortfall(item, 0) <= 0) return Fill(index + 1, state);
                if (!recipes.TryGetValue(item, out var available))
                { failure.Item = item; return false; }
                var candidates = new List<ProductionRecipe<T>>();
                for (int i = 0; i < available.Count; i++)
                    if (available[i].Available && available[i].Output > 0) candidates.Add(available[i]);
                candidates.Sort((a, b) =>
                {
                    int order = Fits(b, state.Budget).CompareTo(Fits(a, state.Budget));
                    if (order == 0) order = a.Work.CompareTo(b.Work);
                    return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
                });
                if (candidates.Count == 0) failure.Item = item;
                for (int i = 0; i < candidates.Count; i++)
                {
                    ProductionRecipe<T> next = candidates[i];
                    // The continuation includes the remaining siblings and parent.
                    // A dead end retries this ingredient's next recipe, retaining
                    // the exact successful choices for dispatch.
                    if (Search(next, state, recipes, intermediaries, path, depth + 1,
                        maximum, failure, value => Fill(index, value))) return true;
                }
                return false;
            }
        }
    }
}
