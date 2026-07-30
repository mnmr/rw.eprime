using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SelectedDisplayNameCacheTests
{
    [Test]
    public async Task ASelectedPoolRenameRefreshesTheDisplayNameAtTheNextPoolRevision()
    {
        var cache = new SelectedDisplayNameCache();
        string poolName = "Meals";
        int resolutions = 0;

        string initial = cache.Get("$pool:7", 12, true, Resolve);
        poolName = "Travel meals";
        string renamed = cache.Get("$pool:7", 13, true, Resolve);
        string reused = cache.Get("$pool:7", 13, true, Resolve);

        await Assert.That(initial).IsEqualTo("Meals");
        await Assert.That(renamed).IsEqualTo("Travel meals");
        await Assert.That(reused).IsEqualTo("Travel meals");
        await Assert.That(resolutions).IsEqualTo(2);

        string Resolve(string _)
        {
            resolutions++;
            return poolName;
        }
    }

    [Test]
    [Arguments("Steel", "steel")]
    [Arguments("@MeatRaw", "Raw meat")]
    public async Task StaticSelectionDisplayNamesStayCachedAcrossPoolRevisions(
        string canonical,
        string label)
    {
        var cache = new SelectedDisplayNameCache();
        int resolutions = 0;

        string initial = cache.Get(canonical, 20, false, Resolve);
        string reused = cache.Get(canonical, 21, false, Resolve);

        await Assert.That(initial).IsEqualTo(label);
        await Assert.That(reused).IsEqualTo(label);
        await Assert.That(resolutions).IsEqualTo(1);

        string Resolve(string _)
        {
            resolutions++;
            return label;
        }
    }
}
