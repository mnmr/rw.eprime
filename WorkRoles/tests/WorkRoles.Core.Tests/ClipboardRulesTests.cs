using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ClipboardRulesTests
{
    [Test]
    public async Task ForeignOwnerClipboardDataIsEmpty()
    {
        var owner = new object();
        var foreignOwner = new object();
        var stored = new[] { new Assignment(7, enabled: true, pinned: false) };

        var snapshot = ClipboardRules.SnapshotForOwner(
            owner,
            foreignOwner,
            stored,
            Clone);

        await Assert.That(snapshot).IsEmpty();
    }

    [Test]
    public async Task CurrentOwnerReceivesDefensiveAssignmentSnapshots()
    {
        var owner = new object();
        var stored = new[] { new Assignment(7, enabled: true, pinned: false) };

        var first = ClipboardRules.SnapshotForOwner(owner, owner, stored, Clone);
        first[0].Enabled = false;
        var second = ClipboardRules.SnapshotForOwner(owner, owner, stored, Clone);

        await Assert.That(second[0].Enabled).IsTrue();
        await Assert.That(ReferenceEquals(first[0], second[0])).IsFalse();
    }

    [Test]
    public async Task MissingNullAndDuplicateRoleIdsAreFilteredDeterministically()
    {
        Assignment[] source =
        {
            null,
            new Assignment(5, enabled: false, pinned: true),
            new Assignment(2, enabled: true, pinned: false),
            new Assignment(5, enabled: true, pinned: false),
            new Assignment(99, enabled: true, pinned: false),
            new Assignment(1, enabled: true, pinned: true)
        };

        var filtered = ClipboardRules.FilterValidDistinct(
            source,
            assignment => assignment?.RoleId,
            new[] { 1, 2, 5 },
            Clone);

        await Assert.That(string.Join(",", filtered.Select(assignment => assignment.RoleId)))
            .IsEqualTo("5,2,1");
        await Assert.That(filtered[0].Enabled).IsFalse();
        await Assert.That(filtered[0].Pinned).IsTrue();
    }

    private static Assignment Clone(Assignment assignment) =>
        new Assignment(assignment.RoleId, assignment.Enabled, assignment.Pinned);

    private sealed class Assignment
    {
        public Assignment(int roleId, bool enabled, bool pinned)
        {
            RoleId = roleId;
            Enabled = enabled;
            Pinned = pinned;
        }

        public int RoleId { get; }
        public bool Enabled { get; set; }
        public bool Pinned { get; }
    }
}
