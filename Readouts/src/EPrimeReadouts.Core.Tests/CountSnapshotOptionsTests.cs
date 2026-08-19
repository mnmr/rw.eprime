using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// These are internal performance invariants: collecting an unused source can
/// produce the same published count, so output-only assertions cannot prove
/// that the expensive map pass was skipped.
public class CountSnapshotOptionsTests
{
    [Test]
    public async Task StorageOnlySnapshotsDoNotRequestScatteredThings()
    {
        var options = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: true,
            plannedWork: default);

        await Assert.That(options.IncludeScattered).IsFalse();
    }

    [Test]
    public async Task AllMapSnapshotsRequestScatteredThings()
    {
        var options = new CountSnapshotOptions(
            storageOnly: false,
            hideForbidden: true,
            plannedWork: default);

        await Assert.That(options.IncludeScattered).IsTrue();
    }

    [Test]
    public async Task ForbiddenStateIsInspectedOnlyWhenTheFilterConsumesIt()
    {
        var ignored = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: false,
            plannedWork: default);
        var consumed = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: true,
            plannedWork: default);

        await Assert.That(ignored.InspectForbidden).IsFalse();
        await Assert.That(consumed.InspectForbidden).IsTrue();
    }

    [Test]
    public async Task EveryCollectionDependencyParticipatesInEquality()
    {
        var baseline = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: true,
            plannedWork: default);
        var allMap = new CountSnapshotOptions(
            storageOnly: false,
            hideForbidden: true,
            plannedWork: default);
        var includeForbidden = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: false,
            plannedWork: default);
        var planned = new CountSnapshotOptions(
            storageOnly: true,
            hideForbidden: true,
            plannedWork: new PlannedWorkOptions(true, false, false));

        await Assert.That(baseline.Equals(allMap)).IsFalse();
        await Assert.That(baseline.Equals(includeForbidden)).IsFalse();
        await Assert.That(baseline.Equals(planned)).IsFalse();
        await Assert.That(baseline.Equals(new CountSnapshotOptions(
            true, true, default))).IsTrue();
    }
}
