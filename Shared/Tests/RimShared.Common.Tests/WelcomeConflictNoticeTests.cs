namespace RimShared.Common.Tests;

using RimShared.Common;

public class WelcomeConflictNoticeTests
{
    [Test]
    public async Task OnlyActiveKnownConflictsAreWarnedAndNewConflictsReopenTheNotice()
    {
        // Observable notice content and dismissal lifetime: unrelated additions
        // must not nag, but an existing save must see a newly added conflict.
        var seen = new List<string>();
        var catalog = new[] { new WelcomeModConflict("example.builder", "Builder conflict"),
            new WelcomeModConflict("example.sharing", "Sharing conflict") };
        var clean = WelcomeConflictNotice.ForActivePackages(catalog, new[] { "example.unrelated" });
        await Assert.That(clean.Format("Heading", "Advice")).IsEqualTo(string.Empty);
        var qb = WelcomeConflictNotice.ForActivePackages(catalog, new[] { "EXAMPLE.BUILDER", "example.unrelated" });
        await Assert.That(qb.Format("Heading", "Advice")).IsEqualTo("Heading\n\nBuilder conflict\n\nAdvice");
        await Assert.That(qb.HasUnseen("save-a", seen)).IsTrue();
        qb.MarkSeen("save-a", seen);
        await Assert.That(qb.HasUnseen("save-a", seen)).IsFalse();
        var both = WelcomeConflictNotice.ForActivePackages(catalog, new[] { "EXAMPLE.SHARING", "example.builder", "example.sharing" });
        await Assert.That(both.Format("Heading", "Advice")).IsEqualTo("Heading\n\nBuilder conflict\n\nSharing conflict\n\nAdvice");
        await Assert.That(both.HasUnseen("save-a", seen)).IsTrue();
        both.MarkSeen("save-a", seen);
        both.MarkSeen("save-a", seen);
        await Assert.That(both.HasUnseen("save-a", seen)).IsFalse();
        await Assert.That(both.HasUnseen("save-b", seen)).IsTrue();
        var emptyCatalog = WelcomeConflictNotice.ForActivePackages(Array.Empty<WelcomeModConflict>(), new[] { "example.builder" });
        await Assert.That(emptyCatalog.Format("Heading", "Advice")).IsEqualTo(string.Empty);
        await Assert.That(emptyCatalog.HasUnseen("save-a", seen)).IsFalse();
    }
}
