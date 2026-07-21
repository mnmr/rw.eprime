using UnityEngine;

namespace WorkRoles.UI
{
    /// Visual tokens shared by independent Work Roles surfaces.
    internal static class WrStyle
    {
        internal static readonly Color PanelBackground =
            new Color(0.08f, 0.08f, 0.08f, 0.9f);
        internal static readonly Color PanelOutline =
            new Color(1f, 1f, 1f, 0.15f);
        internal static readonly Color CaptionText =
            new Color(0.60f, 0.62f, 0.64f);
        internal static readonly Color DimText =
            new Color(0.6f, 0.6f, 0.6f);
        internal static readonly Color DisabledText =
            new Color(0.45f, 0.45f, 0.45f);
        internal static readonly Color MinorAccent =
            new Color(0.95f, 0.9f, 0.55f);
    }
}
