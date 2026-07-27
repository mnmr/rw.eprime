using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class EditorBandTests
{
    // Helper: build a tier list from string arrays
    private static List<List<string>> Tiers(params string[][] tierArrays)
    {
        var result = new List<List<string>>();
        foreach (var arr in tierArrays) result.Add(arr.ToList());
        return result;
    }

    // --- MaxDepth ---

    [Test]
    public async Task MaxDepth_ZeroTiers_Returns1()
    {
        await Assert.That(EditorBand.MaxDepth(Tiers())).IsEqualTo(1);
    }

    [Test]
    public async Task MaxDepth_OneTier_Returns2()
    {
        await Assert.That(EditorBand.MaxDepth(Tiers(new[] { "A" }))).IsEqualTo(2);
    }

    [Test]
    public async Task MaxDepth_TwoTiers_Returns3()
    {
        await Assert.That(EditorBand.MaxDepth(Tiers(new[] { "A" }, new[] { "B" }))).IsEqualTo(3);
    }

    [Test]
    public async Task MaxDepth_ThreeTiers_Returns3()
    {
        await Assert.That(EditorBand.MaxDepth(Tiers(new[] { "A" }, new[] { "B" }, new[] { "C" }))).IsEqualTo(3);
    }

    // --- ClampDepth ---

    [Test]
    public async Task ClampDepth_OutOfRange_Returns1()
    {
        // 0 tiers: MaxDepth=1; oob → 1
        await Assert.That(EditorBand.ClampDepth(Tiers(), 0)).IsEqualTo(1);
        await Assert.That(EditorBand.ClampDepth(Tiers(), 5)).IsEqualTo(1);
        // 2 tiers: MaxDepth=3; oob → 1 (default current tier)
        var two = Tiers(new[] { "A" }, new[] { "B" });
        await Assert.That(EditorBand.ClampDepth(two, 0)).IsEqualTo(1);
        await Assert.That(EditorBand.ClampDepth(two, 9)).IsEqualTo(1);
    }

    [Test]
    public async Task ClampDepth_ValidInRange_KeptAsIs()
    {
        var two = Tiers(new[] { "A" }, new[] { "B" });
        // MaxDepth = 3
        await Assert.That(EditorBand.ClampDepth(two, 1)).IsEqualTo(1);
        await Assert.That(EditorBand.ClampDepth(two, 2)).IsEqualTo(2);
        await Assert.That(EditorBand.ClampDepth(two, 3)).IsEqualTo(3);
    }

}
