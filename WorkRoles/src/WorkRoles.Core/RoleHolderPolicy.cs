using System;
using System.Globalization;
using System.Xml;

namespace WorkRoles.Core
{
    /// RoleDef value encoded as <minHolders waivers="N">M</minHolders>.
    /// The element value is the required total; waivers are slots within it.
    public sealed class ConfiguredHolderRequirement
    {
        public ConfiguredHolderRequirement() { }

        public ConfiguredHolderRequirement(
            int requiredTotal, int trainingWaivers = 0)
        {
            if (requiredTotal < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredTotal));
            if (trainingWaivers < 0)
                throw new ArgumentOutOfRangeException(nameof(trainingWaivers));
            RequiredTotal = requiredTotal;
            TrainingWaivers = trainingWaivers;
        }

        public int RequiredTotal { get; private set; }
        public int TrainingWaivers { get; private set; }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            int requiredTotal = int.Parse(
                xmlRoot.InnerText, CultureInfo.InvariantCulture);
            string rawWaivers = xmlRoot.Attributes?["waivers"]?.Value;
            int trainingWaivers = rawWaivers == null
                ? 0 : int.Parse(rawWaivers, CultureInfo.InvariantCulture);
            if (requiredTotal < 0 || trainingWaivers < 0)
                throw new FormatException(
                    "Role holder required total and training waivers cannot be negative.");
            RequiredTotal = requiredTotal;
            TrainingWaivers = trainingWaivers;
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

        public static (int requiredTotal, int max) WithRequiredTotal(
            int currentRequiredTotal, int currentMax, int value)
        {
            int requiredTotal = RoleHolderRange.Clamp(value);
            int max = RoleHolderRange.Clamp(currentMax);
            return (requiredTotal, System.Math.Max(requiredTotal, max));
        }

        public static (int requiredTotal, int max) WithMax(
            int currentRequiredTotal, int currentMax, int value)
        {
            int requiredTotal = RoleHolderRange.Clamp(currentRequiredTotal);
            int max = RoleHolderRange.Clamp(value);
            return (System.Math.Min(requiredTotal, max), max);
        }

        public static int WithTrainingWaivers(int requiredTotal, int value)
            => new HolderRequirement(requiredTotal, value).TrainingWaivers;

        public static (int requiredTotal, int max, int trainingWaivers) InitialCustom(
            int requiredTotal, int maximum, int trainingWaivers)
        {
            var range = WithRequiredTotal(0, maximum, requiredTotal);
            return (range.requiredTotal, range.max, WithTrainingWaivers(
                range.requiredTotal, trainingWaivers));
        }
    }
}
