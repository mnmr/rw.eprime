using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class DefinitionOwnedCacheTests
{
    [Test]
    public async Task InvalidateDropsOldDefinitionStateAndAllowsReplacement()
    {
        var owner = new DefinitionOwnedCache<DefinitionState>();
        var oldState = new DefinitionState("old");
        var replacement = new DefinitionState("replacement");

        owner.Publish(oldState);
        await Assert.That(owner.Initialized).IsTrue();
        await Assert.That(owner.Disabled).IsFalse();
        await Assert.That(ReferenceEquals(owner.Value, oldState)).IsTrue();

        owner.Disable();
        await Assert.That(owner.Initialized).IsTrue();
        await Assert.That(owner.Disabled).IsTrue();
        await Assert.That(owner.Value).IsNull();

        owner.Invalidate();
        await Assert.That(owner.Initialized).IsFalse();
        await Assert.That(owner.Disabled).IsFalse();
        await Assert.That(owner.Value).IsNull();

        owner.Publish(replacement);
        await Assert.That(ReferenceEquals(owner.Value, replacement)).IsTrue();
        await Assert.That(ReferenceEquals(owner.Value, oldState)).IsFalse();
    }

    private sealed record DefinitionState(string Name);
}
