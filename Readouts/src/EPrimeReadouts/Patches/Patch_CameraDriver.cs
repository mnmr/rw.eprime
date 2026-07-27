using EPrimeReadouts.UI;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Vanilla only exempts a 200x200 top-left corner from edge scrolling, so
    /// interacting with a tall readout near the left edge drifts the camera.
    /// While the mouse is inside the panel rect, report it as covered by UI
    /// for the edge-dolly calculation.
    [HarmonyPatch(typeof(CameraDriver), "CalculateCurInputDollyVect")]
    public static class Patch_CameraDriver
    {
        public static void Prefix(ref bool ___mouseCoveredByUI)
        {
            if (___mouseCoveredByUI) return;
            Vector2 mouse = Verse.UI.MousePositionOnUI;
            var point = new Vector2(mouse.x, Verse.UI.screenHeight - mouse.y);
            if (ReadoutPanel.IsOverPoint(point)) ___mouseCoveredByUI = true;
        }
    }
}
