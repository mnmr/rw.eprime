using System.Collections.Generic;
using RimShared.Common;
using UnityEngine;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Unity/Core color bridging for palette planning (SwatchPickPlanner,
    /// PaletteCoverageEnforcer).
    internal static class PaletteColors
    {
        // Owner: process. Key: none (SwatchPalette is immutable static data).
        // Value: the standard swatches as Core Rgba values. Dependencies:
        // none. Refresh: never. Equality: n/a. Teardown: none.
        private static IReadOnlyList<Rgba>? standardRgba;

        internal static IReadOnlyList<Rgba> StandardRgba()
        {
            if (standardRgba != null) return standardRgba;
            var swatches = SwatchPalette.Swatches;
            var list = new List<Rgba>(swatches.Length);
            for (int i = 0; i < swatches.Length; i++)
                list.Add(ToRgba(swatches[i]));
            return standardRgba = list;
        }

        internal static Rgba ToRgba(Color color) =>
            new Rgba(color.r, color.g, color.b, color.a);

        internal static Color ToColor(Rgba color) =>
            new Color(color.R, color.G, color.B, color.A);
    }
}
