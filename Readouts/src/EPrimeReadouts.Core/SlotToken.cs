namespace EPrimeReadouts.Core
{
    /// Slot token grammar: "[~]defName" or "[~]@CategoryDefName".
    /// '~' = hide-when-zero (default is show); '@' = category pool.
    /// Neither character is legal in RimWorld defNames.
    public static class SlotToken
    {
        public static bool ShowWhenZero(string token) => !token.StartsWith("~");

        public static string Canonical(string token) =>
            ShowWhenZero(token) ? token : token.Substring(1);

        public static bool IsPool(string token) => Canonical(token).StartsWith("@");

        /// The defName or category defName without flag/pool markers.
        public static string MemberName(string token)
        {
            var canonical = Canonical(token);
            return canonical.StartsWith("@") ? canonical.Substring(1) : canonical;
        }

        public static string WithShowWhenZero(string token, bool show) =>
            show ? Canonical(token) : "~" + Canonical(token);
    }
}
