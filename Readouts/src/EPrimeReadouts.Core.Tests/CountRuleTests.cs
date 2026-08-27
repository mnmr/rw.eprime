using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// CountRule value semantics: per-option resolution against the player's
/// global options, the never-stored fully-inherit state, the persistence
/// codec, and the union-of-needs the snapshot pass derives from stored rules.
public class CountRuleTests
{
    [Test]
    public async Task InheritFollowsTheGlobalOptionEitherWay()
    {
        var rule = new CountRule(BasisOverride.Inherit, BasisOverride.Inherit);
        await Assert.That(rule.ResolveStorageOnly(true)).IsTrue();
        await Assert.That(rule.ResolveStorageOnly(false)).IsFalse();
        await Assert.That(rule.ResolveHideForbidden(true)).IsTrue();
        await Assert.That(rule.ResolveHideForbidden(false)).IsFalse();
    }

    [Test]
    public async Task ForceOnAndForceOffIgnoreTheGlobalOption()
    {
        var rule = new CountRule(BasisOverride.ForceOn, BasisOverride.ForceOff);
        await Assert.That(rule.ResolveStorageOnly(false)).IsTrue();
        await Assert.That(rule.ResolveStorageOnly(true)).IsTrue();
        await Assert.That(rule.ResolveHideForbidden(true)).IsFalse();
        await Assert.That(rule.ResolveHideForbidden(false)).IsFalse();
    }

    [Test]
    public async Task IsInheritOnlyWhenBothOptionsInherit()
    {
        await Assert.That(new CountRule(
            BasisOverride.Inherit, BasisOverride.Inherit).IsInherit).IsTrue();
        await Assert.That(new CountRule(
            BasisOverride.ForceOn, BasisOverride.Inherit).IsInherit).IsFalse();
        await Assert.That(new CountRule(
            BasisOverride.Inherit, BasisOverride.ForceOff).IsInherit).IsFalse();
    }

    [Test]
    public async Task CodecRoundTripsEveryCombination()
    {
        var states = new[]
        {
            BasisOverride.Inherit, BasisOverride.ForceOn, BasisOverride.ForceOff,
        };
        foreach (var storageOnly in states)
            foreach (var hideForbidden in states)
            {
                var rule = new CountRule(storageOnly, hideForbidden);
                string encoded = CountRuleCodec.Encode(rule);
                await Assert.That(CountRuleCodec.TryDecode(encoded, out var decoded))
                    .IsTrue();
                await Assert.That(decoded).IsEqualTo(rule);
            }
    }

    [Test]
    public async Task CodecRejectsMalformedInput()
    {
        await Assert.That(CountRuleCodec.TryDecode(null, out _)).IsFalse();
        await Assert.That(CountRuleCodec.TryDecode("", out _)).IsFalse();
        await Assert.That(CountRuleCodec.TryDecode("x", out _)).IsFalse();
        await Assert.That(CountRuleCodec.TryDecode("9", out _)).IsFalse();
        await Assert.That(CountRuleCodec.TryDecode("-1", out _)).IsFalse();
    }

    [Test]
    public async Task ScatteredPassNeededOnlyWhenARuleForcesStorageOnlyOff()
    {
        var rules = new Dictionary<string, CountRule>
        {
            ["Steel"] = new CountRule(BasisOverride.ForceOn, BasisOverride.Inherit),
            ["#3"] = new CountRule(BasisOverride.Inherit, BasisOverride.ForceOn),
        };
        await Assert.That(CountRuleUnion.AnyForcesScattered(rules)).IsFalse();

        rules["Cloth"] = new CountRule(BasisOverride.ForceOff, BasisOverride.Inherit);
        await Assert.That(CountRuleUnion.AnyForcesScattered(rules)).IsTrue();
    }

    [Test]
    public async Task ForbiddenInspectionNeededOnlyWhenARuleForcesHideForbiddenOn()
    {
        var rules = new Dictionary<string, CountRule>
        {
            ["Steel"] = new CountRule(BasisOverride.ForceOff, BasisOverride.Inherit),
            ["#3"] = new CountRule(BasisOverride.Inherit, BasisOverride.ForceOff),
        };
        await Assert.That(CountRuleUnion.AnyForcesForbidden(rules)).IsFalse();

        rules["Cloth"] = new CountRule(BasisOverride.Inherit, BasisOverride.ForceOn);
        await Assert.That(CountRuleUnion.AnyForcesForbidden(rules)).IsTrue();
    }
}
