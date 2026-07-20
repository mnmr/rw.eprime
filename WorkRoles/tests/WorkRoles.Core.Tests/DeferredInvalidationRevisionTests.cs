using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class DeferredInvalidationRevisionTests
{
    [Test]
    public async Task RequestMarksPendingWithoutAdvancingRevision()
    {
        var revision = new DeferredInvalidationRevision();

        revision.Request();

        await Assert.That(revision.Current).IsEqualTo(0);
    }

    [Test]
    public async Task CompleteWithoutRequestIsANoOp()
    {
        var revision = new DeferredInvalidationRevision();

        await Assert.That(revision.Complete()).IsFalse();
        await Assert.That(revision.Current).IsEqualTo(0);
    }

    [Test]
    public async Task PendingCompletionAdvancesExactlyOnce()
    {
        var revision = new DeferredInvalidationRevision();
        revision.Request();

        await Assert.That(revision.Complete()).IsTrue();
        await Assert.That(revision.Current).IsEqualTo(1);
        await Assert.That(revision.Complete()).IsFalse();
        await Assert.That(revision.Current).IsEqualTo(1);
    }

    [Test]
    public async Task RepeatedRequestsCoalesceIntoOneCompletion()
    {
        var revision = new DeferredInvalidationRevision();

        revision.Request();
        revision.Request();
        revision.Request();

        await Assert.That(revision.Complete()).IsTrue();
        await Assert.That(revision.Current).IsEqualTo(1);
        await Assert.That(revision.Complete()).IsFalse();
    }

    [Test]
    public async Task SeparateCompletedRequestsAdvanceMonotonically()
    {
        var revision = new DeferredInvalidationRevision();

        for (int expected = 1; expected <= 3; expected++)
        {
            revision.Request();
            await Assert.That(revision.Complete()).IsTrue();
            await Assert.That(revision.Current).IsEqualTo(expected);
        }
    }
}
