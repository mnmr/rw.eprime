namespace WorkRoles.Core
{
    public enum RoleHolderMode
    {
        Auto,
        Never,
        Custom,
    }

    public static class RoleHolderRange
    {
        public const int Uncapped = 256;

        public static int Clamp(int value) =>
            System.Math.Max(0, System.Math.Min(Uncapped, value));
    }

    public static class RoleHolderPolicy
    {
        public static RoleHolderMode Next(RoleHolderMode mode)
        {
            switch (mode)
            {
                case RoleHolderMode.Auto: return RoleHolderMode.Never;
                case RoleHolderMode.Never: return RoleHolderMode.Custom;
                default: return RoleHolderMode.Auto;
            }
        }

        public static (int min, int max) WithMin(int currentMin, int currentMax, int value)
        {
            int min = RoleHolderRange.Clamp(value);
            int max = RoleHolderRange.Clamp(currentMax);
            return (min, System.Math.Max(min, max));
        }

        public static (int min, int max) WithMax(int currentMin, int currentMax, int value)
        {
            int min = RoleHolderRange.Clamp(currentMin);
            int max = RoleHolderRange.Clamp(value);
            return (System.Math.Min(min, max), max);
        }
    }
}
