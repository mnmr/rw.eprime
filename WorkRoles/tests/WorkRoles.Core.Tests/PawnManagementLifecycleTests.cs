using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PawnManagementLifecycleTests
{
    [Test]
    public async Task UnmanageNotifiesVanillaOnceAfterMirroringFallback()
    {
        var calls = new List<string>();

        PawnManagementLifecycle.Unmanage(
            hasVanillaWorkSettings: true,
            mirrorFallback: () => calls.Add("mirror"),
            removeManagedState: () => calls.Add("remove"),
            notifyVanilla: () => calls.Add("notify"),
            invalidateUi: () => calls.Add("ui"));

        await Assert.That(string.Join(",", calls)).IsEqualTo("mirror,remove,notify,ui");
        await Assert.That(calls.Count(x => x == "notify")).IsEqualTo(1);
    }

    [Test]
    public async Task BatchedUnmanageRequestsProduceOneUiInvalidation()
    {
        var calls = new List<string>();

        using (var batch = new UiInvalidationBatch(() => calls.Add("ui")))
        {
            for (int i = 0; i < 3; i++)
            {
                PawnManagementLifecycle.Unmanage(
                    hasVanillaWorkSettings: true,
                    mirrorFallback: () => calls.Add("mirror"),
                    removeManagedState: () => calls.Add("remove"),
                    notifyVanilla: () => calls.Add("notify"),
                    invalidateUi: batch.Request);
            }
        }

        await Assert.That(calls.Count(x => x == "mirror")).IsEqualTo(3);
        await Assert.That(calls.Count(x => x == "remove")).IsEqualTo(3);
        await Assert.That(calls.Count(x => x == "notify")).IsEqualTo(3);
        await Assert.That(calls.Count(x => x == "ui")).IsEqualTo(1);
        await Assert.That(calls[^1]).IsEqualTo("ui");
    }

    [Test]
    public async Task NoVanillaWorkSettingsStillRemovesManagedStateSafely()
    {
        bool managed = true;
        int mirrorAttempts = 0;
        int notifyAttempts = 0;
        int uiInvalidations = 0;

        PawnManagementLifecycle.Unmanage(
            hasVanillaWorkSettings: false,
            mirrorFallback: () => mirrorAttempts++,
            removeManagedState: () => managed = false,
            notifyVanilla: () => notifyAttempts++,
            invalidateUi: () => uiInvalidations++);

        await Assert.That(managed).IsFalse();
        await Assert.That(mirrorAttempts).IsEqualTo(0);
        await Assert.That(notifyAttempts).IsEqualTo(0);
        await Assert.That(uiInvalidations).IsEqualTo(1);
    }
}
