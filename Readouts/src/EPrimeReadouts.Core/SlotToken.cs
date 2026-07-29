namespace EPrimeReadouts.Core
{
    /// Slot token grammar: "[~](defName | #poolId | @CategoryDefName)".
    /// '~' = hide-when-zero (default is show).
    /// '@' = legacy category-pool (pool members + save-migration source only).
    /// '#' = first-class resource pool reference.
    /// Neither '@', '#', nor '~' is legal in RimWorld defNames.
    public static class SlotToken
    {
        public static bool ShowWhenZero(string token) => !token.StartsWith("~");

        public static string Canonical(string token) =>
            ShowWhenZero(token) ? token : token.Substring(1);

        /// Returns true when token is a legacy @Category pool token.
        public static bool IsPool(string token) => Canonical(token).StartsWith("@");

        /// Returns true when token references a first-class resource pool (#id).
        public static bool IsPoolRef(string token) => Canonical(token).StartsWith("#");

        /// Pool id of a "#id" token; -1 when not a pool ref or unparsable.
        public static int PoolId(string token)
        {
            var canonical = Canonical(token);
            if (!canonical.StartsWith("#")) return -1;
            return int.TryParse(canonical.Substring(1), out int id) ? id : -1;
        }

        /// Builds a plain (no flag) pool-reference token for the given pool id.
        public static string PoolToken(int poolId) => "#" + poolId;

        /// The defName or category defName without flag/pool markers.
        public static string MemberName(string token)
        {
            var canonical = Canonical(token);
            if (canonical.StartsWith("@")) return canonical.Substring(1);
            if (canonical.StartsWith("#")) return canonical.Substring(1);
            return canonical;
        }

        public static string WithShowWhenZero(string token, bool show) =>
            show ? Canonical(token) : "~" + Canonical(token);
    }
}
