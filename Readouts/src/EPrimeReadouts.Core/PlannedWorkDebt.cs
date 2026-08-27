namespace EPrimeReadouts.Core
{
    /// Material a def already owes to work the colony has planned but not yet
    /// performed, split by source so a tooltip can attribute the shortfall.
    /// Both fields are non-negative amounts of the def's own units.
    public readonly struct PlannedWorkDebt
    {
        /// Ingredients outstanding bill iterations will consume.
        public readonly int Bills;
        /// Materials undelivered blueprints and part-built frames still need.
        public readonly int Buildables;

        public PlannedWorkDebt(int bills, int buildables)
        {
            Bills = bills;
            Buildables = buildables;
        }

        public int Total => Bills >= int.MaxValue - Buildables
            ? int.MaxValue : Bills + Buildables;

        public bool Equals(in PlannedWorkDebt other)
            => Bills == other.Bills && Buildables == other.Buildables;
    }
}
