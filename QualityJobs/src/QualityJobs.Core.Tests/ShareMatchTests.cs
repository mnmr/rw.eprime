namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ShareMatchTests
{
    [Test]
    public async Task IdenticalStyleKeysMatch()
    {
        var a = new StyleKey("preceptA", "styleA", false, 2);
        var b = new StyleKey("preceptA", "styleA", false, 2);
        await Assert.That(ShareMatch.StyleCompatible(a, b)).IsTrue();
    }

    [Test]
    public async Task DifferentStyleDoesNotMatch()
    {
        var a = new StyleKey(null, "styleA", false, null);
        var b = new StyleKey(null, "styleB", false, null);
        await Assert.That(ShareMatch.StyleCompatible(a, b)).IsFalse();
    }

    [Test]
    public async Task MissingSnapshotMatchesOnlyDefaultGlobalStyleBills()
    {
        // A UFT pooled while already unbound has no style snapshot (spec §8):
        // conservative rule — only bills with default global style match.
        var defaultBill = new StyleKey(null, null, true, null);
        var styledBill = new StyleKey(null, "styleA", false, null);
        await Assert.That(ShareMatch.StyleCompatible(StyleKey.Unknown, defaultBill)).IsTrue();
        await Assert.That(ShareMatch.StyleCompatible(StyleKey.Unknown, styledBill)).IsFalse();
    }

    [Test]
    public async Task UnknownSnapshotRejectsGraphicOverrideBills()
    {
        // A bill with a graphic override is never a "default-style" bill,
        // so Unknown entry snapshots must not match it.
        await Assert.That(ShareMatch.StyleCompatible(StyleKey.Unknown, new StyleKey(null, null, true, 3))).IsFalse();
    }

    [Test]
    public async Task UnknownBillNeverMatches()
    {
        // The bill argument must always be a Known key built from a live bill.
        // StyleKey.Unknown (Known=false) is only valid as the entry snapshot.
        await Assert.That(ShareMatch.StyleCompatible(new StyleKey(null, null, false, null), StyleKey.Unknown)).IsFalse();
        await Assert.That(ShareMatch.StyleCompatible(StyleKey.Unknown, StyleKey.Unknown)).IsFalse();
    }
}
