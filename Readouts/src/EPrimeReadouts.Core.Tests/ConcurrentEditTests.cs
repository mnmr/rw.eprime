using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// In multiplayer a synced edit runs a few frames after the click, so a
/// second click is issued while the first is still in flight. Every edit
/// therefore carries its intent (select this def, move this token, set this
/// one option) and is applied against the model as it is when it lands.
/// Each scenario issues two edits the UI would build from the same starting
/// model, applies them in order, and requires both to survive.
public class ConcurrentEditTests
{
    private static ReadoutModel ModelWithGroup(params string[][] tiers)
    {
        var model = new ReadoutModel();
        var group = model.CreateGroup(1, "G");
        foreach (var tier in tiers) group.Tiers.Add(tier.ToList());
        return model;
    }

    private static string Layout(ReadoutModel model) =>
        string.Join("|", model.GroupById(1)!.Tiers.Select(t => string.Join(",", t)));

    [Test]
    public async Task TwoQuickPoolMemberTicksBothSurvive()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals").Members.Add("WoodLog");
        var catalog = StaticResources.Catalog();

        await Assert.That(model.SetPoolMemberSelected(1, "Steel", true, catalog)).IsTrue();
        await Assert.That(model.SetPoolMemberSelected(1, "Plasteel", true, catalog)).IsTrue();
        // A repeated intent is a no-op, never a toggle back.
        await Assert.That(model.SetPoolMemberSelected(1, "Steel", true, catalog)).IsFalse();

        await Assert.That(string.Join(",", model.PoolById(1)!.Members))
            .IsEqualTo("WoodLog,Steel,Plasteel");
    }

    [Test]
    public async Task CategoryScopeIntentAppliesToTheCurrentMembers()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meat");
        var catalog = StaticResources.Catalog();

        // Both issued while the pool was empty: tick Steel, then tick the
        // whole raw-meat category.
        model.SetPoolMemberSelected(1, "Steel", true, catalog);
        await Assert.That(model.SetPoolCategoryScopeSelected(1, "MeatRaw",
            new[] { "Meat_Cow", "Meat_Chicken" }, true, catalog)).IsTrue();
        await Assert.That(model.SetPoolCategoryScopeSelected(1, "MeatRaw",
            new[] { "Meat_Cow", "Meat_Chicken" }, true, catalog)).IsFalse();

        await Assert.That(string.Join(",", model.PoolById(1)!.Members))
            .IsEqualTo("Steel,@MeatRaw");
    }

    [Test]
    public async Task FlippingBothCountRuleOptionsQuicklyKeepsBoth()
    {
        var model = new ReadoutModel();

        await Assert.That(model.SetCountRuleStorageOnly("Steel", BasisOverride.ForceOn)).IsTrue();
        await Assert.That(model.SetCountRuleHideForbidden("Steel", BasisOverride.ForceOn)).IsTrue();

        await Assert.That(model.CountRules["Steel"]).IsEqualTo(
            new CountRule(BasisOverride.ForceOn, BasisOverride.ForceOn));

        // Returning both options to inherit removes the rule entirely.
        model.SetCountRuleStorageOnly("Steel", BasisOverride.Inherit);
        model.SetCountRuleHideForbidden("Steel", BasisOverride.Inherit);
        await Assert.That(model.CountRules.ContainsKey("Steel")).IsFalse();
    }

    [Test]
    public async Task TwoQuickAppendsBothLandAndAFullTierOverflowsToTheNext()
    {
        var model = ModelWithGroup(new[] { "A", "B", "C", "D", "E", "F", "G" });

        await Assert.That(model.AddGroupSlot(1, "Steel", -1, -1)).IsTrue();
        await Assert.That(model.AddGroupSlot(1, "Gold", -1, -1)).IsTrue();
        // Already present (any show-when-zero form): refused.
        await Assert.That(model.AddGroupSlot(1, "~Gold", -1, -1)).IsFalse();

        await Assert.That(Layout(model)).IsEqualTo("A,B,C,D,E,F,G,Steel|Gold");
    }

    [Test]
    public async Task AMoveFindsItsTokenAfterAnotherEditShiftedTheSlots()
    {
        // The player drags C (slot 2) to the front while another edit, issued
        // earlier, inserts X at the front. Indexes from drag start now point
        // at B; the move must still move C.
        var model = ModelWithGroup(new[] { "A", "B", "C" });

        model.AddGroupSlot(1, "X", 0, 0);
        await Assert.That(model.MoveGroupSlot(1, "C", 0, 0)).IsTrue();

        await Assert.That(Layout(model)).IsEqualTo("C,X,A,B");
        // A token another edit already removed is a no-op.
        model.RemoveGroupSlot(1, "A");
        await Assert.That(model.MoveGroupSlot(1, "A", 0, 0)).IsFalse();
        await Assert.That(model.RemoveGroupSlot(1, "A")).IsFalse();
    }

    [Test]
    public async Task ShowWhenZeroFollowsTheTokenWhereverItNowIs()
    {
        var model = ModelWithGroup(new[] { "A", "Steel" });

        model.MoveGroupSlot(1, "Steel", 0, 0);
        await Assert.That(model.SetGroupSlotShowWhenZero(1, "Steel", false)).IsTrue();
        await Assert.That(model.SetGroupSlotShowWhenZero(1, "Steel", false)).IsFalse();

        await Assert.That(Layout(model)).IsEqualTo("~Steel,A");
    }
}
