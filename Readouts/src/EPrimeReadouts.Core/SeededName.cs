using System;

namespace EPrimeReadouts.Core
{
    /// Display-only seed resolution. Missing provenance is intentionally treated
    /// as authored: historical saves cannot distinguish custom seed-equivalent names.
    public static class SeededName
    {
        public static string Resolve(string name, string? seedName, Func<string, string>? translate) =>
            translate != null && !string.IsNullOrEmpty(seedName)
                && string.Equals(name, seedName, StringComparison.Ordinal)
                ? translate(seedName!) : name;
    }
}
