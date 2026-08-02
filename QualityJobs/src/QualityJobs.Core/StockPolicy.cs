namespace QualityJobs.Core
{
    /// <summary>Stock control (spec §9): count = ALL spawned UFTs of the product on the
    /// map (active, shared, paused, dispatched — every in-progress item is a
    /// promise of future stock). Enforcement blocks only new starts; resume
    /// paths are never touched. Cap &lt;= 0 disables the limit.</summary>
    public static class StockPolicy
    {
        public static bool CanStartNewItem(int spawnedUftCount, int cap, bool isFinishBill)
        {
            if (isFinishBill) return true;
            if (cap <= 0) return true;
            return spawnedUftCount < cap;
        }
    }
}
