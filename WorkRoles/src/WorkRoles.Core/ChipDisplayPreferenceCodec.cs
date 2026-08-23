#nullable enable

namespace WorkRoles.Core
{
    /// <summary>
    /// Owns the stable text format for the player-local colonist chip display
    /// preference. Values are integers here so the integration enum can remain
    /// outside the deterministic core assembly.
    /// </summary>
    public static class ChipDisplayPreferenceCodec
    {
        public static int Decode(string? persisted)
        {
            switch (persisted)
            {
                case "Compact": return 1;
                case "Minimal": // Legacy name for the mode now rendered as icons.
                case "Icons": return 2;
                case "CompactGrid": return 3;
                case "IconsGrid": return 4;
                default: return 0;
            }
        }

        public static string Encode(int value)
        {
            switch (value)
            {
                case 1: return "Compact";
                case 2: return "Icons";
                case 3: return "CompactGrid";
                case 4: return "IconsGrid";
                default: return "Normal";
            }
        }
    }
}
