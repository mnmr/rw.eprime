using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SetupSessionTests
{
    [Test]
    public async Task ConflictingSaveRequiresOneEditorDecisionBeforeBatchedUpdates()
    {
        var session = new SetupSession();
        session.Changed(); // load-time normalization or a remote edit cannot opt this player in
        await Assert.That(session.Dirty).IsFalse();
        await Assert.That(session.CheckOnOpen(true, true, false)).IsTrue();
        session.Choose(false);
        session.Changed();
        await Assert.That(session.Dirty).IsFalse();
        await Assert.That(session.CheckOnOpen(true, true, false)).IsFalse();
        var accepted = new SetupSession();
        accepted.CheckOnOpen(true, true, false);
        accepted.Choose(true);
        await Assert.That(accepted.Dirty).IsTrue();
        accepted.Written();
        await Assert.That(accepted.Dirty).IsFalse();
        accepted.Changed(); accepted.Changed();
        await Assert.That(accepted.Dirty).IsTrue();
        accepted.Written(); accepted.Disable(); accepted.Changed();
        await Assert.That(accepted.Dirty).IsFalse();
    }

    [Test]
    public async Task IdenticalSetupMayUpdateAfterOpeningButDoesNotWriteOnOpen()
    {
        var session = new SetupSession();
        await Assert.That(session.CheckOnOpen(true, true, true)).IsFalse();
        await Assert.That(session.CanUpdate).IsTrue();
        await Assert.That(session.Dirty).IsFalse();
        session.Changed();
        await Assert.That(session.Dirty).IsTrue();
    }
}
