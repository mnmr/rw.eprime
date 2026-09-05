using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// MultiFloors' prioritized work scanner keeps two giver lanes per pawn: a
    /// high lane holding every giver whose work-type priority is at or below
    /// its threshold, and a low lane holding the rest, both in the pawn's
    /// normal giver order. MultiFloors builds those lanes from vanilla
    /// priorities at work-type granularity; for managed pawns WorkRoles builds
    /// them from the compiled role order instead, applying the same rule to
    /// the vanilla priority projection so the threshold keeps its vanilla
    /// meaning.
    public static class MultiFloorsLanes
    {
        /// Partitions <paramref name="order"/> into <paramref name="high"/> and
        /// <paramref name="low"/>, replacing their contents. Entry i of
        /// <paramref name="workTypePriorities"/> is the vanilla-scale priority
        /// of order[i]'s work type.
        public static void Split<T>(
            IReadOnlyList<T> order,
            IReadOnlyList<int> workTypePriorities,
            int threshold,
            List<T> high,
            List<T> low)
        {
            high.Clear();
            low.Clear();
            for (int i = 0; i < order.Count; i++)
                (workTypePriorities[i] <= threshold ? high : low).Add(order[i]);
        }
    }
}
