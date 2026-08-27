using EPrimeReadouts.Core;
using RimShared.Common;

namespace EPrimeReadouts.Core.Tests;

public class PanelViewportTests
{
    [Test]
    public async Task CellsOutsideTheViewportAreRejectedBeforeDrawing()
    {
        var above = new RectF(0f, 0f, 20f, 10f);
        var crossingTop = new RectF(0f, 8f, 20f, 5f);
        var crossingBottom = new RectF(0f, 18f, 20f, 5f);
        var below = new RectF(0f, 20f, 20f, 5f);

        await Assert.That(PanelViewport.IntersectsVertically(above, 10f, 20f))
            .IsFalse();
        await Assert.That(PanelViewport.IntersectsVertically(crossingTop, 10f, 20f))
            .IsTrue();
        await Assert.That(PanelViewport.IntersectsVertically(crossingBottom, 10f, 20f))
            .IsTrue();
        await Assert.That(PanelViewport.IntersectsVertically(below, 10f, 20f))
            .IsFalse();
    }

    [Test]
    public async Task SlotHitTestingIgnoresClippedHitsAndReturnsTheVisibleHit()
    {
        var hits = new List<SlotHit>
        {
            new SlotHit
            {
                Token = "above",
                Members = new[] { "Steel" },
                Rect = new RectF(0f, 0f, 20f, 10f),
            },
            new SlotHit
            {
                Token = "visible",
                Members = new[] { "WoodLog" },
                Rect = new RectF(0f, 12f, 20f, 10f),
            },
        };

        int clipped = PanelViewport.SlotAt(hits, 5f, 5f, 10f, 20f);
        int visible = PanelViewport.SlotAt(hits, 5f, 15f, 10f, 20f);

        await Assert.That(clipped).IsEqualTo(-1);
        await Assert.That(visible).IsEqualTo(1);
    }

    [Test]
    public async Task MarkerHitTestingUsesTheSameViewportBoundaryAsDrawing()
    {
        var hits = new List<MarkerHit>
        {
            new MarkerHit
            {
                GroupId = 4,
                Rect = new RectF(2f, 18f, 8f, 8f),
            },
        };

        int visible = PanelViewport.MarkerAt(hits, 4f, 19f, 10f, 20f);
        int clipped = PanelViewport.MarkerAt(hits, 4f, 21f, 10f, 20f);

        await Assert.That(visible).IsEqualTo(0);
        await Assert.That(clipped).IsEqualTo(-1);
    }

    [Test]
    public async Task VisibleBandRangeSkipsCompleteOffscreenSections()
    {
        var bands = new List<RenderBand>
        {
            new RenderBand { Rect = new RectF(0f, 0f, 40f, 8f) },
            new RenderBand { Rect = new RectF(0f, 12f, 40f, 8f) },
            new RenderBand { Rect = new RectF(0f, 24f, 40f, 8f) },
        };

        PanelBandRange visible = PanelViewport.VisibleBands(
            bands, viewportTop: 10f, viewportBottom: 22f);

        await Assert.That(visible.Start).IsEqualTo(1);
        await Assert.That(visible.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BandLookupRejectsTheEmptyWidthBesideANarrowGroup()
    {
        var bands = new List<RenderBand>
        {
            new RenderBand { Rect = new RectF(0f, 12f, 20f, 8f) },
        };

        int inside = PanelViewport.BandAt(
            bands, 10f, 15f, viewportTop: 10f, viewportBottom: 22f);
        int beside = PanelViewport.BandAt(
            bands, 25f, 15f, viewportTop: 10f, viewportBottom: 22f);

        await Assert.That(inside).IsEqualTo(0);
        await Assert.That(beside).IsEqualTo(-1);
    }

    [Test]
    public async Task RangedSlotLookupCannotSelectAHiddenBandsHit()
    {
        var hits = new List<SlotHit>
        {
            new SlotHit
            {
                Token = "hidden-band",
                Members = new[] { "Steel" },
                Rect = new RectF(0f, 12f, 20f, 10f),
            },
            new SlotHit
            {
                Token = "visible-band",
                Members = new[] { "WoodLog" },
                Rect = new RectF(0f, 12f, 20f, 10f),
            },
        };

        int visible = PanelViewport.SlotAt(
            hits, start: 1, count: 1,
            x: 5f, y: 15f, viewportTop: 10f, viewportBottom: 20f);

        await Assert.That(visible).IsEqualTo(1);
    }

    [Test]
    public async Task AClickFiresOnlyWhenPressAndReleaseOwnTheSameHit()
    {
        var tracker = new PanelClickTracker();
        PanelHitTarget target = PanelHitTarget.Slot(2);

        tracker.Press(target);
        PanelHitTarget clicked = tracker.Release(target);

        await Assert.That(clicked).IsEqualTo(target);
        await Assert.That(tracker.OwnsPointer).IsFalse();
    }

    [Test]
    public async Task DraggingAwayCancelsTheClickButRetainsReleaseOwnership()
    {
        var tracker = new PanelClickTracker();
        tracker.Press(PanelHitTarget.Marker(1));

        await Assert.That(tracker.OwnsPointer).IsTrue();

        PanelHitTarget clicked = tracker.Release(PanelHitTarget.None);

        await Assert.That(clicked).IsEqualTo(PanelHitTarget.None);
        await Assert.That(tracker.OwnsPointer).IsFalse();
    }

    [Test]
    public async Task ABlockingWindowSuppressesTooltipsWithoutSuppressingRepaint()
    {
        PanelRenderPolicy blocked = PanelRenderPolicy.For(
            repaint: true, inputBlocked: true);
        PanelRenderPolicy clear = PanelRenderPolicy.For(
            repaint: true, inputBlocked: false);
        PanelRenderPolicy inputEvent = PanelRenderPolicy.For(
            repaint: false, inputBlocked: false);

        await Assert.That(blocked.DrawCells).IsTrue();
        await Assert.That(blocked.AllowTooltips).IsFalse();
        await Assert.That(clear.AllowTooltips).IsTrue();
        await Assert.That(inputEvent.DrawCells).IsFalse();
    }

    [Test]
    public async Task ABlockedOwnedGestureStillConsumesDragAndRelease()
    {
        PanelPointerPolicy drag = PanelPointerPolicy.For(
            ownsPointer: true,
            inputBlocked: true,
            isDrag: true,
            isRelease: false);
        PanelPointerPolicy release = PanelPointerPolicy.For(
            ownsPointer: true,
            inputBlocked: true,
            isDrag: false,
            isRelease: true);

        await Assert.That(drag.ConsumeEvent).IsTrue();
        await Assert.That(release.ConsumeEvent).IsTrue();
        await Assert.That(release.ResolveReleaseTarget).IsFalse();
    }
}
