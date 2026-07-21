using WorkRoles.Core;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class MoreThanCapableSignalTests
{
    [Test]
    public async Task CreatesAnExactActiveAwfulWorkAversionSignal()
    {
        Signal signal = MoreThanCapableSignal.Create(
            "Cooking",
            "hated cooking",
            "Causes a severe mood penalty.");

        bool classified = SignalClassificationCatalog.Default.TryClassify(
            signal, out SignalBucket bucket);
        await Assert.That(signal.Type).IsEqualTo(SignalType.Active);
        await Assert.That(signal.Source.Kind).IsEqualTo(SignalSourceKind.WorkAversion);
        await Assert.That(signal.Source.PackageId)
            .IsEqualTo(MoreThanCapableSignal.PackageId);
        await Assert.That(signal.WorkTypeDefName).IsEqualTo("Cooking");
        await Assert.That(signal.SkillDefName == null).IsTrue();
        await Assert.That(signal.Effects.Single().Kind)
            .IsEqualTo(SignalEffectKind.WorkPreference);
        await Assert.That(classified).IsTrue();
        await Assert.That(bucket).IsEqualTo(SignalBucket.Awful);
    }

    [Test]
    public async Task RejectsMissingWorkTypeIdentity()
    {
        await Assert.That(() => MoreThanCapableSignal.Create(
                " ", "hated work", "Severe mood penalty."))
            .Throws<ArgumentException>();
    }
}
