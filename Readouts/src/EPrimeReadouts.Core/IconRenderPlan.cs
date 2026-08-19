namespace EPrimeReadouts.Core
{
    /// Immutable scale decision used by the game renderer's cached icon data.
    /// Unsupported textures retain the correction for the ThingIcon fallback.
    public readonly struct IconRenderPlan
    {
        private IconRenderPlan(
            bool useDirectRendering,
            float correctionScale,
            float fittedScale)
        {
            UseDirectRendering = useDirectRendering;
            CorrectionScale = correctionScale;
            FittedScale = fittedScale;
        }

        public readonly bool UseDirectRendering;
        public readonly float CorrectionScale;
        public readonly float FittedScale;

        public static IconRenderPlan Create(
            bool hasUsableTexture,
            float correctionScale,
            float vanillaDrawScale)
            => new IconRenderPlan(
                hasUsableTexture,
                correctionScale,
                hasUsableTexture
                    ? correctionScale * vanillaDrawScale : 0f);
    }
}
