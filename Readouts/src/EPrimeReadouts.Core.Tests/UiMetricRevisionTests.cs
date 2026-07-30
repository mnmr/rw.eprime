using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class UiMetricRevisionTests
{
    [Test]
    public async Task RevisionAdvancesOnlyWhenObservedTextMetricsChange()
    {
        var revision = new UiMetricRevision();

        revision.Observe(1f, disableTinyText: false, language: "English");
        revision.Observe(1f, disableTinyText: false, language: "English");
        int unchanged = revision.Current;
        revision.Observe(1.25f, disableTinyText: false, language: "English");
        int rescaled = revision.Current;
        revision.Observe(1.25f, disableTinyText: true, language: "English");

        await Assert.That(unchanged).IsEqualTo(0);
        await Assert.That(rescaled).IsEqualTo(1);
        await Assert.That(revision.Current).IsEqualTo(2);
    }

    [Test]
    public async Task LanguageChangeAdvancesPresentationRevision()
    {
        var revision = new UiMetricRevision();

        revision.Observe(1f, disableTinyText: false, language: "English");
        revision.Observe(1f, disableTinyText: false, language: "English");
        int unchanged = revision.Current;
        revision.Observe(1f, disableTinyText: false, language: "Danish");

        await Assert.That(unchanged).IsEqualTo(0);
        await Assert.That(revision.Current).IsEqualTo(1);
    }
}
