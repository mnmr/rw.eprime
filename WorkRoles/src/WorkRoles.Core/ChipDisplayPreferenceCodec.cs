#nullable enable

namespace WorkRoles.Core
{
    /// <summary>
    /// A decoded player-local chip preference: the chip display (0 full
    /// names, 1 initials, 2 icons) and whether chips use the grid layout.
    /// </summary>
    public readonly struct ChipDisplayPreference
    {
        public ChipDisplayPreference(int mode, bool grid)
        {
            Mode = mode;
            Grid = grid;
        }

        public int Mode { get; }
        public bool Grid { get; }
    }

    /// <summary>
    /// Owns the stable text format for the player-local colonist chip display
    /// preference. Values are integers here so the integration enum can remain
    /// outside the deterministic core assembly. The grid layout persists as
    /// its own flag; the retired combined names still load as that display
    /// with the grid on.
    /// </summary>
    public static class ChipDisplayPreferenceCodec
    {
        public static ChipDisplayPreference Decode(string? persistedMode,
            bool persistedGrid)
        {
            switch (persistedMode)
            {
                case "Compact": return new ChipDisplayPreference(1, persistedGrid);
                case "Minimal": // Legacy name for the mode now rendered as icons.
                case "Icons": return new ChipDisplayPreference(2, persistedGrid);
                case "CompactGrid": return new ChipDisplayPreference(1, true);
                case "IconsGrid": return new ChipDisplayPreference(2, true);
                default: return new ChipDisplayPreference(0, persistedGrid);
            }
        }

        public static string Encode(int mode)
        {
            switch (mode)
            {
                case 1: return "Compact";
                case 2: return "Icons";
                default: return "Normal";
            }
        }
    }
}
