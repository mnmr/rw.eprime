using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// Tooltip text conventions, shared by UI and the game-side tip composers:
    /// facts stay default white, labels/hints/meta dim gray, warnings red.
    internal static class TipText
    {
        internal static readonly Color DimColor = new Color(0.62f, 0.62f, 0.62f);
        internal static readonly Color WarningColor = new Color(1f, 0.35f, 0.35f);

        internal static string Dim(string text) => text.Colorize(DimColor);

        internal static string Warning(string text) => text.Colorize(WarningColor);
    }
}
