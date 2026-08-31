namespace RimShared.Common
{
    /// Allows one process-wide batch for each observed frame number. Multiple
    /// map components may call the owner during the same frame; only the first
    /// performs work.
    public sealed class FrameBatchGate
    {
        private int lastFrame = int.MinValue;

        public bool TryEnter(int frame)
        {
            if (lastFrame == frame) return false;
            lastFrame = frame;
            return true;
        }

        public void Reset()
        {
            lastFrame = int.MinValue;
        }
    }
}
