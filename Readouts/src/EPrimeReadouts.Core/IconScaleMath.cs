namespace EPrimeReadouts.Core
{
    public static class IconScaleMath
    {
        private const float TargetCoverage = 0.88f;
        private const float MinScale = 0.80f;
        private const float MaxScale = 1.25f;
        private const float NeutralDeadZone = 0.08f;

        public static float CorrectionFor(
            int opaqueExtent,
            int sampleSize,
            float vanillaDrawScale)
        {
            if (opaqueExtent <= 0 || sampleSize <= 0
                || vanillaDrawScale <= 0f)
                return 1f;

            float coverage = opaqueExtent / (float)sampleSize;
            float desired = TargetCoverage
                / (coverage * vanillaDrawScale);
            float correction = desired < MinScale
                ? MinScale : desired > MaxScale ? MaxScale : desired;
            return System.Math.Abs(correction - 1f) < NeutralDeadZone
                ? 1f : correction;
        }
    }
}
