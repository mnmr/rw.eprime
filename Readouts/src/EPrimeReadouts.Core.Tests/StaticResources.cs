namespace EPrimeReadouts.Core.Tests;

/// Frozen vanilla-like resource set for layout tests. Labels are lowercase
/// like RimWorld's def labels.
public static class StaticResources
{
    public static FakeResourceCatalog Catalog() => new FakeResourceCatalog()
        .With("Steel", "steel")
        .With("WoodLog", "wood")
        .With("Plasteel", "plasteel")
        .With("ComponentIndustrial", "component")
        .With("Silver", "silver")
        .With("Gold", "gold")
        .With("Cloth", "cloth")
        .With("MealSimple", "simple meal")
        .With("MealFine", "fine meal")
        .With("RawRice", "rice")
        .With("MedicineHerbal", "herbal medicine")
        .With("MedicineIndustrial", "medicine")
        .With("Meat_Cow", "cow meat")
        .With("Meat_Chicken", "chicken meat")
        .WithCategory("MeatRaw", "raw meat", "Meat_Cow", "Meat_Chicken");

    public static Dictionary<string, int> Counts(params (string defName, int count)[] entries)
    {
        var counts = new Dictionary<string, int>();
        foreach (var (defName, count) in entries) counts[defName] = count;
        return counts;
    }
}
