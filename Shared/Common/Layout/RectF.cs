namespace RimShared.Common
{
    /// Minimal rect for Core layout output; the game assembly converts to
    /// UnityEngine.Rect at draw time.
    public readonly struct RectF
    {
        public readonly float X, Y, W, H;
        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float Right => X + W;
        public float Bottom => Y + H;
        public bool Contains(float px, float py) =>
            px >= X && px < Right && py >= Y && py < Bottom;
    }
}
