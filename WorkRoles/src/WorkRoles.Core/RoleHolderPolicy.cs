using System;
using System.Globalization;
using System.Xml;

namespace WorkRoles.Core
{
    /// RoleDef value encoded as <minHolders waivers="N">M</minHolders>.
    public sealed class RoleHolderMinimum
    {
        public RoleHolderMinimum() { }

        public RoleHolderMinimum(int count, int waivers = 0)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (waivers < 0) throw new ArgumentOutOfRangeException(nameof(waivers));
            Count = count;
            Waivers = waivers;
        }

        public int Count { get; private set; }
        public int Waivers { get; private set; }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            int count = int.Parse(xmlRoot.InnerText, CultureInfo.InvariantCulture);
            string rawWaivers = xmlRoot.Attributes?["waivers"]?.Value;
            int waivers = rawWaivers == null
                ? 0 : int.Parse(rawWaivers, CultureInfo.InvariantCulture);
            if (count < 0 || waivers < 0)
                throw new FormatException("Role holder minimums and waivers cannot be negative.");
            Count = count;
            Waivers = waivers;
        }
    }

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

        public static int WithTraining(int minimum, int value)
            => System.Math.Max(0, System.Math.Min(RoleHolderRange.Clamp(minimum), value));

        public static (int min, int max, int waivers) InitialCustom(
            int minimum, int maximum, int waivers)
        {
            var range = WithMin(0, maximum, minimum);
            return (range.min, range.max, WithTraining(range.min, waivers));
        }
    }
}
