namespace EPrimeReadouts.Core
{
    public enum Band { Normal, Low, Critical }

    public static class ThresholdBands
    {
        public static Band For(int count, ThresholdSpec spec) =>
            count <= spec.Critical ? Band.Critical :
            count <= spec.Low ? Band.Low : Band.Normal;
    }
}
