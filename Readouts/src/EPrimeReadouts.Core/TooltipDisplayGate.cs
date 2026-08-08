using System;

namespace EPrimeReadouts.Core
{
    public enum TooltipDisplayState
    {
        Pending,
        Opened,
        Visible,
    }

    /// Tracks one tooltip's continuous-hover delay without depending on GUI
    /// repaint frequency for session identity.
    public sealed class TooltipDisplayGate
    {
        private string key;
        private int lastFrame = TipContinuity.NoFrame;
        private float firstSeenAt;
        private bool visible;

        public TooltipDisplayState Observe(
            string stableKey, int frame, float now, float delay)
        {
            if (stableKey == null) throw new ArgumentNullException(nameof(stableKey));
            if (!string.Equals(key, stableKey, StringComparison.Ordinal)
                || TipContinuity.IsBroken(lastFrame, frame))
            {
                key = stableKey;
                firstSeenAt = now;
                visible = false;
            }
            lastFrame = frame;
            if (visible) return TooltipDisplayState.Visible;
            if (now < firstSeenAt + delay) return TooltipDisplayState.Pending;
            visible = true;
            return TooltipDisplayState.Opened;
        }

        public void Reset()
        {
            key = null;
            lastFrame = TipContinuity.NoFrame;
            firstSeenAt = 0f;
            visible = false;
        }
    }
}
