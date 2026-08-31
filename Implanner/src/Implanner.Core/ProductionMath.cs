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
}
