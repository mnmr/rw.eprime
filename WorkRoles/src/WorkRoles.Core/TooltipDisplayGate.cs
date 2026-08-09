using System;

namespace WorkRoles.Core
{
    public enum TooltipDisplayState
    {
        Pending,
        Opened,
        Visible,
        Suppressed,
    }

    /// Tracks one tooltip's continuous-hover delay without depending on GUI
    /// repaint frequency for session identity.
    public sealed class TooltipDisplayGate
    {
        private string key;
        private int lastFrame = TipContinuity.NoFrame;
        private float firstSeenAt;
        private bool visible;
        private bool suppressed;

        public void SetSuppressed(bool value)
        {
            if (suppressed == value) return;
            suppressed = value;
            ResetSession();
        }

        public TooltipDisplayState Observe(
            string stableKey, int frame, float now, float delay)
        {
            if (stableKey == null) throw new ArgumentNullException(nameof(stableKey));
            if (suppressed) return TooltipDisplayState.Suppressed;
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
            suppressed = false;
            ResetSession();
        }

        private void ResetSession()
        {
            key = null;
            lastFrame = TipContinuity.NoFrame;
            firstSeenAt = 0f;
            visible = false;
        }
    }
}
