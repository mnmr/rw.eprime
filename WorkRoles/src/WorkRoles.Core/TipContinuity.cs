namespace WorkRoles.Core
{
    public static class TipContinuity
    {
        public const int NoFrame = int.MinValue;

        public static bool IsBroken(int lastFrame, int currentFrame) =>
            lastFrame == NoFrame || currentFrame - lastFrame > 1;
    }
}
