namespace EPrimeReadouts.Core.Tests;

/// Content equality between rebuilt render models: an unchanged rebuild must
/// be detectable so the publisher can preserve the existing model identity
/// (and with it the base pixel surface keyed on that identity).
public class RenderModelEqualityTests
{
    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
        DepthOf = g => 1,
    };

    [Test]
    public async Task IdenticalInputsBuildContentEqualModels()
    {
        var first = ReadoutLayoutEngine.Build(Input(Group(1, "Steel", "WoodLog")));
        var second = ReadoutLayoutEngine.Build(Input(Group(1, "Steel", "WoodLog")));
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
        await Assert.That(RenderModelEquality.ContentEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task ChangedCountMakesModelsUnequal()
    {
        var first = ReadoutLayoutEngine.Build(Input(Group(1, "Steel")));
        var changed = Input(Group(1, "Steel"));
        changed.Counts = StaticResources.Counts(("Steel", 121), ("WoodLog", 75));
        var second = ReadoutLayoutEngine.Build(changed);
        await Assert.That(RenderModelEquality.ContentEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ChangedDepthMakesModelsUnequal()
    {
        var group = Group(1, "Steel");
        group.Tiers.Add(new List<string> { "WoodLog" });
        var first = ReadoutLayoutEngine.Build(Input(group));
        var deeper = Input(group);
        deeper.DepthOf = g => 2;
        var second = ReadoutLayoutEngine.Build(deeper);
        await Assert.That(RenderModelEquality.ContentEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ChangedHoverTriangleStateMakesModelsUnequal()
    {
        // Hover expansion flips Lit to HoverLit past the configured depth —
        // pixel-relevant, so it must defeat content equality.
        var group = Group(1, "Steel");
        group.Tiers.Add(new List<string> { "WoodLog" });
        var plain = Input(group);
        plain.DepthOf = g => 2;
        var first = ReadoutLayoutEngine.Build(plain);
        var hovered = Input(group);
        hovered.DepthOf = g => 2;
        hovered.ConfiguredDepthOf = g => 1;
        var second = ReadoutLayoutEngine.Build(hovered);
        await Assert.That(RenderModelEquality.ContentEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task NullAndSameReferenceFollowReferenceRules()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "Steel")));
        await Assert.That(RenderModelEquality.ContentEquals(model, model)).IsTrue();
        await Assert.That(RenderModelEquality.ContentEquals(model, null)).IsFalse();
        await Assert.That(RenderModelEquality.ContentEquals(null, model)).IsFalse();
        await Assert.That(RenderModelEquality.ContentEquals(null, null)).IsTrue();
    }
}
