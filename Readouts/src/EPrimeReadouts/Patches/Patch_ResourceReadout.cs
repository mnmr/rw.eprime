using EPrimeReadouts.UI;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Replaces the vanilla resource readout wholesale. The mod setting is an
    /// instant escape hatch back to vanilla behavior, and faults inside the
    /// panel step down through the same ladder on their own: a faulting
    /// frame is handed to vanilla, repeated faults first retire the buffered
    /// renderer (the panel keeps working through the direct renderer), and
    /// faults that persist after that hand the readout to vanilla for the
    /// rest of the session. A broken readout must never leave the player
    /// with no readout at all.
    [HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
    public static class Patch_ResourceReadout
    {
        /// Handled faults before the next step down the ladder.
        private const int FaultThreshold = 5;
        private static int faultCount;
        private static bool vanillaForSession;

        /// True once faults exhausted the ladder and vanilla draws the readout
        /// for the rest of the session (a world reload starts over).
        internal static bool VanillaForSession => vanillaForSession;

        internal static void ResetFaults()
        {
            faultCount = 0;
            vanillaForSession = false;
        }

        public static bool Prefix()
        {
            if (EPrimeReadoutsMod.Settings.useVanillaReadout || vanillaForSession)
                return true;
            // ResourceReadoutOnGUI is invoked for both Layout and Repaint.
            // We replace vanilla on Layout too, but have no layout work of our
            // own, so keep that call out of the panel pipeline entirely.
            if (Event.current.type == EventType.Layout) return false;
            try
            {
                ReadoutPanel.OnGUI();
                return false;
            }
            catch (System.Exception exception)
            {
                // The panel's own draw and scope guards restore GUI state on
                // the way out; this frame goes to vanilla.
                StepDown(exception);
                return true;
            }
        }

        private static void StepDown(System.Exception exception)
        {
            faultCount++;
            if (faultCount < FaultThreshold)
            {
                if (faultCount == 1)
                    Log.Error("[Readouts] Readout panel failed; drawing the "
                        + "vanilla readout for this frame: " + exception);
                return;
            }
            faultCount = 0;
            if (ReadoutPanel.RetireBufferedRenderer(
                    "the panel kept failing (" + FaultThreshold + " faults)"))
            {
                Log.Error("[Readouts] Readout panel failed " + FaultThreshold
                    + " times; buffered rendering is off for this session "
                    + "and the panel draws directly: " + exception);
                return;
            }
            vanillaForSession = true;
            Log.Error("[Readouts] Readout panel failed " + FaultThreshold
                + " more times; the vanilla readout takes over for this "
                + "session: " + exception);
        }
    }
}
