using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PendingUpdateTests
{
    [Test]
    public async Task AutomaticUpdateReplacesEarlierAutomaticUpdate()
    {
        var pending = new PendingUpdate<int>();

        pending.QueueAutomatic(1);
        pending.QueueAutomatic(2);

        await Assert.That(pending.TryConsume(out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task UserUpdateReplacesAutomaticUpdate()
    {
        var pending = new PendingUpdate<int>();

        pending.QueueAutomatic(1);
        pending.QueueUser(2);

        await Assert.That(pending.TryConsume(out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task AutomaticUpdateCannotReplacePendingUserUpdate()
    {
        var pending = new PendingUpdate<int>();

        pending.QueueUser(1);
        pending.QueueAutomatic(2);

        await Assert.That(pending.TryConsume(out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(1);
    }

    [Test]
    public async Task LatestUserUpdateWins()
    {
        var pending = new PendingUpdate<int>();

        pending.QueueUser(1);
        pending.QueueUser(2);

        await Assert.That(pending.TryConsume(out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumeIsOneShotAndAllowsLaterAutomaticUpdate()
    {
        var pending = new PendingUpdate<int>();
        pending.QueueUser(1);

        await Assert.That(pending.TryConsume(out int first)).IsTrue();
        await Assert.That(first).IsEqualTo(1);
        await Assert.That(pending.TryConsume(out int empty)).IsFalse();
        await Assert.That(empty).IsEqualTo(0);

        pending.QueueAutomatic(2);
        await Assert.That(pending.TryConsume(out int second)).IsTrue();
        await Assert.That(second).IsEqualTo(2);
    }

    [Test]
    public async Task PendingUserCanBeReadWithoutConsumingIt()
    {
        var pending = new PendingUpdate<int>();
        pending.QueueAutomatic(1);

        await Assert.That(pending.TryGetUser(out int automatic)).IsFalse();
        await Assert.That(automatic).IsEqualTo(0);

        pending.QueueUser(2);
        await Assert.That(pending.TryGetUser(out int user)).IsTrue();
        await Assert.That(user).IsEqualTo(2);
        await Assert.That(pending.TryConsume(out int consumed)).IsTrue();
        await Assert.That(consumed).IsEqualTo(2);
    }

    [Test]
    public async Task ClearDropsPendingUpdateAndItsPriority()
    {
        var pending = new PendingUpdate<int>();
        pending.QueueUser(1);

        pending.Clear();

        await Assert.That(pending.TryConsume(out int empty)).IsFalse();
        await Assert.That(empty).IsEqualTo(0);
        pending.QueueAutomatic(2);
        await Assert.That(pending.TryConsume(out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }
}
