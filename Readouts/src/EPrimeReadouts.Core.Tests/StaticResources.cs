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

    /// Builds a catalog pre-populated with the given defNames (labels default to the defName).
    public static FakeResourceCatalog CatalogWith(params string[] defNames)
    {
        var catalog = new FakeResourceCatalog();
        foreach (var d in defNames) catalog.With(d, d);
        return catalog;
    }

    public static Dictionary<string, int> Counts(params (string defName, int count)[] entries)
    {
        var counts = new Dictionary<string, int>();
        foreach (var (defName, count) in entries) counts[defName] = count;
        return counts;
    }

    /// Builds a PoolSnapshot with a single pool containing Meat_Cow and Meat_Chicken.
    /// Pool id=1, name="Meats", icon resolves to Meat_Cow.
    public static PoolSnapshot MeatPool(int poolId = 1, string? iconDefName = null)
    {
        var pool = new ResourcePool
        {
            Id = poolId,
            Name = "Meats",
            Members = new List<string> { "@MeatRaw" },
            IconDefName = iconDefName,
        };
        return PoolSnapshot.Build(new List<ResourcePool> { pool }, Catalog());
    }

    /// Builds a PoolSnapshot with an explicit icon defName set.
    public static PoolSnapshot MeatPoolWithIcon(int poolId = 1)
        => MeatPool(poolId, "Meat_Chicken");
}
