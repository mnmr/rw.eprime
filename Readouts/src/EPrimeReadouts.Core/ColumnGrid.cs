namespace EPrimeReadouts.Core
{
    /// Column-major partitioning for height-capped grids: items fill a column
    /// top-to-bottom before the next column starts, so the surface grows wider
    /// rather than taller.
    public static class ColumnGrid
    {
        public static int ColumnCount(int itemCount, int maxRows) =>
            itemCount <= 0 ? 0 : (itemCount + maxRows - 1) / maxRows;

        public static int ColumnOf(int index, int maxRows) => index / maxRows;

        public static int RowOf(int index, int maxRows) => index % maxRows;

        /// Rows occupied by the given column; the last column may be short.
        public static int RowsInColumn(int column, int itemCount, int maxRows)
        {
            int start = column * maxRows;
            if (start >= itemCount) return 0;
            int remaining = itemCount - start;
            return remaining < maxRows ? remaining : maxRows;
        }
    }
}
