using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Caching;

public class OwnerRevisionGateTests
{
    [Test]
    public async Task RefreshesOnlyForOwnerOrRevisionChanges()
    {
        var gate = new OwnerRevisionGate<object>();
        var firstOwner = new object();
        var secondOwner = new object();

        await Assert.That(gate.ShouldRefresh(firstOwner, 3)).IsTrue();
        await Assert.That(gate.ShouldRefresh(firstOwner, 3)).IsFalse();
        await Assert.That(gate.ShouldRefresh(firstOwner, 4)).IsTrue();
        await Assert.That(gate.ShouldRefresh(secondOwner, 4)).IsTrue();

        gate.Reset();

        await Assert.That(gate.ShouldRefresh(secondOwner, 4)).IsTrue();
    }
}
