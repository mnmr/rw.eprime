using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for PoolTriState: IsSelected, CategoryState, ToggleDef, ToggleCategory.
public class PoolTriStateTests
{
    // Catalog: MeatRaw → [Meat_Cow, Meat_Chicken], Steel/WoodLog/Plasteel as plain defs.
    private static IResourceCatalog Cat() => StaticResources.Catalog();

    private static List<string> Members(params string[] m) => new List<string>(m);

    // ── IsSelected ────────────────────────────────────────────────────────

    [Test]
    public async Task IsSelected_ExplicitDef_True()
    {
        await Assert.That(PoolTriState.IsSelected(Members("Steel", "WoodLog"), "Steel", Cat())).IsTrue();
    }

    [Test]
    public async Task IsSelected_MissingDef_False()
    {
        await Assert.That(PoolTriState.IsSelected(Members("WoodLog"), "Steel", Cat())).IsFalse();
    }

    [Test]
    public async Task IsSelected_CoveredByAtRef_True()
    {
        // @MeatRaw expands to Meat_Cow, Meat_Chicken
        await Assert.That(PoolTriState.IsSelected(Members("@MeatRaw"), "Meat_Cow", Cat())).IsTrue();
    }

    [Test]
    public async Task IsSelected_NotCoveredByAtRef_False()
    {
        await Assert.That(PoolTriState.IsSelected(Members("@MeatRaw"), "Steel", Cat())).IsFalse();
    }

    // ── CategoryState ─────────────────────────────────────────────────────

    [Test]
    public async Task CategoryState_ExactAtRefPresent_On()
    {
        // When the exact @ref is listed, the whole category is On
        var state = PoolTriState.CategoryState(Members("@MeatRaw"), "MeatRaw", Cat());
        await Assert.That(state).IsEqualTo(TriState.On);
    }

    [Test]
    public async Task CategoryState_AllDefsListedIndividually_On()
    {
        var state = PoolTriState.CategoryState(
            Members("Meat_Cow", "Meat_Chicken"), "MeatRaw", Cat());
        await Assert.That(state).IsEqualTo(TriState.On);
    }

    [Test]
    public async Task CategoryState_SomeDefs_Partial()
    {
        var state = PoolTriState.CategoryState(Members("Meat_Cow"), "MeatRaw", Cat());
        await Assert.That(state).IsEqualTo(TriState.Partial);
    }

    [Test]
    public async Task CategoryState_NoDefs_Off()
    {
        var state = PoolTriState.CategoryState(Members("Steel"), "MeatRaw", Cat());
        await Assert.That(state).IsEqualTo(TriState.Off);
    }

    [Test]
    public async Task CategoryState_EmptyCategory_Off()
    {
        // A category with no counted defs → Off regardless of members
        var catalog = new FakeResourceCatalog()
            .With("Steel", "steel")
            .WithCategory("EmptyCat", "empty cat");
        var state = PoolTriState.CategoryState(Members("Steel"), "EmptyCat", catalog);
        await Assert.That(state).IsEqualTo(TriState.Off);
    }

    [Test]
    public async Task CategoryState_EmptyMembers_Off()
    {
        var state = PoolTriState.CategoryState(Members(), "MeatRaw", Cat());
        await Assert.That(state).IsEqualTo(TriState.Off);
    }

    // ── ToggleDef ─────────────────────────────────────────────────────────

    [Test]
    public async Task ToggleDef_AddWhenNotSelected()
    {
        var result = PoolTriState.ToggleDef(Members("WoodLog"), "Steel", Cat());
        await Assert.That(result.Contains("Steel")).IsTrue();
        await Assert.That(result.Contains("WoodLog")).IsTrue();
    }

    [Test]
    public async Task ToggleDef_RemoveExplicitDef()
    {
        var result = PoolTriState.ToggleDef(Members("Steel", "WoodLog"), "Steel", Cat());
        await Assert.That(result.Contains("Steel")).IsFalse();
        await Assert.That(result.Contains("WoodLog")).IsTrue();
    }

    [Test]
    public async Task ToggleDef_RemoveUnderAtRef_ExpandsRemainder()
    {
        // @MeatRaw covers Meat_Cow + Meat_Chicken; removing Meat_Cow should
        // expand the @ref into just [Meat_Chicken]
        var result = PoolTriState.ToggleDef(Members("@MeatRaw"), "Meat_Cow", Cat());
        await Assert.That(result.Contains("Meat_Chicken")).IsTrue();
        await Assert.That(result.Contains("Meat_Cow")).IsFalse();
        await Assert.That(result.Contains("@MeatRaw")).IsFalse();
    }

    [Test]
    public async Task ToggleDef_ReturnsNewList()
    {
        // Mutation must not affect the original
        var original = Members("Steel");
        var result = PoolTriState.ToggleDef(original, "WoodLog", Cat());
        await Assert.That(original.Count).IsEqualTo(1);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    // ── ToggleCategory ────────────────────────────────────────────────────

    [Test]
    public async Task ToggleCategory_Off_AddsAtRef()
    {
        var result = PoolTriState.ToggleCategory(Members("Steel"), "MeatRaw", Cat());
        await Assert.That(result.Contains("@MeatRaw")).IsTrue();
        await Assert.That(result.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task ToggleCategory_Partial_AddsAtRefSubsumingExistingDefs()
    {
        // Meat_Cow is explicit; toggling MeatRaw On should replace it with @MeatRaw
        var result = PoolTriState.ToggleCategory(Members("Meat_Cow", "Steel"), "MeatRaw", Cat());
        await Assert.That(result.Contains("@MeatRaw")).IsTrue();
        await Assert.That(result.Contains("Meat_Cow")).IsFalse(); // subsumed
        await Assert.That(result.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task ToggleCategory_On_RemovesAtRef()
    {
        var result = PoolTriState.ToggleCategory(Members("@MeatRaw", "Steel"), "MeatRaw", Cat());
        await Assert.That(result.Contains("@MeatRaw")).IsFalse();
        await Assert.That(result.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task ToggleCategory_On_RemovesIndividualDefs()
    {
        // All defs listed individually → On; toggling Off removes all
        var result = PoolTriState.ToggleCategory(
            Members("Meat_Cow", "Meat_Chicken", "Steel"), "MeatRaw", Cat());
        await Assert.That(result.Contains("Meat_Cow")).IsFalse();
        await Assert.That(result.Contains("Meat_Chicken")).IsFalse();
        await Assert.That(result.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task ToggleCategory_On_RemovesBothAtRefAndIndividualDefs()
    {
        // Edge case: @ref present AND some individual defs — all cleared
        var result = PoolTriState.ToggleCategory(
            Members("@MeatRaw", "Meat_Cow", "Steel"), "MeatRaw", Cat());
        await Assert.That(result.Contains("@MeatRaw")).IsFalse();
        await Assert.That(result.Contains("Meat_Cow")).IsFalse();
        await Assert.That(result.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task ToggleCategory_ReturnsNewList()
    {
        var original = Members("@MeatRaw");
        var result = PoolTriState.ToggleCategory(original, "MeatRaw", Cat());
        // original unchanged
        await Assert.That(original.Count).IsEqualTo(1);
        await Assert.That(result.Contains("@MeatRaw")).IsFalse();
    }
}
