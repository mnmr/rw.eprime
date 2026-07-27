using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SlotTokenTests
{
    // Token shapes: "Steel", "~Steel", "@MeatRaw", "~@MeatRaw"

    [Test]
    public async Task ShowWhenZero_PlainDef_True()
    {
        await Assert.That(SlotToken.ShowWhenZero("Steel")).IsTrue();
    }

    [Test]
    public async Task ShowWhenZero_HideFlaggedDef_False()
    {
        await Assert.That(SlotToken.ShowWhenZero("~Steel")).IsFalse();
    }

    [Test]
    public async Task ShowWhenZero_PlainPool_True()
    {
        await Assert.That(SlotToken.ShowWhenZero("@MeatRaw")).IsTrue();
    }

    [Test]
    public async Task ShowWhenZero_HideFlaggedPool_False()
    {
        await Assert.That(SlotToken.ShowWhenZero("~@MeatRaw")).IsFalse();
    }

    [Test]
    public async Task Canonical_PlainDef_Unchanged()
    {
        await Assert.That(SlotToken.Canonical("Steel")).IsEqualTo("Steel");
    }

    [Test]
    public async Task Canonical_HideFlaggedDef_StripsTilde()
    {
        await Assert.That(SlotToken.Canonical("~Steel")).IsEqualTo("Steel");
    }

    [Test]
    public async Task Canonical_PlainPool_Unchanged()
    {
        await Assert.That(SlotToken.Canonical("@MeatRaw")).IsEqualTo("@MeatRaw");
    }

    [Test]
    public async Task Canonical_HideFlaggedPool_StripsTilde()
    {
        await Assert.That(SlotToken.Canonical("~@MeatRaw")).IsEqualTo("@MeatRaw");
    }

    [Test]
    public async Task IsPool_PlainDef_False()
    {
        await Assert.That(SlotToken.IsPool("Steel")).IsFalse();
    }

    [Test]
    public async Task IsPool_HideFlaggedDef_False()
    {
        await Assert.That(SlotToken.IsPool("~Steel")).IsFalse();
    }

    [Test]
    public async Task IsPool_PlainPool_True()
    {
        await Assert.That(SlotToken.IsPool("@MeatRaw")).IsTrue();
    }

    [Test]
    public async Task IsPool_HideFlaggedPool_True()
    {
        await Assert.That(SlotToken.IsPool("~@MeatRaw")).IsTrue();
    }

    [Test]
    public async Task MemberName_PlainDef_ReturnsDefName()
    {
        await Assert.That(SlotToken.MemberName("Steel")).IsEqualTo("Steel");
    }

    [Test]
    public async Task MemberName_HideFlaggedDef_ReturnsDefName()
    {
        await Assert.That(SlotToken.MemberName("~Steel")).IsEqualTo("Steel");
    }

    [Test]
    public async Task MemberName_PlainPool_ReturnsCategoryDefName()
    {
        await Assert.That(SlotToken.MemberName("@MeatRaw")).IsEqualTo("MeatRaw");
    }

    [Test]
    public async Task MemberName_HideFlaggedPool_ReturnsCategoryDefName()
    {
        await Assert.That(SlotToken.MemberName("~@MeatRaw")).IsEqualTo("MeatRaw");
    }

    [Test]
    public async Task WithShowWhenZero_ShowTrue_RemovesTilde()
    {
        await Assert.That(SlotToken.WithShowWhenZero("~Steel", true)).IsEqualTo("Steel");
    }

    [Test]
    public async Task WithShowWhenZero_ShowFalse_AddsTilde()
    {
        await Assert.That(SlotToken.WithShowWhenZero("Steel", false)).IsEqualTo("~Steel");
    }

    [Test]
    public async Task WithShowWhenZero_AlreadyHidden_ShowFalse_Unchanged()
    {
        await Assert.That(SlotToken.WithShowWhenZero("~Steel", false)).IsEqualTo("~Steel");
    }

    [Test]
    public async Task WithShowWhenZero_AlreadyShown_ShowTrue_Unchanged()
    {
        await Assert.That(SlotToken.WithShowWhenZero("Steel", true)).IsEqualTo("Steel");
    }

    [Test]
    public async Task WithShowWhenZero_Pool_HideFlag()
    {
        await Assert.That(SlotToken.WithShowWhenZero("@MeatRaw", false)).IsEqualTo("~@MeatRaw");
    }

    [Test]
    public async Task WithShowWhenZero_HideFlaggedPool_ShowTrue()
    {
        await Assert.That(SlotToken.WithShowWhenZero("~@MeatRaw", true)).IsEqualTo("@MeatRaw");
    }
}
