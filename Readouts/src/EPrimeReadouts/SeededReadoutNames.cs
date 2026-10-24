using System;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;

namespace EPrimeReadouts
{
    /// Display-only translation for explicitly seeded, unchanged names. Unknown
    /// legacy names remain verbatim because their authorship cannot be recovered.
    internal static class SeededReadoutNames
    {
        internal static readonly Func<string, string> Resolve = TranslateSeed;

        internal static string Name(ReadoutGroup group) => Name(group.Name, group.SeedName);
        internal static string Name(ResourcePool pool) => Name(pool.Name, pool.SeedName);
        internal static string Name(PoolSnapshotEntry pool) => Name(pool.Name, pool.SeedName);
        internal static string Name(ReadoutSnapshot.Pool pool) => Name(pool.Name, pool.SeedName);
        internal static string Name(ReadoutSnapshot.Group group) => Name(group.Name, group.SeedName);
        private static string Name(string value, string? seedName) =>
            SeededName.Resolve(value, seedName, Resolve);

        private static string TranslateSeed(string value)
        {
            switch (value)
            {
                case "Eggs": return UiText.Get("EPR.SeedEggs");
                case "Fertilized eggs": return UiText.Get("EPR.SeedFertilizedeggs");
                case "Hard Drugs": return UiText.Get("EPR.SeedHardDrugs");
                case "Leathers": return UiText.Get("EPR.SeedLeathers");
                case "Meals": return UiText.Get("EPR.SeedMeals");
                case "Meats": return UiText.Get("EPR.SeedMeats");
                case "Mechs": return UiText.Get("EPR.SeedMechs");
                case "Mortar shells": return UiText.Get("EPR.SeedMortarshells");
                case "Plant food": return UiText.Get("EPR.SeedPlantfood");
                case "Sera": return UiText.Get("EPR.SeedSera");
                case "Social Drugs": return UiText.Get("EPR.SeedSocialDrugs");
                case "Stone blocks": return UiText.Get("EPR.SeedStoneblocks");
                case "Stone chunks": return UiText.Get("EPR.SeedStonechunks");
                case "Wools": return UiText.Get("EPR.SeedWools");
                case "Food": return UiText.Get("EPR.SeedFood");
                case "Raw": return UiText.Get("EPR.SeedRaw");
                case "Medicine": return UiText.Get("EPR.SeedMedicine");
                case "Drugs": return UiText.Get("EPR.SeedDrugs");
                case "Textiles": return UiText.Get("EPR.SeedTextiles");
                case "Materials": return UiText.Get("EPR.SeedMaterials");
                case "Wealth": return UiText.Get("EPR.SeedWealth");
                default: return value;
            }
        }
    }
}
