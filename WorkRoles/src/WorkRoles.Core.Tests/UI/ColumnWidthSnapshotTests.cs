namespace WorkRoles.Core.Tests.UI;

public class ColumnWidthSnapshotTests
{
    [Test]
    public async Task ColumnGeometryFollowsEachWidthEvenWhenTotalOrClampedStripIsUnchanged()
    {
        // A header and an unaffected pawn row share the exact published vector.
        // A different pawn's decorators can move width between columns without
        // changing the total or the role strip's minimum-width geometry.
        ColumnWidthSnapshot first = ColumnWidthSnapshot.Publish(null, [90f, 110f]);
        ColumnWidthSnapshot redistributed = ColumnWidthSnapshot.Publish(first, [110f, 90f]);
        await Assert.That(redistributed.TotalWidth).IsEqualTo(first.TotalWidth);
        await Assert.That(ReferenceEquals(first, redistributed)).IsFalse();
        await Assert.That(redistributed.At(0)).IsEqualTo(110f);
        await Assert.That(redistributed.At(1)).IsEqualTo(90f);

        ColumnWidthSnapshot wider = ColumnWidthSnapshot.Publish(redistributed, [128f, 90f]);
        await Assert.That(Math.Max(300f, 480f - redistributed.TotalWidth))
            .IsEqualTo(Math.Max(300f, 480f - wider.TotalWidth));
        await Assert.That(ReferenceEquals(redistributed, wider)).IsFalse();
        await Assert.That(wider.At(0)).IsEqualTo(128f);

        // An external refresh with the same widths does not invalidate another
        // pawn's row or the header; a metric change is a separate consumer key.
        await Assert.That(ReferenceEquals(wider,
            ColumnWidthSnapshot.Publish(wider, [128f, 90f]))).IsTrue();
        ColumnWidthSnapshot removed = ColumnWidthSnapshot.Publish(wider, [218f]);
        await Assert.That(ReferenceEquals(wider, removed)).IsFalse();
        await Assert.That(removed.Count).IsEqualTo(1);
    }
}
