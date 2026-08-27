using UnityEngine;

namespace WorkRoles
{
    /// Built-in swatches: Tailwind v3, shade-major rows (900 top, 600 bottom).
    /// Engine data, not UI: import/export resolves color references and seeding
    /// snaps def colors against it.
    internal static class SwatchPalette
    {
        internal static readonly Color[] Swatches;
        internal static readonly string[] Names;

        /// Export-format name of a built-in swatch: "slate-600" style.
        internal static string ExportName(int index) =>
            Names[index].ToLowerInvariant().Replace(' ', '-');

        internal static Color Hex(string h)
        {
            int r = System.Convert.ToInt32(h.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(h.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(h.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        static SwatchPalette()
        {
            // 19 families (neutral dropped), each with (900, 800, 700, 600) shades.
            // family-major array; drawn as shade-major rows (row 0=all 900s, ... row 3=all 600s).
            var families = new string[,]
            {
                // family        900       800       700       600
                /* slate    */ { "0F172A", "1E293B", "334155", "475569" },
                /* stone    */ { "1C1917", "292524", "44403C", "57534E" },
                /* red      */ { "7F1D1D", "991B1B", "B91C1C", "DC2626" },
                /* orange   */ { "7C2D12", "9A3412", "C2410C", "EA580C" },
                /* amber    */ { "78350F", "92400E", "B45309", "D97706" },
                /* yellow   */ { "713F12", "854D0E", "A16207", "CA8A04" },
                /* lime     */ { "365314", "3F6212", "4D7C0F", "65A30D" },
                /* green    */ { "14532D", "166534", "15803D", "16A34A" },
                /* emerald  */ { "064E3B", "065F46", "047857", "059669" },
                /* teal     */ { "134E4A", "115E59", "0F766E", "0D9488" },
                /* cyan     */ { "164E63", "155E75", "0E7490", "0891B2" },
                /* sky      */ { "0C4A6E", "075985", "0369A1", "0284C7" },
                /* blue     */ { "1E3A8A", "1E40AF", "1D4ED8", "2563EB" },
                /* indigo   */ { "312E81", "3730A3", "4338CA", "4F46E5" },
                /* violet   */ { "4C1D95", "5B21B6", "6D28D9", "7C3AED" },
                /* purple   */ { "581C87", "6B21A8", "7E22CE", "9333EA" },
                /* fuchsia  */ { "701A75", "86198F", "A21CAF", "C026D3" },
                /* pink     */ { "831843", "9D174D", "BE185D", "DB2777" },
                /* rose     */ { "881337", "9F1239", "BE123C", "E11D48" },
            };

            var familyNames = new[]
            {
                "Slate", "Stone", "Red", "Orange", "Amber", "Yellow", "Lime", "Green", "Emerald",
                "Teal", "Cyan", "Sky", "Blue", "Indigo", "Violet", "Purple", "Fuchsia", "Pink", "Rose",
            };
            var shadeNames = new[] { "900", "800", "700", "600" };

            int numFamilies = families.GetLength(0); // 19
            int numShades   = families.GetLength(1); // 4

            // shade-major order: swatches[shade * numFamilies + family]
            Swatches = new Color[numShades * numFamilies];
            Names = new string[numShades * numFamilies];
            for (int shade = 0; shade < numShades; shade++)
                for (int family = 0; family < numFamilies; family++)
                {
                    Swatches[shade * numFamilies + family] = Hex(families[family, shade]);
                    Names[shade * numFamilies + family] = familyNames[family] + " " + shadeNames[shade];
                }
        }
    }
}
