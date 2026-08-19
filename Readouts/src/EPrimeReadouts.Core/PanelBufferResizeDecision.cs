namespace EPrimeReadouts.Core
{
    public readonly struct PanelBufferResizeDecision
    {
        private PanelBufferResizeDecision(
            bool keepFront,
            bool replaceWorking,
            bool replaceBack)
        {
            KeepFront = keepFront;
            ReplaceWorking = replaceWorking;
            ReplaceBack = replaceBack;
        }

        public bool KeepFront { get; }
        public bool ReplaceWorking { get; }
        public bool ReplaceBack { get; }

        public static PanelBufferResizeDecision Create(
            bool hasFront,
            int workingWidth,
            int workingHeight,
            int backWidth,
            int backHeight,
            int nextWidth,
            int nextHeight) =>
            new PanelBufferResizeDecision(
                hasFront,
                workingWidth != nextWidth || workingHeight != nextHeight,
                backWidth != nextWidth || backHeight != nextHeight);
    }
}
