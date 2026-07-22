using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class OwnerScopedTransferTableTests
{
    [Test]
    public async Task PropagationUsesKeyIdentityAndConsumptionIsOneShot()
    {
        var owner = new Owner();
        var source = new ValueEqualKey(1);
        var equalButDifferentSource = new ValueEqualKey(1);
        var clone = new ValueEqualKey(2);
        var transfers = new OwnerScopedTransferTable<ValueEqualKey, Owner, int>();

        transfers.Set(source, owner, 73);

        await Assert.That(transfers.TryGet(
            equalButDifferentSource, owner, out _)).IsFalse();
        await Assert.That(transfers.Propagate(source, clone, owner)).IsTrue();
        await Assert.That(transfers.TryConsume(clone, owner, out int roleId)).IsTrue();
        await Assert.That(roleId).IsEqualTo(73);
        await Assert.That(transfers.TryConsume(clone, owner, out _)).IsFalse();
        await Assert.That(transfers.TryGet(source, owner, out int sourceRoleId)).IsTrue();
        await Assert.That(sourceRoleId).IsEqualTo(73);
    }

    [Test]
    public async Task OwnerMismatchRejectsAndDiscardsOldWorldState()
    {
        var oldOwner = new Owner();
        var currentOwner = new Owner();
        var key = new ValueEqualKey(1);
        var transfers = new OwnerScopedTransferTable<ValueEqualKey, Owner, int>();
        transfers.Set(key, oldOwner, 19);

        await Assert.That(transfers.TryGet(key, currentOwner, out _)).IsFalse();
        await Assert.That(transfers.TryGet(key, oldOwner, out _)).IsFalse();
    }

    [Test]
    public async Task ClearDiscardsEveryPendingTransfer()
    {
        var owner = new Owner();
        var first = new ValueEqualKey(1);
        var second = new ValueEqualKey(2);
        var transfers = new OwnerScopedTransferTable<ValueEqualKey, Owner, int>();
        transfers.Set(first, owner, 19);
        transfers.Set(second, owner, 73);

        transfers.Clear();

        await Assert.That(transfers.TryGet(first, owner, out _)).IsFalse();
        await Assert.That(transfers.TryGet(second, owner, out _)).IsFalse();
    }

    private sealed class Owner { }

    private sealed class ValueEqualKey
    {
        private readonly int value;

        internal ValueEqualKey(int value) => this.value = value;

        public override bool Equals(object obj) =>
            obj is ValueEqualKey other && other.value == value;

        public override int GetHashCode() => value;
    }
}
