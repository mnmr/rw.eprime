using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class IdentitySelectionPreserverTests
{
    [Test]
    public async Task RestoreCarriesSelectionOnlyAcrossTheSameReferenceIdentity()
    {
        var firstPawn = new object();
        var equalButDifferentPawn = new object();
        var prior = new[]
        {
            new Item(firstPawn, included: false),
            new Item(equalButDifferentPawn, included: false),
        };
        var refreshed = new[]
        {
            new Item(firstPawn, included: true),
            new Item(new object(), included: true),
        };

        int included = IdentitySelectionPreserver.Restore(
            prior, refreshed,
            item => item.Identity,
            item => item.Included,
            (item, value) => item.Included = value,
            ReferenceIdentityComparer<object>.Instance);

        await Assert.That(refreshed[0].Included).IsFalse();
        await Assert.That(refreshed[1].Included).IsTrue();
        await Assert.That(included).IsEqualTo(1);
    }

    [Test]
    public async Task CapturedSelectionSurvivesInPlaceMutationBeforeRestore()
    {
        var pawn = new object();
        var entries = new[] { new Item(pawn, included: false) };
        Dictionary<object, bool> captured = IdentitySelectionPreserver.Capture(
            entries, item => item.Identity, item => item.Included,
            ReferenceIdentityComparer<object>.Instance);

        entries[0].Included = true;
        int included = IdentitySelectionPreserver.Restore(
            captured, entries,
            item => item.Identity,
            item => item.Included,
            (item, value) => item.Included = value);

        await Assert.That(entries[0].Included).IsFalse();
        await Assert.That(included).IsEqualTo(0);
    }

    private sealed class Item
    {
        internal Item(object identity, bool included)
        {
            Identity = identity;
            Included = included;
        }

        internal object Identity { get; }
        internal bool Included { get; set; }
    }
}
