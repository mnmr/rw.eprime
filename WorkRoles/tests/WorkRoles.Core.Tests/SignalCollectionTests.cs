using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

/// SignalCollection.Collect: provider fault isolation and output hygiene.
public class SignalCollectionTests
{
    [Test]
    public async Task CollectionContinuesAfterOneProviderFailsAndSortsOutput()
    {
        var errors = new List<(int index, Exception error)>();
        var providers = new List<Func<string, IEnumerable<Signal>>>
        {
            _ => new[] { Make("z", "Shooting") },
            _ => throw new InvalidOperationException("broken optional integration"),
            _ => new[] { Make("a", "Melee") },
        };

        var signals = SignalCollection.Collect("pawn", providers,
            (index, error) => errors.Add((index, error)));

        await Assert.That(string.Join(",", signals.Select(x => x.Source.DefName))).IsEqualTo("a,z");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].index).IsEqualTo(1);
        await Assert.That(errors[0].error.Message).IsEqualTo("broken optional integration");
    }

    [Test]
    public async Task CollectionIgnoresNullSignalsAndNullProviderOutput()
    {
        var providers = new List<Func<int, IEnumerable<Signal>>>
        {
            _ => null,
            _ => new Signal[] { null, Make("valid", null) },
        };

        var signals = SignalCollection.Collect(1, providers);

        await Assert.That(signals.Count).IsEqualTo(1);
        await Assert.That(signals[0].Source.DefName).IsEqualTo("valid");
    }

    private static Signal Make(string defName, string skill) => new(
        SignalType.Active,
        new SignalSource(SignalSourceKind.Gene, defName, "test.package"),
        skill,
        new[]
        {
            new SignalEffect(SignalEffectKind.Capability, SignalOperation.Descriptive,
                null, SignalValueUnit.None, "test"),
        },
        new SignalUi(defName, null, null, null, null, "Test"));
}
