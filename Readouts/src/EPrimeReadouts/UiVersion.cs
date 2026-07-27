namespace EPrimeReadouts
{
    /// Monotonic UI cache stamp. WrText.FitWidth caches measured widths against
    /// this; bump it if cached text metrics can go stale (e.g. UI scale or font
    /// changes).
    public static class UiVersion
    {
        public static int Current { get; private set; }

        public static void Bump() => Current++;
    }
}
