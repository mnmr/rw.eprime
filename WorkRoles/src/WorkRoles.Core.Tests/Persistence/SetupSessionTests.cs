namespace WorkRoles.Core.Tests.Persistence;

public class SetupSessionTests
{
    // This permission boundary protects the profile independently of file IO
    // and GUI timing; the in-game scenario also verifies the resulting files.
    [Test]
    public async Task OpeningAnOlderSaveCannotOverwriteTheDefaultWithoutAnExplicitChoice()
    {
        var session = new SetupSession();
        session.Changed();
        await Assert.That(session.Dirty).IsFalse();
        await Assert.That(session.CheckOnOpen(true, true, false)).IsTrue();
        session.Changed();
        await Assert.That(session.Dirty).IsFalse();
        session.Choose(false);
        session.Changed();
        await Assert.That(session.Dirty).IsFalse();
        session.Choose(true);
        await Assert.That(session.Dirty).IsTrue();
        session.Written();
        await Assert.That(session.Dirty).IsFalse();
        session.Changed();
        await Assert.That(session.Dirty).IsTrue();
        session.Disable();
        session.Changed();
        await Assert.That(session.Dirty).IsFalse();
    }
}
