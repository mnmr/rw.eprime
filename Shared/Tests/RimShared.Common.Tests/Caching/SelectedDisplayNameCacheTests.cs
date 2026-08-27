using RimShared.Common;

namespace RimShared.Common.Tests;

public class SelectedDisplayNameCacheTests
{
    [Test]
    public async Task ASelectedPoolRenameRefreshesTheDisplayNameAtTheNextPoolRevision()
    {
        var cache = new SelectedDisplayNameCache();
        string poolName = "Meals";
        int resolutions = 0;

        string initial = cache.Get(cache, "$pool:7", 12, 1, true, Resolve);
        poolName = "Travel meals";
        string renamed = cache.Get(cache, "$pool:7", 13, 1, true, Resolve);
        string reused = cache.Get(cache, "$pool:7", 13, 1, true, Resolve);

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

        string initial = cache.Get(cache, canonical, 20, 1, false, Resolve);
        string reused = cache.Get(cache, canonical, 21, 1, false, Resolve);

        await Assert.That(initial).IsEqualTo(label);
        await Assert.That(reused).IsEqualTo(label);
        await Assert.That(resolutions).IsEqualTo(1);

        string Resolve(string _)
        {
            resolutions++;
            return label;
        }
    }

    [Test]
    public async Task EqualRevisionsFromAnotherWorldCannotReuseTheOldName()
    {
        var cache = new SelectedDisplayNameCache();
        var worldA = new object();
        var worldB = new object();

        string first = cache.Get(worldA, "#7", 3, 1, true, static _ => "Meals");
        string second = cache.Get(worldB, "#7", 3, 1, true, static _ => "Medicine");

        await Assert.That(first).IsEqualTo("Meals");
        await Assert.That(second).IsEqualTo("Medicine");
    }
}
